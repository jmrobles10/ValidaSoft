using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Documentos;

/// <summary>
/// Una empresa del tenant exige este tipo de documento en sus vínculos de un tipo dado (BO-3). Mientras no existan
/// requisitos en el expediente, decide qué documentos globales se comparten con cada vínculo aceptado.
/// </summary>
public sealed record ExigenciaDeDocumento(Guid EmpresaId, Guid TipoVinculoId);

/// <summary>
/// Tipo de documento del tenant (docs/02 §4.3, esquema <c>validacion</c>): los <b>defaults del grupo</b> (nombre, ayuda,
/// frescura, plazos) y su enlace opcional al canónico que lo hace compartible. El ámbito puede restringir el del canónico
/// (Global ⇒ PorVinculo), nunca ampliarlo (P-2-4); un tipo propio sin canónico es siempre <c>PorVinculo</c> (P7). Las
/// exigencias por empresa y tipo de vínculo viven aquí (BO-3); frescura y plazos se guardan pero aún no se aplican.
/// </summary>
public sealed class TipoDocumentoTenant
{
    public Guid Id { get; set; }

    /// <summary>Tenant dueño (copia del tenant de Marten para cruzarlo en consultas entre tenants).</summary>
    public Guid TenantId { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Código del canónico (<see cref="CatalogoCanonico"/>); nulo ⇒ tipo propio del tenant, <c>PorVinculo</c>.</summary>
    public string? TipoCanonico { get; set; }

    public int? VersionCanonico { get; set; }

    public AmbitoDocumento Ambito { get; set; }

    public ClaseDocumento Clase { get; set; }

    public string? TextoAyuda { get; set; }

    public ReglaFrescura ReglaFrescura { get; set; } = ReglaFrescura.Ninguna;

    public List<int> DiasAviso { get; set; } = [30, 15, 5];

    public int DiasGracia { get; set; } = 15;

    public int DiasRevalidacion { get; set; } = 15;

    public List<ExigenciaDeDocumento> Exigencias { get; set; } = [];

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreadoEn { get; set; }

    /// <summary>¿La empresa lo exige a sus vínculos de ese tipo? Solo cuenta si el tipo está activo.</summary>
    public bool Exige(Guid empresaId, Guid tipoVinculoId) =>
        Activo && Exigencias.Any(e => e.EmpresaId == empresaId && e.TipoVinculoId == tipoVinculoId);

    public static TipoDocumentoTenant Crear(Guid tenantId, string codigo, string? nombre, string? tipoCanonico, AmbitoDocumento? ambito,
        ReglaFrescura? frescura, IEnumerable<int>? diasAviso, int? diasGracia, int? diasRevalidacion, string? textoAyuda,
        IEnumerable<ExigenciaDeDocumento>? exigencias, DateTimeOffset ahora)
    {
        var codigoNormalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (!Regex.IsMatch(codigoNormalizado, "^[A-Z][A-Z0-9_]{1,59}$"))
            throw ReglaDeNegocioException.Validacion("codigo-invalido",
                "El código debe empezar con letra y usar solo mayúsculas, números y guion bajo (máx. 60).");

        TipoDocumentoCanonico? canonico = null;
        if (!string.IsNullOrWhiteSpace(tipoCanonico))
            canonico = CatalogoCanonico.Requerido(tipoCanonico);

        var tipo = new TipoDocumentoTenant
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Codigo = codigoNormalizado,
            TipoCanonico = canonico?.Codigo,
            VersionCanonico = canonico?.Version,
            Clase = canonico?.Clase ?? ClaseDocumento.Archivo,
            CreadoEn = ahora
        };

        tipo.Configurar(string.IsNullOrWhiteSpace(nombre) ? canonico?.Nombre ?? string.Empty : nombre, ambito, frescura ?? canonico?.ReglaFrescuraSugerida, diasAviso, diasGracia,
            diasRevalidacion, textoAyuda, exigencias, activo: true);
        return tipo;
    }

    /// <summary>Código y canónico son inmutables; lo demás se reemplaza (los valores nulos conservan el actual).</summary>
    public void Actualizar(string? nombre, AmbitoDocumento? ambito, ReglaFrescura? frescura, IEnumerable<int>? diasAviso, int? diasGracia,
        int? diasRevalidacion, string? textoAyuda, IEnumerable<ExigenciaDeDocumento>? exigencias, bool? activo) =>
        Configurar(nombre ?? Nombre, ambito ?? Ambito, frescura ?? ReglaFrescura, diasAviso ?? DiasAviso, diasGracia ?? DiasGracia,
            diasRevalidacion ?? DiasRevalidacion, textoAyuda ?? TextoAyuda, exigencias ?? Exigencias, activo ?? Activo);

