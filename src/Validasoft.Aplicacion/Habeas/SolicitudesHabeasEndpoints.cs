using System.Text.Json;
using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Habeas;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Habeas;

/// <summary>Lo que el titular escribe al presentar la solicitud: viaja cifrado con su llave dentro del evento (crypto-shredding, OD-4).</summary>
public sealed record DatosDelTitular(string Nombre, string Identificacion, string Correo, string? Detalle);

/// <summary>
/// Pasos comunes de las solicitudes de habeas data (OD-1…OD-8): leerlas con sus datos protegidos, calcular su plazo y resolverlas. El
/// titular las presenta en su portal; las atiende la empresa (si se dirigen a ella) o la plataforma (la bóveda global).
/// </summary>
public static class ServicioDeSolicitudesHabeas
{
    public const string Entidad = "SolicitudHabeas";
    public const string NombreDeLaPlataforma = "Validasoft";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Plazo(SolicitudHabeas s, DateOnly hoy)
    {
        if (!s.EstaAbierta)
            return "Resuelta";
        if (hoy > s.VenceEl)
            return "Vencida";
        return s.DiasHabilesRestantes(hoy) <= SolicitudHabeas.DiasDeAviso ? "PorVencer" : "EnPlazo";
    }

    public static async Task<SolicitudHabeasDto> ADtoAsync(SolicitudHabeas s, IProtectorDeDatosDelTercero protector, string nombreDelDestino, DateOnly hoy,
        bool paraQuienResponde, CancellationToken ct)
    {
        var datos = await LeerDatosAsync(s, protector, ct);
        var respuesta = s.RespuestaProtegida is null ? null : await protector.RevelarAsync(s.RespuestaProtegida, ct);
        var suprimido = datos is null || (s.RespuestaProtegida is not null && respuesta is null);
        return new SolicitudHabeasDto(s.Id, s.Radicado, s.Tipo.ToString(), s.Alcance?.ToString(), s.Estado.ToString(), s.Resultado?.ToString(),
            new DestinoDeSolicitudHabeasDto(s.EmpresaId, nombreDelDestino), s.RecibidaEn, s.VenceEl, s.DiasHabiles,
            s.EstaAbierta ? s.DiasHabilesRestantes(hoy) : 0, Plazo(s, hoy), s.DentroDelPlazo, s.ResueltaEn, datos?.Detalle, respuesta, suprimido,
            s.ConstanciaHash, paraQuienResponde && datos is not null ? new TitularDeSolicitudDto(datos.Nombre, datos.Identificacion, datos.Correo) : null);
    }

    /// <summary>Los datos del titular, o nulo si ya no se pueden leer (la llave se destruyó).</summary>
    public static async Task<DatosDelTitular?> LeerDatosAsync(SolicitudHabeas s, IProtectorDeDatosDelTercero protector, CancellationToken ct)
    {
        var claro = await protector.RevelarAsync(s.DatosProtegidos, ct);
        return claro is null ? null : JsonSerializer.Deserialize<DatosDelTitular>(claro, Json);
    }

    /// <summary>Nombre de una empresa por id (consulta global).</summary>
    public static async Task<IReadOnlyDictionary<Guid, string>> NombresDeEmpresasAsync(ISesiones sesiones, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distintos = ids.Distinct().ToArray();
        if (distintos.Length == 0)
            return new Dictionary<Guid, string>();
        await using var consulta = sesiones.ConsultaGlobal();
        return (await consulta.LoadManyAsync<Empresa>(ct, distintos)).ToDictionary(e => e.Id, e => e.Nombre);
    }

    public static ConstanciaHabeasDto ADto(ConstanciaDeSolicitud c, string hash) => new(c.Radicado, c.Tipo, c.Alcance, c.Destino, c.RecibidaEl, c.ResueltaEl,
        c.DentroDelPlazo, c.Resultado, [.. c.Suprimidos.Select(x => new ElementoSuprimidoDto(x.Tipo, x.Referencia, x.Descripcion))],
        [.. c.Retenidos.Select(x => new ElementoRetenidoDto(x.Tipo, x.Referencia, x.Descripcion, x.BaseLegal, x.RetenidaHasta))], c.Anonimizados, hash);

    public static ResumenDeSolicitudesHabeasDto Resumen(IReadOnlyCollection<SolicitudHabeas> solicitudes, DateOnly hoy)
    {
        var abiertas = solicitudes.Where(s => s.EstaAbierta).ToList();
        return new ResumenDeSolicitudesHabeasDto(abiertas.Count, abiertas.Count(s => Plazo(s, hoy) == "PorVencer"), abiertas.Count(s => Plazo(s, hoy) == "Vencida"),
            solicitudes.Count - abiertas.Count);
    }

