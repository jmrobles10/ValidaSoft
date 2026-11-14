using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// Quién puede aprobar las invitaciones de un tipo de vínculo en una empresa (decisión AI): las personas activas con rol en la empresa y <c>vinculos:escribir</c> (el
/// permiso de invitar) que cubre lo que el tipo configuró —el rol designado o ser miembro del área con ese código en la empresa—. Una integración (API key) nunca
/// aprueba: las decisiones son de personas, como los veredictos y la cuenta designada.
/// </summary>
public static class AprobadoresDeInvitaciones
{
    /// <summary>Las personas que pueden aprobar las invitaciones de <paramref name="tipo"/> en la empresa; vacío si el tipo no designó quién (o el área ya no existe).</summary>
    public static IReadOnlyList<Usuario> Elegibles(TipoVinculo tipo, Guid empresaId, IEnumerable<Usuario> usuarios, IEnumerable<Area> areas)
    {
        if (tipo.AprobadoresDeInvitacion is not { } aprobadores)
            return [];

        var conPermiso = usuarios.Where(u => u.Activo && u.Roles.Any(r =>
            (r.EmpresaId is null || r.EmpresaId == empresaId) && Permisos.DeRol(r.Rol).Contains(Permisos.VinculosEscribir))).ToList();

        if (aprobadores.Tipo == TipoDeAprobadores.Rol)
        {
            var rol = aprobadores.RolDesignado;
            return [.. conPermiso.Where(u => u.Roles.Any(r => (r.EmpresaId is null || r.EmpresaId == empresaId) && r.Rol == rol))];
        }

        var area = areas.FirstOrDefault(a => a.EmpresaId == empresaId && a.Activa && string.Equals(a.Codigo, aprobadores.Valor, StringComparison.OrdinalIgnoreCase));
        return area is null ? [] : [.. conPermiso.Where(u => area.Miembros.Contains(u.Id))];
    }

    /// <summary>Quién pide: la persona o la integración de la petición.</summary>
    public static ActorDeInvitacion Solicitante(IContextoSolicitud contexto) => new(contexto.ActorTipo, contexto.ActorId);

    /// <summary>El id de la persona de la petición (identidad delegada o su sesión); nulo para una integración.</summary>
    public static Guid? UsuarioDe(IContextoSolicitud contexto) =>
        contexto.ActorTipo == ActorTipo.Usuario && Guid.TryParse(contexto.ActorId, out var id) ? id : null;

    /// <summary>
    /// Antes de retener una invitación comprueba que alguien más pueda aprobarla (422 <c>sin-aprobadores-de-invitacion</c>): si el tipo no designó quién, el área no existe en la
    /// empresa o el único que puede es quien la pide, la invitación quedaría retenida para siempre.
    /// </summary>
    public static async Task ExigirAprobadoresAsync(SesionDeEmpresa empresa, IReadOnlyCollection<TipoVinculo> tipos, IContextoSolicitud contexto, CancellationToken ct)
    {
        var usuarios = await empresa.Sesion.Query<Usuario>().Where(u => u.Activo).ToListAsync(ct);
        var areas = await empresa.Areas().ToListAsync(ct);
        var solicitante = UsuarioDe(contexto);
        foreach (var tipo in tipos)
        {
            if (!Elegibles(tipo, empresa.EmpresaId, usuarios, areas).Any(u => u.Id != solicitante))
                throw ReglaDeNegocioException.Validacion("sin-aprobadores-de-invitacion",
                    $"El tipo de vínculo {tipo.Codigo} exige aprobar la invitación, pero no hay otra persona en la empresa que pueda hacerlo " +
                    "(revisa quién aprueba en su configuración: el rol o el área, con sus miembros).");
        }
    }

