using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Documentos;

/// <summary>Versión inmutable de un documento (docs/02 §10.1, <c>version_documento</c>). Solo cambia su estado.</summary>
public sealed class VersionDocumento
{
    public int Numero { get; set; }

    public EstadoVersion Estado { get; set; }

    public ClaseDocumento Clase { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string Bucket { get; set; } = string.Empty;

    public string ClaveObjeto { get; set; } = string.Empty;

    public string MimeDeclarado { get; set; } = string.Empty;

    public long Tamano { get; set; }

    public string NombreOriginal { get; set; } = string.Empty;

    public Guid? ContextoVinculoId { get; set; }

    public string CargadaPor { get; set; } = string.Empty;

    public DateTimeOffset CargadaEn { get; set; }

    public HechosBasicos? Hechos { get; set; }

    public string? MotivoDevolucion { get; set; }

    public string? DetalleDevolucion { get; set; }

    public string? MotivoRetiro { get; set; }

    /// <summary>Cuándo el tercero retiró la versión; nula si no está retirada (o se retiró antes de que se guardara la fecha).</summary>
    public DateTimeOffset? RetiradaEn { get; set; }

    /// <summary>Alguna empresa emitió una descarga de esta versión: su binario ya no se elimina al retirarla (docs/02 §10.3).</summary>
    public bool AccedidaPorEmpresa { get; set; }

    /// <summary>
    /// El tercero pidió suprimirla (habeas data, OD-6) y no la referencia ninguna aceptación ni certificado: su binario, sus datos
    /// declarados, el nombre original y el análisis de IA se eliminaron y queda solo el rastro técnico (hash, tamaño, fechas).
    /// </summary>
    public bool Suprimida { get; set; }

    /// <summary>
    /// Se conserva por obligación legal pese a una solicitud de supresión (OD-6): la referencia una aceptación o un certificado. No
    /// se anonimiza ni se altera (rompería el hash) y no se vuelve a compartir.
    /// </summary>
    public bool RetenidaPorLey { get; set; }

    public string? BaseLegalDeRetencion { get; set; }

    public DateOnly? RetenidaHasta { get; set; }

    /// <summary>Huella de los datos declarados (TD-4); nula si la versión es solo archivo.</summary>
    public string? HashDatos { get; set; }

    /// <summary>Datos que declaró el tercero, en el orden del formulario; nulos si la versión es solo archivo.</summary>
    public List<DatoDeclarado>? Datos { get; set; }

    /// <summary>Vigencia por vínculo, cada una con la regla del tipo de su tenant (TD-6).</summary>
    public List<VigenciaDeVersion> Vigencias { get; set; } = [];

    /// <summary>Una versión solo de formulario no tiene binario que descargar.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool TieneArchivo => ClaveObjeto.Length > 0;

    /// <summary>Resultado del análisis con OpenAI (ADR-0012); nulo si no se analizó.</summary>
    public AnalisisDeIaDeVersion? Ia { get; set; }

    /// <summary>Motivo del análisis de IA fallido (fail-closed, IA-6): la versión no se vuelve a analizar.</summary>
    public string? FalloIa { get; set; }

    /// <summary>La versión ya tiene un resultado de IA (análisis o fallo): no se vuelve a llamar a OpenAI.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IaResuelta => Ia is not null || FalloIa is not null;

    /// <summary>
    /// Hechos como los ve quien consulta (IA-3): con <paramref name="conIa"/> (la empresa consumió su validación) los del
    /// análisis; sin él los básicos de siempre (sin IA, pendiente de revisión humana y la comparación por hash).
    /// </summary>
    public HechosBasicos? HechosPara(bool conIa) =>
        Hechos is not { } hechos
            ? null
            : conIa && Ia is not null
                ? hechos
                : hechos with { ConIa = false, PendienteRevisionHumana = true, Comparacion = Ia?.ComparacionSinIa ?? hechos.Comparacion };

    public VigenciaDeVersion? VigenciaPara(Guid vinculoId) => Vigencias.FirstOrDefault(v => v.VinculoId == vinculoId);

    /// <summary>El vencimiento más próximo entre los vínculos (lo que obliga primero al tercero, docs/02 §8.3).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public VigenciaDeVersion? ProximaVigencia => Vigencias.Where(v => v.VigenteHasta is not null).MinBy(v => v.VigenteHasta);
}

/// <summary>
/// Lo que el análisis con OpenAI dejó en la versión (proyección de <see cref="AnalisisDeIaRegistrado"/>). Solo lo ve una
/// empresa que consumió su validación de esa versión; <see cref="ComparacionSinIa"/> conserva la comparación por hash
/// para quien no la consumió.
/// </summary>
public sealed class AnalisisDeIaDeVersion
{
    public string Modelo { get; set; } = string.Empty;

    public bool Escalado { get; set; }

    public decimal Confianza { get; set; }

    public bool Legible { get; set; }

    public string TipoDetectado { get; set; } = string.Empty;

    public Dictionary<string, JsonElement> Campos { get; set; } = [];

    public List<string> Alertas { get; set; } = [];

    public string? Observaciones { get; set; }

