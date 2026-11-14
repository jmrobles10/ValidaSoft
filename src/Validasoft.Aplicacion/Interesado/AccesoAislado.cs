using JasperFx.Events;
using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>
/// Acceso aislado (decisión AA del registro): quién entra con acceso completo a un tercero y quién aislado, y qué pasa cuando una empresa invita a un
/// correo que no es contacto conocido de un tercero que ya existía. Antes bastaba el NIT: la empresa A creaba un vínculo con el NIT de un tercero de B,
/// ponía su propio correo y abría la bóveda de ese tercero. Ahora ese correo entra aislado —ve y sube solo lo de su relación con A— y se avisa a los
/// contactos del tercero, que lo aprueban o lo rechazan (<see cref="AccesosSolicitadosEndpoints"/>).
/// </summary>
public static class AccesoAislado
{
    /// <summary>Lo que dura el enlace del aviso a los contactos del tercero (como el del autorregistro).</summary>
    public static readonly TimeSpan VigenciaDelAviso = EnlacesDeAcceso.VigenciaAutorregistro;

    /// <summary>
    /// Correos de los vínculos del tercero (de cualquier tenant) que ya aceptaron su invitación sin pasar por el acceso aislado: entraron cuando el correo
    /// bastaba y siguen entrando completos (se les reconoce como contactos, no se les bloquea).
    /// </summary>
    public static async Task<IReadOnlyList<string>> CorreosDeVinculosAceptadosAsync(IQuerySession consulta, Guid terceroId, CancellationToken ct)
    {
        var aceptados = await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.TerceroId == terceroId && v.InvitacionAceptada).ToListAsync(ct);
        return [.. aceptados.Where(v => v.AccesoAislado is null or EstadoAccesoAislado.Aprobado).Select(v => v.CorreoTercero)];
    }

    /// <summary>
    /// Nivel de acceso del correo que abre una sesión (en cada canje y en cada renovación, así que lo que aprueban los contactos llega a la sesión
    /// siguiente): completo si es contacto conocido; si no, aislado. Un correo cuyo acceso rechazaron los contactos no entra: 403 <c>acceso-rechazado</c>.
    /// </summary>
    public static async Task<(NivelDeAcceso Nivel, Tercero Tercero)> NivelAsync(ISesiones sesiones, IQuerySession consulta, Guid terceroId, string correo,
        CancellationToken ct)
    {
        var tercero = await consulta.LoadAsync<Tercero>(terceroId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero de esta sesión no existe.");
        var nivel = AccesoDelTercero.Nivel(tercero, correo, await CorreosDeVinculosAceptadosAsync(consulta, terceroId, ct));
        if (nivel == NivelDeAcceso.Completo)
            return (nivel, tercero);

        await using var boveda = sesiones.ConsultaBoveda();
        if (await boveda.LoadAsync<SolicitudDeAcceso>(SolicitudDeAcceso.IdPara(terceroId, correo), ct) is { Estado: EstadoSolicitudDeAcceso.Rechazada })
            throw ReglaDeNegocioException.Prohibido("acceso-rechazado",
                "Los contactos del tercero rechazaron el acceso de este correo. Pide a la empresa que te invite con otro correo.");
        return (nivel, tercero);
    }

    /// <summary>
    /// La empresa vinculó al tercero con este correo (al crear el vínculo o al invitar de nuevo): si no es un contacto conocido, deja el vínculo con acceso
    /// aislado y avisa a los contactos del tercero. Si es el primer contacto (tercero nuevo o sin historia ajena) el correo pasa a ser su contacto, como
    /// siempre. <paramref name="agregar"/> anexa los eventos al stream del vínculo (la sesión es la del tenant del vínculo, que también escribe, en la
    /// misma transacción, el stream de la solicitud en la bóveda). <paramref name="correo"/> es el invitado.
    /// </summary>
    public static async Task<NivelDeAcceso> RegistrarInvitacionAsync(IDocumentSession sesion, IMessageBus mensajes, IProtectorDeSecretos protector,
        Guid tenantId, Tercero tercero, bool terceroNuevo, Vinculo vinculo, string correo, Action<object> agregar, DateTimeOffset ahora, CancellationToken ct)
    {
        var invitado = SolicitudDeAcceso.Normalizar(correo);
        // Un tercero nuevo no tiene vínculos que reconocer (y es el caso común de una carga masiva).
        var aceptados = terceroNuevo ? [] : await CorreosDeVinculosAceptadosAsync(sesion, tercero.Id, ct);
        if (AccesoDelTercero.EsContactoConocido(tercero, invitado, aceptados))
            return NivelDeAcceso.Completo;

        var boveda = sesion.ForTenant(TenantsTecnicos.Boveda);
        var terceroId = tercero.Id;
        var hayDocumentos = !terceroNuevo &&
                            await boveda.Query<Documento>().AnyAsync(d => d.TerceroId == terceroId && d.Ambito == AmbitoDocumento.Global, ct);
        var hayOtroGrupo = !terceroNuevo && await HayVinculosDeOtroGrupoAsync(sesion, terceroId, ct);
        if (AccesoDelTercero.EsPrimerContacto(tercero, tenantId, terceroNuevo, hayOtroGrupo, hayDocumentos, hayContactoProbado: aceptados.Count > 0))
        {
            if (tercero.AgregarContacto(invitado, tercero.Nombre, "Principal"))
                sesion.Store(tercero);
            return NivelDeAcceso.Completo;
        }

        var solicitudId = SolicitudDeAcceso.IdPara(terceroId, invitado);
        var empresa = await sesion.LoadAsync<Empresa>(vinculo.EmpresaId, ct);
        var nombreEmpresa = empresa?.Nombre ?? "Una empresa";
        var existente = await boveda.LoadAsync<SolicitudDeAcceso>(solicitudId, ct);
        var estado = existente?.Estado ?? EstadoSolicitudDeAcceso.Pendiente;

        AccesoSolicitadoPorEmpresa? pedida;
        if (existente is null)
        {
            var creada = SolicitudDeAcceso.Crear(terceroId, invitado, ahora);
            pedida = SolicitudDeAcceso.Create(creada).SolicitarPor(tenantId, vinculo.EmpresaId, vinculo.Id, nombreEmpresa, ahora);
            boveda.Events.StartStream<SolicitudDeAcceso>(solicitudId, creada, pedida!);
        }
        else
        {
            pedida = existente.SolicitarPor(tenantId, vinculo.EmpresaId, vinculo.Id, nombreEmpresa, ahora);
            if (pedida is not null)
                (await boveda.Events.FetchForWriting<SolicitudDeAcceso>(solicitudId, ct)).AppendOne(pedida);
        }

        if (vinculo.SolicitarAccesoAislado(solicitudId, invitado, ahora) is { } solicitado)
        {
            agregar(solicitado);
            // Un correo ya rechazado por los contactos no vuelve a avisarles: el vínculo nace con el rechazo y la empresa invita a otro.
            if (estado == EstadoSolicitudDeAcceso.Rechazada)
                agregar(new AccesoAisladoRechazado(solicitudId, invitado, ahora));
        }

        if (pedida is not null && estado == EstadoSolicitudDeAcceso.Pendiente)
            await AvisarALosContactosAsync(sesion, mensajes, protector, tenantId, vinculo, tercero, invitado, nombreEmpresa, aceptados, ahora, ct);
        return NivelDeAcceso.Aislado;
    }

    /// <summary>¿Hay vínculos del tercero en otro grupo (tenant) además de los de este? Se cuentan entre tenants contra los de la sesión.</summary>
    private static async Task<bool> HayVinculosDeOtroGrupoAsync(IDocumentSession sesion, Guid terceroId, CancellationToken ct)
    {
        var total = await sesion.Query<Vinculo>().CountAsync(v => v.AnyTenant() && v.TerceroId == terceroId, ct);
        var propios = await sesion.Query<Vinculo>().CountAsync(v => v.TerceroId == terceroId, ct);
        return total > propios;
    }

    /// <summary>
    /// Correo a cada contacto del tercero (con un enlace de acceso a su cuenta para decidir) además de la campana, que sale del evento del vínculo
    /// (<see cref="AccesoAisladoSolicitado"/>). El correo sale por el outbox en la misma transacción.
    /// </summary>
    private static async Task AvisarALosContactosAsync(IDocumentSession sesion, IMessageBus mensajes, IProtectorDeSecretos protector, Guid tenantId,
        Vinculo vinculo, Tercero tercero, string invitado, string empresa, IReadOnlyList<string> aceptados, DateTimeOffset ahora, CancellationToken ct)
    {
        var tipo = await sesion.LoadAsync<Dominio.Configuracion.TipoVinculo>(vinculo.TipoVinculoId, ct);
        foreach (var contacto in AccesoDelTercero.CorreosConocidos(tercero, aceptados).Where(c => c != invitado))
        {
            var (enlace, token) = EnlaceDeAcceso.Emitir(null, null, tercero.Id, contacto, ahora + VigenciaDelAviso, requiereOtp: false, ahora,
                MotivoEnlace.SolicitudDeAcceso);
            sesion.Store(enlace);
            await mensajes.PublishAsync(new CorreoDeAccesoPendiente(PlantillaCorreo.SolicitudDeAcceso, contacto, protector.Proteger(token), enlace.Expira,
                empresa, tipo?.Nombre, tenantId, null, invitado));
        }
    }
}
