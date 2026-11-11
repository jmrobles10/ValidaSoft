using JasperFx.Events;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>
/// Notificación de la campana de una persona (docs/05 §8, NW-8). Esquema <c>validacion</c>, multi-tenant: una por
/// usuario, hecho y tipo (id determinista: reprocesar el evento no la duplica). <see cref="Orden"/> ordena la bandeja
/// (más reciente primero) y es el cursor.
/// </summary>
public sealed class Notificacion
{
    public Guid Id { get; set; }

    public Guid UsuarioId { get; set; }

    public Guid? EmpresaId { get; set; }

    /// <summary>Tenant en que vive; solo lo guardan las del tercero, que se leen entre tenants (NT-1).</summary>
    public Guid? TenantId { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Titulo { get; set; } = string.Empty;

    public string Mensaje { get; set; } = string.Empty;

    public Guid? ExpedienteId { get; set; }

    public Guid? VinculoId { get; set; }

    public string? Folio { get; set; }

    /// <summary>Ruta de <c>apps/web</c> sin host (<c>/e/{empresa}/expedientes/{id}</c>).</summary>
    public string? Enlace { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public long Orden { get; set; }

    public DateTimeOffset? LeidaEn { get; set; }

    public bool Leida { get; set; }

    public NotificacionDto ADto() => new(Id, Tipo, Titulo, Mensaje, EmpresaId, ExpedienteId, VinculoId, Folio, Enlace, CreadaEn, LeidaEn);

    public void MarcarLeida(DateTimeOffset ahora)
    {
        if (Leida)
            return;
        Leida = true;
        LeidaEn = ahora;
    }
}

/// <summary>Preferencias de correo de una persona por tipo de notificación (id = usuario). Esquema <c>validacion</c>, multi-tenant.</summary>
public sealed class PreferenciasNotificacion
{
    public Guid Id { get; set; }

    public Dictionary<string, bool> Correo { get; set; } = [];

    public DateTimeOffset ActualizadasEn { get; set; }
}

/// <summary>Tipos de notificación a personas y cuáles pueden ir además por correo (por defecto sí).</summary>
public static class TiposNotificacion
{
    public const string NuevoEnArea = "expediente.nuevo_en_area";
    public const string TerceroRespondio = "expediente.tercero_respondio";
    public const string VersionNueva = "expediente.version_nueva";
    public const string Aprobado = "expediente.aprobado";
    public const string Rechazado = "expediente.rechazado";
    public const string Caducado = "expediente.caducado";

    /// <summary>Terminó un lote de invitación masiva (a quien lo creó, LT-9).</summary>
    public const string LoteTerminado = "invitaciones.lote_terminado";
    public const string PorVencer = "documento.por_vencer";
    public const string Vencido = "documento.vencido";
    public const string WebhookDeshabilitado = "webhook.deshabilitado";

    /// <summary>La empresa llegó al 80 % o al 100 % de sus validaciones con IA incluidas del mes (IA-4).</summary>
    public const string CupoIa = "ia.cupo";

    /// <summary>Una consulta de listas (o su monitoreo) encontró coincidencias por analizar (plantilla <c>coincidencia</c>, VE-13).</summary>
    public const string CoincidenciaListas = "verificacion.coincidencia";

    /// <summary>Un expediente que pide el estudio de cumplimiento entró a revisión.</summary>
    public const string EstudioPendiente = "verificacion.estudio_pendiente";

    /// <summary>Venció una verificación con el expediente abierto (plantilla <c>verificacion-expirada</c>, solo campana).</summary>
    public const string VerificacionExpirada = "verificacion.expirada";

    /// <summary>El tercero propuso la cuenta bancaria con la que le cobra a la empresa: a quienes pueden aprobarla (<c>cuentas:aprobar</c>), solo campana (NT-12).</summary>
    public const string CuentaPropuesta = "vinculo.cuenta_propuesta";

    public static readonly IReadOnlyList<string> Todos =
        [NuevoEnArea, TerceroRespondio, VersionNueva, Aprobado, Rechazado, Caducado, PorVencer, Vencido, WebhookDeshabilitado, CupoIa, LoteTerminado,
            CoincidenciaListas, EstudioPendiente, VerificacionExpirada, CuentaPropuesta];

    /// <summary>Los que además de la campana van por correo (si la persona no lo apagó).</summary>
    public static readonly IReadOnlyList<string> ConCorreo =
        [NuevoEnArea, TerceroRespondio, VersionNueva, WebhookDeshabilitado, CupoIa, LoteTerminado, CoincidenciaListas, EstudioPendiente];

    public static bool QuiereCorreo(PreferenciasNotificacion? preferencias, string tipo) =>
        ConCorreo.Contains(tipo) && (preferencias is null || !preferencias.Correo.TryGetValue(tipo, out var valor) || valor);
}

/// <summary>
/// Correo a una persona del tenant (revisor o administrador), ya consolidado: uno por persona y hecho (NW-9). Cola
/// <c>correo</c>; <see cref="Ruta"/> es la de <c>apps/web</c> y el redactor le antepone la URL de la app.
/// </summary>
public sealed record CorreoDeNotificacionPendiente(string Para, string Nombre, string Asunto, IReadOnlyList<string> Lineas, string? Ruta);

/// <summary>Texto aceptado para la constancia del tercero.</summary>
public sealed record TextoAceptadoParaCorreo(string Titulo, int Version, string Hash);

/// <summary>
/// Constancia al tercero de lo que aceptó (plantilla <c>consentimiento-empresa</c>, docs/05 §8, NW-10): textos, versión,
/// huella y fecha. Cola <c>correo</c>.
/// </summary>
public sealed record CorreoDeConstanciaPendiente(string Para, string Empresa, IReadOnlyList<TextoAceptadoParaCorreo> Textos, DateTimeOffset AceptadaEn,
    Guid? TenantId = null, Guid? EmpresaId = null);

/// <summary>Lo que un evento de dominio produce para las personas: notificaciones de la campana y correos.</summary>
public sealed record PlanDeNotificaciones(IReadOnlyList<(Guid TenantId, Notificacion Notificacion)> Campana, IReadOnlyList<object> Correos,
    IReadOnlyList<AvisoDeCambio>? Cambios = null)
{
    public static readonly PlanDeNotificaciones Vacio = new([], []);
}

/// <summary>
/// Decide quién se entera de cada hecho (docs/05 §8): «Nuevo expediente en tu área» (etapa activada), «El tercero
/// respondió» (reenvío, a las áreas que vuelven a revisar), expediente aprobado o rechazado y documento por vencer (a las
/// áreas del expediente) y la constancia al tercero al aceptar. Solo miembros activos con rol en la empresa.
/// </summary>
public sealed class PlanificadorDeNotificaciones
{
    /// <summary>
    /// Lo que el hecho produce para las personas de las empresas y, además, para el tercero (campana de su portal, NT-1; sus
    /// correos los siguen mandando los disparadores de siempre).
    /// </summary>
    public async Task<PlanDeNotificaciones> PlanearAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        var personas = await PlanearParaPersonasAsync(evento, contexto, orden, ct);
        var tercero = await PlanificadorDeNotificacionesDelTercero.PlanearAsync(evento, contexto, orden, ct);
        // Y lo que cambió para quien tiene la pantalla abierta (NT-12): el SSE lo manda aparte de la notificación.
        var cambios = await PlanificadorDeCambios.PlanearAsync(evento, contexto, orden, ct);
        return tercero.Campana.Count == 0 && tercero.Correos.Count == 0 && cambios.Count == 0
            ? personas
            : new PlanDeNotificaciones([.. personas.Campana, .. tercero.Campana], [.. personas.Correos, .. tercero.Correos], cambios);
    }

    private async Task<PlanDeNotificaciones> PlanearParaPersonasAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        switch (evento.Data)
        {
            case ConsentimientosRegistrados consentimientos:
                return await ConstanciaAsync(evento, consentimientos, contexto, ct);
            case EtapaActivada etapa:
                return await AAreasAsync(evento, contexto, TiposNotificacion.NuevoEnArea, etapa.Areas, orden, ct);
            case ExpedienteReenviado reenviado:
                return await AAreasAsync(evento, contexto, TiposNotificacion.TerceroRespondio, reenviado.AreasReabiertas, orden, ct,
                    reenviado.RequisitosReabiertos);
            case VersionAdjuntadaARequisito { NumeroAnterior: not null } adjuntada:
                return await VersionNuevaAsync(evento, adjuntada, contexto, orden, ct);
            case RequisitosAgregados agregados:
                return await RequisitosAgregadosAsync(evento, agregados, contexto, orden, ct);
            case ExpedienteAprobado:
                return await AAreasAsync(evento, contexto, TiposNotificacion.Aprobado, null, orden, ct);
            case ExpedienteCaducado:
                return await AAreasAsync(evento, contexto, TiposNotificacion.Caducado, null, orden, ct);
            case ExpedienteRechazado:
                return await AAreasAsync(evento, contexto, TiposNotificacion.Rechazado, null, orden, ct);
            case Dominio.Invitaciones.LoteTerminado terminado:
                return await LoteTerminadoAsync(evento, terminado, contexto, orden, ct);
            case VencimientoAvisado aviso:
                return await PorVencerAsync(evento, aviso, contexto, orden, ct);
            case VersionVencida vencida:
                return await VencidoAsync(evento, vencida, contexto, orden, ct);
            case CertificadoRevocado revocado:
                return await CertificadoAlTerceroAsync(evento, revocado.Emision, revocado.Fecha, revocado: true, contexto, ct);
            case CertificadoSuspendido suspendido:
                return await CertificadoAlTerceroAsync(evento, suspendido.Emision, suspendido.Fecha, revocado: false, contexto, ct);
            case VerificacionExternaRegistrada registrada when registrada.Coincidencias.Any(c => c.Heredada is null):
                return await ACumplimientoAsync(evento, contexto, TiposNotificacion.CoincidenciaListas, "Coincidencia en listas restrictivas",
                    t => $"La consulta de listas de {t} encontró {registrada.Coincidencias.Count(c => c.Heredada is null)} coincidencia(s) por analizar.", orden, ct);
            case ExpedienteEnviadoARevision:
                return await EstudioPendienteAsync(evento, contexto, orden, ct);
            case CuentaDesignadaPropuesta propuesta:
                return await CuentaPropuestaAsync(evento, propuesta, contexto, orden, ct);
            case VerificacionExpirada expirada:
                return await ACumplimientoAsync(evento, contexto, TiposNotificacion.VerificacionExpirada, "Verificación vencida",
                    t => expirada.Tipo == TipoVerificacion.ListasRestrictivas
                        ? $"Venció la consulta de listas de {t}: se reconsulta sola."
                        : $"Venció el estudio de cumplimiento de {t}: hay que volver a hacerlo antes de cerrar el expediente.", orden, ct);
            default:
                return PlanDeNotificaciones.Vacio;
        }
    }