    public int? PaginasEnviadas { get; set; }

    public int? PaginasOmitidas { get; set; }

    public bool PendienteRevisionHumana { get; set; }

    public List<string> CamposCambiados { get; set; } = [];

    public List<string> CamposCriticosCambiados { get; set; } = [];

    public Comparacion ComparacionSinIa { get; set; }

    public Guid EmpresaQuePago { get; set; }

    public DateTimeOffset Fecha { get; set; }
}

/// <summary>Vínculo con el que la versión actual está compartida y el tipo del tenant cuya regla define su vigencia.</summary>
public sealed record DestinoDeVigencia(Guid TenantId, Guid EmpresaId, Guid VinculoId, TipoDocumentoTenant Tipo);

/// <summary>Versión visible para una empresa por su vínculo (<c>comparticion_version</c>); nunca se revoca (docs/02 §6).</summary>
public sealed record Comparticion(int Numero, Guid TenantId, Guid EmpresaId, Guid VinculoId, OrigenComparticion Origen, DateTimeOffset Fecha);

/// <summary>
/// Vínculo candidato a recibir la versión actual, con lo que la aplicación resolvió fuera del agregado: si el tercero
/// aceptó la invitación, el estado del vínculo y si su flujo vigente o su expediente abierto pide el tipo
/// (<see cref="ExigeElTipo"/>; RQ-3, antes las exigencias por tipo de BO-3).
/// </summary>
public sealed record VinculoCandidato(
    Guid VinculoId, Guid TenantId, Guid EmpresaId, Guid TerceroId, bool InvitacionAceptada, EstadoVinculo Estado, bool ExigeElTipo)
{
    /// <summary>Desvinculado, bloqueado o anulado: la compartición hacia adelante está congelada (docs/02 §6.1, B-5).</summary>
    public bool Congelado => Estado is EstadoVinculo.Bloqueado or EstadoVinculo.Desvinculado or EstadoVinculo.Anulado;
}

/// <summary>Archivo ya leído y verificado por el servidor (hash, tamaño, magic bytes, páginas) listo para registrarse.</summary>
public sealed record ArchivoVerificado(
    int Numero,
    string Sha256,
    string Bucket,
    string ClaveObjeto,
    string MimeDeclarado,
    string MimeReal,
    long Tamano,
    string NombreOriginal,
    int? Paginas,
    Guid? ContextoVinculoId,
    string CargadaPor);

/// <summary>Motivo por el que una versión vuelve al tercero (código estable y texto para él).</summary>
public sealed record MotivoDeDevolucion(string Codigo, string Detalle);

/// <summary>
/// Documento de la bóveda (docs/03 §4), event-sourced en el tenant técnico <c>boveda</c>. Un documento <c>Global</c>
/// existe una vez por tercero y tipo canónico; uno <c>PorVinculo</c> pertenece a un vínculo (y por tanto a una empresa
/// de un tenant). Sus versiones son inmutables, a lo sumo una es <c>Actual</c>, el mismo SHA-256 no crea dos versiones y
/// nada se comparte con una empresa antes de que el tercero acepte su invitación (B-1). Los métodos de decisión devuelven
/// eventos; los <c>Apply</c> solo evolucionan el estado.
/// </summary>
public sealed class Documento
{
    private static readonly JsonSerializerOptions OpcionesClon = new();

    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid TerceroId { get; set; }

    public AmbitoDocumento Ambito { get; set; }

    public ClaseDocumento Clase { get; set; }

    public string? TipoCanonico { get; set; }

    public Guid? TipoDocumentoTenantId { get; set; }

    public string NombreTipo { get; set; } = string.Empty;

    /// <summary>Solo en <c>PorVinculo</c>: tenant, empresa y vínculo dueños.</summary>
    public Guid? TenantId { get; set; }

    public Guid? EmpresaId { get; set; }

    public Guid? VinculoId { get; set; }

    /// <summary><c>G|{tercero}|{canónico}</c> o <c>V|{vínculo}|{tipo del tenant}</c>; índice único.</summary>
    public string ClaveNatural { get; set; } = string.Empty;

    public int? VersionActual { get; set; }

    public List<VersionDocumento> Versiones { get; set; } = [];

    public List<Comparticion> Comparticiones { get; set; } = [];

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    public static string ClaveGlobal(Guid terceroId, string tipoCanonico) => $"G|{terceroId:N}|{tipoCanonico}";

    public static string ClavePorVinculo(Guid vinculoId, Guid tipoDocumentoTenantId) => $"V|{vinculoId:N}|{tipoDocumentoTenantId:N}";

    /// <summary>Número que tomará la próxima versión (sin huecos: también cuentan las devueltas y retiradas).</summary>
    public int SiguienteNumero => Versiones.Count == 0 ? 1 : Versiones.Max(v => v.Numero) + 1;

    public VersionDocumento? Buscar(int numero) => Versiones.FirstOrDefault(v => v.Numero == numero);

    public VersionDocumento VersionRequerida(int numero) =>
        Buscar(numero) ?? throw ReglaDeNegocioException.NoEncontrado("version-no-encontrada", $"El documento no tiene la versión {numero}.");

