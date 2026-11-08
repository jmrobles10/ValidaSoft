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

    /// <summary>Alguna empresa emitió una descarga de esta versión: su binario ya no se elimina al retirarla (docs/02 §10.3).</summary>
    public bool AccedidaPorEmpresa { get; set; }

    /// <summary>Huella de los datos declarados (TD-4); nula si la versión es solo archivo.</summary>
    public string? HashDatos { get; set; }

    /// <summary>Datos que declaró el tercero, en el orden del formulario; nulos si la versión es solo archivo.</summary>
    public List<DatoDeclarado>? Datos { get; set; }

    /// <summary>Vigencia por vínculo, cada una con la regla del tipo de su tenant (TD-6).</summary>
    public List<VigenciaDeVersion> Vigencias { get; set; } = [];

    /// <summary>Una versión solo de formulario no tiene binario que descargar.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool TieneArchivo => ClaveObjeto.Length > 0;

    public VigenciaDeVersion? VigenciaPara(Guid vinculoId) => Vigencias.FirstOrDefault(v => v.VinculoId == vinculoId);

    /// <summary>El vencimiento más próximo entre los vínculos (lo que obliga primero al tercero, docs/02 §8.3).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public VigenciaDeVersion? ProximaVigencia => Vigencias.Where(v => v.VigenteHasta is not null).MinBy(v => v.VigenteHasta);
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
            var calculo = CalculadoraDeVigencia.Calcular(tipo.ReglaFrescura, version.CargadaEn, version.Datos);
            var previa = version.VigenciaPara(destino.VinculoId);
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
    public VersionVencida? Vencer(int numero, Guid vinculoId, DateTimeOffset ahora)
    {
        if (VersionActual != numero || Buscar(numero)?.VigenciaPara(vinculoId) is not { VigenteHasta: { } hasta, VencidaEn: null } vigencia)
            return null;
        return CalculadoraDeVigencia.Fecha(ahora) > vigencia.FinDeGracia
            ? new VersionVencida(numero, vinculoId, hasta, vigencia.DiasGracia, ahora)
            : null;
    }

    /// <summary>
    /// El tercero retira una versión: no vuelve a usarse ni a compartirse. Si era la actual, la superada más reciente
    /// vuelve a serlo (I-11). Devuelta o ya retirada ⇒ 409. (Las aceptaciones y certificados que lo impedirían llegan con H3/H4.)
    /// </summary>
    public IReadOnlyList<object> RetirarVersion(int numero, string? motivo, DateTimeOffset ahora)
    {
        var version = VersionRequerida(numero);
        if (version.Estado is not (EstadoVersion.Actual or EstadoVersion.Superada or EstadoVersion.Vencida))
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"No se puede retirar una versión {version.Estado}.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw ReglaDeNegocioException.Validacion("motivo-requerido", "Indica por qué retiras la versión.");

        var eventos = new List<object> { new VersionRetirada(numero, motivo.Trim(), ahora) };
        if (VersionActual == numero &&
            Versiones.Where(v => v.Estado == EstadoVersion.Superada).MaxBy(v => v.Numero) is { } anterior)
            eventos.Add(new VersionActualRestituida(anterior.Numero, numero, ahora));
        return eventos;
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
            case VersionActualRestituida e: Apply(e); break;
            case VersionCompartida e: Apply(e); break;
            case DocumentoAccedido e: Apply(e); break;
            case VigenciaDeVersionCalculada e: Apply(e); break;
            case VersionVencida e: Apply(e); break;
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
            // Una vigencia recalculada solo conserva el vencimiento si su fin de gracia no cambió.
            VencidaEn = previa?.VencidaEn is { } vencida && previa.FinDeGracia == CalculadoraDeVigencia.FinDeGracia(e.VigenteHasta, e.DiasGracia)
                ? vencida
                : null
        });
        if (version.Estado == EstadoVersion.Vencida && !TodasVencidas(version))
            version.Estado = EstadoVersion.Actual;
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
        if (VersionActual == e.Numero)
            VersionActual = null;
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
}
