using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Invitaciones;

/// <summary>Estado del lote de invitación masiva (docs/05 §4, LT-5).</summary>
public enum EstadoLote
{
    Procesando,
    Pausado,
    Completado,
    CompletadoConErrores,
    Cancelado
}

/// <summary>
/// Estado de una fila del lote: <c>Pendiente</c> (por procesar), <c>Exitosa</c>, <c>Fallida</c> (se puede reintentar),
/// <c>Duplicada</c> (repite la identificación y el tipo de vínculo de una fila anterior del mismo lote) e <c>Invalida</c> (no
/// pasó la validación; no se procesa).
/// </summary>
public enum EstadoFilaLote
{
    Pendiente,
    Exitosa,
    Fallida,
    Duplicada,
    Invalida
}

/// <summary>
/// Qué hizo una fila exitosa: creó el vínculo y lo invitó, reenvió la invitación de un vínculo que aún no la aceptaba, o nada
/// porque el tercero ya la había aceptado (el vínculo sigue su curso).
/// </summary>
public enum AccionFilaLote
{
    Invitado,
    Reinvitado,
    SinCambios
}

/// <summary>Quién creó el lote: sus filas se procesan en su nombre (historial y separación de funciones).</summary>
public sealed record ActorDeLote(ActorTipo Tipo, string Id);

/// <summary>Conteo de filas por estado; <see cref="Filas"/> es el total.</summary>
public sealed record TotalesDeLote(int Filas, int Pendientes, int Exitosas, int Fallidas, int Duplicadas, int Invalidas);

// Eventos del stream LoteDeInvitacion-{id} (tenant de la empresa). Las filas son documentos aparte (FilaDeLote): un lote de
// miles de filas no se vuelve miles de eventos; el avance se registra una vez por tanda.

public sealed record LoteDeInvitacionCreado(Guid LoteId, Guid EmpresaId, string Nombre, string? Archivo, int VigenciaHoras, TotalesDeLote Totales,
    ActorDeLote CreadoPor, Guid Ejecucion, DateTimeOffset Fecha);

/// <summary>Terminó una tanda: los conteos al final de ella.</summary>
public sealed record AvanceDeLoteRegistrado(TotalesDeLote Totales, DateTimeOffset Fecha);

public sealed record LotePausado(DateTimeOffset Fecha);

/// <summary>Reanudado con una ejecución nueva: las tandas de la anterior que sigan en la cola se ignoran.</summary>
public sealed record LoteReanudado(Guid Ejecucion, DateTimeOffset Fecha);

/// <summary>
/// Cancelado: lo pendiente ya no se procesa (queda como estaba en el informe). Los totales son la foto del momento de cancelar;
/// si la tanda en curso termina filas después, un <see cref="AvanceDeLoteRegistrado"/> los corrige.
/// </summary>
public sealed record LoteCancelado(TotalesDeLote Totales, DateTimeOffset Fecha);

/// <summary>Las filas fallidas vuelven a <c>Pendiente</c> y el lote se procesa otra vez (solo ellas).</summary>
public sealed record FilasFallidasReintentadas(int Filas, Guid Ejecucion, DateTimeOffset Fecha);

/// <summary>No quedan filas pendientes: <c>Completado</c> o <c>CompletadoConErrores</c> (alguna fallida).</summary>
public sealed record LoteTerminado(EstadoLote Estado, TotalesDeLote Totales, DateTimeOffset Fecha);

/// <summary>
/// Lote de invitación masiva (saga <c>InvitacionMasiva</c>, docs/11 §H8; paridad con <c>InvitacionLote</c> del SRM, LT-5). Se
/// procesa por tandas en una cola durable de Wolverine: cada fila en su propia transacción (crea o reutiliza el tercero y el
/// vínculo e invita), idempotente por fila. Se pausa, se reanuda, se cancela y sus filas fallidas se reintentan. Los métodos de
/// decisión validan y devuelven eventos; los <c>Apply</c> evolucionan el estado.
/// </summary>
public sealed class LoteDeInvitacion
{
    public const int LongitudMaximaNombre = 150;
    public const int VigenciaMaximaHoras = 30 * 24;

    private static readonly EstadoLote[] Terminales = [EstadoLote.Completado, EstadoLote.CompletadoConErrores, EstadoLote.Cancelado];

    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid EmpresaId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string? Archivo { get; set; }

    public int VigenciaHoras { get; set; }

    public EstadoLote Estado { get; set; }

    public TotalesDeLote Totales { get; set; } = new(0, 0, 0, 0, 0, 0);

    public ActorDeLote CreadoPor { get; set; } = new(ActorTipo.Sistema, "sistema");

