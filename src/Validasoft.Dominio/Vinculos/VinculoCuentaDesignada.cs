using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Vinculos;

public enum EstadoCuentaDesignada
{
    Propuesta,
    Aprobada,
    Rechazada,
    Reemplazada
}

/// <summary>
/// Copia de la cuenta que el tercero designa para cobrarle a esta empresa (CB-2): se congela al proponerla, así lo que
/// aprobó la empresa no cambia aunque el tercero retire la cuenta después. El número viaja protegido.
/// </summary>
public sealed record CuentaParaDesignar(Guid CuentaId, string Banco, string TipoCuenta, string NumeroProtegido, string Ultimos4, string Titular,
    string IdentificacionTitular, string Moneda);

/// <summary>Una aprobación de un usuario de la empresa (cuatro ojos, P-4-7).</summary>
public sealed record AprobacionDeCuenta(string UsuarioId, DateTimeOffset Fecha);

public sealed class CuentaDesignada
{
    public CuentaParaDesignar Cuenta { get; set; } = null!;

    public EstadoCuentaDesignada Estado { get; set; }

    public DateTimeOffset PropuestaEn { get; set; }

    public List<AprobacionDeCuenta> Aprobaciones { get; set; } = [];

    public DateTimeOffset? AprobadaEn { get; set; }

    public string? MotivoRechazo { get; set; }
}

// Eventos del stream Vinculo-{id} (CB-2).

/// <summary>El tercero eligió la cuenta para cobrarle a la empresa; espera la aprobación de dos personas.</summary>
public sealed record CuentaDesignadaPropuesta(CuentaParaDesignar Cuenta, Guid? CuentaAnteriorId, DateTimeOffset Fecha);

/// <summary>Una persona de la empresa aprobó la cuenta propuesta (aún pueden faltar aprobaciones).</summary>
public sealed record AprobacionDeCuentaRegistrada(Guid CuentaId, string UsuarioId, DateTimeOffset Fecha);

/// <summary>Completó las aprobaciones requeridas: es la cuenta designada del vínculo (la anterior queda reemplazada).</summary>
public sealed record CuentaDesignadaAprobada(Guid CuentaId, IReadOnlyList<string> AprobadaPor, DateTimeOffset Fecha, CuentaParaDesignar? Cuenta = null);

public sealed record CuentaDesignadaRechazada(Guid CuentaId, string Por, string Motivo, DateTimeOffset Fecha, CuentaParaDesignar? Cuenta = null);

/// <summary>Cuenta designada del vínculo (CB-2, docs/03 §5.3): la elige el tercero y la aprueban dos personas de la empresa.</summary>
public sealed partial class Vinculo
{
    /// <summary>Aprobaciones distintas que exige una cuenta designada (cuatro ojos, P-4-7).</summary>
    public const int AprobadoresDeCuenta = 2;

    /// <summary>La cuenta aprobada vigente; nula si nunca se aprobó una.</summary>
    public CuentaDesignada? CuentaDesignada { get; set; }

    /// <summary>La que el tercero propuso y la empresa aún no decide; nula si no hay ninguna pendiente.</summary>
    public CuentaDesignada? CuentaPropuesta { get; set; }

    /// <summary>La última propuesta rechazada, con su motivo, hasta que el tercero proponga otra.</summary>
    public CuentaDesignada? CuentaRechazada { get; set; }

