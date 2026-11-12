using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Marten;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Validasoft.Aplicacion.Comun;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Cifrado a nivel de campo con la clave del grupo (PI-11). La clave de cada grupo se <b>deriva</b> de la clave maestra de secretos (HKDF-SHA256, sal = el id del grupo, rótulo
/// <c>validasoft:campo-de-grupo:v1</c>): no se guarda nada nuevo y rotar la maestra rota todas a la vez. Formato <c>vg1.{kid}.{base64url(nonce ‖ cifrado ‖ etiqueta)}</c>
/// con AES-256-GCM y como datos asociados <c>validasoft:campo:v1|{kid}|{grupo}</c>: cambiar el grupo o el kid del texto invalida la etiqueta, así la copia de un evento de
/// un grupo no se lee con la clave de otro. Lo anterior (<c>vs1.</c> de la clave general, o el formato heredado) se descifra con <see cref="IProtectorDeSecretos"/>.
/// </summary>
public sealed class ProtectorDeCamposDelGrupo(ProtectorDeSecretos secretos) : IProtectorDeCamposDelGrupo
{
    public const string Prefijo = "vg1";
    private const int TamanoNonce = 12;
    private const int TamanoEtiqueta = 16;
    private static readonly byte[] Rotulo = Encoding.UTF8.GetBytes("validasoft:campo-de-grupo:v1");

    public string Proteger(Guid tenantId, string secreto)
    {
        var claves = secretos.Claves;
        var plano = Encoding.UTF8.GetBytes(secreto);
        var salida = new byte[TamanoNonce + plano.Length + TamanoEtiqueta];
        var nonce = salida.AsSpan(0, TamanoNonce);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(ClaveDelGrupo(claves.Actual, tenantId), TamanoEtiqueta);
        aes.Encrypt(nonce, plano, salida.AsSpan(TamanoNonce, plano.Length), salida.AsSpan(TamanoNonce + plano.Length), Asociados(claves.KidActual, tenantId));
        return $"{Prefijo}.{claves.KidActual}.{Base64Url.EncodeToString(salida)}";
    }

    public string Desproteger(Guid tenantId, string protegido)
    {
        if (!protegido.StartsWith(Prefijo + ".", StringComparison.Ordinal))
            return secretos.Desproteger(protegido); // anterior a PI-11: clave general de la plataforma (vs1.) o formato heredado

        var partes = protegido.Split('.');
        if (partes.Length != 3 || partes[1].Length != 8)
            throw new CryptographicException("Campo protegido inválido.");
        if (!secretos.Claves.PorKid.TryGetValue(partes[1], out var maestra))
            throw new CryptographicException($"El campo se cifró con la clave {partes[1]}, que no está configurada (agrégala a Validasoft:Secretos:ClavesAnteriores).");

        var datos = Base64Url.DecodeFromChars(partes[2]);
        if (datos.Length < TamanoNonce + TamanoEtiqueta)
            throw new CryptographicException("Campo protegido inválido.");
        var largo = datos.Length - TamanoNonce - TamanoEtiqueta;
        var plano = new byte[largo];
        using var aes = new AesGcm(ClaveDelGrupo(maestra, tenantId), TamanoEtiqueta);
        aes.Decrypt(datos.AsSpan(0, TamanoNonce), datos.AsSpan(TamanoNonce, largo), datos.AsSpan(TamanoNonce + largo), plano, Asociados(partes[1], tenantId));
        return Encoding.UTF8.GetString(plano);
    }

    public bool EstaVigente(Guid tenantId, string protegido) => protegido.StartsWith($"{Prefijo}.{secretos.Claves.KidActual}.", StringComparison.Ordinal);

    private static byte[] ClaveDelGrupo(byte[] maestra, Guid tenantId) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, maestra, ClavesDeSecretos.TamanoClave, tenantId.ToByteArray(), Rotulo);

    private static byte[] Asociados(string kid, Guid tenantId) => Encoding.UTF8.GetBytes($"validasoft:campo:v1|{kid}|{tenantId:N}");
}

