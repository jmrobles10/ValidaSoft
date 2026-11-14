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
/// Solicitudes de acceso de otros correos al tercero (acceso aislado): una empresa invitó a un correo que no es contacto del tercero (o un contacto sin verificar lo
/// desplazó uno verificado) y un contacto <b>verificado</b>, desde su cuenta, lo aprueba o lo rechaza. Solo una sesión con acceso completo las ve, y solo la de un contacto
/// verificado las decide; si el tercero no tiene ninguno, las decide la plataforma desde su consola (<c>/api/v1/plataforma/solicitudes-de-acceso</c>).
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
        var delTercero = await boveda.LoadAsync<Tercero>(terceroId, ct);
        var solicitudes = await boveda.Query<SolicitudDeAcceso>().Where(s => s.TerceroId == terceroId).ToListAsync(ct);
        return
        [
            .. solicitudes.OrderBy(s => s.Estado == EstadoSolicitudDeAcceso.Pendiente ? 0 : 1).ThenByDescending(s => s.ActualizadaEn)
                .Select(s => DecisionDeAcceso.ADto(s, delTercero, tercero.Correo))
        ];
    }

    /// <summary>
    /// Los contactos del tercero con su estado de verificación y el historial de cómo llegaron: quién puede decidir los accesos de otros correos (los verificados) y
    /// cuál es el correo de esta sesión.
    /// </summary>
    [WolverineGet("/public/v1/contactos-del-tercero")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<ContactosDelTerceroDto> Contactos(HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        await using var boveda = sesiones.ConsultaBoveda();
        var delTercero = await boveda.LoadAsync<Tercero>(tercero.TerceroId, ct)
                         ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero no existe.");
        var historial = await boveda.LoadAsync<HistorialDeContactos>(HistorialDeContactos.IdPara(tercero.TerceroId), ct);
        return DecisionDeAcceso.ContactosADto(delTercero, historial, tercero.Correo);
    }

    /// <summary>
    /// Un contacto verificado aprueba el correo: pasa a ser contacto verificado del tercero (acceso completo en su próxima sesión), las empresas que lo invitaron reciben
    /// lo que el tercero ya compartiría con ellas y lo que ese correo subió para cada una se promueve a la bóveda del tercero. Repetir no cambia nada (idempotente).
    /// 404 <c>solicitud-no-encontrada</c> si no es de este tercero; 403 <c>acceso-propio</c> si la decide el mismo correo y <c>contacto-sin-verificar</c> si quien decide no
    /// está verificado.
    /// </summary>
    [WolverinePost("/public/v1/accesos-solicitados/{id}/aprobar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<AccesoSolicitadoDto> Aprobar(Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, ICacheDeSesionesDeTercero cache, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        var decidida = await DecisionDeAcceso.AprobarAsync(id, tercero.TerceroId, QuienDecide.UnContacto(tercero.Correo), sesiones, contexto, mensajes, almacen, reglas,
            cache, reloj.GetUtcNow(), ct);
        return await DecisionDeAcceso.ADtoAsync(decidida, sesiones, tercero.Correo, ct);
    }

    /// <summary>
    /// Un contacto verificado rechaza el correo: se cortan sus sesiones al instante, no vuelve a entrar a este tercero y la empresa ve el rechazo en su vínculo (puede invitar
    /// a otro correo). Repetir no cambia nada; un correo ya aprobado no se rechaza (409 <c>solicitud-ya-aprobada</c>); 403 <c>contacto-sin-verificar</c>.
    /// </summary>
    [WolverinePost("/public/v1/accesos-solicitados/{id}/rechazar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<AccesoSolicitadoDto> Rechazar(Guid id, RechazarAccesoDto dto, HttpContext http, ISesiones sesiones, ICacheDeSesionesDeTercero cache,
        TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        var decidida = await DecisionDeAcceso.RechazarAsync(id, dto.Motivo, tercero.TerceroId, QuienDecide.UnContacto(tercero.Correo), sesiones, cache, reloj.GetUtcNow(), ct);
        return await DecisionDeAcceso.ADtoAsync(decidida, sesiones, tercero.Correo, ct);
    }
}

/// <summary>Quién decide una solicitud de acceso: un contacto verificado, un operador de la plataforma o la verificación del propio correo.</summary>
public sealed record QuienDecide(ViaDeDecision Via, string Identificador, MedioDeVerificacion? Medio = null)
{
    public static QuienDecide UnContacto(string correo) => new(ViaDeDecision.Contacto, correo);

    public static QuienDecide UnOperador(string operador) => new(ViaDeDecision.Plataforma, $"plataforma:{operador}");

    public static QuienDecide LaVerificacion(MedioDeVerificacion medio) => new(ViaDeDecision.Verificacion, "verificacion", medio);
}

/// <summary>
/// La decisión sobre un correo con acceso aislado y su efecto en cada vínculo que lo invitó. La solicitud (stream de la bóveda) es la fuente de la decisión; cada vínculo
/// la refleja en su propio stream (el historial que ve la empresa) en su tenant. Decide un contacto <b>verificado</b>, la plataforma (si el tercero no tiene ninguno) o la
/// verificación del propio correo. Todos los pasos son idempotentes: si uno falla a medias, repetir la decisión termina el trabajo.
/// </summary>
public static class DecisionDeAcceso
{
    public static AccesoSolicitadoDto ADto(SolicitudDeAcceso s, Tercero? tercero, string? correoDeLaSesion) =>
        new(s.Id, s.Correo, s.Estado.ToString(), s.CreadaEn,
            [.. s.Invitaciones.OrderBy(i => i.Fecha).Select(i => new EmpresaQueInvitoDto(i.Empresa, i.Fecha))], s.DecididoPor, s.DecididoEn, s.MotivoDelRechazo,
            PuedesDecidir: tercero is not null && correoDeLaSesion is not null && s.Correo != SolicitudDeAcceso.Normalizar(correoDeLaSesion) &&
                           AccesoDelTercero.PuedeDecidir(tercero, correoDeLaSesion),
            DecideLaPlataforma: tercero is not null && AccesoDelTercero.DecideLaPlataforma(tercero), DecididoVia: s.DecididoVia?.ToString(),
            ContactoDesplazado: s.Origen == SolicitudDeAcceso.OrigenContactoDesplazado);

    public static async Task<AccesoSolicitadoDto> ADtoAsync(SolicitudDeAcceso s, ISesiones sesiones, string? correoDeLaSesion, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaBoveda();
        return ADto(s, await consulta.LoadAsync<Tercero>(s.TerceroId, ct), correoDeLaSesion);
    }

    public static ContactosDelTerceroDto ContactosADto(Tercero tercero, HistorialDeContactos? historial, string correoDeLaSesion) =>
        new([.. tercero.Contactos.Select(c => new ContactoDelTerceroDto(c.Nombre, c.Correo, c.Rol, c.Verificacion.ToString(), c.Medio?.ToString(), c.VerificadoEn,
                string.Equals(c.Correo, correoDeLaSesion, StringComparison.OrdinalIgnoreCase), AccesoDelTercero.EstaDesplazado(tercero, c.Correo)))],
            [.. (historial?.Entradas ?? []).OrderByDescending(e => e.Fecha).Select(e => new EntradaDeHistorialDeContactoDto(e.Fecha, e.Tipo, e.Correo, e.Detalle))],
            AccesoDelTercero.PuedeDecidir(tercero, correoDeLaSesion), AccesoDelTercero.DecideLaPlataforma(tercero));

    public static async Task<SolicitudDeAcceso> AprobarAsync(Guid solicitudId, Guid terceroId, QuienDecide quien, ISesiones sesiones, IContextoSolicitud contexto,
        IMessageBus mensajes, IAlmacenDeArchivos almacen, ReglasDeCarga reglas, ICacheDeSesionesDeTercero cache, DateTimeOffset ahora, CancellationToken ct)
    {
        // 1. La decisión y el contacto verificado, en una sola transacción de la bóveda.
        MedioDeVerificacion medio;
        await using (var sesion = sesiones.Boveda())
        {
            var delTercero = await sesion.LoadAsync<Tercero>(terceroId, ct)
                             ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero no existe.");
            ExigirQuienPuedeDecidir(delTercero, quien);
            var stream = await sesion.Events.FetchForExclusiveWriting<SolicitudDeAcceso>(solicitudId, ct);
            var solicitud = DeEsteTercero(stream.Aggregate, terceroId);
            var aprobada = solicitud.Aprobar(quien.Identificador, ahora, quien.Via, quien.Medio);
            medio = aprobada?.Medio ?? solicitud.MedioAlAprobar ?? MedioDeVerificacion.AprobadoPorContacto;
            if (aprobada is not null)
                stream.AppendOne(aprobada);

            // Contacto verificado del tercero desde ahora: su acceso es completo en la siguiente sesión (la aislada vigente no sube de nivel sola). Si ya era un contacto
            // (sin verificar, desplazado por uno verificado) se verifica; si no, se agrega.
            var por = quien.Via == ViaDeDecision.Verificacion ? null : quien.Identificador;
            var origen = quien.Via == ViaDeDecision.Plataforma ? ContactosDelTercero.OrigenPlataforma : ContactosDelTercero.OrigenAprobacion;
            if (!ContactosDelTercero.Agregar(sesion, delTercero, solicitud.Correo, null, "Contacto", origen, por, medio, por, ahora))
                ContactosDelTercero.Verificar(sesion, delTercero, solicitud.Correo, medio, por, ahora);
            await sesion.SaveChangesAsync(ct);
        }

        var decidida = await CargarAsync(sesiones, solicitudId, ct);
        cache.OlvidarAcceso(terceroId, decidida.Correo);
        // Un contacto verificado nuevo (lo aprobó un contacto o la plataforma) desplaza a los que siguen sin verificar: lo resuelve la sincronización del acceso.
        if (quien.Via != ViaDeDecision.Verificacion)
            await mensajes.PublishAsync(new SincronizarAccesoDelTercero(terceroId));

        // 2. Cada empresa que invitó al correo: el vínculo lo refleja, recibe lo que ya le corresponde y se promueve lo que ese correo subió para ella.
        foreach (var invitacion in decidida.Invitaciones.DistinctBy(i => i.VinculoId))
        {
            var recibe = await Peticiones.ConReintentosAsync(() => AprobarEnElVinculoAsync(invitacion, decidida, terceroId, sesiones, mensajes, contexto, ahora, ct));
            if (recibe)
                await PromoverCuarentenaAsync(invitacion.VinculoId, terceroId, sesiones, mensajes, contexto, almacen, reglas, ahora, ct);
        }

        // 3. Las versiones globales que había subido ese correo y quedaron en revisión (si lo desplazó otro contacto) se confirman con la aprobación.
        await SincronizacionDeAccesoDelTercero.ResolverRevisionDeVersionesAsync(sesiones, terceroId, decidida.Correo, decidida.DecididoPor ?? quien.Identificador, ahora, ct);
        return decidida;
    }

    public static async Task<SolicitudDeAcceso> RechazarAsync(Guid solicitudId, string? motivo, Guid terceroId, QuienDecide quien, ISesiones sesiones,
        ICacheDeSesionesDeTercero cache, DateTimeOffset ahora, CancellationToken ct)
    {
        await using (var sesion = sesiones.Boveda())
        {
            var delTercero = await sesion.LoadAsync<Tercero>(terceroId, ct)
                             ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero no existe.");
            ExigirQuienPuedeDecidir(delTercero, quien);
            var stream = await sesion.Events.FetchForExclusiveWriting<SolicitudDeAcceso>(solicitudId, ct);
            var solicitud = DeEsteTercero(stream.Aggregate, terceroId);
            if (solicitud.Rechazar(quien.Identificador, motivo, ahora, quien.Via) is { } rechazada)
                stream.AppendOne(rechazada);
            await sesion.SaveChangesAsync(ct);
        }

        var decidida = await CargarAsync(sesiones, solicitudId, ct);
        // Sus tokens (aislados) dejan de valer ya, y con ellos las sesiones de refresco: la persona no vuelve a entrar con ese correo.
        cache.OlvidarAcceso(terceroId, decidida.Correo);
        await RevocarSesionesDelCorreoAsync(sesiones, terceroId, decidida.Correo, cache, ahora, ct);

        foreach (var invitacion in decidida.Invitaciones.DistinctBy(i => i.VinculoId))
            await Peticiones.ConReintentosAsync(() => RechazarEnElVinculoAsync(invitacion, decidida, sesiones, ahora, ct));
        return decidida;
    }

    /// <summary>
    /// Quién puede decidir: con <see cref="ViaDeDecision.Contacto"/>, un contacto <b>verificado</b> (403 <c>contacto-sin-verificar</c>); con
    /// <see cref="ViaDeDecision.Plataforma"/>, solo si el tercero no tiene contactos verificados (409 <c>decide-un-contacto</c>); la verificación no necesita permiso.
    /// </summary>
    private static void ExigirQuienPuedeDecidir(Tercero tercero, QuienDecide quien)
    {
        switch (quien.Via)
        {
            case ViaDeDecision.Contacto when !AccesoDelTercero.PuedeDecidir(tercero, quien.Identificador):
                throw ReglaDeNegocioException.Prohibido("contacto-sin-verificar",
                    "Solo un contacto verificado puede aprobar o rechazar los accesos: tu correo aún no está verificado. Si el tercero no tiene contactos verificados, decide la plataforma.");
            case ViaDeDecision.Plataforma when !AccesoDelTercero.DecideLaPlataforma(tercero):
                throw ReglaDeNegocioException.Conflicto("decide-un-contacto",
                    "Este tercero ya tiene contactos verificados: ellos deciden los accesos. Verifica un contacto desde la consola si hace falta.");
        }
    }

    private static SolicitudDeAcceso DeEsteTercero(SolicitudDeAcceso? solicitud, Guid terceroId) =>
        solicitud is not null && solicitud.TerceroId == terceroId
            ? solicitud
            : throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud de acceso.");

    private static async Task<SolicitudDeAcceso> CargarAsync(ISesiones sesiones, Guid solicitudId, CancellationToken ct)
    {
        await using var boveda = sesiones.ConsultaBoveda();
        return await boveda.LoadAsync<SolicitudDeAcceso>(solicitudId, ct)
               ?? throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud de acceso.");
    }

    /// <summary>Revoca todas las sesiones de refresco abiertas de ese correo en el tercero y corta sus tokens (<see cref="ICacheDeSesionesDeTercero"/>).</summary>
    internal static async Task RevocarSesionesDelCorreoAsync(ISesiones sesiones, Guid terceroId, string correo, ICacheDeSesionesDeTercero cache, DateTimeOffset ahora,
        CancellationToken ct, string motivo = SesionDeRefresco.MotivoAccesoRechazado)
    {
        for (var intento = 1; ; intento++)
        {
            await using var sesion = sesiones.Global();
            var abiertas = await sesion.Query<SesionDeRefresco>().Where(s => s.TerceroId == terceroId && s.Correo == correo && s.RevocadaEn == null).ToListAsync(ct);
            foreach (var abierta in abiertas)
            {
                abierta.Revocar(motivo, ahora);
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
    private static async Task<bool> AprobarEnElVinculoAsync(InvitacionDeAcceso invitacion, SolicitudDeAcceso solicitud, Guid terceroId, ISesiones sesiones,
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
            version.NombreOriginal, version.Hechos?.Paginas, cuarentena.VinculoId, version.CargadaPor, version.CargadaPorCorreo);
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