    /// <summary>
    /// Ámbito efectivo pedido para un tipo con o sin canónico: sin canónico solo <c>PorVinculo</c>; con canónico,
    /// igual o más restringido (P-2-4). Ampliar ⇒ 422 <c>ambito-no-ampliable</c>.
    /// </summary>
    public static AmbitoDocumento ResolverAmbito(string? tipoCanonico, AmbitoDocumento? pedido)
    {
        var maximo = CatalogoCanonico.Buscar(tipoCanonico)?.Ambito ?? AmbitoDocumento.PorVinculo;
        var ambito = pedido ?? maximo;
        if (ambito == AmbitoDocumento.Global && maximo == AmbitoDocumento.PorVinculo)
            throw ReglaDeNegocioException.Validacion("ambito-no-ampliable", tipoCanonico is null
                ? "Un tipo propio sin canónico es siempre PorVinculo: solo un canónico global se comparte entre empresas."
                : $"El canónico {tipoCanonico} es PorVinculo; el tipo del tenant no puede ampliarlo a Global.");
        return ambito;
    }

    private void Configurar(string nombre, AmbitoDocumento? ambito, ReglaFrescura? frescura, IEnumerable<int>? diasAviso, int? diasGracia,
        int? diasRevalidacion, string? textoAyuda, IEnumerable<ExigenciaDeDocumento>? exigencias, bool activo)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del tipo de documento es obligatorio.");

        var regla = frescura ?? ReglaFrescura.Ninguna;
        if (regla.Tipo is TipoReglaFrescura.MaxDiasDesdeExpedicion or TipoReglaFrescura.MaxDiasDesdeAprobacion && regla.Dias is not > 0)
            throw ReglaDeNegocioException.Validacion("frescura-invalida", $"La regla {regla.Tipo} necesita un número de días positivo.");
        if (regla.Tipo == TipoReglaFrescura.PeriodoMinimo && string.IsNullOrWhiteSpace(regla.Regla))
            throw ReglaDeNegocioException.Validacion("frescura-invalida", "La regla PeriodoMinimo necesita la descripción del periodo.");

        var avisos = (diasAviso ?? [30, 15, 5]).Distinct().OrderDescending().ToList();
        if (avisos.Any(d => d <= 0) || diasGracia is < 0 || diasRevalidacion is <= 0)
            throw ReglaDeNegocioException.Validacion("dias-invalidos", "Los días de aviso y de revalidación deben ser positivos y la gracia no negativa.");

        var lista = (exigencias ?? []).Distinct().ToList();
        if (lista.Any(e => e.EmpresaId == Guid.Empty || e.TipoVinculoId == Guid.Empty))
            throw ReglaDeNegocioException.Validacion("exigencia-invalida", "Cada exigencia necesita empresa y tipo de vínculo.");

        Ambito = ResolverAmbito(TipoCanonico, ambito);
        Nombre = nombre.Trim();
        ReglaFrescura = regla;
        DiasAviso = avisos;
        DiasGracia = diasGracia ?? 15;
        DiasRevalidacion = diasRevalidacion ?? 15;
        TextoAyuda = string.IsNullOrWhiteSpace(textoAyuda) ? null : textoAyuda.Trim();
        Exigencias = lista;
        Activo = activo;
    }

    /// <summary>Tipos que se siembran al crear un tenant: los canónicos de archivo más comunes, sin exigir ninguno.</summary>
    public static IReadOnlyList<TipoDocumentoTenant> Semilla(Guid tenantId, DateTimeOffset ahora) =>
    [
        .. CatalogoCanonico.SemillaDeTenant.Select(codigo =>
        {
            var canonico = CatalogoCanonico.Requerido(codigo);
            var avisos = codigo is "POLIZA" or "ESTADOS_FINANCIEROS" ? new[] { 60, 30, 7 } : null;
            return Crear(tenantId, codigo, canonico.Nombre, codigo, null, null, avisos, null, null, null, null, ahora);
        })
    ];
}