    /// <summary>
    /// El tercero propuso la cuenta con la que le cobra a la empresa (CB-2): campana a las personas con <c>cuentas:aprobar</c> en esa empresa,
    /// que son quienes deben aprobarla (dos distintas). Antes no se enteraba nadie hasta abrir la ficha del tercero y recargar.
    /// </summary>
    private static async Task<PlanDeNotificaciones> CuentaPropuestaAsync(IEvent evento, CuentaDesignadaPropuesta propuesta, IContextoDeEventos contexto,
        long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo)
            return PlanDeNotificaciones.Vacio;

        var aprobadores = (await contexto.UsuariosDelTenantAsync(tenantId, ct))
            .Where(u => u.Activo && u.Roles.Any(r => (r.EmpresaId is null || r.EmpresaId == vinculo.EmpresaId) && Comun.Permisos.DeRol(r.Rol).Contains(Comun.Permisos.CuentasAprobar)))
            .OrderBy(u => u.Id)
            .ToList();
        if (aprobadores.Count == 0)
            return PlanDeNotificaciones.Vacio;

        var tercero = (await contexto.TerceroAsync(vinculo.TerceroId, ct))?.Nombre ?? "El tercero";
        var empresa = (await contexto.EmpresaAsync(vinculo.EmpresaId, ct))?.Nombre ?? "la empresa";
        var cuenta = $"{propuesta.Cuenta.Banco} {Dominio.Terceros.CuentaBancaria.Enmascarar(propuesta.Cuenta.Ultimos4)}";
        var mensaje = $"{tercero} propuso la cuenta {cuenta} para cobrarle a {ConPunto(empresa)} La aprueban {Vinculo.AprobadoresDeCuenta} personas distintas.";
        var ruta = $"/e/{vinculo.EmpresaId}/vinculos/{vinculo.Id}";
        return new PlanDeNotificaciones([.. aprobadores.Select(u => (tenantId, new Notificacion
        {
            Id = Deterministas.Guid($"notificacion:{evento.Id:N}:{u.Id:N}:{TiposNotificacion.CuentaPropuesta}"),
            UsuarioId = u.Id,
            EmpresaId = vinculo.EmpresaId,
            Tipo = TiposNotificacion.CuentaPropuesta,
            Titulo = "Cuenta bancaria por aprobar",
            Mensaje = mensaje,
            VinculoId = vinculo.Id,
            Enlace = ruta,
            CreadaEn = evento.Timestamp,
            Orden = orden
        }))], []);
    }

    /// <summary>El expediente entró a revisión y pide el estudio de cumplimiento: aviso a los oficiales (VE-13).</summary>
    private static async Task<PlanDeNotificaciones> EstudioPendienteAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct) is not { } expediente ||
            expediente.BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento) is not { Estado: EstadoVerificacion.Pendiente })
            return PlanDeNotificaciones.Vacio;
        return await ACumplimientoAsync(evento, contexto, TiposNotificacion.EstudioPendiente, "Estudio de cumplimiento pendiente",
            t => $"El expediente de {t} está en revisión y necesita el estudio del oficial de cumplimiento.", orden, ct);
    }

    /// <summary>Aviso a los oficiales de cumplimiento de la empresa del expediente (o a sus administradores si no hay, VE-13).</summary>
    private static async Task<PlanDeNotificaciones> ACumplimientoAsync(IEvent evento, IContextoDeEventos contexto, string tipo, string titulo,
        Func<string, string> mensaje, long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct) is not { } expediente)
            return PlanDeNotificaciones.Vacio;
        var usuarios = await contexto.UsuariosDelTenantAsync(tenantId, ct);
        var preferencias = new Dictionary<Guid, PreferenciasNotificacion?>();
        foreach (var usuario in Verificaciones.NotificacionesDeCumplimiento.Destinatarios(usuarios, expediente.EmpresaId))
            preferencias[usuario.Id] = await contexto.PreferenciasAsync(tenantId, usuario.Id, ct);
        var empresa = await contexto.EmpresaAsync(expediente.EmpresaId, ct);
        return Verificaciones.NotificacionesDeCumplimiento.Plan(tenantId, expediente.EmpresaId, tipo, titulo, mensaje(expediente.TerceroNombre),
            $"{evento.Id:N}", expediente.Id, expediente.VinculoId, expediente.Folio, $"/e/{expediente.EmpresaId}/expedientes/{expediente.Id}?pestana=verificaciones",
            empresa?.Nombre, usuarios, id => preferencias.GetValueOrDefault(id), evento.Timestamp, orden);
    }

    /// <summary>
    /// <paramref name="requisitosReabiertos"/> (el tercero respondió): además de las áreas que observaron, las que revisan los
    /// documentos que vuelven a revisión —su decisión sobre una versión anterior quedó sin efecto aunque no hubieran observado—
    /// (NT-4).
    /// </summary>
    private static async Task<PlanDeNotificaciones> AAreasAsync(IEvent evento, IContextoDeEventos contexto, string tipo, IReadOnlyList<Guid>? areas,
        long orden, CancellationToken ct, IReadOnlyList<Guid>? requisitosReabiertos = null)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct) is not { } expediente)
            return PlanDeNotificaciones.Vacio;

        var ids = areas ?? expediente.AreaIds;
        if (requisitosReabiertos is { Count: > 0 })
            ids = [.. ids.Concat(expediente.Requisitos.Where(r => requisitosReabiertos.Contains(r.Id)).SelectMany(r => r.AreasQueRevisan)).Distinct()];
        var deLasAreas = (await contexto.AreasAsync(tenantId, [.. ids], ct)).Where(a => a.EmpresaId == expediente.EmpresaId).ToList();
        var empresa = await contexto.EmpresaAsync(expediente.EmpresaId, ct);
        var (titulo, mensaje) = tipo switch
        {
            TiposNotificacion.NuevoEnArea => ("Nuevo expediente en tu área", $"{expediente.TerceroNombre} ({expediente.TipoVinculoNombre}) espera tu revisión."),
            TiposNotificacion.TerceroRespondio => ("El tercero respondió", $"{expediente.TerceroNombre} respondió las observaciones; el expediente vuelve a tu área."),
            TiposNotificacion.Aprobado => ("Expediente aprobado", $"Se aprobó la validación de {expediente.TerceroNombre}."),
            TiposNotificacion.Caducado => ("Expediente caducado",
                $"{expediente.TerceroNombre} no completó su validación en el plazo; el expediente caducó y se puede reabrir."),
            _ => ("Expediente rechazado", $"Se rechazó la validación de {expediente.TerceroNombre}.")
        };
        return await APersonasAsync(evento, contexto, tenantId, expediente.EmpresaId, empresa?.Nombre, tipo, titulo, mensaje, expediente.Id,
            expediente.VinculoId, expediente.Folio, deLasAreas.Select(a => (a.Nombre, a.Miembros)).ToList(), orden, ct);
    }

    /// <summary>
    /// El tercero subió una versión nueva de un documento mientras el expediente está en revisión (p. ej. una revalidación
    /// ya abierta): avisa a las áreas que revisan ese requisito, porque lo decidido sobre la versión anterior quedó sin efecto
    /// (RQ-10). Con el expediente observado no avisa: de eso se encarga «El tercero respondió» al reenviar.
    /// </summary>
    private static async Task<PlanDeNotificaciones> VersionNuevaAsync(IEvent evento, VersionAdjuntadaARequisito adjuntada, IContextoDeEventos contexto,
        long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct) is not { } expediente ||
            expediente.Estado != EstadoExpediente.EnRevision || expediente.Requisitos.FirstOrDefault(r => r.Id == adjuntada.RequisitoId) is not { } requisito)
            return PlanDeNotificaciones.Vacio;

        var ids = requisito.AreasQueRevisan.Count > 0 ? requisito.AreasQueRevisan : expediente.AreaIds;
        var deLasAreas = (await contexto.AreasAsync(tenantId, [.. ids], ct)).Where(a => a.EmpresaId == expediente.EmpresaId).ToList();
        var empresa = await contexto.EmpresaAsync(expediente.EmpresaId, ct);
        var mensaje = $"{expediente.TerceroNombre} subió la versión {adjuntada.Version.Numero} de {requisito.Nombre} (antes la " +
                      $"{adjuntada.NumeroAnterior}): lo decidido sobre la anterior quedó sin efecto y hay que revisarla de nuevo.";
        return await APersonasAsync(evento, contexto, tenantId, expediente.EmpresaId, empresa?.Nombre, TiposNotificacion.VersionNueva,
            "Versión nueva de un documento", mensaje, expediente.Id, expediente.VinculoId, expediente.Folio,
            deLasAreas.Select(a => (a.Nombre, a.Miembros)).ToList(), orden, ct);
    }

    /// <summary>
    /// La actualización abierta sumó requisitos que cambiaron después de abrirse (RV-1): avisa a las áreas que los revisan, porque
    /// lo que ya habían decidido en el expediente no cubre lo nuevo y vuelven a tener algo pendiente. Con el expediente devuelto al
    /// tercero no avisa: de eso se encarga «El tercero respondió» al reenviar.
    /// </summary>
    private static async Task<PlanDeNotificaciones> RequisitosAgregadosAsync(IEvent evento, RequisitosAgregados agregados, IContextoDeEventos contexto,
        long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct) is not { } expediente ||
            expediente.Estado != EstadoExpediente.EnRevision)
            return PlanDeNotificaciones.Vacio;

        var nuevos = expediente.Requisitos.Where(r => agregados.Requisitos.Any(a => a.RequisitoId == r.Id)).ToList();
        if (nuevos.Count == 0)
            return PlanDeNotificaciones.Vacio;

        var ids = nuevos.SelectMany(r => r.AreasQueRevisan).Distinct().ToList();
        var deLasAreas = (await contexto.AreasAsync(tenantId, ids, ct)).Where(a => a.EmpresaId == expediente.EmpresaId).ToList();
        var empresa = await contexto.EmpresaAsync(expediente.EmpresaId, ct);
        var mensaje = $"{expediente.TerceroNombre} actualizó {string.Join(", ", nuevos.Select(r => r.Nombre))}: se sumó a la actualización que ya estaba " +
                      "abierta y hay que revisarlo, aunque tu área ya hubiera decidido lo demás.";
        return await APersonasAsync(evento, contexto, tenantId, expediente.EmpresaId, empresa?.Nombre, TiposNotificacion.VersionNueva,
            "Requisitos nuevos en una actualización", mensaje, expediente.Id, expediente.VinculoId, expediente.Folio,
            deLasAreas.Select(a => (a.Nombre, a.Miembros)).ToList(), orden, ct);
    }

    private static async Task<PlanDeNotificaciones> PorVencerAsync(IEvent evento, VencimientoAvisado aviso, IContextoDeEventos contexto, long orden,
        CancellationToken ct)
    {
        var documento = await contexto.DocumentoAsync(evento.StreamId, ct);
        var destino = documento?.Comparticiones.LastOrDefault(c => c.VinculoId == aviso.VinculoId);
        if (documento is null || destino is null || await contexto.VinculoAsync(destino.TenantId, aviso.VinculoId, ct) is not { } vinculo)
            return PlanDeNotificaciones.Vacio;

        // Las áreas involucradas son las del expediente abierto del vínculo o, si no hay, las del último.
        var expedienteId = vinculo.ExpedienteAbiertoId ?? vinculo.UltimoExpedienteId;
        var expediente = expedienteId is { } id ? await contexto.ExpedienteAsync(destino.TenantId, id, ct) : null;
        if (expediente is null)
            return PlanDeNotificaciones.Vacio;

        var areas = (await contexto.AreasAsync(destino.TenantId, expediente.AreaIds, ct)).Where(a => a.EmpresaId == vinculo.EmpresaId).ToList();
        var empresa = await contexto.EmpresaAsync(vinculo.EmpresaId, ct);
        var mensaje = $"El documento {documento.NombreTipo} de {expediente.TerceroNombre} vence el {aviso.VigenteHasta:yyyy-MM-dd} " +
                      $"({aviso.DiasRestantes} días).";
        return await APersonasAsync(evento, contexto, destino.TenantId, vinculo.EmpresaId, empresa?.Nombre, TiposNotificacion.PorVencer,
            "Documento por vencer", mensaje, expediente.Id, vinculo.Id, expediente.Folio, areas.Select(a => (a.Nombre, a.Miembros)).ToList(),
            orden, ct, $"/e/{vinculo.EmpresaId}/vinculos/{vinculo.Id}");
    }

    /// <summary>
    /// El certificado del tercero se revocó o se suspendió (plantilla <c>certificado-revocado</c>, docs/05 §8, NW-16): correo
    /// al tercero con el folio y la empresa, sin el motivo (es interno, como en la verificación pública, CE-8).
    /// </summary>
    private static async Task<PlanDeNotificaciones> CertificadoAlTerceroAsync(IEvent evento, int emision, DateTimeOffset fecha, bool revocado,
        IContextoDeEventos contexto, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.CertificadoAsync(tenantId, evento.StreamId, ct) is not { } certificado)
            return PlanDeNotificaciones.Vacio;
        var vinculo = await contexto.VinculoAsync(tenantId, certificado.VinculoId, ct);
        var folio = certificado.Emisiones.FirstOrDefault(e => e.Numero == emision)?.Folio;
        if (vinculo is null || folio is null || string.IsNullOrWhiteSpace(vinculo.CorreoTercero))
            return PlanDeNotificaciones.Vacio;

        var empresa = await contexto.EmpresaAsync(certificado.EmpresaId, ct);
        return new PlanDeNotificaciones([], [new CorreoDeCertificadoPendiente(vinculo.CorreoTercero, empresa?.Nombre ?? "La empresa", folio, revocado, fecha, tenantId,
            certificado.EmpresaId, vinculo.TerceroId)]);
    }

    /// <summary>
    /// Un documento venció para un vínculo (pasó la vigencia y la gracia, TD-7): correo al tercero y campana a las áreas del
    /// expediente del vínculo (docs/05 §8). Antes no se avisaba a nadie: el documento solo aparecía «Vencido» en pantalla.
    /// </summary>
    private static async Task<PlanDeNotificaciones> VencidoAsync(IEvent evento, VersionVencida vencida, IContextoDeEventos contexto, long orden,
        CancellationToken ct)
    {
        var documento = await contexto.DocumentoAsync(evento.StreamId, ct);
        var destino = documento?.Comparticiones.LastOrDefault(c => c.VinculoId == vencida.VinculoId);
        if (documento is null || destino is null || await contexto.VinculoAsync(destino.TenantId, vencida.VinculoId, ct) is not { } vinculo)
            return PlanDeNotificaciones.Vacio;

        var empresa = await contexto.EmpresaAsync(vinculo.EmpresaId, ct);
        List<object> alTercero = string.IsNullOrWhiteSpace(vinculo.CorreoTercero)
            ? []
            : [new CorreoDeVencidoPendiente(vinculo.CorreoTercero, empresa?.Nombre ?? "La empresa", documento.NombreTipo, vencida.VigenteHasta,
                destino.TenantId, vinculo.EmpresaId, vinculo.TerceroId)];

        var expedienteId = vinculo.ExpedienteAbiertoId ?? vinculo.UltimoExpedienteId;
        var expediente = expedienteId is { } id ? await contexto.ExpedienteAsync(destino.TenantId, id, ct) : null;
        if (expediente is null)
            return new PlanDeNotificaciones([], alTercero);

        var areas = (await contexto.AreasAsync(destino.TenantId, expediente.AreaIds, ct)).Where(a => a.EmpresaId == vinculo.EmpresaId).ToList();
        var mensaje = $"El documento {documento.NombreTipo} de {expediente.TerceroNombre} venció el {vencida.VigenteHasta:yyyy-MM-dd}.";
        var plan = await APersonasAsync(evento, contexto, destino.TenantId, vinculo.EmpresaId, empresa?.Nombre, TiposNotificacion.Vencido,
            "Documento vencido", mensaje, expediente.Id, vinculo.Id, expediente.Folio, areas.Select(a => (a.Nombre, a.Miembros)).ToList(),
            orden, ct, $"/e/{vinculo.EmpresaId}/vinculos/{vinculo.Id}");
        return plan with { Correos = [.. plan.Correos, .. alTercero] };
    }

    /// <summary>
    /// Una notificación por persona (aunque esté en varias áreas) y, si su tipo va por correo y no lo apagó, un solo correo
    /// por persona que nombra todas sus áreas (consolidado, NW-9).
    /// </summary>
    private static async Task<PlanDeNotificaciones> APersonasAsync(IEvent evento, IContextoDeEventos contexto, Guid tenantId, Guid empresaId,
        string? empresa, string tipo, string titulo, string mensaje, Guid expedienteId, Guid vinculoId, string folio,
        IReadOnlyList<(string Area, List<Guid> Miembros)> areas, long orden, CancellationToken ct, string? ruta = null)
    {
        var porPersona = areas.SelectMany(a => a.Miembros.Select(m => (Usuario: m, a.Area))).GroupBy(x => x.Usuario)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Area).Distinct().Order(StringComparer.Ordinal).ToList());
        var usuarios = (await contexto.UsuariosAsync(tenantId, porPersona.Keys, ct)).Where(u => u.Activo && u.TieneRolEn(empresaId)).ToList();

        ruta ??= $"/e/{empresaId}/expedientes/{expedienteId}";
        var campana = new List<(Guid, Notificacion)>();
        var correos = new List<object>();
        foreach (var usuario in usuarios.OrderBy(u => u.Id))
        {
            campana.Add((tenantId, new Notificacion
            {
                Id = Deterministas.Guid($"notificacion:{evento.Id:N}:{usuario.Id:N}:{tipo}"),
                UsuarioId = usuario.Id,
                EmpresaId = empresaId,
                Tipo = tipo,
                Titulo = titulo,
                Mensaje = $"{folio}: {mensaje}",
                ExpedienteId = expedienteId,
                VinculoId = vinculoId,
                Folio = folio,
                Enlace = ruta,
                CreadaEn = evento.Timestamp,
                Orden = orden
            }));

            if (TiposNotificacion.QuiereCorreo(await contexto.PreferenciasAsync(tenantId, usuario.Id, ct), tipo))
            {
                var areasDe = porPersona[usuario.Id];
                correos.Add(new CorreoDeNotificacionPendiente(usuario.Correo, usuario.Nombre, $"{titulo}: {folio}",
                [
                    $"{mensaje}",
                    // «de Constructora Sur S.A.S.» ya termina en punto: no se agrega otro («S.A.S..»).
                    ConPunto($"Expediente {folio}{(empresa is null ? string.Empty : $" de {empresa}")}"),
                    (areasDe.Count == 1 ? "Tu área: " : "Tus áreas: ") + string.Join(", ", areasDe) + "."
                ], ruta));
            }
        }

        return new PlanDeNotificaciones(campana, correos);
    }

    private static string ConPunto(string frase) => frase.EndsWith('.') ? frase : frase + ".";

    /// <summary>
    /// Terminó un lote de invitación masiva (docs/05 §8, plantilla <c>lote</c>, LT-9): campana y correo a quien lo creó, si es
    /// una persona del tenant, con los totales y el enlace al detalle del lote.
    /// </summary>
    private static async Task<PlanDeNotificaciones> LoteTerminadoAsync(IEvent evento, Dominio.Invitaciones.LoteTerminado terminado,
        IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.LoteAsync(tenantId, evento.StreamId, ct) is not { } lote ||
            lote.CreadoPor.Tipo != Dominio.Comun.ActorTipo.Usuario || !Guid.TryParse(lote.CreadoPor.Id, out var usuarioId))
            return PlanDeNotificaciones.Vacio;
        if ((await contexto.UsuariosAsync(tenantId, [usuarioId], ct)).FirstOrDefault(u => u.Activo && u.TieneRolEn(lote.EmpresaId)) is not { } usuario)
            return PlanDeNotificaciones.Vacio;

        var t = terminado.Totales;
        var titulo = t.Fallidas > 0 ? "Lote de invitaciones terminado con errores" : "Lote de invitaciones terminado";
        var partes = new List<string> { $"{t.Exitosas} de {t.Filas} filas invitadas" };
        if (t.Fallidas > 0)
            partes.Add($"{t.Fallidas} fallidas");
        if (t.Invalidas > 0)
            partes.Add($"{t.Invalidas} inválidas");
        if (t.Duplicadas > 0)
            partes.Add($"{t.Duplicadas} duplicadas");
        var mensaje = $"«{lote.Nombre}»: {string.Join(", ", partes)}.";
        var ruta = $"/e/{lote.EmpresaId}/invitaciones/lotes/{lote.Id}";
        var notificacion = new Notificacion
        {
            Id = Deterministas.Guid($"notificacion:{evento.Id:N}:{usuario.Id:N}:{TiposNotificacion.LoteTerminado}"),
            UsuarioId = usuario.Id,
            EmpresaId = lote.EmpresaId,
            Tipo = TiposNotificacion.LoteTerminado,
            Titulo = titulo,
            Mensaje = mensaje,
            Enlace = ruta,
            CreadaEn = evento.Timestamp,
            Orden = orden
        };
        List<object> correos = TiposNotificacion.QuiereCorreo(await contexto.PreferenciasAsync(tenantId, usuario.Id, ct), TiposNotificacion.LoteTerminado)
            ? [new CorreoDeNotificacionPendiente(usuario.Correo, usuario.Nombre, titulo, [mensaje], ruta)]
            : [];
        return new PlanDeNotificaciones([(tenantId, notificacion)], correos);
    }

    private static async Task<PlanDeNotificaciones> ConstanciaAsync(IEvent evento, ConsentimientosRegistrados consentimientos, IContextoDeEventos contexto,
        CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo)
            return PlanDeNotificaciones.Vacio;

        var para = string.IsNullOrWhiteSpace(consentimientos.Evidencia.Correo) ? vinculo.CorreoTercero : consentimientos.Evidencia.Correo;
        if (string.IsNullOrWhiteSpace(para))
            return PlanDeNotificaciones.Vacio;

        var empresa = await contexto.EmpresaAsync(vinculo.EmpresaId, ct);
        var textos = (await contexto.TextosLegalesAsync([.. consentimientos.Consentimientos.Select(c => c.TextoLegalId)], ct)).ToDictionary(t => t.Id);
        var lista = consentimientos.Consentimientos
            .Select(c => new TextoAceptadoParaCorreo(Titulo(c.Tipo, empresa?.Nombre), c.Version,
                textos.TryGetValue(c.TextoLegalId, out var texto) ? texto.Hash : c.Hash))
            .ToList();
        return new PlanDeNotificaciones([], [new CorreoDeConstanciaPendiente(para, empresa?.Nombre ?? "La empresa", lista, consentimientos.Fecha, tenantId,
            vinculo.EmpresaId)]);
    }

    private static string Titulo(TipoTextoLegal tipo, string? empresa) => tipo switch
    {
        TipoTextoLegal.AvisoPlataforma => "Aviso de privacidad de Validasoft",
        TipoTextoLegal.AutorizacionEmpresa => $"Autorización de tratamiento de datos de {empresa ?? "la empresa"}",
        _ => $"Términos y condiciones de {empresa ?? "la empresa"}"
    };

    /// <summary>Aviso a los administradores del tenant cuando un webhook se deshabilita por fallos (NW-7).</summary>
    public static PlanDeNotificaciones WebhookDeshabilitado(Guid tenantId, SuscripcionWebhook suscripcion, IEnumerable<Usuario> usuarios,
        Func<Guid, PreferenciasNotificacion?> preferencias, DateTimeOffset ahora, long orden)
    {
        var administradores = usuarios.Where(u => u.Activo && u.Roles.Any(r => r.Rol == Rol.Administrador)).ToList();
        var mensaje = $"La suscripción {suscripcion.Url} del sistema {suscripcion.Sistema} se deshabilitó tras {suscripcion.FallosSeguidos} " +
                      "entregas fallidas seguidas. Revisa el receptor, reactívala y reenvía las entregas fallidas.";
        var campana = administradores.Select(u => (tenantId, new Notificacion
        {
            Id = Deterministas.Guid($"notificacion:webhook:{suscripcion.Id:N}:{ahora.UtcTicks}:{u.Id:N}"),
            UsuarioId = u.Id,
            Tipo = TiposNotificacion.WebhookDeshabilitado,
            Titulo = "Webhook deshabilitado",
            Mensaje = mensaje,
            CreadaEn = ahora,
            Orden = orden
        })).ToList();
        var correos = administradores.Where(u => TiposNotificacion.QuiereCorreo(preferencias(u.Id), TiposNotificacion.WebhookDeshabilitado))
            .Select(u => (object)new CorreoDeNotificacionPendiente(u.Correo, u.Nombre, "Webhook deshabilitado: " + suscripcion.Sistema, [mensaje], null))
            .ToList();
        return new PlanDeNotificaciones(campana, correos);
    }

    /// <summary>
    /// Aviso a los administradores de la empresa (rol de todo el tenant o de esa empresa) cuando consume el 80 % o el 100 %
    /// de sus validaciones con IA incluidas del mes (IA-4): campana y correo, uno por umbral y mes (id determinista).
    /// </summary>
    public static PlanDeNotificaciones CupoDeIa(Guid tenantId, Guid empresaId, string? empresa, int umbral, string mes, int consumidas, int incluidas,
        bool adicionalHabilitado, IEnumerable<Usuario> usuarios, Func<Guid, PreferenciasNotificacion?> preferencias, DateTimeOffset ahora, long orden)
    {
        var administradores = usuarios
            .Where(u => u.Activo && u.Roles.Any(r => r.Rol == Rol.Administrador && (r.EmpresaId is null || r.EmpresaId == empresaId)))
            .OrderBy(u => u.Id)
            .ToList();
        var titulo = umbral >= 100 ? "Validaciones con IA agotadas" : $"Validaciones con IA al {umbral} %";
        var siguiente = umbral < 100
            ? "Cuando se agoten, "
              + (adicionalHabilitado ? "las siguientes se cobran como adicionales." : "los documentos nuevos quedan sin análisis automático, para revisión humana.")
            : adicionalHabilitado
                ? "Las siguientes se cobran como adicionales."
                : "Los documentos nuevos quedan sin análisis automático, para revisión humana, hasta el próximo mes.";
        var mensaje = $"{empresa ?? "La empresa"} usó {consumidas} de {incluidas} validaciones con IA incluidas en {mes}. {siguiente}";
        var ruta = $"/e/{empresaId}/uso-ia";
        var campana = administradores.Select(u => (tenantId, new Notificacion
        {
            Id = Deterministas.Guid($"notificacion:ia-cupo:{empresaId:N}:{mes}:{umbral}:{u.Id:N}"),
            UsuarioId = u.Id,
            EmpresaId = empresaId,
            Tipo = TiposNotificacion.CupoIa,
            Titulo = titulo,
            Mensaje = mensaje,
            Enlace = ruta,
            CreadaEn = ahora,
            Orden = orden
        })).ToList();
        var correos = administradores.Where(u => TiposNotificacion.QuiereCorreo(preferencias(u.Id), TiposNotificacion.CupoIa))
            .Select(u => (object)new CorreoDeNotificacionPendiente(u.Correo, u.Nombre, $"{titulo}: {empresa ?? mes}", [mensaje], ruta))
            .ToList();
        return new PlanDeNotificaciones(campana, correos);
    }
}

