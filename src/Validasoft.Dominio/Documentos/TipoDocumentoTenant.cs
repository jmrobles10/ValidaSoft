using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Documentos;

/// <summary>
/// Tipo de documento del tenant (docs/02 §4.3, esquema <c>validacion</c>): los <b>defaults del grupo</b> (nombre, ayuda,
/// frescura, plazos) y su enlace opcional al canónico que lo hace compartible. El ámbito puede restringir el del canónico
/// (Global ⇒ PorVinculo), nunca ampliarlo (P-2-4); un tipo propio sin canónico es siempre <c>PorVinculo</c> (P7). Qué se
/// pide a cada vínculo (y por tanto qué se comparte) lo deciden los requisitos de su flujo (RQ-3; las antiguas
/// exigencias por empresa y tipo de vínculo de BO-3 ya no existen). Desde TD-1…TD-7 además configura qué acepta como
/// archivo (formatos, peso y páginas; nunca más laxo que el canónico ni que los límites globales), cómo se captura
/// (archivo, campos o ambos), su plantilla descargable versionada y, con frescura, avisos y gracia, la vigencia de cada
/// versión.
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

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreadoEn { get; set; }

    /// <summary>Formatos (MIME) que admite; vacío ⇒ los del canónico (o toda la lista blanca). Subconjunto del canónico (TD-1).</summary>
    public List<string> FormatosPermitidos { get; set; } = [];

    /// <summary>Peso máximo en MB; nulo ⇒ el global (o el del canónico). Nunca por encima de ellos.</summary>
    public int? TamanoMaximoMb { get; set; }

    /// <summary>Páginas máximas de un PDF; nulo ⇒ las globales (o las del canónico).</summary>
    public int? PaginasMaximas { get; set; }

    /// <summary>Qué entrega el tercero (TD-4). Por defecto el del canónico: <c>Formulario</c> si es de esa clase, si no <c>Archivo</c>.</summary>
    public ModoCaptura Modo { get; set; } = ModoCaptura.Archivo;

    /// <summary>Campos que llena el tercero, en orden (solo con <see cref="Modo"/> distinto de <c>Archivo</c>).</summary>
    public List<CampoFormulario> Campos { get; set; } = [];

    /// <summary>Historial de plantillas (versión 1, 2…); <see cref="PlantillaVigente"/> es la que se descarga (TD-5).</summary>
    public List<PlantillaDeTipo> Plantillas { get; set; } = [];

    public int? PlantillaVigente { get; set; }

    /// <summary>Subida de plantilla en curso (paso 1); a lo sumo una: iniciar otra la reemplaza.</summary>
    public CargaDePlantilla? CargaDePlantilla { get; set; }

    /// <summary>
    /// ¿El tercero puede entregar este tipo? Solo archivo: si la clase es <c>Archivo</c>. Con campos: si tiene campos y es
    /// <c>PorVinculo</c> (un formulario global necesita el esquema canónico, que aún no existe). Si no ⇒ 422 <c>clase-no-soportada</c>.
    /// </summary>
    [JsonIgnore]
    public bool CargaSoportada => Modo == ModoCaptura.Archivo
        ? Clase == ClaseDocumento.Archivo
        : Campos.Count > 0 && Ambito == AmbitoDocumento.PorVinculo;

    [JsonIgnore]
    public bool PideArchivo => Modo != ModoCaptura.Formulario;

    [JsonIgnore]
    public bool PideDatos => Modo != ModoCaptura.Archivo;

    [JsonIgnore]
    public PlantillaDeTipo? Plantilla => PlantillaVigente is { } n ? Plantillas.FirstOrDefault(p => p.Version == n) : null;

    public static TipoDocumentoTenant Crear(Guid tenantId, string codigo, string? nombre, string? tipoCanonico, AmbitoDocumento? ambito,
        ReglaFrescura? frescura, IEnumerable<int>? diasAviso, int? diasGracia, int? diasRevalidacion, string? textoAyuda, DateTimeOffset ahora)
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
            Modo = canonico?.Clase == ClaseDocumento.Formulario ? ModoCaptura.Formulario : ModoCaptura.Archivo,
            CreadoEn = ahora
        };

        tipo.Configurar(string.IsNullOrWhiteSpace(nombre) ? canonico?.Nombre ?? string.Empty : nombre, ambito, frescura ?? canonico?.ReglaFrescuraSugerida, diasAviso, diasGracia,
            diasRevalidacion, textoAyuda, activo: true);
        return tipo;
    }

    /// <summary>Código y canónico son inmutables; lo demás se reemplaza (los valores nulos conservan el actual).</summary>
    public void Actualizar(string? nombre, AmbitoDocumento? ambito, ReglaFrescura? frescura, IEnumerable<int>? diasAviso, int? diasGracia,
        int? diasRevalidacion, string? textoAyuda, bool? activo)
    {
        ValidarCaptura(Modo, Campos, ResolverAmbito(TipoCanonico, ambito ?? Ambito));
        Configurar(nombre ?? Nombre, ambito ?? Ambito, frescura ?? ReglaFrescura, diasAviso ?? DiasAviso, diasGracia ?? DiasGracia,
            diasRevalidacion ?? DiasRevalidacion, textoAyuda ?? TextoAyuda, activo ?? Activo);
    }

    /// <summary>
    /// Reglas de archivo y de captura (TD-1, TD-4); los nulos conservan el valor. <paramref name="tamanoMaximoMb"/> o
    /// <paramref name="paginasMaximas"/> en 0 vuelven al límite global; <paramref name="formatos"/> vacío ⇒ los del canónico.
    /// Nunca más laxo que el canónico (422 <c>formato-mas-laxo</c>, <c>limite-mas-laxo</c>); límites fuera de rango ⇒
    /// <c>limite-invalido</c>. Modo y campos deben ser coherentes (<c>modo-invalido</c>, <c>campos-requeridos</c>) y un
    /// tipo con campos es siempre <c>PorVinculo</c> (<c>formulario-solo-por-vinculo</c>).
    /// </summary>
    public void ConfigurarCaptura(IEnumerable<string>? formatos, int? tamanoMaximoMb, int? paginasMaximas, ModoCaptura? modo,
        IEnumerable<CampoFormulario>? campos)
    {
        // Se valida todo antes de cambiar nada: un error no deja el tipo a medias.
        var canonico = CatalogoCanonico.Buscar(TipoCanonico);

        var nuevosFormatos = FormatosPermitidos;
        if (formatos is not null)
        {
            nuevosFormatos = FormatosDeArchivo.NormalizarLista(formatos);
            if (canonico is { Formatos.Count: > 0 } && nuevosFormatos.Any(f => !canonico.Formatos.Contains(f)))
                throw ReglaDeNegocioException.Validacion("formato-mas-laxo",
                    $"{canonico.Codigo} solo admite {FormatosDeArchivo.Enumerar(canonico.Formatos)}: el tipo puede ser más estricto, no más laxo.");
        }

        var nuevoTamano = tamanoMaximoMb is { } tamano ? Limite(tamano, canonico?.TamanoMaximoMb, 1024, "El peso máximo", "MB") : TamanoMaximoMb;
        var nuevasPaginas = paginasMaximas is { } paginas
            ? Limite(paginas, canonico?.PaginasMaximas, 1000, "El máximo de páginas", "páginas")
            : PaginasMaximas;
        var nuevosCampos = campos is null ? Campos : EsquemaDeFormulario.Validar(campos);
        var nuevoModo = Modo;
        if (modo is { } pedido)
        {
            if (!Enum.IsDefined(pedido))
                throw ReglaDeNegocioException.Validacion("modo-invalido", "El modo debe ser Archivo, Formulario o ArchivoYFormulario.");
            if (pedido == ModoCaptura.Archivo && canonico?.Clase == ClaseDocumento.Formulario)
                throw ReglaDeNegocioException.Validacion("modo-invalido", $"{canonico.Codigo} es un formulario: usa Formulario o ArchivoYFormulario.");
            if (pedido != ModoCaptura.Archivo && nuevosCampos.Count == 0)
                throw ReglaDeNegocioException.Validacion("campos-requeridos", $"El modo {pedido} necesita al menos un campo.");
            nuevoModo = pedido;
        }

        ValidarCaptura(nuevoModo, nuevosCampos, Ambito);
        FormatosPermitidos = nuevosFormatos;
        TamanoMaximoMb = nuevoTamano;
        PaginasMaximas = nuevasPaginas;
        Campos = nuevosCampos;
        Modo = nuevoModo;
    }

    /// <summary>Reglas de archivo efectivas con los límites globales de la plataforma.</summary>
    public ReglasDeArchivo ReglasEfectivas(long tamanoGlobalBytes, int paginasGlobales) =>
        ReglasDeArchivo.Efectivas(CatalogoCanonico.Buscar(TipoCanonico), this, tamanoGlobalBytes, paginasGlobales);

    // ---------- Plantilla (TD-5) ----------

    /// <summary>
    /// Paso 1 de la plantilla: reserva la versión siguiente y la clave temporal (reemplaza otra subida en curso). El tipo
    /// declarado debe ser PDF, DOCX o XLSX (415) y el tamaño no superar <paramref name="tamanoMaximoBytes"/> (413).
    /// </summary>
    public CargaDePlantilla IniciarPlantilla(string bucket, string nombreArchivo, long tamanoBytes, string mime, string? sha256,
        long tamanoMaximoBytes, TimeSpan vigencia, DateTimeOffset ahora)
    {
        var nombre = CargaDeDocumento.NormalizarNombre(nombreArchivo);
        var tipo = (mime ?? string.Empty).Trim().ToLowerInvariant();
        if (tamanoBytes <= 0)
            throw ReglaDeNegocioException.Validacion("tamano-invalido", "Indica el tamaño del archivo en bytes.");
        if (tamanoBytes > tamanoMaximoBytes)
            throw ReglaDeNegocioException.DemasiadoGrande($"La plantilla supera el máximo de {FormatosDeArchivo.Tamano(tamanoMaximoBytes)}.");
        if (!FormatosDePlantilla.Permitidos.Contains(tipo))
            throw ReglaDeNegocioException.TipoNoSoportado($"La plantilla debe ser {FormatosDePlantilla.Descripcion}.");

        var id = Guid.CreateVersion7();
        var version = Plantillas.Count == 0 ? 1 : Plantillas.Max(p => p.Version) + 1;
        CargaDePlantilla = new CargaDePlantilla(id, version, bucket, $"cargas/{id:N}", nombre, tamanoBytes, tipo,
            CargaDeDocumento.NormalizarSha256(sha256), ahora + vigencia);
        return CargaDePlantilla;
    }

    /// <summary>La carga pendiente de esa versión; si no hay (o es otra) ⇒ 404 <c>carga-no-encontrada</c>.</summary>
    public CargaDePlantilla CargaDePlantillaRequerida(int version) =>
        CargaDePlantilla is { } carga && carga.Version == version
            ? carga
            : throw ReglaDeNegocioException.NoEncontrado("carga-no-encontrada",
                "No hay una carga de plantilla pendiente para esa versión: iníciala de nuevo.");

    /// <summary>
    /// Paso 2: registra la versión verificada y la vuelve vigente. Mismo SHA-256 que la vigente ⇒ no hay versión nueva
    /// (devuelve la vigente con <c>Duplicado</c>).
    /// </summary>
    public (PlantillaDeTipo Plantilla, bool Duplicado) ConfirmarPlantilla(int version, string sha256, string mimeReal, long tamano,
        string claveObjeto, string cargadaPor, DateTimeOffset ahora)
    {
        var carga = CargaDePlantillaRequerida(version);
        CargaDePlantilla = null;
        if (Plantilla is { } vigente && vigente.Sha256 == sha256)
            return (vigente, true);

        var nueva = new PlantillaDeTipo
        {
            Version = carga.Version,
            Sha256 = sha256,
            NombreOriginal = carga.NombreOriginal,
            Mime = mimeReal,
            Tamano = tamano,
            Bucket = carga.Bucket,
            ClaveObjeto = claveObjeto,
            CargadaPor = cargadaPor,
            CargadaEn = ahora
        };
        Plantillas.Add(nueva);
        PlantillaVigente = nueva.Version;
        return (nueva, false);
    }

    /// <summary>Deja el tipo sin plantilla vigente (el historial se conserva). Sin vigente ⇒ 404 <c>plantilla-no-encontrada</c>.</summary>
    public PlantillaDeTipo RetirarPlantilla(DateTimeOffset ahora)
    {
        var vigente = Plantilla ?? throw ReglaDeNegocioException.NoEncontrado("plantilla-no-encontrada", "El tipo no tiene plantilla vigente.");
        vigente.RetiradaEn = ahora;
        PlantillaVigente = null;
        return vigente;
    }

    /// <summary>La plantilla vigente (sin <paramref name="version"/>) o esa versión del historial; si no ⇒ 404 <c>plantilla-no-encontrada</c>.</summary>
    public PlantillaDeTipo PlantillaRequerida(int? version = null) =>
        (version is { } n ? Plantillas.FirstOrDefault(p => p.Version == n) : Plantilla)
        ?? throw ReglaDeNegocioException.NoEncontrado("plantilla-no-encontrada", "El tipo no tiene esa plantilla.");

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
        int? diasRevalidacion, string? textoAyuda, bool activo)
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

        Ambito = ResolverAmbito(TipoCanonico, ambito);
        Nombre = nombre.Trim();
        ReglaFrescura = regla;
        DiasAviso = avisos;
        DiasGracia = diasGracia ?? 15;
        DiasRevalidacion = diasRevalidacion ?? 15;
        TextoAyuda = string.IsNullOrWhiteSpace(textoAyuda) ? null : textoAyuda.Trim();
        Activo = activo;
    }

    private static int? Limite(int valor, int? delCanonico, int maximo, string que, string unidad)
    {
        if (valor == 0)
            return null;
        if (valor < 0 || valor > maximo)
            throw ReglaDeNegocioException.Validacion("limite-invalido", $"{que} debe estar entre 1 y {maximo} {unidad} (0 vuelve al límite global).");
        if (delCanonico is { } tope && valor > tope)
            throw ReglaDeNegocioException.Validacion("limite-mas-laxo", $"{que} no puede superar el del canónico ({tope} {unidad}).");
        return valor;
    }

    /// <summary>Coherencia entre modo, campos y ámbito.</summary>
    private static void ValidarCaptura(ModoCaptura modo, IReadOnlyCollection<CampoFormulario> campos, AmbitoDocumento ambito)
    {
        if (modo == ModoCaptura.Archivo && campos.Count > 0)
            throw ReglaDeNegocioException.Validacion("modo-invalido", "Un tipo de solo archivo no lleva campos: usa Formulario o ArchivoYFormulario.");
        if (campos.Count > 0 && ambito != AmbitoDocumento.PorVinculo)
            throw ReglaDeNegocioException.Validacion("formulario-solo-por-vinculo",
                "Un tipo con campos es siempre PorVinculo: los datos que pide una empresa no se comparten con otras.");
    }

    /// <summary>Tipos que se siembran al crear un tenant: los canónicos de archivo más comunes (ningún flujo los pide aún).</summary>
    public static IReadOnlyList<TipoDocumentoTenant> Semilla(Guid tenantId, DateTimeOffset ahora) =>
    [
        .. CatalogoCanonico.SemillaDeTenant.Select(codigo =>
        {
            var canonico = CatalogoCanonico.Requerido(codigo);
            var avisos = codigo is "POLIZA" or "ESTADOS_FINANCIEROS" ? new[] { 60, 30, 7 } : null;
            return Crear(tenantId, codigo, canonico.Nombre, codigo, null, null, avisos, null, null, null, ahora);
        })
    ];
}