    public VersionDocumento? BuscarPorHash(string sha256) =>
        Versiones.FirstOrDefault(v => string.Equals(v.Sha256, sha256, StringComparison.OrdinalIgnoreCase));

    /// <summary>Misma versión = mismo archivo y mismos datos declarados (docs/02 §5 regla 1: <c>sha256</c> o <c>hash_datos</c>).</summary>
    public VersionDocumento? BuscarPorHuella(string sha256, string? hashDatos) =>
        Versiones.FirstOrDefault(v => string.Equals(v.Sha256, sha256, StringComparison.OrdinalIgnoreCase) &&
                                      string.Equals(v.HashDatos, hashDatos, StringComparison.OrdinalIgnoreCase));

    public bool EstaCompartida(int numero, Guid vinculoId) => Comparticiones.Any(c => c.Numero == numero && c.VinculoId == vinculoId);

    // ---------- Decisiones ----------

    /// <summary>Documento global del tercero para un tipo canónico de ámbito <c>Global</c>.</summary>
    public static DocumentoCreado CrearGlobal(Guid terceroId, TipoDocumentoCanonico canonico, DateTimeOffset ahora)
    {
        if (canonico.Ambito != AmbitoDocumento.Global)
            throw ReglaDeNegocioException.Validacion("vinculo-requerido",
                $"{canonico.Codigo} es un documento por vínculo: indica el vínculo (empresa) al que pertenece.");
        AsegurarClaseSoportada(canonico.Clase, canonico.Codigo);

        return new DocumentoCreado(Guid.CreateVersion7(), terceroId, AmbitoDocumento.Global, canonico.Clase, canonico.Codigo, null,
            canonico.Nombre, null, null, null, ahora);
    }

    /// <summary>Documento privado de un vínculo: solo existe para esa empresa (E5).</summary>
    public static DocumentoCreado CrearPorVinculo(Guid terceroId, TipoDocumentoTenant tipo, Guid tenantId, Guid empresaId, Guid vinculoId,
        DateTimeOffset ahora)
    {
        if (tipo.Ambito != AmbitoDocumento.PorVinculo)
            throw new InvalidOperationException("Un tipo de ámbito Global se guarda en el documento global del canónico.");
        if (tipo.TenantId != tenantId)
            throw new InvalidOperationException("El tipo de documento es de otro tenant.");
        AsegurarCargaSoportada(tipo);

        return new DocumentoCreado(Guid.CreateVersion7(), terceroId, AmbitoDocumento.PorVinculo, tipo.Clase, tipo.TipoCanonico, tipo.Id,
            tipo.Nombre, tenantId, empresaId, vinculoId, ahora);
    }

    /// <summary>Un canónico global de clase <c>Formulario</c> aún no tiene esquema: 422 <c>clase-no-soportada</c>.</summary>
    public static void AsegurarClaseSoportada(ClaseDocumento clase, string codigo)
    {
        if (clase != ClaseDocumento.Archivo)
            throw ReglaDeNegocioException.Validacion("clase-no-soportada",
                $"{codigo} es un formulario sin esquema canónico todavía; la empresa puede pedirlo con sus propios campos (PorVinculo).");
    }

    /// <summary>El tipo del tenant se puede entregar (<see cref="TipoDocumentoTenant.CargaSoportada"/>); si no ⇒ 422 <c>clase-no-soportada</c>.</summary>
    public static void AsegurarCargaSoportada(TipoDocumentoTenant tipo)
    {
        if (!tipo.CargaSoportada)
            throw ReglaDeNegocioException.Validacion("clase-no-soportada",
                $"{tipo.Codigo} es un formulario sin campos configurados (o global): la empresa debe definir sus campos y dejarlo PorVinculo.");
    }

    /// <summary>Mismo archivo y mismos datos ya registrados en el documento ⇒ el hecho de duplicado (sin versión nueva); si no, nulo.</summary>
    public VersionDuplicadaDetectada? DetectarDuplicado(string sha256, DateTimeOffset ahora, string? hashDatos = null) =>
        BuscarPorHuella(sha256, hashDatos) is { } existente
            ? new VersionDuplicadaDetectada(existente.Numero, existente.Sha256, ahora, existente.HashDatos)
            : null;

