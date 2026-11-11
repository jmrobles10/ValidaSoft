using System.Text.RegularExpressions;
using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Invitaciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Invitaciones;

/// <summary>
/// Una fila ya validada: sus errores (inválida), la fila anterior que repite (duplicada) y, si es válida, la clave de su
/// identificación y lo que se prevé hacer con ella (<c>Invitar</c>, <c>Reinvitar</c> o <c>SinCambios</c>).
/// </summary>
public sealed record FilaValidada(int N, FilaDeLoteEntrada Datos, IReadOnlyList<ErrorDeFila> Errores, int? DuplicadaDe, string? Clave, string? Accion)
{
    public EstadoFilaLote Estado => Errores.Count > 0 ? EstadoFilaLote.Invalida : DuplicadaDe is not null ? EstadoFilaLote.Duplicada : EstadoFilaLote.Pendiente;
}

/// <summary>
/// Validación por fila del lote (LT-7), la misma en la vista previa y al crearlo: identificación (multipaís, con su dígito de
/// verificación), tipo de persona coherente con el tipo de identificación, nombre, correo, tipo de vínculo activo del tenant,
/// flujo de la empresa para ese tipo (si se indica) y vínculo existente que no se pueda invitar (bloqueado o terminado). Dos
/// filas con la misma identificación y tipo de vínculo: la segunda es duplicada.
/// </summary>
public static partial class ValidacionDeLote
{
    public const int LongitudMaximaTexto = 200;
    private const int Bloque = 500;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex RegexCorreo();

    public static async Task<IReadOnlyList<FilaValidada>> ValidarAsync(SesionDeEmpresa empresa, IReadOnlyList<FilaDeLoteEntrada> filas,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var tipos = (await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct)).ToDictionary(t => t.Codigo, StringComparer.OrdinalIgnoreCase);
        var flujos = (await empresa.Flujos().ToListAsync(ct)).ToDictionary(f => f.Codigo, StringComparer.OrdinalIgnoreCase);

        var previas = new List<(int N, FilaDeLoteEntrada Datos, List<ErrorDeFila> Errores, string? Clave, TipoVinculo? Tipo)>();
        for (var i = 0; i < filas.Count; i++)
        {
            var (errores, clave, tipo) = Validar(filas[i], tipos, flujos, ahora);
            previas.Add((i + 1, filas[i], errores, clave, tipo));
        }

