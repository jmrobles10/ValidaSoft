using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Delegaciones;
using Validasoft.Dominio.Identidad;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Delegaciones;

/// <summary>
/// Lo que las delegaciones de revisión cambian en el resto de la aplicación (OP-7, OP-8): quién revisa qué área hoy. Un usuario revisa las áreas
/// de las que es miembro y las que le delegaron y siguen vigentes. Todo lo demás (separación de funciones, conflicto de intereses, el permiso
/// <c>expedientes:revisar</c>, el rol en la empresa) se evalúa sobre él, no sobre quien delegó.
/// </summary>
public static class DelegacionesDeRevision
{
    /// <summary>Delegaciones de la empresa que hoy dan a <paramref name="usuarioId"/> la revisión de algún área.</summary>
    public static async Task<IReadOnlyList<DelegacionDeRevision>> VigentesDelDelegadoAsync(IQuerySession sesion, Guid empresaId, Guid usuarioId, DateOnly hoy,
        CancellationToken ct) =>
        [.. (await sesion.Query<DelegacionDeRevision>().Where(d => d.EmpresaId == empresaId && d.AUsuarioId == usuarioId && d.RevocadaEn == null).ToListAsync(ct))
            .Where(d => d.EstadoEn(hoy) == EstadoDeDelegacion.Vigente)];

    /// <summary>La delegación vigente que le da a <paramref name="usuarioId"/> la revisión de <paramref name="area"/>, si hay.</summary>
    public static async Task<DelegacionDeRevision?> VigenteParaAreaAsync(IQuerySession sesion, Area area, Guid usuarioId, DateOnly hoy, CancellationToken ct) =>
        (await VigentesDelDelegadoAsync(sesion, area.EmpresaId, usuarioId, hoy, ct)).FirstOrDefault(d => d.CubreA(area, usuarioId, hoy));

    /// <summary>Ids de las áreas que revisa el usuario hoy: las suyas como miembro y las delegadas.</summary>
    public static async Task<IReadOnlySet<Guid>> AreasQueRevisaAsync(IQuerySession sesion, IReadOnlyCollection<Area> areasDeLaEmpresa, Guid empresaId, Guid usuarioId,
        DateOnly hoy, CancellationToken ct)
    {
        var propias = areasDeLaEmpresa.Where(a => a.Miembros.Contains(usuarioId)).Select(a => a.Id).ToHashSet();
        var delegaciones = await VigentesDelDelegadoAsync(sesion, empresaId, usuarioId, hoy, ct);
        foreach (var area in areasDeLaEmpresa.Where(a => !propias.Contains(a.Id)))
            if (delegaciones.Any(d => d.CubreA(area, usuarioId, hoy)))
                propias.Add(area.Id);
        return propias;
    }

    /// <summary>
    /// Las personas que revisan el área hoy: sus miembros y las delegadas por ellos con una delegación vigente que cubra el área (las
    /// notificaciones del área les llegan también).
    /// </summary>
    public static IReadOnlyList<Guid> QuienesRevisan(Area area, IEnumerable<DelegacionDeRevision> delegaciones, DateOnly hoy) =>
        QuienesRevisan(area.Id, area.Miembros, delegaciones, hoy);

    public static IReadOnlyList<Guid> QuienesRevisan(Guid areaId, IReadOnlyCollection<Guid> miembros, IEnumerable<DelegacionDeRevision> delegaciones, DateOnly hoy) =>
        [.. miembros.Concat(delegaciones.Where(d => d.Cubre(areaId, miembros, hoy)).Select(d => d.AUsuarioId)).Distinct()];
}

/// <summary>
/// Delegación de revisión por ausencias (OP-7, OP-8): crear, consultar y revocar. Quien tiene <c>expedientes:revisar</c> delega lo suyo; un administrador
/// de expedientes puede delegar en nombre de otra persona. Cada alta y cada revocación queda en la auditoría de configuración y cada decisión tomada en
/// delegación lleva en el historial de eventos de quién era (<c>delegadoDe</c>).
/// </summary>
public static class DelegacionesEndpoints
{
    /// <summary>Quienes pueden recibir una delegación: activos, con rol en la empresa y permiso de revisar.</summary>
    [WolverineGet("/api/v1/delegaciones/candidatos")]
    [Authorize(Policy = Permisos.ExpedientesRevisar)]
    public static async Task<IReadOnlyList<CandidatoDeDelegacionDto>> Candidatos(SesionDeEmpresa empresa, CancellationToken ct) =>
        [.. (await empresa.Sesion.Query<Usuario>().Where(u => u.Activo).ToListAsync(ct)).Where(u => PuedeRevisar(u, empresa.EmpresaId)).OrderBy(u => u.Nombre)
            .Select(u => new CandidatoDeDelegacionDto(u.Id, u.Nombre, u.Correo))];

