using System.Text.Json;
using System.Text.Json.Serialization;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;

namespace Validasoft.Dominio.Habeas;

/// <summary>Derecho que ejerce el titular (Ley 1581 de 2012): conocer sus datos, corregirlos o pedir que se supriman.</summary>
public enum TipoDeSolicitudHabeas
{
    /// <summary>Consulta (art. 14): 10 días hábiles.</summary>
    Consulta,

    /// <summary>Reclamo de corrección o actualización (art. 15): 15 días hábiles.</summary>
    Correccion,

    /// <summary>Reclamo de supresión (art. 15): 15 días hábiles.</summary>
    Supresion
}

/// <summary>Qué pide suprimir el titular (solo en <see cref="TipoDeSolicitudHabeas.Supresion"/>).</summary>
public enum AlcanceDeSupresion
{
    Todo,
    Documentos,
    Datos
}

public enum EstadoDeSolicitudHabeas
{
    Recibida,
    Resuelta
}

/// <summary>Cómo se resolvió: atendida del todo, atendida con retenciones por ley (supresión) o rechazada con su motivo.</summary>
public enum ResultadoDeSolicitudHabeas
{
    Atendida,
    Parcial,
    Rechazada
}

/// <summary>Aviso de plazo a quien responde: faltan dos días hábiles o el plazo ya venció.</summary>
public enum AvisoDePlazoHabeas
{
    PorVencer,
    Vencida
}

/// <summary>
/// A quién se dirige: a una empresa (responsable de los datos de sus vínculos) o, sin empresa, a la plataforma, que responde por la
/// bóveda global del tercero (documentos, ficha, cuentas).
/// </summary>
public sealed record DestinoDeSolicitud(Guid? TenantId, Guid? EmpresaId)
{
    [JsonIgnore]
    public bool EsPlataforma => EmpresaId is null;
}

public sealed record ActorDeSolicitud(string Tipo, string Id);

/// <summary>Algo que la supresión eliminó o anonimizó, descrito sin datos personales (tipo de documento, versión, sección).</summary>
public sealed record ElementoSuprimido(string Tipo, string Referencia, string Descripcion);

/// <summary>Algo que se conserva por obligación legal, con su base y hasta cuándo.</summary>
public sealed record ElementoRetenido(string Tipo, string Referencia, string Descripcion, string BaseLegal, DateOnly? RetenidaHasta);

/// <summary>
/// Constancia de la respuesta (docs/07 §8): qué se pidió, cuándo, qué se hizo y qué se conserva con su base legal. No lleva datos
/// personales: queda como prueba aunque la llave del tercero se destruya.
/// </summary>
public sealed record ConstanciaDeSolicitud(
    string Radicado,
    string Tipo,
    string? Alcance,
    string Destino,
    DateOnly RecibidaEl,
    DateOnly ResueltaEl,
    bool DentroDelPlazo,
    string Resultado,
    IReadOnlyList<ElementoSuprimido> Suprimidos,
    IReadOnlyList<ElementoRetenido> Retenidos,
    IReadOnlyList<string> Anonimizados)
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>JSON canónico (RFC 8785) y su SHA-256: la huella que recibe el titular.</summary>
    public (string Json, string Hash) Sellar()
    {
        var json = Jcs.Canonicalizar(JsonSerializer.Serialize(this, Opciones));
        return (json, HuellaDeCertificado.Hash(json));
    }

    public static ConstanciaDeSolicitud Leer(string json) =>
        JsonSerializer.Deserialize<ConstanciaDeSolicitud>(json, Opciones) ?? throw new FormatException("La constancia está vacía.");
}

// ---------- Eventos (stream SolicitudHabeas-{id}, tenant técnico «boveda») ----------

/// <summary>
/// El titular presentó la solicitud. <see cref="DatosProtegidos"/> trae —cifrados con la llave del tercero— su nombre,
/// identificación, correo y el detalle que escribió: el evento no puede borrarse, pero sin la llave no se lee (crypto-shredding).
/// </summary>
public sealed record SolicitudHabeasRecibida(
    Guid SolicitudId,
    string Radicado,
    Guid TerceroId,
    TipoDeSolicitudHabeas Tipo,
    AlcanceDeSupresion? Alcance,
    DestinoDeSolicitud Destino,
    string DatosProtegidos,
    int DiasHabiles,
    DateOnly RecibidaEl,
    DateOnly VenceEl,
    string Ip,
    DateTimeOffset Fecha);