    /// <summary>Estados pedidos en la lista: <c>abiertas</c> (por omisión: todas), <c>resueltas</c> o <c>vencidas</c>.</summary>
    public static IEnumerable<SolicitudHabeas> Filtrar(IEnumerable<SolicitudHabeas> solicitudes, string? estado, DateOnly hoy) => (estado ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "abiertas" or "recibidas" => solicitudes.Where(s => s.EstaAbierta),
        "resueltas" => solicitudes.Where(s => !s.EstaAbierta),
        "vencidas" => solicitudes.Where(s => s.EstaAbierta && hoy > s.VenceEl),
        "" or "todas" => solicitudes,
        _ => throw ReglaDeNegocioException.Validacion("estado-invalido", "El estado es abiertas, resueltas, vencidas o todas.")
    };

    /// <summary>Abiertas primero (las que vencen antes arriba) y luego las resueltas, la más reciente primero.</summary>
    public static IOrderedEnumerable<SolicitudHabeas> Ordenar(IEnumerable<SolicitudHabeas> solicitudes) =>
        solicitudes.OrderBy(s => s.EstaAbierta ? 0 : 1).ThenBy(s => s.EstaAbierta ? s.VenceEl : DateOnly.MaxValue).ThenByDescending(s => s.RecibidaEn);

    public static (string Decision, string Respuesta) LeerRespuesta(ResolverSolicitudHabeasDto dto)
    {
        var decision = (dto.Decision ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "atender" => "Atender",
            "rechazar" => "Rechazar",
            _ => throw ReglaDeNegocioException.Validacion("decision-invalida", "La decisión es Atender o Rechazar.")
        };
        var respuesta = (dto.Respuesta ?? string.Empty).Trim();
        if (respuesta.Length < SolicitudHabeas.LongitudMinimaRespuesta)
            throw ReglaDeNegocioException.Validacion("respuesta-requerida",
                $"Escribe la respuesta que recibirá el titular (mínimo {SolicitudHabeas.LongitudMinimaRespuesta} caracteres).");
        if (respuesta.Length > 4000)
            throw ReglaDeNegocioException.Validacion("respuesta-demasiado-larga", "La respuesta admite hasta 4000 caracteres.");
        return (decision, ConBaseLegal(decision, respuesta, dto.BaseLegal));
    }

    private static string ConBaseLegal(string decision, string respuesta, string? baseLegal) =>
        decision == "Rechazar" && !string.IsNullOrWhiteSpace(baseLegal) ? $"{respuesta}\n\nBase legal: {baseLegal.Trim()}" : respuesta;

    /// <summary>
    /// Resuelve la solicitud sobre su stream bloqueado: protege la respuesta con la llave del titular, sella la constancia, anota el evento
    /// y le escribe al titular (acuse con la constancia, por el outbox). Con <paramref name="ejecucion"/> la constancia lista lo suprimido
    /// y lo retenido.
    /// </summary>
    public static async Task<(SolicitudHabeas Solicitud, string Respuesta)> ResolverAsync(IEventStream<SolicitudHabeas> stream,
        IMessageBus mensajes, IProtectorDeDatosDelTercero protector, IProtectorDeSecretos secretos, string decision, string respuesta, string destino,
        ResultadoDeSupresion? ejecucion, string? correo, ActorDeSolicitud actor, Guid? tenantId, DateTimeOffset ahora, CancellationToken ct)
    {
        var actual = stream.Aggregate!;
        var resultado = decision == "Rechazar"
            ? ResultadoDeSolicitudHabeas.Rechazada
            : ejecucion is { HayRetenciones: true } ? ResultadoDeSolicitudHabeas.Parcial : ResultadoDeSolicitudHabeas.Atendida;
        var dia = CalculadoraDeVigencia.Fecha(ahora);
        var constancia = new ConstanciaDeSolicitud(actual.Radicado, actual.Tipo.ToString(), actual.Alcance?.ToString(), destino, actual.RecibidaEl, dia,
            dia <= actual.VenceEl, resultado.ToString(), ejecucion?.Suprimidos ?? [], ejecucion?.Retenidos ?? [], ejecucion?.Anonimizados ?? []);
        var (json, hash) = constancia.Sellar();
        var evento = actual.Resolver(resultado, await protector.ProtegerAsync(actual.TerceroId, respuesta, ct), json, hash, actor, ahora);
        stream.AppendOne(evento);

        if (!string.IsNullOrWhiteSpace(correo))
            await mensajes.PublishAsync(new CorreoDeSolicitudHabeasPendiente(correo, actual.Radicado, MomentoDeSolicitudHabeas.Resuelta, actual.Tipo, destino,
                actual.VenceEl, resultado, secretos.Proteger(respuesta), hash, tenantId, actual.EmpresaId));
        return (actual, respuesta);
    }
}