    /// <summary>Delegaciones de la empresa: todas para quien administra expedientes; las propias (que dio o recibió) para los demás. <c>estado</c> opcional.</summary>
    [WolverineGet("/api/v1/delegaciones")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<IReadOnlyList<DelegacionDto>> Listar(SesionDeEmpresa empresa, IContextoSolicitud contexto, string? estado, CancellationToken ct)
    {
        EstadoDeDelegacion? filtro = null;
        if (!string.IsNullOrWhiteSpace(estado))
            filtro = Enum.TryParse<EstadoDeDelegacion>(estado, true, out var e) && Enum.IsDefined(e)
                ? e
                : throw ReglaDeNegocioException.Validacion("estado-invalido", "El estado es Programada, Vigente, Vencida o Revocada.");

        var empresaId = empresa.EmpresaId;
        var hoy = empresa.Hoy;
        var todas = await empresa.Sesion.Query<DelegacionDeRevision>().Where(d => d.EmpresaId == empresaId).OrderByDescending(d => d.Desde).ToListAsync(ct);
        var propia = UsuarioActual(contexto);
        var visibles = contexto.Scopes.Contains(Permisos.ExpedientesAdministrar) ? todas : [.. todas.Where(d => propia is { } p && (d.DeUsuarioId == p || d.AUsuarioId == p))];
        return await ADtosAsync(empresa, [.. visibles.Where(d => filtro is null || d.EstadoEn(hoy) == filtro)], ct);
    }

    /// <summary>
    /// Delega la revisión. <c>deUsuarioId</c> es quien llama si no se indica; delegar lo de otra persona pide <c>expedientes:administrar</c> (403
    /// <c>delegacion-ajena</c>). 422 si <c>a</c> no puede revisar (<c>delegado-sin-permiso-de-revision</c>), si quien delega no es miembro de un área
    /// (<c>no-es-miembro-del-area</c>) o no tiene áreas (<c>sin-areas-que-delegar</c>); 409 <c>delegacion-solapada</c> si ya hay una igual en esas fechas.
    /// </summary>
    [WolverinePost("/api/v1/delegaciones")]
    [Authorize(Policy = Permisos.ExpedientesRevisar)]
    public static async Task<DelegacionDto> Crear(CrearDelegacionDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var propia = UsuarioActual(contexto);
        var de = dto.DeUsuarioId ?? propia ?? throw ReglaDeNegocioException.Validacion("usuario-requerido", "Indica quién delega (deUsuarioId).");
        if (de != propia && !contexto.Scopes.Contains(Permisos.ExpedientesAdministrar))
            throw ReglaDeNegocioException.Prohibido("delegacion-ajena", "Solo un administrador de expedientes delega la revisión de otra persona.");

        var empresaId = empresa.EmpresaId;
        var quien = await empresa.Sesion.LoadAsync<Usuario>(de, ct);
        if (quien is not { Activo: true } || !quien.TieneRolEn(empresaId))
            throw ReglaDeNegocioException.Validacion("usuario-invalido", "Quien delega no es una persona activa de la empresa.");
        var delegado = await empresa.Sesion.LoadAsync<Usuario>(dto.AUsuarioId, ct);
        if (delegado is not { Activo: true } || !PuedeRevisar(delegado, empresaId))
            throw ReglaDeNegocioException.Validacion("delegado-sin-permiso-de-revision", "La persona elegida no puede revisar en esta empresa.");

        var areasDeLaEmpresa = await empresa.Areas().ToListAsync(ct);
        var suyas = areasDeLaEmpresa.Where(a => a.Activa && a.Miembros.Contains(de)).ToList();
        var todas = dto.Areas is not { Count: > 0 };
        var elegidas = todas
            ? suyas
            : [.. dto.Areas!.Distinct().Select(id => areasDeLaEmpresa.FirstOrDefault(a => a.Id == id)
                                                   ?? throw ReglaDeNegocioException.NoEncontrado("area-no-encontrada", "No existe esa área en la empresa."))];
        if (elegidas.Count == 0)
            throw ReglaDeNegocioException.Validacion("sin-areas-que-delegar", "Quien delega no es miembro de ningún área activa: no hay nada que delegar.");
        if (elegidas.FirstOrDefault(a => !a.Miembros.Contains(de)) is { } ajena)
            throw ReglaDeNegocioException.Validacion("no-es-miembro-del-area", $"Quien delega no es miembro del área {ajena.Codigo}: solo se delega lo propio.");

        var ahora = reloj.GetUtcNow();
        var hoy = empresa.Hoy;
        var delegacion = DelegacionDeRevision.Crear(empresaId, de, dto.AUsuarioId, elegidas, todas, dto.Desde, dto.Hasta, dto.Motivo,
            $"{contexto.ActorTipo}:{contexto.ActorId}", hoy, ahora);

        var existentes = await empresa.Sesion.Query<DelegacionDeRevision>()
            .Where(d => d.EmpresaId == empresaId && d.DeUsuarioId == de && d.AUsuarioId == dto.AUsuarioId).ToListAsync(ct);
        if (existentes.Any(d => d.SePisaCon(delegacion, hoy)))
            throw ReglaDeNegocioException.Conflicto("delegacion-solapada", "Ya hay una delegación de estas áreas a esta persona que se cruza con esas fechas.");

        empresa.Sesion.Store(delegacion);
        var nueva = (await ADtosAsync(empresa, [delegacion], ct))[0];
        Auditoria.Registrar(empresa.Sesion, contexto, "DelegacionDeRevision", delegacion.Id.ToString("N"), AccionConfiguracion.Creado, null, nueva, ahora, empresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return nueva;
    }

    /// <summary>Revoca una delegación programada o vigente: la puede revocar quien delegó, el delegado o un administrador de expedientes (409 <c>delegacion-no-revocable</c>).</summary>
    [WolverinePost("/api/v1/delegaciones/{id}/revocar")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<DelegacionDto> Revocar(Guid id, RevocarDelegacionDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var delegacion = await empresa.Sesion.LoadAsync<DelegacionDeRevision>(id, ct);
        var propia = UsuarioActual(contexto);
        if (delegacion is null || delegacion.EmpresaId != empresa.EmpresaId ||
            (propia != delegacion.DeUsuarioId && propia != delegacion.AUsuarioId && !contexto.Scopes.Contains(Permisos.ExpedientesAdministrar)))
            throw ReglaDeNegocioException.NoEncontrado("delegacion-no-encontrada", "No existe esa delegación en la empresa.");

        var antes = (await ADtosAsync(empresa, [delegacion], ct))[0];
        var ahora = reloj.GetUtcNow();
        delegacion.Revocar($"{contexto.ActorTipo}:{contexto.ActorId}", dto.Motivo, empresa.Hoy, ahora);
        empresa.Sesion.Store(delegacion);
        var despues = (await ADtosAsync(empresa, [delegacion], ct))[0];
        Auditoria.Registrar(empresa.Sesion, contexto, "DelegacionDeRevision", delegacion.Id.ToString("N"), AccionConfiguracion.Modificado, antes, despues, ahora, empresa.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return despues;
    }

    private static Guid? UsuarioActual(IContextoSolicitud contexto) =>
        contexto.ActorTipo == ActorTipo.Usuario && Guid.TryParse(contexto.ActorId, out var id) ? id : null;

    /// <summary>Activo, con rol en la empresa y con un rol que da el permiso de revisar.</summary>
    private static bool PuedeRevisar(Usuario u, Guid empresaId) =>
        u.Activo && u.Roles.Any(r => (r.EmpresaId is null || r.EmpresaId == empresaId) && Permisos.DeRol(r.Rol).Contains(Permisos.ExpedientesRevisar));

    private static async Task<IReadOnlyList<DelegacionDto>> ADtosAsync(SesionDeEmpresa empresa, IReadOnlyList<DelegacionDeRevision> delegaciones, CancellationToken ct)
    {
        if (delegaciones.Count == 0)
            return [];
        var usuarioIds = delegaciones.SelectMany(d => new[] { d.DeUsuarioId, d.AUsuarioId }).Distinct().ToArray();
        var usuarios = (await empresa.Sesion.LoadManyAsync<Usuario>(ct, usuarioIds)).ToDictionary(u => u.Id);
        var areas = (await empresa.Areas().ToListAsync(ct)).ToDictionary(a => a.Id);
        var hoy = empresa.Hoy;
        string Nombre(Guid id) => usuarios.TryGetValue(id, out var u) ? u.Nombre : "Persona retirada";
        return
        [
            .. delegaciones.Select(d => new DelegacionDto(d.Id, d.DeUsuarioId, Nombre(d.DeUsuarioId), d.AUsuarioId, Nombre(d.AUsuarioId),
                [.. d.Areas.Where(areas.ContainsKey).Select(a => new AreaDeDelegacionDto(a, areas[a].Codigo, areas[a].Nombre))], d.TodasLasAreas, d.Desde, d.Hasta, d.Motivo,
                d.EstadoEn(hoy).ToString(), d.CreadaPor, d.CreadaEn, d.RevocadaEn, d.MotivoDeRevocacion))
        ];
    }
}