    /// <summary>
    /// Quien decide una invitación o un lote: una persona (403 <c>usuario-delegado-requerido</c> si no) que apruebe <b>todos</b> los tipos que la exigen (403
    /// <c>no-es-aprobador-de-invitaciones</c>). Devuelve su identidad para las reglas de conflicto de intereses del dominio.
    /// </summary>
    public static async Task<Revisor> DecisorAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, IReadOnlyCollection<TipoVinculo> tipos, CancellationToken ct)
    {
        var usuarioId = UsuarioDe(contexto)
                        ?? throw ReglaDeNegocioException.Validacion("usuario-delegado-requerido",
                            "Las invitaciones las aprueba una persona: actúa con la identidad delegada (X-Usuario-Delegado) o con su sesión.");

        var usuarios = await empresa.Sesion.Query<Usuario>().Where(u => u.Activo).ToListAsync(ct);
        var areas = await empresa.Areas().ToListAsync(ct);
        foreach (var tipo in tipos)
        {
            if (Elegibles(tipo, empresa.EmpresaId, usuarios, areas).All(u => u.Id != usuarioId))
                throw ReglaDeNegocioException.Prohibido("no-es-aprobador-de-invitaciones",
                    $"No eres de quienes aprueban las invitaciones de tipo {tipo.Codigo} en esta empresa.");
        }

        var usuario = usuarios.First(u => u.Id == usuarioId);
        return new Revisor(usuarioId, usuario.Identificacion?.Clave);
    }

    /// <summary>Anota la solicitud o la decisión en la auditoría de la empresa (la misma transacción que el evento).</summary>
    internal static void Auditar(SesionDeEmpresa empresa, IContextoSolicitud contexto, string entidad, Guid id, AccionConfiguracion accion, object? antes, object? despues,
        DateTimeOffset ahora) =>
        Auditoria.Registrar(empresa.Sesion, contexto, entidad, id.ToString(), accion, antes, despues, ahora, empresa.EmpresaId);

    /// <summary>La aprobación del vínculo con los nombres de quienes pidieron y decidieron y lo que puede hacer quien consulta.</summary>
    internal static InvitacionPorAprobarDto? ConNombres(InvitacionPorAprobarDto? dto, IReadOnlyDictionary<string, string> nombres, bool? puedesDecidir) =>
        dto is null
            ? null
            : dto with
            {
                SolicitadaPorNombre = NombreDe(dto.SolicitadaPor, nombres),
                DecididaPorNombre = dto.DecididaPor is null ? null : NombreDe(dto.DecididaPor, nombres),
                PuedesDecidir = puedesDecidir
            };

    private static string? NombreDe(string actor, IReadOnlyDictionary<string, string> nombres) =>
        actor.StartsWith($"{ActorTipo.Usuario}:", StringComparison.Ordinal) && nombres.TryGetValue(actor[(ActorTipo.Usuario.ToString().Length + 1)..], out var nombre) ? nombre : null;

    /// <summary>Los nombres de las personas que aparecen como actores (<c>Usuario:{id}</c>), para mostrar quién pidió y quién decidió.</summary>
    internal static async Task<IReadOnlyDictionary<string, string>> NombresAsync(SesionDeEmpresa empresa, IEnumerable<string?> actores, CancellationToken ct)
    {
        var ids = actores.OfType<string>()
            .Where(a => a.StartsWith($"{ActorTipo.Usuario}:", StringComparison.Ordinal))
            .Select(a => Guid.TryParse(a[(ActorTipo.Usuario.ToString().Length + 1)..], out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToArray();
        return ids.Length == 0
            ? new Dictionary<string, string>()
            : (await empresa.Sesion.LoadManyAsync<Usuario>(ct, ids)).ToDictionary(u => u.Id.ToString(), u => u.Nombre);
    }
}

/// <summary>
/// Aprobación antes de invitar (decisión AI, equivale al estado -5 del SRM): la bandeja de quienes aprueban y las decisiones sobre cada invitación retenida. Con el tipo de vínculo en
/// <c>RequiereAprobacionParaInvitar</c> la invitación no sale al crear el vínculo: queda retenida hasta que otra persona —del rol o del área que el tipo designó— la apruebe (sale la
/// invitación de siempre) o la rechace con un motivo (el vínculo queda <c>Anulado</c>). Los lotes se deciden completos en <see cref="Invitaciones.LotesEndpoints"/>.
/// </summary>
public static class InvitacionesPorAprobarEndpoints
{
    private const int TamanoDeBloque = 200;

    /// <summary>
    /// Las invitaciones retenidas de la empresa, de la más antigua a la más reciente (cursor opaco). <c>soloLasMias</c>: solo las que quien consulta puede decidir (aprueba el
    /// tipo y no las pidió él). Cada una dice <c>puedesDecidir</c>.
    /// </summary>
    [WolverineGet("/api/v1/invitaciones/por-aprobar")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<PaginaDto<InvitacionPorAprobarItemDto>> Listar(SesionDeEmpresa empresa, IContextoSolicitud contexto, bool? soloLasMias, int? limite,
        string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        DateTimeOffset? marca = string.IsNullOrWhiteSpace(cursor) ? null : VinculosEndpoints.Cursor.Leer(cursor);
        var tipos = (await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct)).ToDictionary(t => t.Id);
        var usuarios = await empresa.Sesion.Query<Usuario>().Where(u => u.Activo).ToListAsync(ct);
        var areas = await empresa.Areas().ToListAsync(ct);
        var yo = AprobadoresDeInvitaciones.UsuarioDe(contexto);
        var elegiblesPorTipo = tipos.Values.ToDictionary(t => t.Id,
            t => AprobadoresDeInvitaciones.Elegibles(t, empresa.EmpresaId, usuarios, areas).Select(u => u.Id).ToHashSet());

        bool PuedeDecidir(Vinculo v) =>
            yo is { } usuario && elegiblesPorTipo.TryGetValue(v.TipoVinculoId, out var elegibles) && elegibles.Contains(usuario) &&
            !(v.InvitacionSolicitadaPor is { Tipo: ActorTipo.Usuario } pidio && string.Equals(pidio.Id, usuario.ToString(), StringComparison.OrdinalIgnoreCase));

        var filtra = soloLasMias is true;
        var elegidos = new List<Vinculo>();
        var bloque = filtra ? TamanoDeBloque : tamano + 1;
        while (elegidos.Count <= tamano)
        {
            var consulta = empresa.Vinculos().Where(Vinculo.EsInvitacionPorAprobar);
            if (marca is { } desde)
                consulta = consulta.Where(v => v.InvitacionSolicitadaEn > desde);
            var pagina = await consulta.OrderBy(v => v.InvitacionSolicitadaEn).Take(bloque).ToListAsync(ct);
            foreach (var vinculo in pagina)
            {
                marca = vinculo.InvitacionSolicitadaEn;
                if (!filtra || PuedeDecidir(vinculo))
                    elegidos.Add(vinculo);
                if (elegidos.Count > tamano)
                    break;
            }

            if (pagina.Count < bloque)
                break;
        }

        var visibles = elegidos.Take(tamano).ToList();
        var siguiente = elegidos.Count > tamano && visibles[^1].InvitacionSolicitadaEn is { } ultima ? VinculosEndpoints.Cursor.Escribir(ultima) : null;
        var terceros = visibles.Count == 0
            ? new Dictionary<Guid, Tercero>()
            : (await empresa.Sesion.LoadManyAsync<Tercero>(ct, visibles.Select(v => v.TerceroId).Distinct().ToArray())).ToDictionary(t => t.Id);
        var nombres = await AprobadoresDeInvitaciones.NombresAsync(empresa, visibles.Select(v => v.InvitacionSolicitadaPor.ATexto()), ct);
        return new PaginaDto<InvitacionPorAprobarItemDto>([.. visibles.Select(v =>
        {
            var tipo = tipos.GetValueOrDefault(v.TipoVinculoId);
            var tercero = terceros.GetValueOrDefault(v.TerceroId);
            var solicitante = v.InvitacionSolicitadaPor.ATexto() ?? string.Empty;
            return new InvitacionPorAprobarItemDto(v.Id, v.TerceroId, tercero?.Nombre ?? string.Empty,
                tercero?.Identificacion.ADto() ?? new IdentificacionDto(string.Empty, string.Empty, string.Empty, null),
                new TipoVinculoResumenDto(v.TipoVinculoId, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty),
                v.InvitacionPendienteCorreo ?? v.CorreoTercero, v.InvitacionSolicitadaEn ?? v.CreadoEn, solicitante,
                nombres.GetValueOrDefault(v.InvitacionSolicitadaPor?.Id ?? string.Empty), PuedeDecidir(v));
        })], siguiente);
    }

    /// <summary>
    /// Aprueba la invitación retenida: en la misma transacción sale la invitación de siempre (enlace nuevo, correo por el outbox y la decisión del acceso del correo). Quien aprueba debe
    /// ser de quienes el tipo designó (403 <c>no-es-aprobador-de-invitaciones</c>), una persona (422 <c>usuario-delegado-requerido</c>) y no quien la pidió (403
    /// <c>separacion-de-funciones</c>) ni tener la identificación del tercero (403 <c>conflicto-de-intereses</c>); 409 <c>sin-invitacion-por-aprobar</c>.
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/invitacion/aprobar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Aprobar(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http, IMessageBus mensajes,
        IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var ahora = reloj.GetUtcNow();
        var stream = await empresa.VinculoParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
        var vinculo = stream.Aggregate!;
        var tipo = await empresa.Sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct)
                   ?? throw ReglaDeNegocioException.NoEncontrado("tipo-vinculo-no-encontrado", "El tipo del vínculo ya no existe.");
        var tercero = await empresa.Sesion.LoadAsync<Tercero>(vinculo.TerceroId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero del vínculo ya no existe.");
        var decisor = await AprobadoresDeInvitaciones.DecisorAsync(empresa, contexto, [tipo], ct);

        var (aprobada, enviada) = vinculo.AprobarInvitacion(decisor, tercero.Identificacion.Clave, null, ahora);
        stream.AppendOne(aprobada);
        stream.AppendOne(enviada);
        await EnlacesDeAcceso.InvitarAsync(empresa.Sesion, mensajes, protector, contexto.TenantRequerido(), vinculo, tipo, enviada, ct);
        // Ahora sí se decide el acceso del correo invitado: contacto conocido, primer contacto del tercero o desconocido (acceso aislado hasta que lo apruebe un contacto).
        await AccesoAislado.RegistrarInvitacionAsync(empresa.Sesion, mensajes, protector, contexto.TenantRequerido(), tercero, terceroNuevo: false, vinculo,
            enviada.CorreoDestino, stream.AppendOne, ahora, ct);
        AprobadoresDeInvitaciones.Auditar(empresa, contexto, "InvitacionDeVinculo", id, AccionConfiguracion.Modificado,
            new { Estado = nameof(EstadoAprobacionDeInvitacion.Pendiente) },
            new { Estado = nameof(EstadoAprobacionDeInvitacion.Aprobada), aprobada.SolicitadaPor, aprobada.AprobadaPor, Correo = enviada.CorreoDestino }, ahora);
        await empresa.Sesion.SaveChangesAsync(ct);
        return await DtoAsync(empresa, contexto, id, ct);
    }

    /// <summary>
    /// Rechaza la invitación retenida con un motivo (422 <c>motivo-requerido</c>): el vínculo queda <c>Anulado</c> con ese motivo y nunca se le escribió al tercero. Las mismas reglas de
    /// quién decide que al aprobarla.
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/invitacion/rechazar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Rechazar(Guid id, MotivoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http, IMessageBus mensajes,
        TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var ahora = reloj.GetUtcNow();
        var stream = await empresa.VinculoParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
        var vinculo = stream.Aggregate!;
        var tipo = await empresa.Sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct)
                   ?? throw ReglaDeNegocioException.NoEncontrado("tipo-vinculo-no-encontrado", "El tipo del vínculo ya no existe.");
        var tercero = await empresa.Sesion.LoadAsync<Tercero>(vinculo.TerceroId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero del vínculo ya no existe.");
        var decisor = await AprobadoresDeInvitaciones.DecisorAsync(empresa, contexto, [tipo], ct);

        var (rechazada, anulada) = vinculo.RechazarInvitacion(decisor, tercero.Identificacion.Clave, dto.Motivo, ahora);
        stream.AppendOne(rechazada);
        stream.AppendOne(anulada);
        AprobadoresDeInvitaciones.Auditar(empresa, contexto, "InvitacionDeVinculo", id, AccionConfiguracion.Modificado,
            new { Estado = nameof(EstadoAprobacionDeInvitacion.Pendiente) },
            new { Estado = nameof(EstadoAprobacionDeInvitacion.Rechazada), rechazada.SolicitadaPor, rechazada.RechazadaPor, rechazada.Motivo }, ahora);
        await empresa.Sesion.SaveChangesAsync(ct);
        return await DtoAsync(empresa, contexto, id, ct);
    }

    /// <summary>El vínculo recargado con su aprobación, los nombres de quienes pidieron y decidieron y si quien consulta puede decidirla.</summary>
    internal static async Task<VinculoDto> DtoAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, Guid id, CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        var tipo = await empresa.Sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct);
        return await ConAprobacionAsync(empresa, contexto, vinculo.ADto(tipo), vinculo, tipo, ct);
    }

    /// <summary>Completa <c>InvitacionPorAprobar</c> del DTO con los nombres y <c>PuedesDecidir</c> (solo mientras está retenida).</summary>
    internal static async Task<VinculoDto> ConAprobacionAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, VinculoDto dto, Vinculo vinculo, TipoVinculo? tipo,
        CancellationToken ct)
    {
        if (dto.InvitacionPorAprobar is null)
            return dto;

        bool? puedes = null;
        if (vinculo.InvitacionPorAprobar && tipo is not null)
        {
            var yo = AprobadoresDeInvitaciones.UsuarioDe(contexto);
            var usuarios = await empresa.Sesion.Query<Usuario>().Where(u => u.Activo).ToListAsync(ct);
            var areas = await empresa.Areas().ToListAsync(ct);
            puedes = yo is { } usuario && AprobadoresDeInvitaciones.Elegibles(tipo, empresa.EmpresaId, usuarios, areas).Any(u => u.Id == usuario) &&
                     !(vinculo.InvitacionSolicitadaPor is { Tipo: ActorTipo.Usuario } pidio && string.Equals(pidio.Id, usuario.ToString(), StringComparison.OrdinalIgnoreCase));
        }

        var nombres = await AprobadoresDeInvitaciones.NombresAsync(empresa, [dto.InvitacionPorAprobar.SolicitadaPor, dto.InvitacionPorAprobar.DecididaPor], ct);
        return dto with { InvitacionPorAprobar = AprobadoresDeInvitaciones.ConNombres(dto.InvitacionPorAprobar, nombres, puedes) };
    }
}