/// <summary>Se avisó a quien responde que el plazo vence pronto o ya venció (uno de cada).</summary>
public sealed record AvisoDePlazoDeSolicitudEnviado(AvisoDePlazoHabeas Aviso, DateTimeOffset Fecha);

/// <summary>
/// La solicitud se resolvió. <see cref="RespuestaProtegida"/> (cifrada, como los datos del titular) es lo que se le contestó;
/// <see cref="ConstanciaJson"/> y su huella, la prueba sin datos personales.
/// </summary>
public sealed record SolicitudHabeasResuelta(
    ResultadoDeSolicitudHabeas Resultado,
    string RespuestaProtegida,
    string ConstanciaJson,
    string ConstanciaHash,
    ActorDeSolicitud Por,
    DateOnly ResueltaEl,
    bool DentroDelPlazo,
    DateTimeOffset Fecha);

/// <summary>
/// Solicitud de habeas data (Ley 1581 de 2012, docs/07 §8) — agregado event-sourced, un stream por solicitud en la bóveda. Dice quién
/// la presentó, qué derecho ejerce, a quién se dirige y en cuántos días hábiles vence (10 las consultas, 15 los reclamos, contados
/// en el calendario hábil, OD-1). Los métodos de decisión validan y devuelven eventos; los <c>Apply</c> evolucionan el estado.
/// </summary>
public sealed class SolicitudHabeas
{
    public const int DiasHabilesConsulta = 10;
    public const int DiasHabilesReclamo = 15;

    /// <summary>Días hábiles que faltan para que el plazo venza desde los que se avisa a quien responde.</summary>
    public const int DiasDeAviso = 2;

    public const int LongitudMaximaDetalle = 2000;
    public const int LongitudMinimaRespuesta = 10;

    public Guid Id { get; set; }

    public long Version { get; set; }

    public string Radicado { get; set; } = string.Empty;

    public Guid TerceroId { get; set; }

    public TipoDeSolicitudHabeas Tipo { get; set; }

    public AlcanceDeSupresion? Alcance { get; set; }

    /// <summary>Tenant de la empresa a la que se dirige; nulo si es de la plataforma.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Empresa a la que se dirige; nula si es de la plataforma.</summary>
    public Guid? EmpresaId { get; set; }

    /// <summary>Nombre, identificación, correo y detalle del titular, cifrados con su llave.</summary>
    public string DatosProtegidos { get; set; } = string.Empty;

    public int DiasHabiles { get; set; }

    public DateOnly RecibidaEl { get; set; }

    public DateOnly VenceEl { get; set; }

    public EstadoDeSolicitudHabeas Estado { get; set; }

    public List<AvisoDePlazoHabeas> Avisos { get; set; } = [];

    public ResultadoDeSolicitudHabeas? Resultado { get; set; }

    public string? RespuestaProtegida { get; set; }

    public string? ConstanciaJson { get; set; }

    public string? ConstanciaHash { get; set; }

    public ActorDeSolicitud? ResueltaPor { get; set; }

    public DateOnly? ResueltaEl { get; set; }

    public bool? DentroDelPlazo { get; set; }

    public DateTimeOffset RecibidaEn { get; set; }

    public DateTimeOffset? ResueltaEn { get; set; }

    [JsonIgnore]
    public bool EsPlataforma => EmpresaId is null;

    [JsonIgnore]
    public bool EstaAbierta => Estado == EstadoDeSolicitudHabeas.Recibida;

    public static int DiasHabilesDe(TipoDeSolicitudHabeas tipo) => tipo == TipoDeSolicitudHabeas.Consulta ? DiasHabilesConsulta : DiasHabilesReclamo;

    /// <summary><c>HD-{año}-{8 hexadecimales}</c>: el número con el que el titular sigue su solicitud.</summary>
    public static string RadicadoDe(Guid solicitudId, int anio) => $"HD-{anio}-{solicitudId.ToString("N")[^8..].ToUpperInvariant()}";

    // ---------- Decisiones ----------