public static class CorreoDeNotificacionPendienteHandler
{
    public static Task Handle(CorreoDeNotificacionPendiente correo, Comun.IEnviadorDeCorreo enviador, Comun.RedactorDeCorreos redactor, CancellationToken ct) =>
        enviador.EnviarAsync(redactor.RedactarNotificacion(correo), ct);
}

/// <summary>
/// Aviso al tercero de que un documento venció para una empresa (plantilla <c>vencido</c>, docs/05 §8): pasó su vigencia y
/// la gracia. Cola <c>correo</c>.
/// </summary>
public sealed record CorreoDeVencidoPendiente(string Para, string Empresa, string Documento, DateOnly VigenteHasta, Guid? TenantId = null,
    Guid? EmpresaId = null, Guid? TerceroId = null);

/// <summary>
/// El certificado del tercero dejó de valer: revocado (definitivo) o suspendido (mientras la empresa lo revisa). Cola
/// <c>correo</c>.
/// </summary>
public sealed record CorreoDeCertificadoPendiente(string Para, string Empresa, string Folio, bool Revocado, DateTimeOffset Fecha, Guid? TenantId = null,
    Guid? EmpresaId = null, Guid? TerceroId = null);

// Los correos al tercero de una empresa llevan la plantilla de esa empresa (PL-1) si la tiene.
public static class CorreoDeCertificadoPendienteHandler
{
    public static async Task Handle(CorreoDeCertificadoPendiente correo, Comun.IEnviadorDeCorreo enviador, Comun.RedactorDeCorreos redactor,
        Comun.PlantillasDeCorreo plantillas, PreferenciasDeCorreoDelTercero preferencias, CancellationToken ct)
    {
        if (!await preferencias.QuiereAsync(correo.TerceroId, TiposNotificacionTercero.Certificado, ct))
            return;
        await enviador.EnviarAsync(await plantillas.AplicarAsync(redactor.RedactarCertificado(correo), CatalogoDePlantillas.Certificado,
            correo.TenantId, correo.EmpresaId, Comun.EjemplosDeCorreo.VariablesDe(correo.Empresa, correo.Folio), ct), ct);
    }
}

