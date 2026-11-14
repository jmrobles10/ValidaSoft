using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>
/// Pide poner al día quién tiene acceso a un tercero y quién lo decide (decisión AV del registro): se publica cuando un contacto se verifica, cuando llega el análisis de IA de
/// su RUT o de su Cámara de comercio y desde la migración. Es <b>idempotente</b>: recalcula desde lo que hay.
/// </summary>
public sealed record SincronizarAccesoDelTercero(Guid TerceroId);

public static class SincronizarAccesoDelTerceroHandler
{
    public static Task Handle(SincronizarAccesoDelTercero mensaje, ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector, ICacheDeSesionesDeTercero cache,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct) =>
        SincronizacionDeAccesoDelTercero.EjecutarAsync(mensaje.TerceroId, sesiones, runtime, protector, cache, almacen, reglas, contexto, reloj.GetUtcNow(), ct);
}

/// <summary>Lo que cambió una sincronización: contactos que se verificaron, solicitudes que se aprobaron solas y contactos que pasaron a acceso aislado.</summary>
public sealed record ResultadoDeSincronizacion(int Verificados, int Aprobadas, int Desplazados)
{
    public static readonly ResultadoDeSincronizacion Vacio = new(0, 0, 0);
}

/// <summary>
/// Pone al día el acceso de un tercero en tres pasos, cada uno idempotente:
/// <list type="number">
/// <item><b>Verifica</b> los contactos que ya tienen con qué: su correo es el del RUT o el de la Cámara de comercio (versión actual del documento, analizada con IA) <b>y esa
/// versión la respalda alguien más que el candidato</b> (la subió un contacto verificado o la aceptó una empresa de otro grupo; ver <see cref="CorreoOficial.Respalda"/>), o dos
/// grupos distintos lo invitaron por su cuenta y el tercero aún no tenía un contacto verificado.</item>
/// <item><b>Aprueba solas</b> las solicitudes pendientes cuyo correo se verificó así (la persona ya es del tercero: no hace falta que nadie la apruebe).</item>
/// <item><b>Desplaza</b> a los contactos sin verificar mientras el tercero tenga uno verificado: el verificado prevalece. El desplazado pasa a acceso aislado (su solicitud
/// espera a un contacto verificado), se cortan sus sesiones, sus versiones globales quedan marcadas para revisión y se avisa a los contactos verificados y a las empresas.</item>
/// </list>
/// </summary>
public static class SincronizacionDeAccesoDelTercero
{
    /// <summary>Motivo de la revisión que ve quien abre una versión marcada.</summary>
    public const string MotivoDeRevision = "La subió un contacto sin verificar, desplazado por otro contacto verificado del tercero.";

    public static async Task<ResultadoDeSincronizacion> EjecutarAsync(Guid terceroId, ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector,
        ICacheDeSesionesDeTercero cache, IAlmacenDeArchivos almacen, ReglasDeCarga reglas, IContextoSolicitud contexto, DateTimeOffset ahora, CancellationToken ct)
    {
        var verificados = await VerificarContactosAsync(terceroId, sesiones, ahora, ct);
        var aprobadas = await AprobarSolicitudesVerificadasAsync(terceroId, sesiones, runtime, almacen, reglas, cache, contexto, ahora, ct);
        var desplazados = await DesplazarContactosAsync(terceroId, sesiones, runtime, protector, cache, ahora, ct);
        return new ResultadoDeSincronizacion(verificados, aprobadas, desplazados);
    }

    // ---------- 1. verificar ----------