    /// <summary>
    /// Registra un archivo verificado: <c>VersionCargada</c> → <c>AnalisisDeVersionIniciado</c> (sin IA) →
    /// <c>HechosDeVersionRegistrados</c> → <c>VersionActualCambiada</c> (la anterior queda superada) o
    /// <c>VersionDevueltaAlTercero</c>. Sin IA basta el hash verificado, el MIME real permitido y el antivirus (D-3); si
    /// el antivirus no está disponible y se exige, la versión espera en <c>EnAnalisis</c> (T-11).
    /// </summary>
    public IReadOnlyList<object> RegistrarVersionArchivo(ArchivoVerificado archivo, MotivoDeDevolucion? devolucion, Antivirus antivirus,
        bool exigirAntivirus, DateTimeOffset ahora, DatosVerificados? datos = null)
    {
        if (BuscarPorHuella(archivo.Sha256, datos?.Hash) is not null)
            throw ReglaDeNegocioException.Conflicto("version-duplicada", "Ese archivo ya es una versión de este documento.");

        if (archivo.Numero != SiguienteNumero)
            throw ReglaDeNegocioException.Conflicto("carga-desactualizada",
                $"La versión {archivo.Numero} ya no es la siguiente del documento ({SiguienteNumero}); inicia la carga de nuevo.");

        var comparacion = VersionActual is null ? Comparacion.SinAnterior : Comparacion.Distinta;
        var hechos = new HechosBasicos(true, archivo.MimeReal, archivo.Paginas, antivirus, comparacion, ConIa: false, PendienteRevisionHumana: true);

        var eventos = new List<object>
        {
            new VersionCargada(archivo.Numero, ClaseDocumento.Archivo, archivo.Sha256, archivo.Bucket, archivo.ClaveObjeto, archivo.MimeDeclarado,
                archivo.Tamano, archivo.NombreOriginal, archivo.ContextoVinculoId, archivo.CargadaPor, ahora, datos?.Hash, datos?.Datos),
            new AnalisisDeVersionIniciado(archivo.Numero, ConIa: false, ahora),
            new HechosDeVersionRegistrados(archivo.Numero, hechos, ahora)
        };

        if (antivirus is Antivirus.Infectado or Antivirus.Cuarentena)
            devolucion ??= new MotivoDeDevolucion("archivo-infectado", "El antivirus detectó contenido malicioso en el archivo.");

        if (devolucion is not null)
            eventos.Add(new VersionDevueltaAlTercero(archivo.Numero, devolucion.Codigo, devolucion.Detalle, ahora));
        else if (antivirus == Antivirus.Limpio || !exigirAntivirus)
            eventos.Add(new VersionActualCambiada(archivo.Numero, VersionActual, comparacion, [], [], ConIa: false, ahora));

        return eventos;
    }

    /// <summary>
    /// Registra una versión solo de formulario (TD-4): sin binario, su huella es la de los datos. No hay antivirus que
    /// esperar ni páginas que revisar: pasa a <c>Actual</c> (la anterior queda superada). Mismos datos ⇒ 409
    /// <c>version-duplicada</c> (el llamador detecta antes el duplicado con <see cref="DetectarDuplicado"/>).
    /// </summary>
    public IReadOnlyList<object> RegistrarVersionFormulario(DatosVerificados datos, Guid? contextoVinculoId, string cargadaPor, DateTimeOffset ahora)
    {
        if (BuscarPorHuella(datos.Hash, datos.Hash) is not null)
            throw ReglaDeNegocioException.Conflicto("version-duplicada", "Esos datos ya son una versión de este documento.");

        var numero = SiguienteNumero;
        var comparacion = VersionActual is null ? Comparacion.SinAnterior : Comparacion.Distinta;
        var hechos = new HechosBasicos(true, "application/json", null, Antivirus.NoDisponible, comparacion, ConIa: false, PendienteRevisionHumana: true);
        return
        [
            new VersionCargada(numero, ClaseDocumento.Formulario, datos.Hash, string.Empty, string.Empty, "application/json", 0, "formulario",
                contextoVinculoId, cargadaPor, ahora, datos.Hash, datos.Datos),
            new AnalisisDeVersionIniciado(numero, ConIa: false, ahora),
            new HechosDeVersionRegistrados(numero, hechos, ahora),
            new VersionActualCambiada(numero, VersionActual, comparacion, [], [], ConIa: false, ahora)
        ];
    }

    /// <summary>
    /// Vigencias de la versión actual para cada vínculo con el que está compartida (TD-6), cada una con la regla del tipo
    /// del tenant de ese vínculo: <c>MaxDiasDesdeExpedicion</c> desde <c>fecha_expedicion</c> declarada (o la carga),
    /// acotada por <c>fecha_vencimiento</c> declarada (docs/02 §8.1). Solo emite lo que cambió (idempotente).
    /// </summary>
    public IReadOnlyList<VigenciaDeVersionCalculada> CalcularVigencias(IEnumerable<DestinoDeVigencia> destinos, DateTimeOffset ahora)
    {
        if (VersionActual is not { } numero)
            return [];

        var version = VersionRequerida(numero);
        var eventos = new List<VigenciaDeVersionCalculada>();
        foreach (var destino in destinos.DistinctBy(d => d.VinculoId))
        {
            if (!EstaCompartida(numero, destino.VinculoId))
                continue;

            var tipo = destino.Tipo;
            var previa = version.VigenciaPara(destino.VinculoId);
            var calculo = CalculadoraDeVigencia.Calcular(tipo.ReglaFrescura, version.CargadaEn, version.Datos, previa?.RatificadaEn);
            if (previa is not null && previa.VigenteHasta == calculo.VigenteHasta && previa.Base == calculo.Base &&
                previa.DiasGracia == tipo.DiasGracia && previa.TipoDocumentoTenantId == tipo.Id && previa.DiasAviso.SequenceEqual(tipo.DiasAviso))
                continue;

            eventos.Add(new VigenciaDeVersionCalculada(numero, destino.TenantId, destino.EmpresaId, destino.VinculoId, tipo.Id, calculo.VigenteHasta,
                calculo.Base, [.. tipo.DiasAviso], tipo.DiasGracia, ahora));
        }

        return eventos;
    }