    /// <summary>Ejecución vigente: una tanda solo corre si lleva este token (pausar y reanudar no dejan dos cadenas vivas).</summary>
    public Guid? Ejecucion { get; set; }

    public int Tandas { get; set; }

    public int Reintentos { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    public DateTimeOffset? TerminadoEn { get; set; }

    [JsonIgnore]
    public bool EsTerminal => Terminales.Contains(Estado);

    // ---------- Decisiones ----------

    /// <summary>
    /// Nombre (o el del archivo; hasta 150 caracteres), vigencia del enlace entre 1 hora y 30 días y al menos una fila que se
    /// pueda procesar (422 <c>lote-sin-filas-validas</c>).
    /// </summary>
    public static LoteDeInvitacionCreado Crear(Guid empresaId, string? nombre, string? archivo, int vigenciaHoras, TotalesDeLote totales,
        ActorDeLote creadoPor, DateTimeOffset ahora)
    {
        if (vigenciaHoras is < 1 or > VigenciaMaximaHoras)
            throw ReglaDeNegocioException.Validacion("vigencia-invalida", "La vigencia del enlace de invitación debe estar entre 1 hora y 30 días.");
        if (totales.Pendientes == 0)
            throw ReglaDeNegocioException.Validacion("lote-sin-filas-validas", "El lote no tiene filas válidas para invitar.");

        var texto = (string.IsNullOrWhiteSpace(nombre) ? archivo : nombre)?.Trim();
        if (string.IsNullOrWhiteSpace(texto))
            texto = $"Lote del {ahora:yyyy-MM-dd HH:mm} UTC";
        if (texto.Length > LongitudMaximaNombre)
            texto = texto[..LongitudMaximaNombre];

        return new LoteDeInvitacionCreado(Guid.CreateVersion7(), empresaId, texto, archivo?.Trim(), vigenciaHoras, totales, creadoPor, Guid.NewGuid(), ahora);
    }

    public LotePausado Pausar(DateTimeOffset ahora)
    {
        AsegurarNoTerminal();
        return Estado == EstadoLote.Procesando
            ? new LotePausado(ahora)
            : throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo se pausa un lote en proceso; este está {Estado}.");
    }

    public LoteReanudado Reanudar(DateTimeOffset ahora)
    {
        AsegurarNoTerminal();
        return Estado == EstadoLote.Pausado
            ? new LoteReanudado(Guid.NewGuid(), ahora)
            : throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo se reanuda un lote pausado; este está {Estado}.");
    }

    public LoteCancelado Cancelar(TotalesDeLote totales, DateTimeOffset ahora)
    {
        AsegurarNoTerminal();
        return new LoteCancelado(totales, ahora);
    }

    /// <summary>Solo un lote terminado con filas fallidas (409 <c>sin-filas-fallidas</c>; cancelado: <c>lote-en-estado-terminal</c>).</summary>
    public FilasFallidasReintentadas ReintentarFallidas(int fallidas, DateTimeOffset ahora)
    {
        if (Estado == EstadoLote.Cancelado)
            throw ReglaDeNegocioException.Conflicto("lote-en-estado-terminal", "El lote está cancelado.");
        if (!EsTerminal)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", "Las filas fallidas se reintentan cuando el lote termina.");
        if (fallidas == 0)
            throw ReglaDeNegocioException.Conflicto("sin-filas-fallidas", "El lote no tiene filas fallidas.");
        return new FilasFallidasReintentadas(fallidas, Guid.NewGuid(), ahora);
    }

    /// <summary>
    /// Avance al final de una tanda (en proceso o pausado: la tanda en curso termina). Sin pendientes, además termina.
    /// Cancelado: la tanda que estaba en curso al cancelar alcanza a terminar las filas que ya tenía en mano (la cancelación
    /// solo se nota entre filas), así que sus totales se actualizan —solo los conteos, el lote sigue cancelado y no se reanuda ni
    /// se termina— para que reflejen lo que de verdad se procesó y no la foto del momento de cancelar. Terminado: nada.
    /// </summary>
    public IReadOnlyList<object> RegistrarAvance(TotalesDeLote totales, DateTimeOffset ahora)
    {
        if (Estado == EstadoLote.Cancelado)
            return totales == Totales ? [] : [new AvanceDeLoteRegistrado(totales, ahora)];
        if (EsTerminal)
            return [];
        var avance = new AvanceDeLoteRegistrado(totales, ahora);
        return totales.Pendientes > 0
            ? [avance]
            : [avance, new LoteTerminado(totales.Fallidas > 0 ? EstadoLote.CompletadoConErrores : EstadoLote.Completado, totales, ahora)];
    }

    // ---------- Evolución (Marten) ----------

    public static LoteDeInvitacion Create(LoteDeInvitacionCreado e) => new()
    {
        Id = e.LoteId,
        EmpresaId = e.EmpresaId,
        Nombre = e.Nombre,
        Archivo = e.Archivo,
        VigenciaHoras = e.VigenciaHoras,
        Estado = EstadoLote.Procesando,
        Totales = e.Totales,
        CreadoPor = e.CreadoPor,
        Ejecucion = e.Ejecucion,
        CreadoEn = e.Fecha,
        ActualizadoEn = e.Fecha
    };

    public void Apply(AvanceDeLoteRegistrado e)
    {
        Totales = e.Totales;
        Tandas++;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(LotePausado e)
    {
        Estado = EstadoLote.Pausado;
        Ejecucion = null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(LoteReanudado e)
    {
        Estado = EstadoLote.Procesando;
        Ejecucion = e.Ejecucion;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(LoteCancelado e)
    {
        Estado = EstadoLote.Cancelado;
        Totales = e.Totales;
        Ejecucion = null;
        TerminadoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(FilasFallidasReintentadas e)
    {
        Estado = EstadoLote.Procesando;
        Totales = Totales with { Pendientes = Totales.Pendientes + e.Filas, Fallidas = Math.Max(0, Totales.Fallidas - e.Filas) };
        Ejecucion = e.Ejecucion;
        Reintentos++;
        TerminadoEn = null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(LoteTerminado e)
    {
        Estado = e.Estado;
        Totales = e.Totales;
        Ejecucion = null;
        TerminadoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    private void AsegurarNoTerminal()
    {
        if (EsTerminal)
            throw ReglaDeNegocioException.Conflicto("lote-en-estado-terminal", $"El lote ya terminó ({Estado}).");
    }
}

/// <summary>Datos de una fila tal como llegaron (Excel, CSV o JSON), ya recortados.</summary>
public sealed record FilaDeLoteEntrada(
    string? Pais,
    string? TipoIdentificacion,
    string? NumeroIdentificacion,
    string? DigitoVerificacion,
    string? TipoPersona,
    string? Nombre,
    string? NombreComercial,
    string? Correo,
    string? TipoVinculo,
    string? Flujo,
    string? Contacto,
    string? Telefono);

/// <summary>Un error de validación o de proceso de una fila (<see cref="Campo"/> nulo si no es de un campo).</summary>
public sealed record ErrorDeFila(string? Campo, string Codigo, string Mensaje);

/// <summary>
/// Fila de un lote (documento del tenant, id <c>{lote:N}:{n:D6}</c>): sus datos, su estado y lo que pasó al procesarla. Se
/// escribe en la misma transacción que el tercero, el vínculo y la invitación que produce: si la transacción se revierte, la
/// fila sigue <c>Pendiente</c> y se vuelve a intentar sin duplicar nada.
/// </summary>
public sealed class FilaDeLote
{
    public string Id { get; set; } = string.Empty;

    public Guid LoteId { get; set; }

    public Guid EmpresaId { get; set; }

    public int N { get; set; }

    public FilaDeLoteEntrada Datos { get; set; } = new(null, null, null, null, null, null, null, null, null, null, null, null);

    /// <summary>Identificación normalizada (<c>CO|NIT|900123456</c>) si es válida.</summary>
    public string? ClaveIdentificacion { get; set; }

    public EstadoFilaLote Estado { get; set; }

    public AccionFilaLote? Accion { get; set; }

    public List<ErrorDeFila> Errores { get; set; } = [];

    /// <summary>Fila anterior del mismo lote que esta repite.</summary>
    public int? DuplicadaDe { get; set; }

    public Guid? TerceroId { get; set; }

    public Guid? VinculoId { get; set; }

    public bool TerceroCreado { get; set; }

    /// <summary>Aclaración de una fila exitosa (p. ej. por qué no se reenvió la invitación).</summary>
    public string? Detalle { get; set; }

    public int Intentos { get; set; }

    public DateTimeOffset? ProcesadaEn { get; set; }

    public static string Clave(Guid loteId, int n) => $"{loteId:N}:{n:D6}";

    public void MarcarExitosa(AccionFilaLote accion, Guid terceroId, Guid vinculoId, bool terceroCreado, string? detalle, DateTimeOffset ahora)
    {
        Estado = EstadoFilaLote.Exitosa;
        Accion = accion;
        TerceroId = terceroId;
        VinculoId = vinculoId;
        TerceroCreado = terceroCreado;
        Errores = [];
        Detalle = detalle;
        Intentos++;
        ProcesadaEn = ahora;
    }

    public void MarcarFallida(ErrorDeFila error, DateTimeOffset ahora)
    {
        Estado = EstadoFilaLote.Fallida;
        Accion = null;
        Errores = [error];
        Detalle = null;
        Intentos++;
        ProcesadaEn = ahora;
    }
}