    private static async Task<int> VerificarContactosAsync(Guid terceroId, ISesiones sesiones, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.Boveda();
        var tercero = await sesion.LoadAsync<Tercero>(terceroId, ct);
        if (tercero is null || tercero.SandboxTenantId is not null)
            return 0;

        var sinVerificar = tercero.Contactos.Where(c => !c.EstaVerificado).ToList();
        if (sinVerificar.Count == 0)
            return 0;

        var oficiales = await ContactosDelTercero.CorreosOficialesAsync(sesion, terceroId, ct);
        // La corroboración vale solo si, al empezar, nadie estaba verificado (con un verificado presente manda él).
        var nadieVerificado = !tercero.TieneContactoVerificado;
        var cambiaron = 0;
        foreach (var contacto in sinVerificar)
        {
            // El correo de un RUT o una Cámara solo verifica si alguien más lo respalda (lo subió un contacto verificado o lo aceptó otro grupo, no el que invitó a este correo).
            var grupos = await ContactosDelTercero.GruposQueInvitaronAsync(sesion, terceroId, contacto.Correo, null, ct);
            var medio = VerificacionDeContactos.PorDocumento(contacto.Correo, oficiales, tercero.EsContactoVerificado, grupos);
            if (medio is null && nadieVerificado && VerificacionDeContactos.Corroborado(grupos))
                medio = MedioDeVerificacion.Corroborado;
            if (medio is { } verificado && ContactosDelTercero.Verificar(sesion, tercero, contacto.Correo, verificado, null, ahora))
                cambiaron++;
        }

        if (cambiaron > 0)
            await sesion.SaveChangesAsync(ct);
        return cambiaron;
    }

    // ---------- 2. aprobar solas las solicitudes ya verificadas ----------

    private static async Task<int> AprobarSolicitudesVerificadasAsync(Guid terceroId, ISesiones sesiones, IWolverineRuntime runtime, IAlmacenDeArchivos almacen, ReglasDeCarga reglas,
        ICacheDeSesionesDeTercero cache, IContextoSolicitud contexto, DateTimeOffset ahora, CancellationToken ct)
    {
        List<(Guid Id, MedioDeVerificacion Medio)> porAprobar = [];
        await using (var consulta = sesiones.ConsultaBoveda())
        {
            var tercero = await consulta.LoadAsync<Tercero>(terceroId, ct);
            if (tercero is null || tercero.SandboxTenantId is not null)
                return 0;

            var pendientes = (await consulta.Query<SolicitudDeAcceso>().Where(s => s.TerceroId == terceroId).ToListAsync(ct)).Where(s => s.EstaPendiente).ToList();
            if (pendientes.Count == 0)
                return 0;

            var oficiales = await ContactosDelTercero.CorreosOficialesAsync(consulta, terceroId, ct);
            foreach (var solicitud in pendientes)
            {
                // El correo ya es un contacto verificado, o coincide con el RUT o la Cámara, o dos grupos lo invitaron por su cuenta y nadie está verificado.
                var medio = tercero.BuscarContacto(solicitud.Correo) is { EstaVerificado: true } contacto
                    ? contacto.Medio ?? MedioDeVerificacion.AprobadoPorContacto
                    : VerificacionDeContactos.Medio(solicitud.Correo, oficiales, await ContactosDelTercero.GruposQueInvitaronAsync(consulta, terceroId, solicitud.Correo, null, ct),
                        tercero.EsContactoVerificado, tercero.TieneContactoVerificado);
                if (medio is { } aprobable)
                    porAprobar.Add((solicitud.Id, aprobable));
            }
        }

        foreach (var (id, medio) in porAprobar)
            await DecisionDeAcceso.AprobarAsync(id, terceroId, QuienDecide.LaVerificacion(medio), sesiones, contexto, new MessageContext(runtime), almacen, reglas, cache, ahora, ct);
        return porAprobar.Count;
    }

    // ---------- 3. desplazar a los sin verificar ----------

    private static async Task<int> DesplazarContactosAsync(Guid terceroId, ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector,
        ICacheDeSesionesDeTercero cache, DateTimeOffset ahora, CancellationToken ct)
    {
        Tercero tercero;
        await using (var consulta = sesiones.ConsultaBoveda())
            tercero = await consulta.LoadAsync<Tercero>(terceroId, ct) ?? throw new InvalidOperationException("El tercero no existe.");

        var desplazados = AccesoDelTercero.Desplazados(tercero);
        var nuevos = 0;
        foreach (var contacto in desplazados)
            if (await DesplazarAsync(tercero, contacto.Correo, sesiones, runtime, protector, cache, ahora, ct))
                nuevos++;
        return nuevos;
    }