/// <summary>
/// El titular presenta y sigue sus solicitudes de habeas data desde su portal (sesión <c>Tercero</c>, OD-1): consulta de sus datos,
/// corrección o supresión, dirigida a una empresa con la que tiene un vínculo o a la plataforma (la bóveda global). Cada solicitud
/// nace con su radicado y su plazo en días hábiles (10 las consultas, 15 los reclamos).
/// </summary>
public static class SolicitudesHabeasDelTerceroEndpoints
{
    private const int MaximoSolicitudesAbiertasIguales = 1;

    /// <summary>
    /// Presenta la solicitud. 422 <c>tipo-invalido</c>, <c>alcance-invalido</c>, <c>detalle-requerido</c> (la corrección), <c>detalle-demasiado-largo</c>;
    /// 404 <c>empresa-no-encontrada</c> si no tiene vínculo con esa empresa; 409 <c>solicitud-en-curso</c> si ya tiene una igual sin resolver.
    /// </summary>
    [WolverinePost("/public/v1/solicitudes-habeas")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IResult> Crear(CrearSolicitudHabeasDto dto, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        IProtectorDeDatosDelTercero protector, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        if (!Enum.TryParse<TipoDeSolicitudHabeas>(dto.Tipo, true, out var tipo) || !Enum.IsDefined(tipo))
            throw ReglaDeNegocioException.Validacion("tipo-invalido", "El derecho solicitado es Consulta, Correccion o Supresion.");
        AlcanceDeSupresion? alcance = null;
        if (!string.IsNullOrWhiteSpace(dto.Alcance))
        {
            if (!Enum.TryParse<AlcanceDeSupresion>(dto.Alcance, true, out var leido) || !Enum.IsDefined(leido))
                throw ReglaDeNegocioException.Validacion("alcance-invalido", "El alcance de la supresión es Todo, Documentos o Datos.");
            alcance = leido;
        }

        var detalle = string.IsNullOrWhiteSpace(dto.Detalle) ? null : dto.Detalle.Trim();
        if (detalle is { Length: > SolicitudHabeas.LongitudMaximaDetalle })
            throw ReglaDeNegocioException.Validacion("detalle-demasiado-largo", $"El detalle admite hasta {SolicitudHabeas.LongitudMaximaDetalle} caracteres.");
        if (tipo == TipoDeSolicitudHabeas.Correccion && (detalle is null || detalle.Length < 10))
            throw ReglaDeNegocioException.Validacion("detalle-requerido", "Cuéntanos qué dato quieres corregir y cuál es el correcto (mínimo 10 caracteres).");

        DestinoDeSolicitud destino = new(null, null);
        if (dto.EmpresaId is { } empresaId)
        {
            var enSesion = tercero.Vinculos.FirstOrDefault(v => v.EmpresaId == empresaId)
                           ?? throw ReglaDeNegocioException.NoEncontrado("empresa-no-encontrada", "No tienes un vínculo con esa empresa.");
            destino = new DestinoDeSolicitud(enSesion.TenantId, enSesion.EmpresaId);
        }

        await using var consultaGlobal = sesiones.ConsultaGlobal();
        var datosDelTercero = await consultaGlobal.LoadAsync<Tercero>(tercero.TerceroId, ct)
                              ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe el tercero de esta sesión.");
        var ahora = reloj.GetUtcNow();

        await using var consulta = sesiones.ConsultaBoveda();
        var propias = await consulta.Query<SolicitudHabeas>().Where(s => s.TerceroId == tercero.TerceroId && s.Estado == EstadoDeSolicitudHabeas.Recibida).ToListAsync(ct);
        if (propias.Count(s => s.Tipo == tipo && s.EmpresaId == destino.EmpresaId) >= MaximoSolicitudesAbiertasIguales)
        {
            var existente = propias.First(s => s.Tipo == tipo && s.EmpresaId == destino.EmpresaId);
            throw ReglaDeNegocioException.Conflicto("solicitud-en-curso", $"Ya tienes una solicitud igual en trámite (radicado {existente.Radicado}).");
        }

        var nombreDestino = destino.EsPlataforma
            ? ServicioDeSolicitudesHabeas.NombreDeLaPlataforma
            : (await ServicioDeSolicitudesHabeas.NombresDeEmpresasAsync(sesiones, [destino.EmpresaId!.Value], ct)).GetValueOrDefault(destino.EmpresaId.Value) ?? "la empresa";
        var datos = new DatosDelTitular(datosDelTercero.Nombre, $"{datosDelTercero.Identificacion.Tipo} {datosDelTercero.Identificacion.Numero}", tercero.Correo, detalle);
        var protegidos = await protector.ProtegerAsync(tercero.TerceroId, JsonSerializer.Serialize(datos, new JsonSerializerOptions(JsonSerializerDefaults.Web)), ct);
        var evento = SolicitudHabeas.Recibir(tercero.TerceroId, tipo, alcance, destino, protegidos, BovedaEndpoints.Ip(http), ahora);

        await using (var sesion = sesiones.Boveda(mensajes))
        {
            sesion.Events.StartStream<SolicitudHabeas>(evento.SolicitudId, evento);
            await mensajes.PublishAsync(new CorreoDeSolicitudHabeasPendiente(tercero.Correo, evento.Radicado, MomentoDeSolicitudHabeas.Recibida, tipo, nombreDestino,
                evento.VenceEl, TenantId: destino.TenantId, EmpresaId: destino.EmpresaId));
            await sesion.SaveChangesAsync(ct);
        }

        await AvisarYAuditarAsync(sesiones, mensajes, contexto, evento, nombreDestino, ahora, ct);
        var solicitud = SolicitudHabeas.Create(evento);
        return Results.Created($"/public/v1/solicitudes-habeas/{evento.SolicitudId}",
            await ServicioDeSolicitudesHabeas.ADtoAsync(solicitud, protector, nombreDestino, CalculadoraDeVigencia.Fecha(ahora), false, ct));
    }

    /// <summary>
    /// Avisa a quien responde (los administradores y el oficial de cumplimiento de la empresa, o los correos de la plataforma) y deja el
    /// hecho en la auditoría del destino.
    /// </summary>
    private static async Task AvisarYAuditarAsync(ISesiones sesiones, IMessageBus mensajes, IContextoSolicitud contexto, SolicitudHabeasRecibida evento,
        string nombreDestino, DateTimeOffset ahora, CancellationToken ct)
    {
        var derecho = RedaccionDeCorreosDeHabeas.Derecho(evento.Tipo);
        var auditado = new { evento.Radicado, Tipo = evento.Tipo.ToString(), Alcance = evento.Alcance?.ToString(), evento.VenceEl };
        if (evento.Destino is { EmpresaId: { } empresaId, TenantId: { } tenantId })
        {
            await AvisosAAdministradores.AvisarAsync(sesiones, mensajes, tenantId, new DestinatariosDeAviso(empresaId, ConOficial: true),
                TiposNotificacion.HabeasSolicitud, $"Solicitud de {derecho} de datos personales",
                $"Un tercero presentó una solicitud de {derecho} (radicado {evento.Radicado}). Tienes hasta el {evento.VenceEl:dd/MM/yyyy} para responderla ({evento.DiasHabiles} días hábiles).",
                $"/e/{empresaId}/habeas-data", $"habeas-recibida:{evento.SolicitudId:N}", nombreDestino, ahora, ct);
            await using var sesion = sesiones.ParaTenant(tenantId);
            Auditoria.Registrar(sesion, contexto, ServicioDeSolicitudesHabeas.Entidad, evento.SolicitudId.ToString(), AccionConfiguracion.Creado, null, auditado, ahora, empresaId);
            await sesion.SaveChangesAsync(ct);
        }
        else
        {
            await using var sesion = sesiones.Global();
            Auditoria.Registrar(sesion, contexto, ServicioDeSolicitudesHabeas.Entidad, evento.SolicitudId.ToString(), AccionConfiguracion.Creado, null, auditado, ahora);
            await sesion.SaveChangesAsync(ct);
        }
    }

    [WolverineGet("/public/v1/solicitudes-habeas")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IReadOnlyList<SolicitudHabeasDto>> Listar(HttpContext http, ISesiones sesiones, IProtectorDeDatosDelTercero protector, TimeProvider reloj,
        CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var consulta = sesiones.ConsultaBoveda();
        var propias = await consulta.Query<SolicitudHabeas>().Where(s => s.TerceroId == tercero.TerceroId).OrderByDescending(s => s.RecibidaEn).ToListAsync(ct);
        var nombres = await ServicioDeSolicitudesHabeas.NombresDeEmpresasAsync(sesiones, propias.Where(s => s.EmpresaId is not null).Select(s => s.EmpresaId!.Value), ct);
        var hoy = CalculadoraDeVigencia.Fecha(reloj.GetUtcNow());
        var lista = new List<SolicitudHabeasDto>(propias.Count);
        foreach (var solicitud in propias)
            lista.Add(await ServicioDeSolicitudesHabeas.ADtoAsync(solicitud, protector, NombreDelDestino(solicitud, nombres), hoy, false, ct));
        return lista;
    }

    [WolverineGet("/public/v1/solicitudes-habeas/{id}")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<SolicitudHabeasDto> Obtener(Guid id, HttpContext http, ISesiones sesiones, IProtectorDeDatosDelTercero protector, TimeProvider reloj,
        CancellationToken ct)
    {
        var solicitud = await CargarAsync(id, SesionDeTercero.De(http.User), sesiones, ct);
        var nombres = await ServicioDeSolicitudesHabeas.NombresDeEmpresasAsync(sesiones, solicitud.EmpresaId is { } e ? [e] : [], ct);
        return await ServicioDeSolicitudesHabeas.ADtoAsync(solicitud, protector, NombreDelDestino(solicitud, nombres), CalculadoraDeVigencia.Fecha(reloj.GetUtcNow()), false, ct);
    }

    /// <summary>
    /// Constancia de la respuesta, sin datos personales y con su huella SHA-256: qué se pidió, cuándo se respondió, qué se eliminó y qué
    /// se conserva por obligación legal. 409 <c>solicitud-sin-resolver</c> mientras no haya respuesta.
    /// </summary>
    [WolverineGet("/public/v1/solicitudes-habeas/{id}/constancia")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<ConstanciaHabeasDto> Constancia(Guid id, HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var solicitud = await CargarAsync(id, SesionDeTercero.De(http.User), sesiones, ct);
        return solicitud is { ConstanciaJson: { } json, ConstanciaHash: { } hash }
            ? ServicioDeSolicitudesHabeas.ADto(ConstanciaDeSolicitud.Leer(json), hash)
            : throw ReglaDeNegocioException.Conflicto("solicitud-sin-resolver", "La solicitud todavía no tiene respuesta.");
    }

    internal static string NombreDelDestino(SolicitudHabeas s, IReadOnlyDictionary<Guid, string> nombres) =>
        s.EmpresaId is { } id ? nombres.GetValueOrDefault(id) ?? "la empresa" : ServicioDeSolicitudesHabeas.NombreDeLaPlataforma;

    private static async Task<SolicitudHabeas> CargarAsync(Guid id, SesionDeTercero tercero, ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaBoveda();
        var solicitud = await consulta.LoadAsync<SolicitudHabeas>(id, ct);
        return solicitud is not null && solicitud.TerceroId == tercero.TerceroId
            ? solicitud
            : throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud.");
    }
}

/// <summary>
/// Lado empresa del habeas data (docs/05, <c>supresiones:resolver</c>): las solicitudes que los terceros dirigen a la empresa activa.
/// La empresa es la responsable de los datos de sus vínculos: responde ella, dentro del plazo de la ley, y su respuesta queda en la
/// constancia que recibe el titular.
/// </summary>
public static class SolicitudesHabeasDeLaEmpresaEndpoints
{
    private const int TamanoPagina = 50;

    /// <summary>
    /// Las solicitudes de la empresa, las abiertas primero (las que vencen antes arriba). <c>estado</c>: <c>abiertas</c>,
    /// <c>vencidas</c>, <c>resueltas</c> o <c>todas</c> (por omisión). Paginadas por cursor.
    /// </summary>
    [WolverineGet("/api/v1/solicitudes-habeas")]
    [Authorize(Policy = Permisos.SupresionesResolver)]
    public static async Task<PaginaDto<SolicitudHabeasDto>> Listar(string? estado, int? limite, string? cursor, SesionDeEmpresa empresa, ISesiones sesiones,
        IProtectorDeDatosDelTercero protector, TimeProvider reloj, CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        var tamano = Math.Clamp(limite ?? TamanoPagina, 1, 200);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        await using var consulta = sesiones.ConsultaBoveda();
        var todas = await consulta.Query<SolicitudHabeas>().Where(s => s.EmpresaId == empresaId).ToListAsync(ct);
        var hoy = CalendarioDeNegocio.Hoy(reloj);
        var filtradas = ServicioDeSolicitudesHabeas.Ordenar(ServicioDeSolicitudesHabeas.Filtrar(todas, estado, hoy)).ToList();
        var pagina = filtradas.Skip(desde).Take(tamano + 1).ToList();
        var hayMas = pagina.Count > tamano;
        if (hayMas)
            pagina.RemoveAt(pagina.Count - 1);

        var nombre = (await ServicioDeSolicitudesHabeas.NombresDeEmpresasAsync(sesiones, [empresaId], ct)).GetValueOrDefault(empresaId) ?? "la empresa";
        var dtos = new List<SolicitudHabeasDto>(pagina.Count);
        foreach (var solicitud in pagina)
            dtos.Add(await ServicioDeSolicitudesHabeas.ADtoAsync(solicitud, protector, nombre, hoy, true, ct));
        return new PaginaDto<SolicitudHabeasDto>(dtos, hayMas ? Peticiones.EscribirDesplazamiento(desde + tamano) : null);
    }

    [WolverineGet("/api/v1/solicitudes-habeas/resumen")]
    [Authorize(Policy = Permisos.SupresionesResolver)]
    public static async Task<ResumenDeSolicitudesHabeasDto> Resumen(SesionDeEmpresa empresa, ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        await using var consulta = sesiones.ConsultaBoveda();
        return ServicioDeSolicitudesHabeas.Resumen(await consulta.Query<SolicitudHabeas>().Where(s => s.EmpresaId == empresaId).ToListAsync(ct), CalendarioDeNegocio.Hoy(reloj));
    }

    [WolverineGet("/api/v1/solicitudes-habeas/{id}")]
    [Authorize(Policy = Permisos.SupresionesResolver)]
    public static async Task<SolicitudHabeasDto> Obtener(Guid id, SesionDeEmpresa empresa, ISesiones sesiones, IProtectorDeDatosDelTercero protector, TimeProvider reloj,
        CancellationToken ct)
    {
        var solicitud = await CargarAsync(id, empresa, sesiones, ct);
        var nombre = (await ServicioDeSolicitudesHabeas.NombresDeEmpresasAsync(sesiones, [empresa.EmpresaId], ct)).GetValueOrDefault(empresa.EmpresaId) ?? "la empresa";
        return await ServicioDeSolicitudesHabeas.ADtoAsync(solicitud, protector, nombre, CalendarioDeNegocio.Hoy(reloj), true, ct);
    }

    /// <summary>
    /// La empresa responde: <c>Atender</c> o <c>Rechazar</c> con la respuesta que recibirá el titular (por correo y en su portal) y la
    /// constancia sellada. 409 <c>solicitud-ya-resuelta</c>; 422 <c>decision-invalida</c>, <c>respuesta-requerida</c>.
    /// </summary>
    [WolverinePost("/api/v1/solicitudes-habeas/{id}/resolver")]
    [Authorize(Policy = Permisos.SupresionesResolver)]
    public static async Task<SolicitudHabeasDto> Resolver(Guid id, ResolverSolicitudHabeasDto dto, SesionDeEmpresa empresa, ISesiones sesiones,
        IContextoSolicitud contexto, IProtectorDeDatosDelTercero protector, IProtectorDeSecretos secretos, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        var (decision, respuesta) = ServicioDeSolicitudesHabeas.LeerRespuesta(dto);
        var empresaId = empresa.EmpresaId;
        var tenantId = contexto.TenantRequerido();
        await CargarAsync(id, empresa, sesiones, ct);
        var nombre = (await ServicioDeSolicitudesHabeas.NombresDeEmpresasAsync(sesiones, [empresaId], ct)).GetValueOrDefault(empresaId) ?? "la empresa";
        var ahora = reloj.GetUtcNow();

        await using (var sesion = sesiones.Boveda(mensajes))
        {
            var stream = await sesion.Events.FetchForExclusiveWriting<SolicitudHabeas>(id, ct);
            var actual = stream.Aggregate ?? throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud.");
            var datos = await ServicioDeSolicitudesHabeas.LeerDatosAsync(actual, protector, ct);
            await ServicioDeSolicitudesHabeas.ResolverAsync(stream, mensajes, protector, secretos, decision, respuesta, nombre, null, datos?.Correo,
                new ActorDeSolicitud(contexto.ActorTipo.ToString(), contexto.ActorId), tenantId, ahora, ct);
            await sesion.SaveChangesAsync(ct);
        }

        Auditoria.Registrar(empresa.Sesion, contexto, ServicioDeSolicitudesHabeas.Entidad, id.ToString(), AccionConfiguracion.Modificado,
            new { Estado = nameof(EstadoDeSolicitudHabeas.Recibida) }, new { Estado = nameof(EstadoDeSolicitudHabeas.Resuelta), Decision = decision }, ahora, empresaId);
        await empresa.Sesion.SaveChangesAsync(ct);

        await using var consulta = sesiones.ConsultaBoveda();
        var proyectada = (await consulta.LoadAsync<SolicitudHabeas>(id, ct))!;
        return await ServicioDeSolicitudesHabeas.ADtoAsync(proyectada, protector, nombre, CalendarioDeNegocio.Hoy(reloj), true, ct);
    }

    private static async Task<SolicitudHabeas> CargarAsync(Guid id, SesionDeEmpresa empresa, ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaBoveda();
        var solicitud = await consulta.LoadAsync<SolicitudHabeas>(id, ct);
        return solicitud is not null && solicitud.EmpresaId == empresa.EmpresaId
            ? solicitud
            : throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud en la empresa.");
    }
}

/// <summary>Hoy en la zona del negocio (FE-1).</summary>
public static class CalendarioDeNegocio
{
    public static DateOnly Hoy(TimeProvider reloj) => CalculadoraDeVigencia.Fecha(reloj.GetUtcNow());
}

/// <summary>
/// Lado plataforma del habeas data: la plataforma responde por la bóveda global del tercero (documentos, ficha, cuentas). La
/// supresión se ejecuta sobre un plan: lo que ninguna aceptación ni certificado referencia se elimina; lo referenciado se conserva
/// íntegro con su base legal y su fecha, y el titular recibe la constancia.
/// </summary>
public static class SolicitudesHabeasDeLaPlataformaEndpoints
{
    private const int TamanoPagina = 50;

    [WolverineGet("/api/v1/plataforma/solicitudes-habeas")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<PaginaDto<SolicitudHabeasDto>> Listar(string? estado, int? limite, string? cursor, ISesiones sesiones, IProtectorDeDatosDelTercero protector,
        TimeProvider reloj, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? TamanoPagina, 1, 200);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        await using var consulta = sesiones.ConsultaBoveda();
        var todas = await consulta.Query<SolicitudHabeas>().Where(s => s.EmpresaId == null).ToListAsync(ct);
        var hoy = CalendarioDeNegocio.Hoy(reloj);
        var pagina = ServicioDeSolicitudesHabeas.Ordenar(ServicioDeSolicitudesHabeas.Filtrar(todas, estado, hoy)).Skip(desde).Take(tamano + 1).ToList();
        var hayMas = pagina.Count > tamano;
        if (hayMas)
            pagina.RemoveAt(pagina.Count - 1);

        var dtos = new List<SolicitudHabeasDto>(pagina.Count);
        foreach (var solicitud in pagina)
            dtos.Add(await ServicioDeSolicitudesHabeas.ADtoAsync(solicitud, protector, ServicioDeSolicitudesHabeas.NombreDeLaPlataforma, hoy, true, ct));
        return new PaginaDto<SolicitudHabeasDto>(dtos, hayMas ? Peticiones.EscribirDesplazamiento(desde + tamano) : null);
    }

    [WolverineGet("/api/v1/plataforma/solicitudes-habeas/resumen")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResumenDeSolicitudesHabeasDto> Resumen(ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaBoveda();
        return ServicioDeSolicitudesHabeas.Resumen(await consulta.Query<SolicitudHabeas>().Where(s => s.EmpresaId == null).ToListAsync(ct), CalendarioDeNegocio.Hoy(reloj));
    }

    [WolverineGet("/api/v1/plataforma/solicitudes-habeas/{id}")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SolicitudHabeasDto> Obtener(Guid id, ISesiones sesiones, IProtectorDeDatosDelTercero protector, TimeProvider reloj, CancellationToken ct) =>
        await ServicioDeSolicitudesHabeas.ADtoAsync(await CargarAsync(id, sesiones, ct), protector, ServicioDeSolicitudesHabeas.NombreDeLaPlataforma,
            CalendarioDeNegocio.Hoy(reloj), true, ct);

    /// <summary>
    /// Lo que ejecutaría la supresión sobre la bóveda del tercero, sin cambiar nada: qué se elimina, qué se conserva con su base legal y
    /// hasta cuándo, y qué se anonimiza. 409 <c>no-es-supresion</c>.
    /// </summary>
    [WolverineGet("/api/v1/plataforma/solicitudes-habeas/{id}/plan")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<PlanDeSupresionDto> Plan(Guid id, ISesiones sesiones, RetencionDelTercero retencion, CancellationToken ct)
    {
        var solicitud = await CargarAsync(id, sesiones, ct);
        if (solicitud.Tipo != TipoDeSolicitudHabeas.Supresion)
            throw ReglaDeNegocioException.Conflicto("no-es-supresion", "Solo las solicitudes de supresión tienen un plan de supresión.");
        return (await retencion.PlanearAsync(solicitud.TerceroId, solicitud.Alcance ?? AlcanceDeSupresion.Todo, ct)).ADto(id);
    }

    /// <summary>
    /// La plataforma responde. En una supresión, <c>Atender</c> ejecuta el plan (OD-6): elimina lo que ninguna empresa referencia y
    /// conserva —con su base legal— lo que sí; con el alcance «todo», además destruye la llave de datos del tercero, así lo que
    /// quedó cifrado en eventos se vuelve ilegible (OD-4). 409 <c>solicitud-ya-resuelta</c>.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/solicitudes-habeas/{id}/resolver")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SolicitudHabeasDto> Resolver(Guid id, ResolverSolicitudHabeasDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        IProtectorDeDatosDelTercero protector, IProtectorDeSecretos secretos, RetencionDelTercero retencion, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        var (decision, respuesta) = ServicioDeSolicitudesHabeas.LeerRespuesta(dto);
        var ahora = reloj.GetUtcNow();
        var actor = new ActorDeSolicitud(contexto.ActorTipo.ToString(), contexto.ActorId);
        Guid terceroId;
        bool destruirLlave;

        await using (var sesion = sesiones.Boveda(mensajes))
        {
            var stream = await sesion.Events.FetchForExclusiveWriting<SolicitudHabeas>(id, ct);
            var actual = stream.Aggregate is { EmpresaId: null } propia ? propia : throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud.");
            if (!actual.EstaAbierta)
                throw ReglaDeNegocioException.Conflicto("solicitud-ya-resuelta", "Esa solicitud ya se resolvió.");

            terceroId = actual.TerceroId;
            var datos = await ServicioDeSolicitudesHabeas.LeerDatosAsync(actual, protector, ct);
            ResultadoDeSupresion? ejecucion = null;
            if (actual.Tipo == TipoDeSolicitudHabeas.Supresion && decision == "Atender")
            {
                // El titular recibe la respuesta cifrada antes de que su llave se destruya; la ejecución no puede deshacerse.
                var plan = await retencion.PlanearAsync(actual.TerceroId, actual.Alcance ?? AlcanceDeSupresion.Todo, ct);
                ejecucion = await retencion.EjecutarAsync(plan, id, ct);
            }

            await ServicioDeSolicitudesHabeas.ResolverAsync(stream, mensajes, protector, secretos, decision, respuesta, ServicioDeSolicitudesHabeas.NombreDeLaPlataforma,
                ejecucion, datos?.Correo, actor, null, ahora, ct);
            destruirLlave = ejecucion is not null && actual.Alcance is null or AlcanceDeSupresion.Todo;
            await sesion.SaveChangesAsync(ct);
        }

        await using (var sesion = sesiones.Global())
        {
            Auditoria.Registrar(sesion, contexto, ServicioDeSolicitudesHabeas.Entidad, id.ToString(), AccionConfiguracion.Modificado,
                new { Estado = nameof(EstadoDeSolicitudHabeas.Recibida) }, new { Estado = nameof(EstadoDeSolicitudHabeas.Resuelta), Decision = decision }, ahora);
            await sesion.SaveChangesAsync(ct);
        }

        // La llave se destruye al final y solo si el titular no tiene otra solicitud abierta cuyo texto necesite leerse.
        if (destruirLlave)
        {
            await using var consulta = sesiones.ConsultaBoveda();
            var abiertas = await consulta.Query<SolicitudHabeas>().CountAsync(s => s.TerceroId == terceroId && s.Estado == EstadoDeSolicitudHabeas.Recibida, ct);
            if (abiertas == 0)
                await protector.DestruirAsync(terceroId, ct);
        }

        await using var lectura = sesiones.ConsultaBoveda();
        var proyectada = (await lectura.LoadAsync<SolicitudHabeas>(id, ct))!;
        return await ServicioDeSolicitudesHabeas.ADtoAsync(proyectada, protector, ServicioDeSolicitudesHabeas.NombreDeLaPlataforma, CalendarioDeNegocio.Hoy(reloj), true, ct);
    }

    private static async Task<SolicitudHabeas> CargarAsync(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaBoveda();
        var solicitud = await consulta.LoadAsync<SolicitudHabeas>(id, ct);
        return solicitud is { EmpresaId: null } ? solicitud : throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud.");
    }
}
