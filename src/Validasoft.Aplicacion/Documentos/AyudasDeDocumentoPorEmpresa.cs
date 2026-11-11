using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>
/// Ayuda de un tipo de documento que una empresa escribió para sí (OP-15): sobrescribe la del grupo (<see cref="TipoDocumentoTenant.TextoAyuda"/>) solo para
/// los terceros de esa empresa. Esquema <c>validacion</c>, multi-tenant; id determinista <c>{empresa:N}:{tipo:N}</c>. Sin documento, vale la del grupo.
/// </summary>
public sealed class AyudaDeDocumentoDeEmpresa
{
    public const int LongitudMaxima = 2000;

    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public Guid TipoDocumentoTenantId { get; set; }

    public string Texto { get; set; } = string.Empty;

    public DateTimeOffset ActualizadaEn { get; set; }

    public static string Clave(Guid empresaId, Guid tipoId) => $"{empresaId:N}:{tipoId:N}";

    /// <summary>Texto limpio (saltos de línea normalizados); vacío = nulo. 422 <c>texto-demasiado-largo</c> si pasa de 2 000 caracteres.</summary>
    public static string? Normalizar(string? texto)
    {
        var limpio = (texto ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (limpio.Length > LongitudMaxima)
            throw ReglaDeNegocioException.Validacion("texto-demasiado-largo", $"La ayuda admite hasta {LongitudMaxima} caracteres.");
        return limpio.Length == 0 ? null : limpio;
    }

    /// <summary>Las ayudas que las empresas dieron a los tipos <paramref name="tipoIds"/>, por tipo, para una empresa (se lee con cualquier sesión: <c>AnyTenant</c>).</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> DeLaEmpresaAsync(IQuerySession sesion, Guid empresaId, IReadOnlyCollection<Guid> tipoIds, CancellationToken ct)
    {
        if (tipoIds.Count == 0)
            return new Dictionary<Guid, string>();
        var claves = tipoIds.Select(t => Clave(empresaId, t)).ToArray();
        var ayudas = await sesion.Query<AyudaDeDocumentoDeEmpresa>().Where(a => a.AnyTenant() && a.Id.IsOneOf(claves)).ToListAsync(ct);
        return ayudas.ToDictionary(a => a.TipoDocumentoTenantId, a => a.Texto);
    }
}

/// <summary>
/// La ayuda por documento por empresa (OP-15, OP-16): cada empresa ve la de su grupo y puede escribir la suya; el tercero ve la de la empresa con la que
/// valida (la del grupo si la empresa no escribió una). Con auditoría de configuración.
/// </summary>
public static class AyudasDeDocumentoEndpoints
{
    [WolverineGet("/api/v1/ayudas-de-documentos")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<IReadOnlyList<AyudaDeDocumentoDto>> Listar(SesionDeEmpresa empresa, CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        var tipos = (await empresa.Sesion.Query<TipoDocumentoTenant>().Where(t => t.Activo).ToListAsync(ct)).OrderBy(t => t.Nombre).ToList();
        var propias = (await empresa.Sesion.Query<AyudaDeDocumentoDeEmpresa>().Where(a => a.EmpresaId == empresaId).ToListAsync(ct)).ToDictionary(a => a.TipoDocumentoTenantId);
        return [.. tipos.Select(t => ADto(t, propias.GetValueOrDefault(t.Id)))];
    }

    /// <summary>
    /// Fija la ayuda de la empresa para un tipo; <c>texto</c> vacío la quita (vuelve a valer la del grupo). 404 <c>tipo-documento-no-encontrado</c>; 422
    /// <c>texto-demasiado-largo</c>.
    /// </summary>
    [WolverinePut("/api/v1/ayudas-de-documentos/{tipoId}")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<AyudaDeDocumentoDto> Actualizar(Guid tipoId, ActualizarAyudaDeDocumentoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var tipo = await empresa.Sesion.LoadAsync<TipoDocumentoTenant>(tipoId, ct)
                   ?? throw ReglaDeNegocioException.NoEncontrado("tipo-documento-no-encontrado", "No existe ese tipo de documento.");
        var texto = AyudaDeDocumentoDeEmpresa.Normalizar(dto.Texto);
        var empresaId = empresa.EmpresaId;
        var clave = AyudaDeDocumentoDeEmpresa.Clave(empresaId, tipoId);
        var actual = await empresa.Sesion.LoadAsync<AyudaDeDocumentoDeEmpresa>(clave, ct);
        var ahora = reloj.GetUtcNow();
        var antes = actual?.Texto;

        AyudaDeDocumentoDeEmpresa? resultado = null;
        if (texto is null)
        {
            if (actual is not null)
                empresa.Sesion.Delete(actual);
        }
        else
        {
            resultado = actual ?? new AyudaDeDocumentoDeEmpresa { Id = clave, EmpresaId = empresaId, TipoDocumentoTenantId = tipoId };
            resultado.Texto = texto;
            resultado.ActualizadaEn = ahora;
            empresa.Sesion.Store(resultado);
        }

        if (antes != texto)
            Auditoria.Registrar(empresa.Sesion, contexto, "AyudaDeDocumento", clave, actual is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado,
                antes is null ? null : new { tipo.Codigo, Texto = antes }, texto is null ? null : new { tipo.Codigo, Texto = texto }, ahora, empresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return ADto(tipo, resultado);
    }

    private static AyudaDeDocumentoDto ADto(TipoDocumentoTenant tipo, AyudaDeDocumentoDeEmpresa? propia) =>
        new(tipo.Id, tipo.Codigo, tipo.Nombre, tipo.TextoAyuda, propia?.Texto, propia?.Texto ?? tipo.TextoAyuda, propia?.ActualizadaEn);
}