    /// <summary>Desplaza a un contacto; devuelve si era la primera vez (la solicitud no existía): solo entonces se avisa y se deja el rastro.</summary>
    private static async Task<bool> DesplazarAsync(Tercero tercero, string correo, ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector,
        ICacheDeSesionesDeTercero cache, DateTimeOffset ahora, CancellationToken ct)
    {
        var terceroId = tercero.Id;
        var solicitudId = SolicitudDeAcceso.IdPara(terceroId, correo);

        // Los vínculos (de cualquier grupo) que invitaron a ese correo, con la empresa y su tenant.
        List<InvitacionDeAcceso> invitaciones;
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            // Una invitación retenida a la espera de aprobación (decisión AI) no se refleja ni se avisa: al aprobarla se decide el acceso del correo con lo que valga entonces.
            var vinculos = (await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.TerceroId == terceroId).ToListAsync(ct))
                .Where(v => !v.InvitacionPorAprobar && string.Equals(SolicitudDeAcceso.Normalizar(v.CorreoTercero), correo, StringComparison.Ordinal))
                .ToList();
            var empresas = vinculos.Count == 0
                ? []
                : (await consulta.LoadManyAsync<Empresa>(ct, vinculos.Select(v => v.EmpresaId).Distinct().ToArray())).ToDictionary(e => e.Id);
            invitaciones =
            [
                .. vinculos.Where(v => empresas.ContainsKey(v.EmpresaId))
                    .Select(v => new InvitacionDeAcceso(empresas[v.EmpresaId].TenantId, v.EmpresaId, v.Id, empresas[v.EmpresaId].Nombre, ahora))
            ];
        }

        // La solicitud, el historial y el aviso a los contactos verificados: una sola transacción de la bóveda. Si otro nodo (u otra sincronización) la creó al mismo tiempo,
        // el reintento ve la solicitud existente y solo le suma lo que falte.
        var (primeraVez, estado) = await Peticiones.ConReintentosAsync(async () =>
        {
            // Cada intento con su propio contexto de mensajería: los correos salen por el outbox de esta transacción.
            var mensajes = new MessageContext(runtime);
            await using var sesion = sesiones.Boveda(mensajes);
            var existente = await sesion.LoadAsync<SolicitudDeAcceso>(solicitudId, ct);
            if (existente is null)
            {
                var creada = SolicitudDeAcceso.Crear(terceroId, correo, ahora, SolicitudDeAcceso.OrigenContactoDesplazado);
                List<object> eventos = [creada, .. invitaciones.Select(i => new AccesoSolicitadoPorEmpresa(solicitudId, i.TenantId, i.EmpresaId, i.VinculoId, i.Empresa, ahora))];
                sesion.Events.StartStream<SolicitudDeAcceso>(solicitudId, eventos);
                ContactosDelTercero.Historial(sesion, terceroId, new ContactoPasoAAislado(terceroId, correo, "Otro contacto del tercero se verificó y el verificado prevalece.", ahora));
                foreach (var decisor in AccesoDelTercero.CorreosQueDecidenLosAccesos(tercero).Where(c => c != correo))
                    await AccesoAislado.EnviarAvisoAsync(sesion, mensajes, protector, terceroId, decisor, PlantillaCorreo.ContactoEnRevision, null, null, null, correo, ahora);
            }
            else
            {
                var faltantes = invitaciones.Where(i => existente.Invitaciones.All(x => x.VinculoId != i.VinculoId)).ToList();
                if (faltantes.Count > 0)
                {
                    var stream = await sesion.Events.FetchForWriting<SolicitudDeAcceso>(solicitudId, ct);
                    foreach (var i in faltantes)
                        stream.AppendOne(new AccesoSolicitadoPorEmpresa(solicitudId, i.TenantId, i.EmpresaId, i.VinculoId, i.Empresa, ahora));
                }
            }

            await sesion.SaveChangesAsync(ct);
            return (PrimeraVez: existente is null, Estado: existente?.Estado ?? EstadoSolicitudDeAcceso.Pendiente);
        });

        foreach (var invitacion in invitaciones)
            await Peticiones.ConReintentosAsync(() => ReflejarEnElVinculoAsync(invitacion, terceroId, solicitudId, correo, estado, sesiones, ahora, ct));

        // Pierde la bóveda global ya: se cortan sus sesiones (al entrar de nuevo la sesión nace aislada).
        cache.OlvidarAcceso(terceroId, correo);
        await DecisionDeAcceso.RevocarSesionesDelCorreoAsync(sesiones, terceroId, correo, cache, ahora, ct, SesionDeRefresco.MotivoContactoDesplazado);
        await MarcarVersionesAsync(terceroId, correo, sesiones, ahora, ct);
        return primeraVez;
    }

    /// <summary>El vínculo de un contacto desplazado refleja que su acceso espera la decisión (o nace rechazado si ya se rechazó); lo ve la empresa en su historial.</summary>
    private static async Task<bool> ReflejarEnElVinculoAsync(InvitacionDeAcceso invitacion, Guid terceroId, Guid solicitudId, string correo, EstadoSolicitudDeAcceso estado,
        ISesiones sesiones, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.ParaTenant(invitacion.TenantId);
        var stream = await sesion.Events.FetchForWriting<Vinculo>(invitacion.VinculoId, ct);
        if (stream.Aggregate is not { } vinculo || vinculo.TerceroId != terceroId || !string.Equals(SolicitudDeAcceso.Normalizar(vinculo.CorreoTercero), correo, StringComparison.Ordinal))
            return false;

        if (vinculo.SolicitarAccesoAislado(solicitudId, correo, ahora, SolicitudDeAcceso.OrigenContactoDesplazado) is { } solicitado)
        {
            stream.AppendOne(solicitado);
            if (estado == EstadoSolicitudDeAcceso.Rechazada)
                stream.AppendOne(new AccesoAisladoRechazado(solicitudId, correo, ahora));
            await sesion.SaveChangesAsync(ct);
        }

        return true;
    }

    // ---------- versiones en revisión ----------

    /// <summary>Marca para revisión las versiones globales del tercero que subió ese correo (cada documento en su transacción; repetir no marca dos veces).</summary>
    internal static async Task MarcarVersionesAsync(Guid terceroId, string correo, ISesiones sesiones, DateTimeOffset ahora, CancellationToken ct)
    {
        foreach (var documentoId in await DocumentosConVersionesDeAsync(terceroId, correo, soloSinMarca: true, sesiones, ct))
            await Peticiones.ConReintentosAsync(async () =>
            {
                await using var sesion = sesiones.Boveda();
                var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
                var documento = stream.Aggregate ?? throw new InvalidOperationException($"El documento {documentoId} no tiene agregado.");
                var marcas = documento.MarcarParaRevision(correo, MotivoDeRevision, ahora);
                if (marcas.Count == 0)
                    return true;
                foreach (var marca in marcas)
                    stream.AppendOne(marca);
                await sesion.SaveChangesAsync(ct);
                return true;
            });
    }

    /// <summary>Quita la marca de revisión de las versiones que subió ese correo cuando se le aprobó el acceso de nuevo.</summary>
    internal static async Task ResolverRevisionDeVersionesAsync(ISesiones sesiones, Guid terceroId, string correo, string por, DateTimeOffset ahora, CancellationToken ct)
    {
        foreach (var documentoId in await DocumentosConVersionesDeAsync(terceroId, correo, soloSinMarca: false, sesiones, ct))
            await Peticiones.ConReintentosAsync(async () =>
            {
                await using var sesion = sesiones.Boveda();
                var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
                var documento = stream.Aggregate ?? throw new InvalidOperationException($"El documento {documentoId} no tiene agregado.");
                var resueltas = documento.ResolverRevision(correo, por, ahora);
                if (resueltas.Count == 0)
                    return true;
                foreach (var resuelta in resueltas)
                    stream.AppendOne(resuelta);
                await sesion.SaveChangesAsync(ct);
                return true;
            });
    }

    private static async Task<IReadOnlyList<Guid>> DocumentosConVersionesDeAsync(Guid terceroId, string correo, bool soloSinMarca, ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaBoveda();
        var documentos = await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId && d.Ambito == AmbitoDocumento.Global).ToListAsync(ct);
        return
        [
            .. documentos.Where(d => d.Versiones.Any(v => string.Equals(v.CargadaPorCorreo, correo, StringComparison.Ordinal) && (soloSinMarca ? v.EnRevisionDesde is null : v.EnRevisionDesde is not null)))
                .Select(d => d.Id)
        ];
    }
}
