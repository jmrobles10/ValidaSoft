using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Campanias;

/// <summary>Estado de la campaña de renovación (OP-1): se procesa por tandas, se pausa, se reanuda y se cancela como un lote de invitación (LT-5).</summary>
public enum EstadoCampania
{
    Procesando,
    Pausada,
    Completada,
    CompletadaConErrores,
    Cancelada
}

/// <summary>
/// Estado de una fila: <c>Pendiente</c> (por procesar), <c>Exitosa</c> (se abrió la renovación o se le recordó al tercero), <c>Fallida</c> (se puede
/// reintentar) u <c>Omitida</c> (no aplica: ya tiene otro expediente abierto, está bloqueado, no hay nada por renovar…; no se procesa).
/// </summary>
public enum EstadoFilaCampania
{
    Pendiente,
    Exitosa,
    Fallida,
    Omitida
}

/// <summary>Qué hizo una fila exitosa: abrió la renovación del vínculo o, si ya la tenía abierta, le recordó al tercero.</summary>
public enum AccionFilaCampania
{
    RenovacionAbierta,
    Recordada
}

/// <summary>
/// Qué vínculos entran (OP-1): los que <b>vencen en los próximos <see cref="DiasParaVencer"/> días</b> (por su vigencia o la de alguno de sus
/// documentos; lo que ya venció también), de un tipo de vínculo (opcional) y en alguno de los <see cref="Estados"/>.
/// </summary>
public sealed record FiltroDeCampania(int DiasParaVencer, Guid? TipoVinculoId, IReadOnlyList<EstadoVinculo> Estados)
{
    public const int MaximoDias = 365;

    public static readonly IReadOnlyList<EstadoVinculo> EstadosPorOmision = [EstadoVinculo.Activo, EstadoVinculo.PorVencer, EstadoVinculo.Vencido];

    /// <summary>422 <c>dias-invalidos</c> (0 a 365) y <c>estado-invalido</c> (solo Activo, PorVencer o Vencido). Sin estados, los tres.</summary>
    public FiltroDeCampania Validar()
    {
        if (DiasParaVencer is < 0 or > MaximoDias)
            throw ReglaDeNegocioException.Validacion("dias-invalidos", $"Los días para vencer van de 0 a {MaximoDias}.");
        var estados = Estados.Count == 0 ? EstadosPorOmision : [.. Estados.Distinct()];
        if (estados.Any(e => !EstadosPorOmision.Contains(e)))
            throw ReglaDeNegocioException.Validacion("estado-invalido", "Una campaña solo trae vínculos Activo, PorVencer o Vencido.");
        return this with { Estados = estados };
    }
}

/// <summary>Quién creó la campaña: sus filas se procesan en su nombre (historial del expediente).</summary>
public sealed record ActorDeCampania(ActorTipo Tipo, string Id);

public sealed record TotalesDeCampania(int Filas, int Pendientes, int Exitosas, int Fallidas, int Omitidas);

// Eventos del stream CampaniaDeRenovacion-{id} (tenant de la empresa). Las filas son documentos aparte (FilaDeCampania), igual que en el lote de
// invitación: el avance se registra una vez por tanda, no una vez por vínculo.

public sealed record CampaniaDeRenovacionCreada(Guid CampaniaId, Guid EmpresaId, string Nombre, FiltroDeCampania Filtro, TotalesDeCampania Totales,
    ActorDeCampania CreadaPor, Guid Ejecucion, DateTimeOffset Fecha);

public sealed record AvanceDeCampaniaRegistrado(TotalesDeCampania Totales, DateTimeOffset Fecha);

public sealed record CampaniaPausada(DateTimeOffset Fecha);

public sealed record CampaniaReanudada(Guid Ejecucion, DateTimeOffset Fecha);

public sealed record CampaniaCancelada(TotalesDeCampania Totales, DateTimeOffset Fecha);

public sealed record FilasDeCampaniaReintentadas(int Filas, Guid Ejecucion, DateTimeOffset Fecha);

public sealed record CampaniaTerminada(EstadoCampania Estado, TotalesDeCampania Totales, DateTimeOffset Fecha);

/// <summary>
/// Campaña de renovación (OP-1…OP-6, docs/11 §H8 <c>Campania</c>): la empresa elige vínculos por filtro y abre su renovación en lote. Reutiliza el patrón del
/// lote de invitación (LT-5…LT-8): un stream con snapshot inline, filas como documentos y un proceso por tandas en una cola durable, cada vínculo en su
/// propia transacción e idempotente. Reanudable: se pausa, se reanuda y las filas fallidas se reintentan.
/// </summary>
public sealed class CampaniaDeRenovacion
{
    public const int LongitudMaximaNombre = 150;