/// <summary>
/// Pasa a la clave del grupo el número de cuenta congelado en los eventos de un vínculo y en el documento del vínculo que los proyecta (PI-11). Opera con SQL sobre
/// <c>eventos.mt_events</c> (<c>data -> cuenta -> numeroProtegido</c>) y <c>validacion.mt_doc_vinculo</c> (<c>cuentaDesignada</c>, <c>cuentaPropuesta</c> y <c>cuentaRechazada</c>): solo
/// cambia el texto cifrado de ese campo, en una transacción por grupo, y es idempotente (lo que ya está con la clave vigente del grupo no se toca).
/// </summary>
public sealed class RecifradorDeCuentasDeVinculos(IConfiguration configuracion, IProtectorDeCamposDelGrupo campos) : IRecifradorDeCuentasDeVinculos
{
    private static readonly string[] RutasDeVinculo = ["cuentaDesignada", "cuentaPropuesta", "cuentaRechazada"];

    public async Task<(ConteoDeRecifrado Eventos, ConteoDeRecifrado Vinculos)> RecorrerAsync(Guid tenantId, bool simular, CancellationToken ct)
    {
        var cadena = configuracion.GetConnectionString("Validasoft") ?? throw new InvalidOperationException("Falta ConnectionStrings:Validasoft.");
        await using var conexion = new NpgsqlConnection(cadena);
        await conexion.OpenAsync(ct);
        await using var transaccion = await conexion.BeginTransactionAsync(ct);

        var eventos = new Conteo();
        await RecorrerTablaAsync(conexion, transaccion, tenantId, simular, eventos, "eventos.mt_events", "seq_id", "data #>> '{cuenta,numeroProtegido}'",
            "jsonb_set(data, '{cuenta,numeroProtegido}', to_jsonb(@nuevo::text))", ct);

        var vinculos = new Conteo();
        foreach (var ruta in RutasDeVinculo)
            await RecorrerTablaAsync(conexion, transaccion, tenantId, simular, vinculos, "validacion.mt_doc_vinculo", "id", $"data #>> '{{{ruta},cuenta,numeroProtegido}}'",
                $"jsonb_set(data, '{{{ruta},cuenta,numeroProtegido}}', to_jsonb(@nuevo::text))", ct);

        await transaccion.CommitAsync(ct);
        return (eventos.Resultado(), vinculos.Resultado());
    }

    private async Task RecorrerTablaAsync(NpgsqlConnection conexion, NpgsqlTransaction transaccion, Guid tenantId, bool simular, Conteo conteo, string tabla, string clave,
        string expresionDelValor, string expresionNueva, CancellationToken ct)
    {
        var filas = new List<(object Id, string Valor)>();
        await using (var consulta = new NpgsqlCommand($"select {clave}, {expresionDelValor} from {tabla} where tenant_id = @tenant and {expresionDelValor} is not null", conexion, transaccion))
        {
            consulta.Parameters.AddWithValue("tenant", NpgsqlDbType.Text, tenantId.ToString("N"));
            await using var lector = await consulta.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
                filas.Add((lector.GetValue(0), lector.GetString(1)));
        }

        foreach (var (id, valor) in filas)
        {
            conteo.Total++;
            if (campos.EstaVigente(tenantId, valor))
            {
                conteo.Vigentes++;
                continue;
            }

            string nuevo;
            try
            {
                nuevo = campos.Proteger(tenantId, campos.Desproteger(tenantId, valor));
            }
            catch (Exception e) when (e is CryptographicException or FormatException or ArgumentException)
            {
                conteo.Fallidos++;
                continue;
            }

            if (simular)
                continue;
            await using var cambio = new NpgsqlCommand($"update {tabla} set data = {expresionNueva} where {clave} = @id and tenant_id = @tenant", conexion, transaccion);
            cambio.Parameters.AddWithValue("nuevo", NpgsqlDbType.Text, nuevo);
            cambio.Parameters.AddWithValue("id", id);
            cambio.Parameters.AddWithValue("tenant", NpgsqlDbType.Text, tenantId.ToString("N"));
            await cambio.ExecuteNonQueryAsync(ct);
            conteo.Recifrados++;
        }
    }

    private sealed class Conteo
    {
        public int Total, Vigentes, Recifrados, Fallidos;

        public ConteoDeRecifrado Resultado() => new(Total, Vigentes, Recifrados, Fallidos);
    }
}