    /// <summary>
    /// La versión vence para el vínculo si sigue siendo la actual, su vigencia allí tiene fecha y ya pasó
    /// <c>vigenteHasta + diasGracia</c>; si no (o ya venció), nulo. No toca el vínculo ni abre renovaciones (H6).
    /// </summary>
    /// <summary>
    /// El tercero ratifica la versión actual para un vínculo (RN-2, condición única I-14): el tipo lo permite, la versión es
    /// la actual, está compartida con el vínculo y no trae una <c>fecha_vencimiento</c> ya cumplida. Si no, 409 o 422
    /// <c>ratificacion-no-permitida</c>. La vigencia se recalcula después con <see cref="CalcularVigencias"/>.
    /// </summary>
    public VersionRatificada Ratificar(int numero, Guid vinculoId, bool permiteRatificacion, string por, DateTimeOffset ahora)
    {
        if (VersionActual != numero)
            throw ReglaDeNegocioException.Conflicto("version-superada", $"La versión {numero} ya no es la actual del documento.");
        if (!EstaCompartida(numero, vinculoId))
            throw ReglaDeNegocioException.Conflicto("version-no-compartida", "Esa versión no está compartida con la empresa.");
        if (!permiteRatificacion)
            throw ReglaDeNegocioException.Validacion("ratificacion-no-permitida", "Este documento no se puede ratificar: sube una versión nueva.");
        if (EsquemaDeFormulario.Fecha(VersionRequerida(numero).Datos, EsquemaDeFormulario.FechaVencimiento) is { } vence &&
            vence <= CalculadoraDeVigencia.Fecha(ahora))
            throw ReglaDeNegocioException.Validacion("ratificacion-no-permitida", "El documento ya venció: sube una versión nueva.");
        return new VersionRatificada(numero, vinculoId, por, ahora);
    }

    public VersionVencida? Vencer(int numero, Guid vinculoId, DateTimeOffset ahora)
    {
        if (VersionActual != numero || Buscar(numero)?.VigenciaPara(vinculoId) is not { VigenteHasta: { } hasta, VencidaEn: null } vigencia)
            return null;
        return CalculadoraDeVigencia.Fecha(ahora) > vigencia.FinDeGracia
            ? new VersionVencida(numero, vinculoId, hasta, vigencia.DiasGracia, ahora)
            : null;
    }

    /// <summary>
    /// ¿Se puede retirar la versión? Solo la actual, una superada o una vencida; una devuelta o ya retirada ⇒ 409
    /// <c>estado-invalido</c>. Lo usan el retiro y la consulta de su impacto, que deben coincidir.
    /// </summary>
    public VersionDocumento VersionRetirable(int numero)
    {
        var version = VersionRequerida(numero);
        if (version.Estado is not (EstadoVersion.Actual or EstadoVersion.Superada or EstadoVersion.Vencida))
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"No se puede retirar una versión {version.Estado}.");
        return version;
    }

    /// <summary>
    /// La versión que quedaría vigente si se retirara <paramref name="numero"/> (I-11): la misma actual si era otra; si era la
    /// actual, la superada más reciente, o ninguna si no hay. No comprueba que se pueda retirar.
    /// </summary>
    public int? VersionTrasRetirar(int numero) =>
        VersionActual == numero
            ? Versiones.Where(v => v.Estado == EstadoVersion.Superada && v.Numero != numero).MaxBy(v => v.Numero)?.Numero
            : VersionActual;

    /// <summary>
    /// El tercero retira una versión: no vuelve a usarse ni a compartirse. Si era la actual, la superada más reciente
    /// vuelve a serlo (I-11). Devuelta o ya retirada ⇒ 409; sin motivo ⇒ 422. Qué pasa con las empresas que la tenían (revalidar,
    /// volver a revisar) lo calcula la aplicación (<c>ImpactoDeRetiro</c>) y lo dispara la sincronización de requisitos.
    /// </summary>
    public IReadOnlyList<object> RetirarVersion(int numero, string? motivo, DateTimeOffset ahora)
    {
        VersionRetirable(numero);
        if (string.IsNullOrWhiteSpace(motivo))
            throw ReglaDeNegocioException.Validacion("motivo-requerido", "Indica por qué retiras la versión.");

        var eventos = new List<object> { new VersionRetirada(numero, motivo.Trim(), ahora) };
        if (VersionActual == numero && VersionTrasRetirar(numero) is { } anterior)
            eventos.Add(new VersionActualRestituida(anterior, numero, ahora));
        return eventos;
    }

    /// <summary>
    /// Suprime una versión por una solicitud de habeas data (OD-6): ya no hay binario, datos declarados ni análisis. Solo si no está
    /// retenida por ley; una ya suprimida no repite el evento (nulo). La aplicación decide antes que ninguna aceptación ni certificado la
    /// referencia.
    /// </summary>
    public VersionSuprimida? Suprimir(int numero, string motivo, DateTimeOffset ahora)
    {
        var version = VersionRequerida(numero);
        if (version.RetenidaPorLey)
            throw ReglaDeNegocioException.Conflicto("version-retenida-por-ley", "Esa versión se conserva por obligación legal y no se puede suprimir.");
        return version.Suprimida ? null : new VersionSuprimida(numero, motivo, ahora);
    }

