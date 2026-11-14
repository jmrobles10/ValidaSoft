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
/// Acceso aislado (decisiones AA y AV del registro): quién entra con acceso completo a un tercero y quién aislado, y qué pasa cuando una empresa invita a un
/// correo que no es contacto del tercero. Antes bastaba el NIT: la empresa A creaba un vínculo con el NIT de un tercero de B, ponía su propio correo y abría la
/// bóveda de ese tercero. Ahora ese correo entra aislado —ve y sube solo lo de su relación con A— y se avisa a quien decide: los contactos <b>verificados</b> del
/// tercero (<see cref="AccesosSolicitadosEndpoints"/>) o, si no tiene ninguno, la plataforma (la cola de la consola). Y quien registra primero un NIT ajeno deja su
/// correo como contacto sin verificar: no decide y, si otro contacto se verifica, el verificado prevalece (<see cref="SincronizacionDeAccesoDelTercero"/>).
/// </summary>
public static class AccesoAislado
{
    /// <summary>Lo que dura el enlace del aviso a los contactos del tercero (como el del autorregistro).</summary>
    public static readonly TimeSpan VigenciaDelAviso = EnlacesDeAcceso.VigenciaAutorregistro;

    /// <summary>
    /// Nivel de acceso del correo que abre una sesión (en cada canje y en cada renovación, así que lo que aprueban los contactos llega a la sesión
    /// siguiente): completo si es contacto del tercero y no lo desplazó uno verificado; si no, aislado. Un correo cuyo acceso rechazaron
    /// los contactos o la plataforma no entra: 403 <c>acceso-rechazado</c>.
    /// </summary>
    public static async Task<(NivelDeAcceso Nivel, Tercero Tercero)> NivelAsync(ISesiones sesiones, IQuerySession consulta, Guid terceroId, string correo,
        CancellationToken ct)
    {
        var tercero = await consulta.LoadAsync<Tercero>(terceroId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero de esta sesión no existe.");
        var nivel = AccesoDelTercero.Nivel(tercero, correo);
        if (nivel == NivelDeAcceso.Completo)
            return (nivel, tercero);

        await using var boveda = sesiones.ConsultaBoveda();
        if (await boveda.LoadAsync<SolicitudDeAcceso>(SolicitudDeAcceso.IdPara(terceroId, correo), ct) is { Estado: EstadoSolicitudDeAcceso.Rechazada })
            throw ReglaDeNegocioException.Prohibido("acceso-rechazado",
                "Los contactos del tercero rechazaron el acceso de este correo. Pide a la empresa que te invite con otro correo.");
        return (nivel, tercero);
    }

    /// <summary>
    /// La empresa vinculó al tercero con este correo (al crear el vínculo o al invitar de nuevo). Cuatro caminos:
    /// <list type="number">
    /// <item>El correo es de un contacto verificado: entra completo.</item>
    /// <item>El correo se verifica ahora (es el del RUT o la Cámara del tercero, o dos grupos distintos lo invitaron por su cuenta y el tercero aún no tiene un contacto
    /// verificado): pasa a ser contacto verificado y entra completo; si eso desplaza a otro contacto sin verificar, <see cref="SincronizacionDeAccesoDelTercero"/> lo resuelve.</item>
    /// <item>Es el primer contacto (tercero nuevo, de pruebas o de esta empresa y sin historia) o un contacto sin verificar mientras nadie más esté verificado: entra
    /// completo, <b>sin verificar</b>.</item>
    /// <item>Cualquier otro caso (un correo desconocido de un tercero que ya existía, o un contacto sin verificar desplazado por uno verificado): el vínculo queda con
    /// acceso aislado y se avisa a quien decide.</item>
    /// </list>
    /// <paramref name="agregar"/> anexa los eventos al stream del vínculo (la sesión es la del tenant del vínculo, que también escribe, en la misma transacción, el stream
    /// de la solicitud y el historial de contactos en la bóveda). <paramref name="correo"/> es el invitado.
    /// </summary>
    public static async Task<NivelDeAcceso> RegistrarInvitacionAsync(IDocumentSession sesion, IMessageBus mensajes, IProtectorDeSecretos protector,
        Guid tenantId, Tercero tercero, bool terceroNuevo, Vinculo vinculo, string correo, Action<object> agregar, DateTimeOffset ahora, CancellationToken ct)
    {
        var invitado = SolicitudDeAcceso.Normalizar(correo);
        var contacto = tercero.BuscarContacto(invitado);
        var empresa = await sesion.LoadAsync<Empresa>(vinculo.EmpresaId, ct);
        var nombreEmpresa = empresa?.Nombre ?? "Una empresa";

        // 1. Contacto verificado (o contacto de un tercero de pruebas, que no comparte nada con otro grupo): entra completo.
        if (contacto is not null && (contacto.EstaVerificado || tercero.SandboxTenantId is not null))
            return NivelDeAcceso.Completo;

        var boveda = sesion.ForTenant(TenantsTecnicos.Boveda);
        var terceroId = tercero.Id;

        // 2. ¿Se verifica ahora? El correo del RUT o de la Cámara; la corroboración de dos grupos si nadie está verificado todavía.
        if (!terceroNuevo && tercero.SandboxTenantId is null)
        {
            var oficiales = await ContactosDelTercero.CorreosOficialesAsync(sesion.ForTenant(TenantsTecnicos.Boveda), terceroId, ct);
            var grupos = (await ContactosDelTercero.GruposQueInvitaronAsync(sesion, terceroId, invitado, vinculo.Id, ct)).Append(tenantId);
            if (VerificacionDeContactos.Medio(invitado, oficiales, grupos, tercero.TieneContactoVerificado) is { } medio)
            {
                if (contacto is null)
                    ContactosDelTercero.Agregar(sesion, tercero, invitado, tercero.Nombre, "Principal", ContactosDelTercero.OrigenEmpresa, nombreEmpresa, medio, null, ahora);
                else
                    ContactosDelTercero.Verificar(sesion, tercero, invitado, medio, null, ahora);
                await mensajes.PublishAsync(new SincronizarAccesoDelTercero(terceroId));
                return NivelDeAcceso.Completo;
            }
        }

        // 3. Un contacto sin verificar mientras ningún otro esté verificado: el flujo de siempre (entra completo, sin verificar).
        if (contacto is not null && !tercero.TieneContactoVerificado)
            return NivelDeAcceso.Completo;

        // 4. El primer contacto del tercero (si no lo es ya): se vuelve contacto, sin verificar.
        if (contacto is null)
        {
            var hayDocumentos = !terceroNuevo &&
                                await boveda.Query<Documento>().AnyAsync(d => d.TerceroId == terceroId && d.Ambito == AmbitoDocumento.Global, ct);
            var hayOtroGrupo = !terceroNuevo && await HayVinculosDeOtroGrupoAsync(sesion, terceroId, ct);
            var hayContactoProbado = !terceroNuevo && (tercero.TieneContactoVerificado || await ContactosDelTercero.HayInvitacionAceptadaAsync(sesion, terceroId, ct));
            if (AccesoDelTercero.EsPrimerContacto(tercero, tenantId, terceroNuevo, hayOtroGrupo, hayDocumentos, hayContactoProbado))
            {
                ContactosDelTercero.Agregar(sesion, tercero, invitado, tercero.Nombre, "Principal", ContactosDelTercero.OrigenEmpresa, nombreEmpresa, null, null, ahora);
                return NivelDeAcceso.Completo;
            }
        }

        // 5. Acceso aislado: la solicitud queda a la espera de quien decide (los contactos verificados o la plataforma).
        var solicitudId = SolicitudDeAcceso.IdPara(terceroId, invitado);
        var existente = await boveda.LoadAsync<SolicitudDeAcceso>(solicitudId, ct);
        var estado = existente?.Estado ?? EstadoSolicitudDeAcceso.Pendiente;
        var desplazado = contacto is not null;

        AccesoSolicitadoPorEmpresa? pedida;
        if (existente is null)
        {
            var creada = SolicitudDeAcceso.Crear(terceroId, invitado, ahora, desplazado ? SolicitudDeAcceso.OrigenContactoDesplazado : null);
            pedida = SolicitudDeAcceso.Create(creada).SolicitarPor(tenantId, vinculo.EmpresaId, vinculo.Id, nombreEmpresa, ahora);
            boveda.Events.StartStream<SolicitudDeAcceso>(solicitudId, creada, pedida!);
        }
        else
        {
            pedida = existente.SolicitarPor(tenantId, vinculo.EmpresaId, vinculo.Id, nombreEmpresa, ahora);
            if (pedida is not null)
                (await boveda.Events.FetchForWriting<SolicitudDeAcceso>(solicitudId, ct)).AppendOne(pedida);
        }

        if (vinculo.SolicitarAccesoAislado(solicitudId, invitado, ahora, desplazado ? SolicitudDeAcceso.OrigenContactoDesplazado : null) is { } solicitado)
        {
            agregar(solicitado);
            // Un correo ya rechazado no vuelve a avisar: el vínculo nace con el rechazo y la empresa invita a otro.
            if (estado == EstadoSolicitudDeAcceso.Rechazada)
                agregar(new AccesoAisladoRechazado(solicitudId, invitado, ahora));
        }

        if (pedida is not null && estado == EstadoSolicitudDeAcceso.Pendiente)
            await AvisarAQuienDecideAsync(sesion, mensajes, protector, tenantId, vinculo, tercero, invitado, nombreEmpresa, ahora, ct);
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
    /// Correo a cada contacto <b>verificado</b> del tercero (con un enlace de acceso a su cuenta para decidir) además de la campana, que sale del evento del vínculo
    /// (<see cref="AccesoAisladoSolicitado"/>). Si el tercero no tiene ninguno, no hay a quién escribir: la solicitud espera en la cola de la consola de Plataforma. El
    /// correo sale por el outbox en la misma transacción.
    /// </summary>
    internal static async Task AvisarAQuienDecideAsync(IDocumentSession sesion, IMessageBus mensajes, IProtectorDeSecretos protector, Guid tenantId, Vinculo vinculo,
        Tercero tercero, string invitado, string empresa, DateTimeOffset ahora, CancellationToken ct)
    {
        var tipo = await sesion.LoadAsync<Dominio.Configuracion.TipoVinculo>(vinculo.TipoVinculoId, ct);
        foreach (var contacto in AccesoDelTercero.CorreosQueDecidenLosAccesos(tercero).Where(c => c != invitado))
            await EnviarAvisoAsync(sesion, mensajes, protector, tercero.Id, contacto, PlantillaCorreo.SolicitudDeAcceso, empresa, tipo?.Nombre, tenantId, invitado, ahora);
    }

    /// <summary>Un correo con enlace de acceso a la cuenta del tercero (72 h) para un contacto que debe decidir algo.</summary>
    internal static async Task EnviarAvisoAsync(IDocumentSession sesion, IMessageBus mensajes, IProtectorDeSecretos protector, Guid terceroId, string para,
        PlantillaCorreo plantilla, string? empresa, string? tipoVinculo, Guid? tenantId, string invitado, DateTimeOffset ahora)
    {
        var (enlace, token) = EnlaceDeAcceso.Emitir(null, null, terceroId, para, ahora + VigenciaDelAviso, requiereOtp: false, ahora, MotivoEnlace.SolicitudDeAcceso);
        sesion.Store(enlace);
        await mensajes.PublishAsync(new CorreoDeAccesoPendiente(plantilla, para, protector.Proteger(token), enlace.Expira, empresa, tipoVinculo, tenantId, null, invitado));
    }
}
