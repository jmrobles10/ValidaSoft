using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Configuracion;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;

namespace Validasoft.Infraestructura.Correo;

/// <summary>
/// Proveedor de dominios de correo <b>simulado</b> (OD-19), el de desarrollo y pruebas: no consulta ningún DNS ni ningún proveedor real.
/// Da registros deterministas (el selector DKIM y su «clave» salen del propio dominio) y decide la verificación con una regla local: un
/// dominio cuyo nombre contiene <c>sin-dns</c> no tiene ningún registro; uno que contiene <c>dkim-pendiente</c> tiene SPF y DMARC pero no
/// DKIM; cualquier otro está bien configurado. Así se prueba el flujo completo —pendiente, fallido, verificado— sin tocar nada fuera.
/// </summary>
public sealed class ProveedorSimuladoDeDominios(IConfiguration configuracion) : IProveedorDeDominiosDeCorreo
{
    public const string NombreDelProveedor = "Simulado";
    public const string SelectorDkim = "vs1";

    public string Nombre => NombreDelProveedor;

    public bool EsSimulado => true;

    private string IncluirSpf => configuracion["Validasoft:Correo:Dominios:Simulado:IncluirSpf"] is { Length: > 0 } valor ? valor : "spf.simulado.validasoft.test";

    public Task<IReadOnlyList<RegistroDns>> RegistrarAsync(string dominio, CancellationToken ct) => Task.FromResult(Registros(dominio));

    public Task<IReadOnlyList<ComprobacionDeRegistroDns>> VerificarAsync(string dominio, IReadOnlyList<RegistroDns> esperados, CancellationToken ct)
    {
        var sinDns = dominio.Contains("sin-dns", StringComparison.Ordinal);
        var sinDkim = dominio.Contains("dkim-pendiente", StringComparison.Ordinal);
        IReadOnlyList<ComprobacionDeRegistroDns> resultado =
        [
            .. esperados.Select(r => sinDns || (sinDkim && r.Finalidad == FinalidadDeRegistroDns.Dkim)
                ? new ComprobacionDeRegistroDns(r.Finalidad, false, null)
                : new ComprobacionDeRegistroDns(r.Finalidad, true, r.Valor))
        ];
        return Task.FromResult(resultado);
    }

    private IReadOnlyList<RegistroDns> Registros(string dominio) =>
    [
        new(FinalidadDeRegistroDns.Spf, "TXT", dominio, $"v=spf1 include:{IncluirSpf} ~all"),
        new(FinalidadDeRegistroDns.Dkim, "TXT", $"{SelectorDkim}._domainkey.{dominio}", $"v=DKIM1; k=rsa; p={ClaveSimulada(dominio)}"),
        new(FinalidadDeRegistroDns.Dmarc, "TXT", $"_dmarc.{dominio}", $"v=DMARC1; p=none; rua=mailto:dmarc@{dominio}")
    ];

    /// <summary>Un texto con forma de clave pública (base64 de 270 bytes derivados del dominio); no es una clave real.</summary>
    private static string ClaveSimulada(string dominio)
    {
        var bytes = new byte[270];
        var semilla = SHA256.HashData(Encoding.UTF8.GetBytes("simulado:" + dominio));
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = semilla[i % semilla.Length];
        return Convert.ToBase64String(bytes);
    }
}

/// <summary>Un proveedor elegido en la configuración que esta instalación no trae: todo intento de usarlo responde 503.</summary>
public sealed class ProveedorDeDominiosNoConfigurado(string nombre) : IProveedorDeDominiosDeCorreo
{
    public string Nombre => nombre;

    public bool EsSimulado => false;

    public Task<IReadOnlyList<RegistroDns>> RegistrarAsync(string dominio, CancellationToken ct) => throw NoDisponible();

    public Task<IReadOnlyList<ComprobacionDeRegistroDns>> VerificarAsync(string dominio, IReadOnlyList<RegistroDns> esperados, CancellationToken ct) => throw NoDisponible();

    private ReglaDeNegocioException NoDisponible() => ReglaDeNegocioException.NoDisponible("proveedor-de-dominios-no-configurado",
        $"El proveedor de dominios de correo «{nombre}» no está disponible en esta instalación (solo el simulado de desarrollo).");
}
