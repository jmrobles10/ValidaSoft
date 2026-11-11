using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;

namespace Validasoft.Dominio.Delegaciones;

/// <summary>Estado de una delegación en un día: aún no empieza, está vigente, ya terminó o se revocó.</summary>
public enum EstadoDeDelegacion
{
    Programada,
    Vigente,
    Vencida,
    Revocada
}

/// <summary>
/// Delegación de revisión por ausencia (OP-7, docs/11 §H8 <c>Delegacion</c>): entre <see cref="Desde"/> y <see cref="Hasta"/> (días de calendario
/// del negocio, inclusive) <see cref="AUsuarioId"/> revisa en lugar de <see cref="DeUsuarioId"/> las áreas delegadas. Es de una empresa (esquema
/// <c>validacion</c>, multi-tenant). Solo transfiere lo que quien delega puede hacer <b>en el momento</b>: sus áreas como miembro (no las que
/// recibió por otra delegación: no se encadenan). El delegado sigue siendo él mismo ante las reglas: la separación de funciones (no revisa
/// lo que invitó) y el conflicto de intereses (su identificación contra la del tercero) se evalúan con su propia identidad.
/// </summary>
public sealed class DelegacionDeRevision
{
    public const int MaximoDias = 366;
    public const int LongitudMaximaMotivo = 300;

    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid DeUsuarioId { get; set; }

    public Guid AUsuarioId { get; set; }

    /// <summary>Áreas delegadas (ids); con <see cref="TodasLasAreas"/> son las que <c>de</c> tenía al crearla, solo para mostrarlas.</summary>
    public List<Guid> Areas { get; set; } = [];

    /// <summary>Delegó todas las áreas de las que sea miembro mientras dure (también las que le asignen después).</summary>
    public bool TodasLasAreas { get; set; }

    public DateOnly Desde { get; set; }

    public DateOnly Hasta { get; set; }

    public string? Motivo { get; set; }

    /// <summary><c>{tipoDeActor}:{id}</c> de quien la creó (puede ser un administrador).</summary>
    public string CreadaPor { get; set; } = string.Empty;

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset? RevocadaEn { get; set; }

    public string? RevocadaPor { get; set; }

    public string? MotivoDeRevocacion { get; set; }

    public EstadoDeDelegacion EstadoEn(DateOnly hoy) =>
        RevocadaEn is not null ? EstadoDeDelegacion.Revocada
        : hoy < Desde ? EstadoDeDelegacion.Programada
        : hoy > Hasta ? EstadoDeDelegacion.Vencida
        : EstadoDeDelegacion.Vigente;

    /// <summary>
    /// ¿Esta delegación hace que <paramref name="usuarioId"/> revise el <paramref name="area"/> hoy? Vigente, es la persona delegada, quien delega
    /// sigue siendo miembro del área y el área está entre las delegadas.
    /// </summary>
    public bool CubreA(Area area, Guid usuarioId, DateOnly hoy) =>
        area.EmpresaId == EmpresaId && AUsuarioId == usuarioId && Cubre(area.Id, area.Miembros, hoy);

    /// <summary>¿Está vigente y cubre el área <paramref name="areaId"/>, de la que <paramref name="miembros"/> son sus miembros? Quien delega debe seguir siendo uno de ellos.</summary>
    public bool Cubre(Guid areaId, IReadOnlyCollection<Guid> miembros, DateOnly hoy) =>
        EstadoEn(hoy) == EstadoDeDelegacion.Vigente && miembros.Contains(DeUsuarioId) && (TodasLasAreas || Areas.Contains(areaId));

    /// <summary>¿Se pisan en el tiempo y en las áreas? (una delegación de todas las áreas pisa a cualquiera de la misma persona a la misma persona).</summary>
    public bool SePisaCon(DelegacionDeRevision otra, DateOnly hoy) =>
        EstadoEn(hoy) is EstadoDeDelegacion.Programada or EstadoDeDelegacion.Vigente && DeUsuarioId == otra.DeUsuarioId && AUsuarioId == otra.AUsuarioId &&
        Desde <= otra.Hasta && otra.Desde <= Hasta && (TodasLasAreas || otra.TodasLasAreas || Areas.Intersect(otra.Areas).Any());

    /// <summary>
    /// 422 <c>delegacion-a-si-mismo</c>, <c>rango-invalido</c> (desde después de hasta), <c>delegacion-vencida</c> (ya pasó), <c>rango-demasiado-largo</c>
    /// (más de 366 días) y <c>motivo-demasiado-largo</c>. Quién puede delegar a quién y las áreas las valida la aplicación.
    /// </summary>
    public static DelegacionDeRevision Crear(Guid empresaId, Guid deUsuarioId, Guid aUsuarioId, IEnumerable<Area> areas, bool todasLasAreas, DateOnly desde,
        DateOnly hasta, string? motivo, string creadaPor, DateOnly hoy, DateTimeOffset ahora)
    {
        if (deUsuarioId == aUsuarioId)
            throw ReglaDeNegocioException.Validacion("delegacion-a-si-mismo", "No se puede delegar la revisión a uno mismo.");
        if (desde > hasta)
            throw ReglaDeNegocioException.Validacion("rango-invalido", "La fecha inicial es posterior a la final.");
        if (hasta < hoy)
            throw ReglaDeNegocioException.Validacion("delegacion-vencida", "La delegación terminaría antes de hoy.");
        if (hasta.DayNumber - desde.DayNumber + 1 > MaximoDias)
            throw ReglaDeNegocioException.Validacion("rango-demasiado-largo", $"Una delegación dura hasta {MaximoDias} días: crea otra cuando termine.");
        var texto = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (texto?.Length > LongitudMaximaMotivo)
            throw ReglaDeNegocioException.Validacion("motivo-demasiado-largo", $"El motivo admite hasta {LongitudMaximaMotivo} caracteres.");

        return new DelegacionDeRevision
        {
            Id = Guid.CreateVersion7(),
            EmpresaId = empresaId,
            DeUsuarioId = deUsuarioId,
            AUsuarioId = aUsuarioId,
            Areas = [.. areas.Select(a => a.Id).Distinct()],
            TodasLasAreas = todasLasAreas,
            Desde = desde,
            Hasta = hasta,
            Motivo = texto,
            CreadaPor = creadaPor,
            CreadaEn = ahora
        };
    }

    /// <summary>Revoca una delegación programada o vigente (409 <c>delegacion-no-revocable</c> si ya terminó o ya se revocó).</summary>
    public void Revocar(string por, string? motivo, DateOnly hoy, DateTimeOffset ahora)
    {
        if (EstadoEn(hoy) is not (EstadoDeDelegacion.Programada or EstadoDeDelegacion.Vigente))
            throw ReglaDeNegocioException.Conflicto("delegacion-no-revocable", "Solo se revoca una delegación programada o vigente.");
        RevocadaEn = ahora;
        RevocadaPor = por;
        MotivoDeRevocacion = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
    }
}