    /// <summary>
    /// El tercero designa una cuenta suya. Solo con la invitación aceptada y un vínculo no terminado ni bloqueado. Si ya es
    /// la designada, 409 <c>cuenta-ya-designada</c>; si ya está propuesta, nada. Otra propuesta pendiente se reemplaza.
    /// </summary>
    public CuentaDesignadaPropuesta? ProponerCuenta(CuentaParaDesignar cuenta, DateTimeOffset ahora)
    {
        AsegurarNoTerminal("designar una cuenta en");
        if (Estado == EstadoVinculo.Bloqueado)
            throw ReglaDeNegocioException.Conflicto("vinculo-bloqueado", "La empresa suspendió temporalmente este vínculo.");
        if (!InvitacionAceptada)
            throw ReglaDeNegocioException.Conflicto("invitacion-no-aceptada", "Primero acepta la invitación de la empresa.");
        if (CuentaDesignada?.Cuenta.CuentaId == cuenta.CuentaId)
            throw ReglaDeNegocioException.Conflicto("cuenta-ya-designada", "Esa ya es tu cuenta para esta empresa.");
        if (CuentaPropuesta?.Cuenta.CuentaId == cuenta.CuentaId)
            return null;
        return new CuentaDesignadaPropuesta(cuenta, CuentaDesignada?.Cuenta.CuentaId, ahora);
    }

    /// <summary>
    /// Un usuario de la empresa aprueba la propuesta. Dos personas distintas (409 <c>aprobador-repetido</c>); con la última
    /// queda designada.
    /// </summary>
    public IReadOnlyList<object> AprobarCuenta(string usuarioId, DateTimeOffset ahora)
    {
        var propuesta = CuentaPropuesta ?? throw ReglaDeNegocioException.Conflicto("sin-cuenta-propuesta", "No hay una cuenta por aprobar.");
        if (propuesta.Aprobaciones.Any(a => a.UsuarioId == usuarioId))
            throw ReglaDeNegocioException.Conflicto("aprobador-repetido", "Ya aprobaste esta cuenta: falta que la apruebe otra persona.");

        var cuentaId = propuesta.Cuenta.CuentaId;
        var eventos = new List<object> { new AprobacionDeCuentaRegistrada(cuentaId, usuarioId, ahora) };
        if (propuesta.Aprobaciones.Count + 1 >= AprobadoresDeCuenta)
            eventos.Add(new CuentaDesignadaAprobada(cuentaId, [.. propuesta.Aprobaciones.Select(a => a.UsuarioId), usuarioId], ahora, propuesta.Cuenta));
        return eventos;
    }

    public CuentaDesignadaRechazada RechazarCuenta(string usuarioId, string? motivo, DateTimeOffset ahora)
    {
        var propuesta = CuentaPropuesta ?? throw ReglaDeNegocioException.Conflicto("sin-cuenta-propuesta", "No hay una cuenta por aprobar.");
        RequerirTexto(motivo, "motivo-requerido", "Rechazar la cuenta necesita un motivo para el tercero.");
        return new CuentaDesignadaRechazada(propuesta.Cuenta.CuentaId, usuarioId, motivo!.Trim(), ahora, propuesta.Cuenta);
    }

    public void Apply(CuentaDesignadaPropuesta e)
    {
        CuentaPropuesta = new CuentaDesignada { Cuenta = e.Cuenta, Estado = EstadoCuentaDesignada.Propuesta, PropuestaEn = e.Fecha };
        CuentaRechazada = null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(AprobacionDeCuentaRegistrada e)
    {
        CuentaPropuesta?.Aprobaciones.Add(new AprobacionDeCuenta(e.UsuarioId, e.Fecha));
        ActualizadoEn = e.Fecha;
    }

    public void Apply(CuentaDesignadaAprobada e)
    {
        if (CuentaPropuesta is { } propuesta)
        {
            propuesta.Estado = EstadoCuentaDesignada.Aprobada;
            propuesta.AprobadaEn = e.Fecha;
            CuentaDesignada = propuesta;
            CuentaPropuesta = null;
        }

        ActualizadoEn = e.Fecha;
    }

    public void Apply(CuentaDesignadaRechazada e)
    {
        if (CuentaPropuesta is { } propuesta)
        {
            propuesta.Estado = EstadoCuentaDesignada.Rechazada;
            propuesta.MotivoRechazo = e.Motivo;
            CuentaRechazada = propuesta;
        }

        CuentaPropuesta = null;
        ActualizadoEn = e.Fecha;
    }
}