    /// <summary>
    /// Marca una versión como retenida por ley (OD-6): una solicitud de supresión no la elimina porque la referencia una aceptación o un
    /// certificado. Con la misma base y fecha no repite el evento (nulo); una retención más larga la reemplaza.
    /// </summary>
    public RetencionLegalMarcada? MarcarRetencion(int numero, string baseLegal, DateOnly? retenidaHasta, Guid solicitudId, DateTimeOffset ahora)
    {
        var version = VersionRequerida(numero);
        return version.RetenidaPorLey && version.BaseLegalDeRetencion == baseLegal && version.RetenidaHasta == retenidaHasta
            ? null
            : new RetencionLegalMarcada(numero, baseLegal, retenidaHasta, solicitudId, ahora);
    }

    /// <summary>
    /// Comparte la versión actual con un vínculo si corresponde; nulo si no: sin versión actual, invitación no aceptada
    /// (B-1), compartición congelada, el flujo del vínculo no pide el tipo (globales) o es otro vínculo (<c>PorVinculo</c>), o ya
    /// estaba compartida (idempotente por <c>(vínculo, versión)</c>, invariante 8 de docs/02 §13).
    /// </summary>
    public VersionCompartida? Compartir(VinculoCandidato vinculo, OrigenComparticion origen, DateTimeOffset ahora)
    {
        if (vinculo.TerceroId != TerceroId)
            throw new InvalidOperationException("El vínculo es de otro tercero.");

        if (VersionActual is not { } actual || !vinculo.InvitacionAceptada || vinculo.Congelado)
            return null;

        // Retenida por una solicitud de supresión (OD-6): se conserva, pero no se vuelve a compartir con nadie.
        if (Buscar(actual) is { RetenidaPorLey: true })
            return null;

        var corresponde = Ambito == AmbitoDocumento.Global ? vinculo.ExigeElTipo : vinculo.VinculoId == VinculoId;
        if (!corresponde || EstaCompartida(actual, vinculo.VinculoId))
            return null;

        return new VersionCompartida(actual, vinculo.TenantId, vinculo.EmpresaId, vinculo.VinculoId, origen, ahora);
    }

    public IReadOnlyList<VersionCompartida> CompartirActual(IEnumerable<VinculoCandidato> vinculos, OrigenComparticion origen, DateTimeOffset ahora) =>
        [.. vinculos.Select(v => Compartir(v, origen, ahora)).OfType<VersionCompartida>()];

    /// <summary>
    /// Se emite una URL de descarga. Una empresa solo descarga versiones compartidas con su vínculo; si no, 404 (T-4).
    /// </summary>
    public DocumentoAccedido RegistrarAcceso(int numero, DestinoDeComparticion? empresa, string actorTipo, string actorId, string ip,
        string userAgent, string motivo, DateTimeOffset ahora)
    {
        var version = VersionRequerida(numero);
        if (empresa is not null && !EstaCompartida(version.Numero, empresa.VinculoId))
            throw ReglaDeNegocioException.NoEncontrado("version-no-encontrada", $"El documento no tiene la versión {numero}.");
        if (!version.TieneArchivo)
            throw ReglaDeNegocioException.Conflicto("version-sin-archivo", $"La versión {numero} es un formulario: sus datos vienen en la versión, no hay archivo.");

        return new DocumentoAccedido(numero, empresa?.TenantId, empresa?.EmpresaId, empresa?.VinculoId, actorTipo, actorId, ip, userAgent,
            motivo, ahora);
    }

    /// <summary>
    /// La versión anterior a <paramref name="numero"/> con análisis de IA (la más reciente no devuelta): contra ella se
    /// comparan los campos clave del análisis nuevo; nula si no hay.
    /// </summary>
    public VersionDocumento? AnteriorConIa(int numero) =>
        Versiones.Where(v => v.Numero < numero && v.Ia is not null && v.Estado != EstadoVersion.Devuelta).MaxBy(v => v.Numero);

    /// <summary>
    /// Registra el análisis de IA de una versión de archivo: una sola vez por versión (si ya tiene análisis o fallo ⇒ nulo,
    /// idempotente). La versión debe existir y tener archivo.
    /// </summary>
    public AnalisisDeIaRegistrado? RegistrarAnalisisDeIa(AnalisisDeIaRegistrado analisis)
    {
        var version = VersionRequerida(analisis.Numero);
        if (!version.TieneArchivo)
            throw new InvalidOperationException("Una versión sin archivo no se analiza con IA.");
        return version.IaResuelta ? null : analisis;
    }

    /// <summary>El análisis falló (fail-closed, IA-6): una sola vez por versión; si ya tiene resultado ⇒ nulo.</summary>
    public AnalisisDeIaFallido? RegistrarFalloDeIa(int numero, string motivo, Guid? empresaQuePago, DateTimeOffset ahora) =>
        VersionRequerida(numero).IaResuelta ? null : new AnalisisDeIaFallido(numero, motivo, ahora, empresaQuePago);