public static class CorreoDeVencidoPendienteHandler
{
    public static async Task Handle(CorreoDeVencidoPendiente correo, Comun.IEnviadorDeCorreo enviador, Comun.RedactorDeCorreos redactor,
        Comun.PlantillasDeCorreo plantillas, PreferenciasDeCorreoDelTercero preferencias, CancellationToken ct)
    {
        if (!await preferencias.QuiereAsync(correo.TerceroId, TiposNotificacionTercero.DocumentoVencido, ct))
            return;
        await enviador.EnviarAsync(await plantillas.AplicarAsync(redactor.RedactarVencido(correo), CatalogoDePlantillas.Vencido,
            correo.TenantId, correo.EmpresaId, Comun.EjemplosDeCorreo.VariablesDe(correo.Empresa, documento: correo.Documento), ct), ct);
    }
}

public static class CorreoDeConstanciaPendienteHandler
{
    public static async Task Handle(CorreoDeConstanciaPendiente correo, Comun.IEnviadorDeCorreo enviador, Comun.RedactorDeCorreos redactor,
        Comun.PlantillasDeCorreo plantillas, CancellationToken ct) =>
        await enviador.EnviarAsync(await plantillas.AplicarAsync(redactor.RedactarConstancia(correo), CatalogoDePlantillas.ConsentimientoEmpresa,
            correo.TenantId, correo.EmpresaId, Comun.EjemplosDeCorreo.VariablesDe(correo.Empresa), ct), ct);
}
