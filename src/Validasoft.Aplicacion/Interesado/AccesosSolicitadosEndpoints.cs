using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>
/// Solicitudes de acceso de otros correos al tercero (acceso aislado): una empresa invitó a un correo que no es contacto conocido del tercero y sus contactos,
/// desde su cuenta, lo aprueban o lo rechazan. Solo una sesión con acceso completo (un contacto del tercero) las ve y las decide; una aislada no.
/// </summary>
public static class AccesosSolicitadosEndpoints
{
    /// <summary>Las solicitudes de acceso al tercero de la sesión, las pendientes primero y luego las más recientes.</summary>
    [WolverineGet("/public/v1/accesos-solicitados")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IReadOnlyList<AccesoSolicitadoDto>> Listar(HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        var terceroId = tercero.TerceroId;
        await using var boveda = sesiones.ConsultaBoveda();
        var solicitudes = await boveda.Query<SolicitudDeAcceso>().Where(s => s.TerceroId == terceroId).ToListAsync(ct);
        return
        [
            .. solicitudes.OrderBy(s => s.Estado == EstadoSolicitudDeAcceso.Pendiente ? 0 : 1).ThenByDescending(s => s.ActualizadaEn).Select(DecisionDeAcceso.ADto)
        ];
    }

    /// <summary>
    /// Un contacto aprueba el correo: pasa a ser contacto del tercero (acceso completo en su próxima sesión), las empresas que lo invitaron reciben lo que el
    /// tercero ya compartiría con ellas y lo que ese correo subió para cada una se promueve a la bóveda del tercero. Repetir no cambia nada (idempotente).
    /// 404 <c>solicitud-no-encontrada</c> si no es de este tercero; 403 <c>acceso-propio</c> si la decide el mismo correo.
    /// </summary>
    [WolverinePost("/public/v1/accesos-solicitados/{id}/aprobar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<AccesoSolicitadoDto> Aprobar(Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, ICacheDeSesionesDeTercero cache, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        return DecisionDeAcceso.ADto(await DecisionDeAcceso.AprobarAsync(id, tercero, sesiones, contexto, mensajes, almacen, reglas, cache, reloj.GetUtcNow(), ct));
    }

    /// <summary>
    /// Un contacto rechaza el correo: se cortan sus sesiones al instante, no vuelve a entrar a este tercero y la empresa ve el rechazo en su vínculo (puede invitar
    /// a otro correo). Repetir no cambia nada; un correo ya aprobado no se rechaza (409 <c>solicitud-ya-aprobada</c>).
    /// </summary>
    [WolverinePost("/public/v1/accesos-solicitados/{id}/rechazar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<AccesoSolicitadoDto> Rechazar(Guid id, RechazarAccesoDto? dto, HttpContext http, ISesiones sesiones, ICacheDeSesionesDeTercero cache,
        TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        return DecisionDeAcceso.ADto(await DecisionDeAcceso.RechazarAsync(id, dto?.Motivo, tercero, sesiones, cache, reloj.GetUtcNow(), ct));
    }
}

/// <summary>
/// La decisión de los contactos sobre un correo con acceso aislado y su efecto en cada vínculo que lo invitó. La solicitud (stream de la bóveda) es la
/// fuente de la decisión; cada vínculo la refleja en su propio stream (el historial que ve la empresa) en su tenant. Todos los pasos son idempotentes: si uno
/// falla a medias, repetir la decisión termina el trabajo.
/// </summary>
public static class DecisionDeAcceso
{
    public static AccesoSolicitadoDto ADto(SolicitudDeAcceso s) =>
        new(s.Id, s.Correo, s.Estado.ToString(), s.CreadaEn,
            [.. s.Invitaciones.OrderBy(i => i.Fecha).Select(i => new EmpresaQueInvitoDto(i.Empresa, i.Fecha))], s.DecididoPor, s.DecididoEn, s.MotivoDelRechazo);

    public static async Task<SolicitudDeAcceso> AprobarAsync(Guid solicitudId, SesionDeTercero tercero, ISesiones sesiones, IContextoSolicitud contexto,
        IMessageBus mensajes, IAlmacenDeArchivos almacen, ReglasDeCarga reglas, ICacheDeSesionesDeTercero cache, DateTimeOffset ahora, CancellationToken ct)
    {
        // 1. La decisión y el contacto nuevo, en una sola transacción de la bóveda.
        await using (var sesion = sesiones.Boveda())
        {
            var stream = await sesion.Events.FetchForExclusiveWriting<SolicitudDeAcceso>(solicitudId, ct);
            var solicitud = DeEsteTercero(stream.Aggregate, tercero);
            if (solicitud.Aprobar(tercero.Correo, ahora) is { } aprobada)
                stream.AppendOne(aprobada);

            var delTercero = await sesion.LoadAsync<Tercero>(tercero.TerceroId, ct)
                             ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero no existe.");
            // Contacto del tercero desde ahora: su acceso es completo en la siguiente sesión (la aislada vigente no sube de nivel sola).
            if (delTercero.AgregarContacto(solicitud.Correo, null, "Contacto"))
                sesion.Store(delTercero);
            await sesion.SaveChangesAsync(ct);
        }

        var decidida = await CargarAsync(sesiones, solicitudId, ct);
        cache.OlvidarAcceso(tercero.TerceroId, decidida.Correo);

        // 2. Cada empresa que invitó al correo: el vínculo lo refleja, recibe lo que ya le corresponde y se promueve lo que ese correo subió para ella.
        foreach (var invitacion in decidida.Invitaciones.DistinctBy(i => i.VinculoId))
        {
            var recibe = await Peticiones.ConReintentosAsync(() => AprobarEnElVinculoAsync(invitacion, decidida, tercero, sesiones, mensajes, contexto, ahora, ct));
            if (recibe)
                await PromoverCuarentenaAsync(invitacion.VinculoId, tercero.TerceroId, sesiones, mensajes, contexto, almacen, reglas, ahora, ct);
        }

        return decidida;
    }

    public static async Task<SolicitudDeAcceso> RechazarAsync(Guid solicitudId, string? motivo, SesionDeTercero tercero, ISesiones sesiones,
        ICacheDeSesionesDeTercero cache, DateTimeOffset ahora, CancellationToken ct)
    {
        await using (var sesion = sesiones.Boveda())
        {
            var stream = await sesion.Events.FetchForExclusiveWriting<SolicitudDeAcceso>(solicitudId, ct);
            var solicitud = DeEsteTercero(stream.Aggregate, tercero);
            if (solicitud.Rechazar(tercero.Correo, motivo, ahora) is { } rechazada)
                stream.AppendOne(rechazada);
            await sesion.SaveChangesAsync(ct);
        }

        var decidida = await CargarAsync(sesiones, solicitudId, ct);
        // Sus tokens (aislados) dejan de valer ya, y con ellos las sesiones de refresco: la persona no vuelve a entrar con ese correo.
        cache.OlvidarAcceso(tercero.TerceroId, decidida.Correo);
        await RevocarSesionesDelCorreoAsync(sesiones, tercero.TerceroId, decidida.Correo, cache, ahora, ct);

        foreach (var invitacion in decidida.Invitaciones.DistinctBy(i => i.VinculoId))
            await Peticiones.ConReintentosAsync(() => RechazarEnElVinculoAsync(invitacion, decidida, sesiones, ahora, ct));
        return decidida;
    }

    private static SolicitudDeAcceso DeEsteTercero(SolicitudDeAcceso? solicitud, SesionDeTercero tercero) =>
        solicitud is not null && solicitud.TerceroId == tercero.TerceroId
            ? solicitud
            : throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud de acceso.");

    private static async Task<SolicitudDeAcceso> CargarAsync(ISesiones sesiones, Guid solicitudId, CancellationToken ct)
    {
        await using var boveda = sesiones.ConsultaBoveda();
        return await boveda.LoadAsync<SolicitudDeAcceso>(solicitudId, ct)
               ?? throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud de acceso.");
    }

    /// <summary>Revoca todas las sesiones de refresco abiertas de ese correo en el tercero y corta sus tokens (<see cref="ICacheDeSesionesDeTercero"/>).</summary>
    private static async Task RevocarSesionesDelCorreoAsync(ISesiones sesiones, Guid terceroId, string correo, ICacheDeSesionesDeTercero cache, DateTimeOffset ahora,
        CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            await using var sesion = sesiones.Global();
            var abiertas = await sesion.Query<SesionDeRefresco>().Where(s => s.TerceroId == terceroId && s.Correo == correo && s.RevocadaEn == null).ToListAsync(ct);
            foreach (var abierta in abiertas)
            {
                abierta.Revocar(SesionDeRefresco.MotivoAccesoRechazado, ahora);
                sesion.Store(abierta);
            }

            try
            {
                await sesion.SaveChangesAsync(ct);
            }
            catch (Exception e) when (intento < 5 && Peticiones.EsCarrera(e))
            {
                // Una renovación simultánea rotó el token: se revoca sobre la versión actual.
                continue;
            }

            foreach (var abierta in abiertas)
                cache.OlvidarSesion(abierta.Id);
            return;
        }
    }

    /// <summary>
    /// El vínculo refleja la aprobación y, si ya aceptó la invitación, la empresa recibe de la bóveda lo que su flujo pide (lo que no recibió mientras el acceso
    /// estaba pendiente) y su expediente se pone al día. Devuelve si el vínculo ya puede recibir la bóveda (para promover lo que ese correo subió).
    /// </summary>
    private static async Task<bool> AprobarEnElVinculoAsync(InvitacionDeAcceso invitacion, SolicitudDeAcceso solicitud, SesionDeTercero tercero, ISesiones sesiones,
        IMessageBus mensajes, IContextoSolicitud contexto, DateTimeOffset ahora, CancellationToken ct)
    {
        DocumentosDelVinculo? documentos = null;
        bool recibe;
        await using (var sesion = sesiones.ParaTenant(invitacion.TenantId))
        {
            var stream = await sesion.Events.FetchForWriting<Vinculo>(invitacion.VinculoId, ct);
            var vinculo = stream.Aggregate;
            // El vínculo ya no es de este correo (la empresa invitó a otro) o no existe: nada que reflejar.
            if (vinculo is null || vinculo.TerceroId != solicitud.TerceroId || !string.Equals(vinculo.CorreoTercero, solicitud.Correo, StringComparison.OrdinalIgnoreCase))
                return false;

            if (vinculo.AprobarAccesoAislado(solicitud.Id, ahora) is { } aprobado)
            {
                stream.AppendOne(aprobado);
                vinculo.Apply(aprobado);
            }

            recibe = vinculo.InvitacionAceptada && vinculo.BovedaDisponible;
            if (recibe && await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct) is { } flujo)
            {
                var pedidos = await RequisitosDeExpediente.DelFlujoAsync(sesion, flujo.Version, await RequisitosDeExpediente.ContextoAsync(sesion, vinculo, ct), ct);
                documentos = await ComparticionDeDocumentos.CompartirConVinculoAsync(sesion, vinculo, invitacion.TenantId, RequisitosDeExpediente.CodigosGlobales(pedidos),
                    OrigenComparticion.Automatica, ahora, ct);
            }

            await sesion.SaveChangesAsync(ct);
        }

        if (recibe)
        {
            if (documentos is { Compartidos.Count: > 0 })
            {
                await mensajes.PublishAsync(new CalcularVigenciasDelVinculo(solicitud.TerceroId, invitacion.VinculoId));
                await mensajes.PublishAsync(new Ia.AnalizarIaDelVinculo(solicitud.TerceroId, invitacion.VinculoId));
            }

            await mensajes.PublishAsync(new SincronizarRequisitosDelVinculo(invitacion.TenantId, invitacion.VinculoId, contexto.ActorTipo.ToString(), contexto.ActorId));
        }

        return recibe;
    }

    private static async Task<bool> RechazarEnElVinculoAsync(InvitacionDeAcceso invitacion, SolicitudDeAcceso solicitud, ISesiones sesiones, DateTimeOffset ahora,
        CancellationToken ct)
    {
        await using var sesion = sesiones.ParaTenant(invitacion.TenantId);
        var stream = await sesion.Events.FetchForWriting<Vinculo>(invitacion.VinculoId, ct);
        if (stream.Aggregate is not { } vinculo || vinculo.TerceroId != solicitud.TerceroId ||
            !string.Equals(vinculo.CorreoTercero, solicitud.Correo, StringComparison.OrdinalIgnoreCase))
            return false;

        if (vinculo.RechazarAccesoAislado(solicitud.Id, ahora) is { } rechazado)
            stream.AppendOne(rechazado);
        await sesion.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Lo que el correo subió para esta empresa mientras su acceso estaba pendiente (cuarentena) sigue el camino normal: se promueve a la bóveda global del
    /// tercero y, como ya es la versión actual, se comparte con quien la pida. Una versión de la bóveda más reciente manda (no se pisa), y un contenido que la
    /// bóveda ya tiene no se duplica. Cada documento va en su propia transacción: un fallo no deshace los demás y repetir la aprobación lo termina.
    /// </summary>
    private static async Task PromoverCuarentenaAsync(Guid vinculoId, Guid terceroId, ISesiones sesiones, IMessageBus mensajes, IContextoSolicitud contexto,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, DateTimeOffset ahora, CancellationToken ct)
    {
        IReadOnlyList<Guid> pendientes;
        await using (var consulta = sesiones.ConsultaBoveda())
        {
            pendientes =
            [
                .. (await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId && d.VinculoId == vinculoId && d.EnCuarentena && d.CuarentenaResueltaEn == null)
                    .ToListAsync(ct)).Select(d => d.Id)
            ];
        }

        foreach (var documentoId in pendientes)
            await PromoverUnoAsync(documentoId, terceroId, sesiones, mensajes, contexto, almacen, reglas, ahora, ct);
    }

    private static async Task PromoverUnoAsync(Guid documentoId, Guid terceroId, ISesiones sesiones, IMessageBus mensajes, IContextoSolicitud contexto,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, DateTimeOffset ahora, CancellationToken ct)
    {
        // El documento global del canónico existe antes de escribirle (como en los formularios): se crea en su propia transacción si falta.
        Documento origen;
        await using (var consulta = sesiones.ConsultaBoveda())
            origen = await consulta.LoadAsync<Documento>(documentoId, ct) ?? throw new InvalidOperationException("El documento en cuarentena no existe.");
        if (origen.CuarentenaResueltaEn is not null)
            return;

        var version = origen.VersionActual is { } actual ? origen.VersionRequerida(actual) : null;
        Guid? globalId = null;
        if (version is { TieneArchivo: true } && origen.TipoCanonico is { } codigo)
        {
            var canonico = CatalogoCanonico.Requerido(codigo);
            var clave = Documento.ClaveGlobal(terceroId, codigo);
            await using var creacion = sesiones.Boveda();
            var existente = await creacion.Query<Documento>().FirstOrDefaultAsync(d => d.ClaveNatural == clave, ct);
            if (existente is null)
            {
                var creado = Documento.CrearGlobal(terceroId, canonico, ahora);
                creacion.Events.StartStream<Documento>(creado.DocumentoId, creado);
                await creacion.SaveChangesAsync(ct);
                globalId = creado.DocumentoId;
            }
            else
            {
                globalId = existente.Id;
            }
        }

        await using var sesion = sesiones.Boveda(mensajes);
        var deOrigen = await sesion.Events.FetchForWriting<Documento>(documentoId, ct);
        var cuarentena = deOrigen.Aggregate!;
        if (cuarentena.CuarentenaResueltaEn is not null)
            return;

        if (globalId is not { } destinoId || version is null)
        {
            deOrigen.AppendOne(cuarentena.ResolverCuarentena(false, null, null, "No había una versión con archivo que promover a la bóveda.", ahora)!);
            await sesion.SaveChangesAsync(ct);
            return;
        }

        var deDestino = await sesion.Events.FetchForExclusiveWriting<Documento>(destinoId, ct);
        var global = deDestino.Aggregate!;
        if (global.BuscarPorHash(version.Sha256) is { } igual)
        {
            deOrigen.AppendOne(cuarentena.ResolverCuarentena(true, global.Id, igual.Numero, "La bóveda ya tenía este mismo archivo.", ahora)!);
            await sesion.SaveChangesAsync(ct);
            return;
        }

        if (global.VersionActual is { } vigente && global.VersionRequerida(vigente).CargadaEn > version.CargadaEn)
        {
            deOrigen.AppendOne(cuarentena.ResolverCuarentena(false, global.Id, null, "La bóveda ya tenía una versión más reciente.", ahora)!);
            await sesion.SaveChangesAsync(ct);
            return;
        }

        var numero = global.SiguienteNumero;
        var claveDefinitiva = ClavesDeAlmacen.Global(terceroId, version.Sha256);
        var mime = version.Hechos?.MimeReal ?? version.MimeDeclarado;
        await almacen.AsegurarBucketAsync(ClavesDeAlmacen.BucketBoveda, ct);
        await almacen.CopiarAsync(version.Bucket, version.ClaveObjeto, ClavesDeAlmacen.BucketBoveda, claveDefinitiva, mime, new Dictionary<string, string>
        {
            ["nombre-original"] = Uri.EscapeDataString(version.NombreOriginal),
            ["documento-id"] = global.Id.ToString(),
            ["version-n"] = numero.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["mime"] = mime
        }, ct);

        var archivo = new ArchivoVerificado(numero, version.Sha256, ClavesDeAlmacen.BucketBoveda, claveDefinitiva, version.MimeDeclarado, mime, version.Tamano,
            version.NombreOriginal, version.Hechos?.Paginas, cuarentena.VinculoId, version.CargadaPor);
        var eventos = global.RegistrarVersionArchivo(archivo, null, Antivirus.NoDisponible, reglas.ExigirAntivirus, ahora);
        foreach (var evento in eventos)
            deDestino.AppendOne(evento);

        var proyectado = global.Proyectar(eventos);
        if (proyectado.VersionActual == numero)
            await ComparticionDeDocumentos.CompartirActualAsync(sesion, deDestino, proyectado, OrigenComparticion.Automatica, ahora, ct, mensajes, contexto);

        deOrigen.AppendOne(cuarentena.ResolverCuarentena(true, global.Id, numero, "El acceso se aprobó y la versión pasó a la bóveda del tercero.", ahora)!);
        await sesion.SaveChangesAsync(ct);
    }
}