        // Vínculos que ya existen en la empresa para las identificaciones válidas (por bloques: un lote puede tener miles).
        var sandbox = empresa.TenantSandbox; // en un sandbox los terceros llevan su tenant por delante (SB-4)
        var claves = previas.Where(p => p.Errores.Count == 0).Select(p => AislamientoDeSandbox.ClaveDeTercero(p.Clave!, sandbox)).Distinct().ToList();
        var terceros = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var bloque in claves.Chunk(Bloque))
            foreach (var t in await empresa.Sesion.Query<Tercero>().Where(t => t.ClaveIdentificacion.IsOneOf(bloque)).ToListAsync(ct))
                terceros[t.Identificacion.Clave] = t.Id;
        var vinculos = new Dictionary<(Guid, Guid), Vinculo>();
        foreach (var bloque in terceros.Values.Distinct().Chunk(Bloque))
            foreach (var v in await empresa.Vinculos().Where(v => v.TerceroId.IsOneOf(bloque)).ToListAsync(ct))
                vinculos[(v.TerceroId, v.TipoVinculoId)] = v;

        var vistas = new Dictionary<string, int>(StringComparer.Ordinal);
        var resultado = new List<FilaValidada>(previas.Count);
        foreach (var (n, datos, errores, clave, tipo) in previas)
        {
            if (errores.Count > 0)
            {
                resultado.Add(new FilaValidada(n, datos, errores, null, clave, null));
                continue;
            }

            var llave = $"{clave}|{tipo!.Codigo}";
            if (vistas.TryGetValue(llave, out var primera))
            {
                resultado.Add(new FilaValidada(n, datos, [], primera, clave, null));
                continue;
            }

            vistas[llave] = n;
            var existente = terceros.TryGetValue(clave!, out var terceroId) && vinculos.TryGetValue((terceroId, tipo.Id), out var v) ? v : null;
            var (accion, error) = AccionPrevista(existente);
            resultado.Add(error is null
                ? new FilaValidada(n, datos, [], null, clave, accion)
                : new FilaValidada(n, datos, [error], null, clave, null));
        }

        return resultado;
    }

    /// <summary>Lo que hará el proceso con un vínculo existente; bloqueado o terminado no se puede invitar.</summary>
    public static (string? Accion, ErrorDeFila? Error) AccionPrevista(Vinculo? existente) => existente switch
    {
        null => (nameof(AccionFilaLote.Invitado), null),
        { Estado: EstadoVinculo.Bloqueado or EstadoVinculo.Anulado or EstadoVinculo.Desvinculado } v =>
            (null, new ErrorDeFila(null, "vinculo-no-invitable", $"La empresa ya tiene ese vínculo y está {v.Estado}: no se puede invitar.")),
        { Estado: EstadoVinculo.Invitado, InvitacionAceptada: false } => (nameof(AccionFilaLote.Reinvitado), null),
        _ => (nameof(AccionFilaLote.SinCambios), null)
    };

    private static (List<ErrorDeFila> Errores, string? Clave, TipoVinculo? Tipo) Validar(FilaDeLoteEntrada f, Dictionary<string, TipoVinculo> tipos,
        Dictionary<string, Flujo> flujos, DateTimeOffset ahora)
    {
        var errores = new List<ErrorDeFila>();

        TipoVinculo? tipo = null;
        if (f.TipoVinculo is null)
            errores.Add(new ErrorDeFila("tipoVinculo", "tipo-vinculo-requerido", "Falta el tipo de vínculo."));
        else if (!tipos.TryGetValue(f.TipoVinculo, out tipo) || !tipo.Activo)
        {
            errores.Add(new ErrorDeFila("tipoVinculo", "tipo-vinculo-invalido", $"El tipo de vínculo «{f.TipoVinculo}» no existe o está inactivo."));
            tipo = null;
        }

        if (f.Flujo is not null && tipo is not null &&
            (!flujos.TryGetValue(f.Flujo, out var flujo) || flujo.TipoVinculoId != tipo.Id || flujo.VersionPublicada is null))
            errores.Add(new ErrorDeFila("flujo", "flujo-invalido", $"El flujo «{f.Flujo}» no existe en la empresa para {tipo.Codigo} o no está publicado."));

        if (f.Correo is null)
            errores.Add(new ErrorDeFila("correo", "correo-requerido", "Falta el correo del tercero."));
        else if (f.Correo.Length > 254 || !RegexCorreo().IsMatch(f.Correo))
            errores.Add(new ErrorDeFila("correo", "correo-invalido", $"«{f.Correo}» no es un correo válido."));

        if (f.Nombre is null)
            errores.Add(new ErrorDeFila("nombre", "nombre-requerido", "Falta la razón social o los nombres."));
        foreach (var (campo, valor) in new[] { ("nombre", f.Nombre), ("nombreComercial", f.NombreComercial), ("contacto", f.Contacto), ("telefono", f.Telefono) })
            if (valor is { Length: > LongitudMaximaTexto })
                errores.Add(new ErrorDeFila(campo, "texto-demasiado-largo", $"Admite hasta {LongitudMaximaTexto} caracteres."));

        string? clave = null;
        if (f.TipoIdentificacion is null)
            errores.Add(new ErrorDeFila("tipoIdentificacion", "tipo-identificacion-requerido", "Falta el tipo de identificación."));
        if (f.NumeroIdentificacion is null)
            errores.Add(new ErrorDeFila("numeroIdentificacion", "numero-identificacion-requerido", "Falta el número de identificación."));
        if (f.TipoIdentificacion is not null && f.NumeroIdentificacion is not null)
        {
            try
            {
                var identificacion = Identificacion.Crear(f.Pais ?? "CO", f.TipoIdentificacion, f.NumeroIdentificacion, f.DigitoVerificacion);
                if (TipoPersonaDe(f, identificacion) is not { } tipoPersona)
                    errores.Add(new ErrorDeFila("tipoPersona", "tipo-persona-invalido", "El tipo de persona es Natural, Juridica o Consorcio."));
                else if (f.Nombre is not null)
                    _ = Tercero.Crear(identificacion, tipoPersona, f.Nombre, f.NombreComercial, null, ahora); // tipo de identificación vs. persona
                clave = identificacion.Clave;
            }
            catch (ReglaDeNegocioException e)
            {
                var campo = e.Codigo switch
                {
                    "tipo-identificacion-invalido" => "tipoIdentificacion",
                    "digito-verificacion-invalido" => "digitoVerificacion",
                    "tipo-identificacion-no-aplica" => "tipoPersona",
                    "pais-invalido" => "pais",
                    _ => "numeroIdentificacion"
                };
                errores.Add(new ErrorDeFila(campo, e.Codigo, e.Message));
            }
        }

        return (errores, errores.Count == 0 ? clave : null, tipo);
    }

    /// <summary>El indicado (sin tildes ni mayúsculas) o, si falta, el que corresponde al tipo de identificación.</summary>
    public static TipoPersona? TipoPersonaDe(FilaDeLoteEntrada f, Identificacion identificacion)
    {
        if (f.TipoPersona is null)
            return CatalogoIdentificacion.Buscar(identificacion.Pais, identificacion.Tipo)?.AplicaA == AplicaA.Natural ? TipoPersona.Natural : TipoPersona.Juridica;
        return ArchivoDeLote.Normalizar(f.TipoPersona) switch
        {
            "natural" or "personanatural" => TipoPersona.Natural,
            "juridica" or "personajuridica" => TipoPersona.Juridica,
            "consorcio" or "uniontemporal" => TipoPersona.Consorcio,
            _ => null
        };
    }
}