    private static readonly EstadoCampania[] Terminales = [EstadoCampania.Completada, EstadoCampania.CompletadaConErrores, EstadoCampania.Cancelada];

    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid EmpresaId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public FiltroDeCampania Filtro { get; set; } = new(30, null, []);

    public EstadoCampania Estado { get; set; }

    public TotalesDeCampania Totales { get; set; } = new(0, 0, 0, 0, 0);

    public ActorDeCampania CreadaPor { get; set; } = new(ActorTipo.Sistema, "sistema");

    /// <summary>Ejecución vigente: una tanda solo corre si lleva este token (pausar y reanudar no dejan dos cadenas vivas).</summary>
    public Guid? Ejecucion { get; set; }

    public int Tandas { get; set; }

    public int Reintentos { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset ActualizadaEn { get; set; }

    public DateTimeOffset? TerminadaEn { get; set; }

    [JsonIgnore]
    public bool EsTerminal => Terminales.Contains(Estado);

    // ---------- Decisiones ----------

    /// <summary>Filtro válido y al menos un vínculo por procesar (422 <c>campania-sin-vinculos</c>). Sin nombre: «Renovación del {fecha}».</summary>
    public static CampaniaDeRenovacionCreada Crear(Guid empresaId, string? nombre, FiltroDeCampania filtro, TotalesDeCampania totales, ActorDeCampania creadaPor,
        DateTimeOffset ahora)
    {
        var validado = filtro.Validar();
        if (totales.Pendientes == 0)
            throw ReglaDeNegocioException.Validacion("campania-sin-vinculos", "Ningún vínculo con ese filtro necesita que se abra su renovación.");
        var texto = string.IsNullOrWhiteSpace(nombre) ? $"Renovación del {ahora:yyyy-MM-dd}" : nombre.Trim();
        if (texto.Length > LongitudMaximaNombre)
            texto = texto[..LongitudMaximaNombre];
        return new CampaniaDeRenovacionCreada(Guid.CreateVersion7(), empresaId, texto, validado, totales, creadaPor, Guid.NewGuid(), ahora);
    }

    public CampaniaPausada Pausar(DateTimeOffset ahora)
    {
        AsegurarNoTerminal();
        return Estado == EstadoCampania.Procesando
            ? new CampaniaPausada(ahora)
            : throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo se pausa una campaña en proceso; esta está {Estado}.");
    }

    public CampaniaReanudada Reanudar(DateTimeOffset ahora)
    {
        AsegurarNoTerminal();
        return Estado == EstadoCampania.Pausada
            ? new CampaniaReanudada(Guid.NewGuid(), ahora)
            : throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo se reanuda una campaña pausada; esta está {Estado}.");
    }

    public CampaniaCancelada Cancelar(TotalesDeCampania totales, DateTimeOffset ahora)
    {
        AsegurarNoTerminal();
        return new CampaniaCancelada(totales, ahora);
    }

    /// <summary>Solo una campaña terminada con filas fallidas (409 <c>sin-filas-fallidas</c>; cancelada: <c>campania-en-estado-terminal</c>).</summary>
    public FilasDeCampaniaReintentadas ReintentarFallidas(int fallidas, DateTimeOffset ahora)
    {
        if (Estado == EstadoCampania.Cancelada)
            throw ReglaDeNegocioException.Conflicto("campania-en-estado-terminal", "La campaña está cancelada.");
        if (!EsTerminal)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", "Las filas fallidas se reintentan cuando la campaña termina.");
        if (fallidas == 0)
            throw ReglaDeNegocioException.Conflicto("sin-filas-fallidas", "La campaña no tiene filas fallidas.");
        return new FilasDeCampaniaReintentadas(fallidas, Guid.NewGuid(), ahora);
    }

    /// <summary>Avance al final de una tanda; sin pendientes, además termina. Terminal o cancelada: nada.</summary>
    public IReadOnlyList<object> RegistrarAvance(TotalesDeCampania totales, DateTimeOffset ahora)
    {
        if (EsTerminal)
            return [];
        var avance = new AvanceDeCampaniaRegistrado(totales, ahora);
        return totales.Pendientes > 0
            ? [avance]
            : [avance, new CampaniaTerminada(totales.Fallidas > 0 ? EstadoCampania.CompletadaConErrores : EstadoCampania.Completada, totales, ahora)];
    }

    // ---------- Evolución (Marten) ----------

    public static CampaniaDeRenovacion Create(CampaniaDeRenovacionCreada e) => new()
    {
        Id = e.CampaniaId,
        EmpresaId = e.EmpresaId,
        Nombre = e.Nombre,
        Filtro = e.Filtro,
        Estado = EstadoCampania.Procesando,
        Totales = e.Totales,
        CreadaPor = e.CreadaPor,
        Ejecucion = e.Ejecucion,
        CreadaEn = e.Fecha,
        ActualizadaEn = e.Fecha
    };

    public void Apply(AvanceDeCampaniaRegistrado e)
    {
        Totales = e.Totales;
        Tandas++;
        ActualizadaEn = e.Fecha;
    }

    public void Apply(CampaniaPausada e)
    {
        Estado = EstadoCampania.Pausada;
        Ejecucion = null;
        ActualizadaEn = e.Fecha;
    }

    public void Apply(CampaniaReanudada e)
    {
        Estado = EstadoCampania.Procesando;
        Ejecucion = e.Ejecucion;
        ActualizadaEn = e.Fecha;
    }

    public void Apply(CampaniaCancelada e)
    {
        Estado = EstadoCampania.Cancelada;
        Totales = e.Totales;
        Ejecucion = null;
        TerminadaEn = e.Fecha;
        ActualizadaEn = e.Fecha;
    }

    public void Apply(FilasDeCampaniaReintentadas e)
    {
        Estado = EstadoCampania.Procesando;
        Totales = Totales with { Pendientes = Totales.Pendientes + e.Filas, Fallidas = Math.Max(0, Totales.Fallidas - e.Filas) };
        Ejecucion = e.Ejecucion;
        Reintentos++;
        TerminadaEn = null;
        ActualizadaEn = e.Fecha;
    }

    public void Apply(CampaniaTerminada e)
    {
        Estado = e.Estado;
        Totales = e.Totales;
        Ejecucion = null;
        TerminadaEn = e.Fecha;
        ActualizadaEn = e.Fecha;
    }

    private void AsegurarNoTerminal()
    {
        if (EsTerminal)
            throw ReglaDeNegocioException.Conflicto("campania-en-estado-terminal", $"La campaña ya terminó ({Estado}).");
    }
}

/// <summary>
/// Fila de una campaña (documento del tenant, id <c>{campaña:N}:{n:D6}</c>): el vínculo tal como estaba al crearla y lo que pasó al procesarlo. Se escribe en
/// la misma transacción que el expediente de renovación y el aviso que produce: si se revierte, la fila sigue <c>Pendiente</c> y se reintenta sin duplicar.
/// </summary>
public sealed class FilaDeCampania
{
    public string Id { get; set; } = string.Empty;