/// <summary>Versión de la plantilla descargable de un tipo (bucket del tenant, clave content-addressed).</summary>
public sealed class PlantillaDeTipo
{
    public int Version { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string NombreOriginal { get; set; } = string.Empty;

    public string Mime { get; set; } = string.Empty;

    public long Tamano { get; set; }

    public string Bucket { get; set; } = string.Empty;

    public string ClaveObjeto { get; set; } = string.Empty;

    public string CargadaPor { get; set; } = string.Empty;

    public DateTimeOffset CargadaEn { get; set; }

    public DateTimeOffset? RetiradaEn { get; set; }
}

/// <summary>Subida de plantilla en curso (paso 1): versión reservada y zona temporal <c>cargas/{id}</c> del bucket del tenant.</summary>
public sealed record CargaDePlantilla(
    Guid Id, int Version, string Bucket, string ClaveTemporal, string NombreOriginal, long TamanoDeclarado, string MimeDeclarado,
    string? Sha256Declarado, DateTimeOffset Expira);

/// <summary>
/// Formatos de una plantilla (TD-5): PDF y, como la empresa suele entregar formatos para diligenciar, DOCX y XLSX. El tipo
/// real sale de los bytes: <c>%PDF-</c> o un ZIP de Office Open XML con <c>word/document.xml</c> o <c>xl/workbook.xml</c>.
/// </summary>
public static class FormatosDePlantilla
{
    public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static readonly IReadOnlyList<string> Permitidos = [InspectorDeArchivos.Pdf, Docx, Xlsx];

    public const string Descripcion = "PDF, DOCX o XLSX";

    /// <summary>Tipo real de la plantilla; nulo si no es ninguno de los permitidos.</summary>
    public static string? Detectar(byte[] contenido)
    {
        if (InspectorDeArchivos.DetectarMime(contenido) == InspectorDeArchivos.Pdf)
            return InspectorDeArchivos.Pdf;
        if (contenido.Length < 4 || contenido[0] != 0x50 || contenido[1] != 0x4B || contenido[2] != 0x03 || contenido[3] != 0x04)
            return null;

        try
        {
            using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(contenido), System.IO.Compression.ZipArchiveMode.Read);
            var nombres = zip.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return nombres.Contains("word/document.xml") ? Docx : nombres.Contains("xl/workbook.xml") ? Xlsx : null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }
}