    /// <summary>
    /// Presenta la solicitud. El alcance solo vale en la supresión (por omisión, todo). El plazo se cuenta en días hábiles desde el
    /// día en que se recibe (zona del negocio). 422 <c>tipo-invalido</c>.
    /// </summary>
    public static SolicitudHabeasRecibida Recibir(Guid terceroId, TipoDeSolicitudHabeas tipo, AlcanceDeSupresion? alcance, DestinoDeSolicitud destino,
        string datosProtegidos, string ip, DateTimeOffset ahora)
    {
        if (!Enum.IsDefined(tipo))
            throw ReglaDeNegocioException.Validacion("tipo-invalido", "El derecho solicitado es consulta, corrección o supresión.");
        if (string.IsNullOrWhiteSpace(datosProtegidos))
            throw new ArgumentException("Faltan los datos del titular.", nameof(datosProtegidos));

        var dia = CalculadoraDeVigencia.Fecha(ahora);
        var dias = DiasHabilesDe(tipo);
        var id = Guid.CreateVersion7();
        return new SolicitudHabeasRecibida(id, RadicadoDe(id, dia.Year), terceroId, tipo,
            tipo == TipoDeSolicitudHabeas.Supresion ? alcance ?? AlcanceDeSupresion.Todo : null, destino, datosProtegidos, dias, dia,
            CalendarioHabil.SumarDiasHabiles(dia, dias), ip, ahora);
    }

    /// <summary>
    /// Resuelve la solicitud (atendida, parcial o rechazada) con la respuesta —que no puede quedar vacía— y la constancia que ya
    /// sellaron los llamadores. 409 <c>solicitud-ya-resuelta</c>; 422 <c>respuesta-requerida</c>.
    /// </summary>
    public SolicitudHabeasResuelta Resolver(ResultadoDeSolicitudHabeas resultado, string respuestaProtegida, string constanciaJson, string constanciaHash,
        ActorDeSolicitud por, DateTimeOffset ahora)
    {
        if (!EstaAbierta)
            throw ReglaDeNegocioException.Conflicto("solicitud-ya-resuelta", "Esa solicitud ya se resolvió.");
        if (string.IsNullOrWhiteSpace(respuestaProtegida))
            throw ReglaDeNegocioException.Validacion("respuesta-requerida", "Escribe la respuesta que recibirá el titular.");

        var dia = CalculadoraDeVigencia.Fecha(ahora);
        return new SolicitudHabeasResuelta(resultado, respuestaProtegida, constanciaJson, constanciaHash, por, dia, dia <= VenceEl, ahora);
    }

    /// <summary>Días hábiles que le quedan al plazo desde <paramref name="hoy"/>; negativo si ya venció.</summary>
    public int DiasHabilesRestantes(DateOnly hoy) => hoy > VenceEl
        ? -CalendarioHabil.DiasHabilesEntre(VenceEl, hoy)
        : CalendarioHabil.DiasHabilesEntre(hoy, VenceEl);

    /// <summary>
    /// El aviso que toca enviar hoy, si lo hay: <c>PorVencer</c> cuando faltan <see cref="DiasDeAviso"/> días hábiles o menos y
    /// <c>Vencida</c> pasado el plazo; cada uno una sola vez. Resuelta: ninguno.
    /// </summary>
    public AvisoDePlazoHabeas? AvisoPendiente(DateOnly hoy)
    {
        if (!EstaAbierta)
            return null;
        if (hoy > VenceEl)
            return Avisos.Contains(AvisoDePlazoHabeas.Vencida) ? null : AvisoDePlazoHabeas.Vencida;
        return DiasHabilesRestantes(hoy) <= DiasDeAviso && Avisos.Count == 0 ? AvisoDePlazoHabeas.PorVencer : null;
    }

    // ---------- Evolución (Marten) ----------

    public static SolicitudHabeas Create(SolicitudHabeasRecibida e) => new()
    {
        Id = e.SolicitudId,
        Radicado = e.Radicado,
        TerceroId = e.TerceroId,
        Tipo = e.Tipo,
        Alcance = e.Alcance,
        TenantId = e.Destino.TenantId,
        EmpresaId = e.Destino.EmpresaId,
        DatosProtegidos = e.DatosProtegidos,
        DiasHabiles = e.DiasHabiles,
        RecibidaEl = e.RecibidaEl,
        VenceEl = e.VenceEl,
        Estado = EstadoDeSolicitudHabeas.Recibida,
        RecibidaEn = e.Fecha
    };

    public void Apply(AvisoDePlazoDeSolicitudEnviado e) => Avisos.Add(e.Aviso);

    public void Apply(SolicitudHabeasResuelta e)
    {
        Estado = EstadoDeSolicitudHabeas.Resuelta;
        Resultado = e.Resultado;
        RespuestaProtegida = e.RespuestaProtegida;
        ConstanciaJson = e.ConstanciaJson;
        ConstanciaHash = e.ConstanciaHash;
        ResueltaPor = e.Por;
        ResueltaEl = e.ResueltaEl;
        DentroDelPlazo = e.DentroDelPlazo;
        ResueltaEn = e.Fecha;
    }
}