    /// <summary>Copia del documento con los eventos aplicados (para decidir sin tocar el agregado que Marten proyecta).</summary>
    public Documento Proyectar(IEnumerable<object> eventos)
    {
        var copia = JsonSerializer.Deserialize<Documento>(JsonSerializer.Serialize(this, OpcionesClon), OpcionesClon)!;
        foreach (var evento in eventos)
            copia.Aplicar(evento);
        return copia;
    }

    public void Aplicar(object evento)
    {
        switch (evento)
        {
            case VersionCargada e: Apply(e); break;
            case VersionDuplicadaDetectada e: Apply(e); break;
            case AnalisisDeVersionIniciado e: Apply(e); break;
            case HechosDeVersionRegistrados e: Apply(e); break;
            case VersionActualCambiada e: Apply(e); break;
            case VersionDevueltaAlTercero e: Apply(e); break;
            case VersionRetirada e: Apply(e); break;
            case VersionSuprimida e: Apply(e); break;
            case RetencionLegalMarcada e: Apply(e); break;
            case VersionActualRestituida e: Apply(e); break;
            case VersionCompartida e: Apply(e); break;
            case DocumentoAccedido e: Apply(e); break;
            case VigenciaDeVersionCalculada e: Apply(e); break;
            case VersionVencida e: Apply(e); break;
            case VersionRatificada e: Apply(e); break;
            case AnalisisDeIaRegistrado e: Apply(e); break;
            case AnalisisDeIaFallido e: Apply(e); break;
            default: throw new InvalidOperationException($"Evento no aplicable a Documento: {evento.GetType().Name}.");
        }
    }

    // ---------- Evolución ----------

    public static Documento Create(DocumentoCreado e) => new()
    {
        Id = e.DocumentoId,
        TerceroId = e.TerceroId,
        Ambito = e.Ambito,
        Clase = e.Clase,
        TipoCanonico = e.TipoCanonico,
        TipoDocumentoTenantId = e.TipoDocumentoTenantId,
        NombreTipo = e.NombreTipo,
        TenantId = e.TenantId,
        EmpresaId = e.EmpresaId,
        VinculoId = e.VinculoId,
        ClaveNatural = e.Ambito == AmbitoDocumento.Global
            ? ClaveGlobal(e.TerceroId, e.TipoCanonico!)
            : ClavePorVinculo(e.VinculoId!.Value, e.TipoDocumentoTenantId!.Value),
        CreadoEn = e.Fecha,
        ActualizadoEn = e.Fecha
    };

    public void Apply(VersionCargada e)
    {
        Versiones.Add(new VersionDocumento
        {
            Numero = e.Numero,
            Estado = EstadoVersion.Recibida,
            Clase = e.Clase,
            Sha256 = e.Sha256,
            Bucket = e.Bucket,
            ClaveObjeto = e.ClaveObjeto,
            MimeDeclarado = e.MimeDeclarado,
            Tamano = e.Tamano,
            NombreOriginal = e.NombreOriginal,
            ContextoVinculoId = e.ContextoVinculoId,
            CargadaPor = e.CargadaPor,
            CargadaEn = e.Fecha,
            HashDatos = e.HashDatos,
            Datos = e.Datos is null ? null : [.. e.Datos]
        });
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VigenciaDeVersionCalculada e)
    {
        var version = VersionRequerida(e.Numero);
        var previa = version.VigenciaPara(e.VinculoId);
        version.Vigencias.RemoveAll(v => v.VinculoId == e.VinculoId);
        version.Vigencias.Add(new VigenciaDeVersion
        {
            TenantId = e.TenantId,
            EmpresaId = e.EmpresaId,
            VinculoId = e.VinculoId,
            TipoDocumentoTenantId = e.TipoDocumentoTenantId,
            VigenteHasta = e.VigenteHasta,
            Base = e.Base,
            DiasAviso = [.. e.DiasAviso],
            DiasGracia = e.DiasGracia,
            CalculadaEn = e.Fecha,
            RatificadaEn = previa?.RatificadaEn,
            // Una vigencia recalculada solo conserva el vencimiento si su fin de gracia no cambió.
            VencidaEn = previa?.VencidaEn is { } vencida && previa.FinDeGracia == CalculadoraDeVigencia.FinDeGracia(e.VigenteHasta, e.DiasGracia)
                ? vencida
                : null
        });
        if (version.Estado == EstadoVersion.Vencida && !TodasVencidas(version))
            version.Estado = EstadoVersion.Actual;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VersionRatificada e)
    {
        if (VersionRequerida(e.Numero).VigenciaPara(e.VinculoId) is { } vigencia)
            vigencia.RatificadaEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VersionVencida e)
    {
        var version = VersionRequerida(e.Numero);
        if (version.VigenciaPara(e.VinculoId) is { } vigencia)
            vigencia.VencidaEn = e.Fecha;
        if (version.Estado == EstadoVersion.Actual && TodasVencidas(version))
            version.Estado = EstadoVersion.Vencida;
        ActualizadoEn = e.Fecha;
    }