    public Guid CampaniaId { get; set; }

    public Guid EmpresaId { get; set; }

    public int N { get; set; }

    public Guid VinculoId { get; set; }

    public Guid TerceroId { get; set; }

    public string TerceroNombre { get; set; } = string.Empty;

    public string TerceroIdentificacion { get; set; } = string.Empty;

    public string TipoVinculoNombre { get; set; } = string.Empty;

    public EstadoVinculo EstadoDelVinculo { get; set; }

    /// <summary>El primer vencimiento (del vínculo o de uno de sus documentos); nulo si no tiene fecha.</summary>
    public DateOnly? VenceEn { get; set; }

    public EstadoFilaCampania Estado { get; set; }

    public AccionFilaCampania? Accion { get; set; }

    /// <summary>Por qué se omitió, o aclaración de una fila exitosa.</summary>
    public string? Detalle { get; set; }

    public string? ErrorCodigo { get; set; }

    public string? ErrorMensaje { get; set; }

    public Guid? ExpedienteId { get; set; }

    public string? Folio { get; set; }

    public int Intentos { get; set; }

    public DateTimeOffset? ProcesadaEn { get; set; }

    public static string Clave(Guid campaniaId, int n) => $"{campaniaId:N}:{n:D6}";

    public void MarcarExitosa(AccionFilaCampania accion, Guid? expedienteId, string? folio, string? detalle, DateTimeOffset ahora)
    {
        Estado = EstadoFilaCampania.Exitosa;
        Accion = accion;
        ExpedienteId = expedienteId;
        Folio = folio;
        Detalle = detalle;
        ErrorCodigo = null;
        ErrorMensaje = null;
        Intentos++;
        ProcesadaEn = ahora;
    }

    /// <summary>No aplicaba al procesarla (cambió desde la vista previa): queda como omitida con su motivo.</summary>
    public void MarcarOmitida(string motivo, DateTimeOffset ahora)
    {
        Estado = EstadoFilaCampania.Omitida;
        Accion = null;
        Detalle = motivo;
        Intentos++;
        ProcesadaEn = ahora;
    }

    public void MarcarFallida(string codigo, string mensaje, DateTimeOffset ahora)
    {
        Estado = EstadoFilaCampania.Fallida;
        Accion = null;
        ErrorCodigo = codigo;
        ErrorMensaje = mensaje;
        Intentos++;
        ProcesadaEn = ahora;
    }
}