    /// <summary>La versión vence para el documento cuando venció en todos los vínculos donde tiene fecha de vigencia.</summary>
    private static bool TodasVencidas(VersionDocumento version)
    {
        var conFecha = version.Vigencias.Where(v => v.VigenteHasta is not null).ToList();
        return conFecha.Count > 0 && conFecha.All(v => v.VencidaEn is not null);
    }

    public void Apply(VersionDuplicadaDetectada e) => ActualizadoEn = e.Fecha;

    public void Apply(AnalisisDeVersionIniciado e)
    {
        VersionRequerida(e.Numero).Estado = EstadoVersion.EnAnalisis;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(HechosDeVersionRegistrados e)
    {
        VersionRequerida(e.Numero).Hechos = e.Hechos;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VersionActualCambiada e)
    {
        if (VersionActual is { } anterior && Buscar(anterior) is { } previa)
            previa.Estado = EstadoVersion.Superada;
        VersionRequerida(e.Numero).Estado = EstadoVersion.Actual;
        VersionActual = e.Numero;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VersionDevueltaAlTercero e)
    {
        var version = VersionRequerida(e.Numero);
        version.Estado = EstadoVersion.Devuelta;
        version.MotivoDevolucion = e.Motivo;
        version.DetalleDevolucion = e.Detalle;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VersionRetirada e)
    {
        var version = VersionRequerida(e.Numero);
        version.Estado = EstadoVersion.Retirada;
        version.MotivoRetiro = e.Motivo;
        version.RetiradaEn = e.Fecha;
        if (VersionActual == e.Numero)
            VersionActual = null;
        ActualizadoEn = e.Fecha;
    }

    /// <summary>La versión queda sin binario, datos declarados, nombre original ni análisis; pasa a retirada y deja de ser la actual.</summary>
    public void Apply(VersionSuprimida e)
    {
        var version = VersionRequerida(e.Numero);
        version.Suprimida = true;
        version.Estado = EstadoVersion.Retirada;
        version.MotivoRetiro = e.Motivo;
        version.RetiradaEn = e.Fecha;
        version.Bucket = string.Empty;
        version.ClaveObjeto = string.Empty;
        version.NombreOriginal = "(suprimido)";
        version.Datos = null;
        version.Ia = null;
        version.Vigencias = [];
        if (VersionActual == e.Numero)
            VersionActual = null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(RetencionLegalMarcada e)
    {
        var version = VersionRequerida(e.Numero);
        version.RetenidaPorLey = true;
        version.BaseLegalDeRetencion = e.BaseLegal;
        version.RetenidaHasta = e.RetenidaHasta;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VersionActualRestituida e)
    {
        VersionRequerida(e.Numero).Estado = EstadoVersion.Actual;
        VersionActual = e.Numero;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VersionCompartida e)
    {
        Comparticiones.Add(new Comparticion(e.Numero, e.TenantId, e.EmpresaId, e.VinculoId, e.Origen, e.Fecha));
        ActualizadoEn = e.Fecha;
    }

    public void Apply(DocumentoAccedido e)
    {
        if (e.EmpresaId is not null)
            VersionRequerida(e.Numero).AccedidaPorEmpresa = true;
    }

    /// <summary>Los hechos de la versión reflejan el análisis (<c>ConIa</c>, revisión humana y comparación); los básicos quedan para quien no lo consumió.</summary>
    public void Apply(AnalisisDeIaRegistrado e)
    {
        var version = VersionRequerida(e.Numero);
        var basica = version.Hechos?.Comparacion ?? Comparacion.SinAnterior;
        version.Ia = new AnalisisDeIaDeVersion
        {
            Modelo = e.Modelo,
            Escalado = e.Escalado,
            Confianza = e.Confianza,
            Legible = e.Legible,
            TipoDetectado = e.TipoDetectado,
            Campos = e.Campos.ToDictionary(c => c.Key, c => c.Value.Clone(), StringComparer.Ordinal),
            Alertas = [.. e.Alertas],
            Observaciones = e.Observaciones,
            PaginasEnviadas = e.PaginasEnviadas,
            PaginasOmitidas = e.PaginasOmitidas,
            PendienteRevisionHumana = e.PendienteRevisionHumana,
            CamposCambiados = [.. e.CamposCambiados ?? []],
            CamposCriticosCambiados = [.. e.CamposCriticosCambiados ?? []],
            ComparacionSinIa = basica,
            EmpresaQuePago = e.EmpresaQuePago,
            Fecha = e.Fecha
        };
        if (version.Hechos is { } hechos)
            version.Hechos = hechos with
            {
                ConIa = true,
                PendienteRevisionHumana = e.PendienteRevisionHumana,
                Comparacion = e.ComparacionConAnterior ?? hechos.Comparacion
            };
        ActualizadoEn = e.Fecha;
    }

    public void Apply(AnalisisDeIaFallido e)
    {
        VersionRequerida(e.Numero).FalloIa = e.Motivo;
        ActualizadoEn = e.Fecha;
    }
}
