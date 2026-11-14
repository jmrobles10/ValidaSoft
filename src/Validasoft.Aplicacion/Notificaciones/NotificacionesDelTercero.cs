using System.Globalization;
using System.Text.Json;
using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Notificaciones;

// Campana del tercero (NT-1, docs/05 §8.5). El tercero se entera en su portal de lo mismo que hoy le llega por correo —y de lo que
// no tenía cómo enterarse— con la misma notificación que ve el equipo de una empresa (`Notificacion`), pero cuyo destinatario es el
// tercero. Cada una vive en el tenant de la empresa que la origina (la privacidad entre empresas sale del propio dato) y el
// tercero las lee entre tenants, solo las de los vínculos de su sesión.

/// <summary>
/// Tipos de notificación al tercero y cuáles pueden ir además por correo. La campana nunca se apaga; el correo sí, por tipo
/// (<see cref="PreferenciasDelTercero"/>). Invitación, actualización y propuesta solo van por la campana: sus correos, si existen,
/// llevan el enlace de acceso y no se pueden apagar.
/// </summary>
public static class TiposNotificacionTercero
{
    /// <summary>La empresa devolvió la validación con observaciones por corregir.</summary>
    public const string Observado = "tercero.observado";

    /// <summary>La empresa le recuerda las observaciones que siguen pendientes (a mano o automático, NT-2).</summary>
    public const string Recordatorio = "tercero.recordatorio";

    /// <summary>
    /// Hay una actualización abierta (o ampliada). Si la abrió la empresa para pedirle información, dice qué hay por actualizar; si la abrió la
    /// propia versión nueva del tercero (lo habitual), le confirma que la recibió y que la empresa la está revisando (VR-12). Solo campana.
    /// </summary>
    public const string Actualizacion = "tercero.actualizacion";

    /// <summary>La validación se cerró: aprobada (con certificado) o rechazada.</summary>
    public const string Resultado = "tercero.resultado";

    public const string DocumentoPorVencer = "tercero.documento_por_vencer";
    public const string DocumentoVencido = "tercero.documento_vencido";

    /// <summary>El certificado se revocó o se suspendió.</summary>
    public const string Certificado = "tercero.certificado";

    /// <summary>Recordatorio del plazo de cargue, caducidad o reapertura del expediente.</summary>
    public const string PlazoDeCargue = "tercero.plazo_de_cargue";

    /// <summary>La lectura de un documento propone cambios para su información (no es de ninguna empresa).</summary>
    public const string Propuesta = "tercero.propuesta";

    /// <summary>Una empresa lo invitó a validar su información.</summary>
    public const string Invitacion = "tercero.invitacion";

    /// <summary>La empresa aprobó o rechazó la cuenta bancaria que designó para cobrarle (solo campana).</summary>
    public const string Cuenta = "tercero.cuenta";

    /// <summary>La validación con una empresa está por vencer, venció o la empresa abrió su renovación (VV, campañas de renovación, OP-1).</summary>
    public const string Vigencia = "tercero.vigencia";

    /// <summary>La empresa devolvió a revisión una validación que ya estaba aprobada (desaprobar, DS-2).</summary>
    public const string Desaprobado = "tercero.desaprobado";

    /// <summary>
    /// Una empresa invitó a un correo que no es contacto conocido del tercero (acceso aislado): un contacto debe aprobarlo o rechazarlo. Solo campana (el
    /// correo, con el enlace para decidir, sale al invitar) y solo para quienes ya son contacto: la sesión aislada no la ve.
    /// </summary>
    public const string AccesoSolicitado = "tercero.acceso_solicitado";

    /// <summary>La empresa reintegró su vínculo, con o sin validar de nuevo (DS-5).</summary>
    public const string Reintegrado = "tercero.reintegrado";

    public static readonly IReadOnlyList<string> Todos =
        [Observado, Recordatorio, Actualizacion, Resultado, DocumentoPorVencer, DocumentoVencido, Certificado, PlazoDeCargue, Propuesta, Invitacion, Cuenta,
            Desaprobado, Reintegrado, Vigencia, AccesoSolicitado];

    /// <summary>Los que además de la campana van por correo (si el tercero no lo apagó).</summary>
    public static readonly IReadOnlyList<string> ConCorreo =
        [Observado, Recordatorio, Resultado, DocumentoPorVencer, DocumentoVencido, Certificado, PlazoDeCargue, Desaprobado, Reintegrado, Vigencia];

    public static bool QuiereCorreo(PreferenciasDelTercero? preferencias, string tipo) =>
        ConCorreo.Contains(tipo) && (preferencias is null || !preferencias.Correo.TryGetValue(tipo, out var valor) || valor);
}

/// <summary>
/// Qué correos quiere recibir un tercero, por tipo (id = tercero). Global como el propio tercero: no es de una empresa ni de un
/// grupo, vale para todas con las que se relaciona. Sin documento, todo va por correo.
/// </summary>
public sealed class PreferenciasDelTercero
{
    public Guid Id { get; set; }

    public Dictionary<string, bool> Correo { get; set; } = [];

    public DateTimeOffset ActualizadasEn { get; set; }
}

/// <summary>Decide al enviar un correo al tercero si lo apagó: sin tercero conocido (mensajes ya encolados) se envía.</summary>
public sealed class PreferenciasDeCorreoDelTercero(ISesiones sesiones)
{
    public async Task<bool> QuiereAsync(Guid? terceroId, string tipo, CancellationToken ct)
    {
        if (terceroId is not { } id)
            return true;
        await using var consulta = sesiones.ConsultaGlobal();
        return TiposNotificacionTercero.QuiereCorreo(await consulta.LoadAsync<PreferenciasDelTercero>(id, ct), tipo);
    }
}

/// <summary>
/// Decide qué se entera el tercero de cada hecho (NT-1): observaciones y su recordatorio, actualizaciones que pide la empresa,
/// resultado, documentos por vencer o vencidos, certificado revocado o suspendido, plazo de cargue, propuestas desde un documento
/// e invitaciones. Solo campana: los correos los siguen mandando sus disparadores de siempre. Cada notificación es de la empresa
/// que la origina (su tenant, su marca) y sin nombres de revisores ni de áreas.
/// </summary>
public static class PlanificadorDeNotificacionesDelTercero
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public static async Task<PlanDeNotificaciones> PlanearAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        switch (evento.Data)
        {
            case ExpedienteObservado:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) =>
                {
                    var n = e.ObservacionesVigentes.Count;
                    var cuantas = n switch { 0 => "algo", 1 => "1 punto", _ => $"{n} puntos" };
                    return (TiposNotificacionTercero.Observado, "Tienes observaciones por corregir",
                        $"{empresa} revisó tu validación {e.Folio} y te pidió corregir {cuantas}.");
                });
            case RecordatorioDeObservacionesEnviado recordatorio:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) => (TiposNotificacionTercero.Recordatorio,
                    "Recordatorio: observaciones pendientes",
                    $"{empresa} te recuerda que tu validación {e.Folio} " +
                    (recordatorio.DiasObservado > 0 ? $"lleva {Dias(recordatorio.DiasObservado)} con observaciones por corregir." : "tiene observaciones por corregir.")));
            case ExpedienteAbierto { Tipo: TipoExpediente.Renovacion, AbiertoPor.Tipo: not ActorTipo.Sistema }:
                // La empresa abrió la renovación a mano (campaña de renovación, OP-1): lo del sistema (vigencia, vencimiento) ya avisa por su evento.
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) => (TiposNotificacionTercero.Vigencia,
                    "Tu empresa abrió tu renovación", $"{empresa} abrió la renovación de tu validación {e.Folio}: ratifica o actualiza tu información."));
            case ExpedienteAbierto { Tipo: TipoExpediente.Actualizacion } abierta:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) => PidioLaEmpresa(abierta.AbiertoPor)
                    ? (TiposNotificacionTercero.Actualizacion,
                        "Hay información por actualizar", $"{empresa} necesita que actualices {Lista(e.Requisitos.Select(r => r.Nombre).ToList(), "tu información")}.")
                    : (TiposNotificacionTercero.Actualizacion,
                        "Recibimos tu actualización",
                        $"Recibimos tu actualización{De(Lista(e.Requisitos.Select(r => r.Nombre).ToList(), string.Empty))}; {empresa} la está revisando y te avisamos si necesita algo."));
            case RequisitosAgregados agregados:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) =>
                {
                    var nuevos = Lista([.. e.Requisitos.Where(r => agregados.Requisitos.Any(a => a.RequisitoId == r.Id)).Select(r => r.Nombre)], "más información");
                    return PidioLaEmpresa(e.AbiertoPor)
                        ? (TiposNotificacionTercero.Actualizacion, "Se sumó algo a tu actualización", $"{empresa} sumó {nuevos} a la actualización que tienes abierta.")
                        : (TiposNotificacionTercero.Actualizacion, "Sumamos algo a tu actualización", $"{empresa} también está revisando {nuevos}, que actualizaste después.");
                });
            case ExpedienteAprobado:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) => (TiposNotificacionTercero.Resultado,
                    "Validación aprobada", $"{empresa} aprobó tu validación {e.Folio}: tu vínculo está activo y ya tienes tu certificado."));
            case ExpedienteDesaprobado:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) => (TiposNotificacionTercero.Desaprobado,
                    "Tu validación volvió a revisión",
                    $"{empresa} volvió a revisar tu validación {e.Folio}: tu vínculo queda en validación mientras la revisa y te avisamos cuando termine."));
            case VinculoReintegrado reintegrado:
                return await DeReintegroAsync(evento, reintegrado, contexto, orden, ct);
            case ExpedienteRechazado rechazado:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) =>
                {
                    var motivos = RedactorDeCorreos.UnirMotivos(rechazado.Motivos.Select(m => m.TextoAlTercero));
                    // Una actualización rechazada deja el vínculo como estaba (RV-2): lo ya aprobado sigue vigente (VR-13).
                    return e.Tipo == TipoExpediente.Actualizacion
                        ? (TiposNotificacionTercero.Resultado, "Tu actualización no se aprobó",
                            $"{empresa} no aprobó tu actualización {e.Folio}; lo que ya tenías aprobado sigue vigente." + (motivos.Length == 0 ? string.Empty : $" Motivos: {motivos}"))
                        : (TiposNotificacionTercero.Resultado, "Validación cerrada sin aprobar",
                            $"{empresa} cerró tu validación {e.Folio} sin aprobarla." + (motivos.Length == 0 ? string.Empty : $" Motivos: {motivos}"));
                });
            case RecordatorioDeCargueEnviado recordatorio:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) => (TiposNotificacionTercero.PlazoDeCargue,
                    "Se acerca el plazo de tu validación",
                    $"Te quedan {Dias(Math.Max(1, (int)Math.Ceiling((recordatorio.PlazoHasta - recordatorio.Fecha).TotalDays)))} para completar tu validación " +
                    $"{e.Folio} con {empresa}."));
            case ExpedienteCaducado:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) => (TiposNotificacionTercero.PlazoDeCargue,
                    "Tu validación caducó", $"El plazo de tu validación {e.Folio} con {empresa} venció. Escríbele a la empresa si quieres continuar."));
            case ExpedienteReabierto reabierto:
                return await DeExpedienteAsync(evento, contexto, orden, ct, (e, empresa) => (TiposNotificacionTercero.PlazoDeCargue,
                    "Reabrieron tu validación",
                    $"{empresa} reabrió tu validación {e.Folio}. Tienes hasta el {Fecha(reabierto.PlazoHasta)} para completarla."));
            case VencimientoAvisado aviso:
                return await DeDocumentoAsync(evento, contexto, aviso.VinculoId, orden, ct, (documento, empresa) => (TiposNotificacionTercero.DocumentoPorVencer,
                    "Documento por vencer",
                    $"Tu {documento} vence el {Fecha(aviso.VigenteHasta)} para {empresa} ({Dias(aviso.DiasRestantes)})."));
            case VersionVencida vencida:
                return await DeDocumentoAsync(evento, contexto, vencida.VinculoId, orden, ct, (documento, empresa) => (TiposNotificacionTercero.DocumentoVencido,
                    "Documento vencido",
                    $"Tu {documento} venció para {empresa} el {Fecha(vencida.VigenteHasta)}. Sube la versión nueva para seguir al día."));
            case CertificadoRevocado revocado:
                return await DeCertificadoAsync(evento, contexto, revocado.Emision, revocado: true, orden, ct);
            case CertificadoSuspendido suspendido:
                return await DeCertificadoAsync(evento, contexto, suspendido.Emision, revocado: false, orden, ct);
            case VinculoPorVencer porVencer:
                return await DeLaVigenciaAsync(evento, contexto, orden, ct, "Tu validación está por vencer",
                    (empresa, vinculo) => $"La validación con {empresa} vence el {Fecha(porVencer.VigenteHasta)} ({Dias(porVencer.DiasRestantes)}). Ratifica o actualiza tu información antes de esa fecha.");
            case VinculoVencido { Causa: CausaDeVencimiento.Vigencia } vencido:
                return await DeLaVigenciaAsync(evento, contexto, orden, ct, "Tu validación venció",
                    (empresa, vinculo) => $"La validación con {empresa} venció. Ratifica o actualiza tu información para volver a estar al día.");
            case InvitacionEnviada:
                return await DeInvitacionAsync(evento, contexto, orden, ct);
            case AccesoAisladoSolicitado solicitado:
                return await DeAccesoSolicitadoAsync(evento, solicitado, contexto, orden, ct);
            case CuentaDesignadaAprobada aprobada:
                return await DeLaCuentaAsync(evento, contexto, orden, ct, "Cuenta aprobada",
                    empresa => $"{empresa} aprobó la cuenta {Descripcion(aprobada.Cuenta)} con la que le cobras.");
            case CuentaDesignadaRechazada rechazada:
                return await DeLaCuentaAsync(evento, contexto, orden, ct, "Cuenta rechazada",
                    empresa => $"{empresa} rechazó la cuenta {Descripcion(rechazada.Cuenta)}: {rechazada.Motivo.TrimEnd('.')}. Elige otra cuenta para esta empresa.");
            case AnalisisDeIaRegistrado analisis:
                return await PropuestaAsync(evento, analisis, contexto, orden, ct);
            default:
                return PlanDeNotificaciones.Vacio;
        }
    }

    /// <summary>Notificación de un hecho del expediente (la ruta del tercero es <c>/expediente/{id}</c>).</summary>
    private static async Task<PlanDeNotificaciones> DeExpedienteAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct,
        Func<Expediente, string, (string Tipo, string Titulo, string Mensaje)> redactar)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct) is not { } expediente ||
            expediente.TerceroId == Guid.Empty)
            return PlanDeNotificaciones.Vacio;

        var empresa = (await contexto.EmpresaAsync(expediente.EmpresaId, ct))?.Nombre ?? "La empresa";
        var (tipo, titulo, mensaje) = redactar(expediente, empresa);
        return Plan(Nueva(evento, tenantId, expediente.TerceroId, expediente.EmpresaId, expediente.VinculoId, expediente.Id, expediente.Folio, tipo, titulo,
            mensaje, $"/expediente/{expediente.Id}", orden), tenantId);
    }

    /// <summary>
    /// La empresa reintegró el vínculo (DS-5): sin revalidar queda como estaba y su certificado se emite de nuevo; revalidando, vuelve a
    /// validación con una renovación abierta (hay que subir los documentos) o, si nunca se aprobó, a la invitación. El enlace lleva a
    /// lo que hay que hacer. Nunca lleva la justificación (es interna).
    /// </summary>
    private static async Task<PlanDeNotificaciones> DeReintegroAsync(IEvent evento, VinculoReintegrado reintegrado, IContextoDeEventos contexto, long orden,
        CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo ||
            vinculo.TerceroId == Guid.Empty)
            return PlanDeNotificaciones.Vacio;

        var empresa = (await contexto.EmpresaAsync(vinculo.EmpresaId, ct))?.Nombre ?? "La empresa";
        var expedienteId = reintegrado.SinRevalidar ? vinculo.UltimoExpedienteId : vinculo.ExpedienteAbiertoId;
        var expediente = expedienteId is { } id ? await contexto.ExpedienteAsync(tenantId, id, ct) : null;
        var (titulo, mensaje, enlace) = (reintegrado.SinRevalidar, reintegrado.EstadoResultante, expediente) switch
        {
            (true, _, _) => ("Tu vínculo se reintegró",
                $"{empresa} reintegró tu vínculo sin pedirte documentos nuevos: quedó como estaba antes y tu certificado se emitió de nuevo.", "/empresas"),
            (false, EstadoVinculo.EnValidacion, { } abierto) => ("Tu vínculo se reintegró: falta validar",
                $"{empresa} reintegró tu vínculo y abrió una nueva validación ({abierto.Folio}): sube o confirma tus documentos para completarla.",
                $"/expediente/{abierto.Id}"),
            _ => ("Tu vínculo se reintegró: falta validar",
                $"{empresa} reintegró tu vínculo: completa la validación de tu información para dejarlo activo.", $"/empresas/{vinculo.Id}")
        };
        return Plan(Nueva(evento, tenantId, vinculo.TerceroId, vinculo.EmpresaId, vinculo.Id, expediente?.Id, expediente?.Folio, TiposNotificacionTercero.Reintegrado,
            titulo, mensaje, enlace, orden), tenantId);
    }

    /// <summary>Notificación de un hecho de un documento de la bóveda para uno de sus vínculos: va al tenant de ese vínculo.</summary>
    private static async Task<PlanDeNotificaciones> DeDocumentoAsync(IEvent evento, IContextoDeEventos contexto, Guid vinculoId, long orden, CancellationToken ct,
        Func<string, string, (string Tipo, string Titulo, string Mensaje)> redactar)
    {
        var documento = await contexto.DocumentoAsync(evento.StreamId, ct);
        var destino = documento?.Comparticiones.LastOrDefault(c => c.VinculoId == vinculoId);
        if (documento is null || destino is null || await contexto.VinculoAsync(destino.TenantId, vinculoId, ct) is not { } vinculo)
            return PlanDeNotificaciones.Vacio;

        var empresa = (await contexto.EmpresaAsync(vinculo.EmpresaId, ct))?.Nombre ?? "la empresa";
        var (tipo, titulo, mensaje) = redactar(documento.NombreTipo, empresa);
        return Plan(Nueva(evento, destino.TenantId, vinculo.TerceroId, vinculo.EmpresaId, vinculo.Id, null, null, tipo, titulo, mensaje, "/boveda", orden),
            destino.TenantId);
    }

    private static async Task<PlanDeNotificaciones> DeCertificadoAsync(IEvent evento, IContextoDeEventos contexto, int emision, bool revocado, long orden,
        CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.CertificadoAsync(tenantId, evento.StreamId, ct) is not { } certificado ||
            certificado.Emisiones.FirstOrDefault(e => e.Numero == emision) is not { } emitida)
            return PlanDeNotificaciones.Vacio;

        var folio = emitida.Folio;

        var empresa = (await contexto.EmpresaAsync(certificado.EmpresaId, ct))?.Nombre ?? "La empresa";
        var (titulo, mensaje) = revocado
            ? ("Certificado revocado", $"{empresa} revocó tu certificado {folio}: ya no es válido y no puede volver a usarse.")
            : ("Certificado suspendido", $"{empresa} suspendió tu certificado {folio} mientras revisa tu vínculo.");
        return Plan(Nueva(evento, tenantId, certificado.TerceroId, certificado.EmpresaId, certificado.VinculoId, emitida.ExpedienteId, folio,
            TiposNotificacionTercero.Certificado, titulo, mensaje, "/empresas", orden), tenantId);
    }

    /// <summary>La empresa decidió la cuenta que el tercero designó (CB-2): aviso en la campana, que lleva a «Mis cuentas».</summary>
    private static async Task<PlanDeNotificaciones> DeLaCuentaAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct, string titulo,
        Func<string, string> mensaje)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo ||
            vinculo.TerceroId == Guid.Empty)
            return PlanDeNotificaciones.Vacio;

        var empresa = (await contexto.EmpresaAsync(vinculo.EmpresaId, ct))?.Nombre ?? "La empresa";
        return Plan(Nueva(evento, tenantId, vinculo.TerceroId, vinculo.EmpresaId, vinculo.Id, null, null, TiposNotificacionTercero.Cuenta, titulo,
            mensaje(empresa), "/cuentas", orden), tenantId);
    }

    private static string Descripcion(CuentaParaDesignar? cuenta) =>
        cuenta is null ? "designada" : $"{cuenta.Banco} {Dominio.Terceros.CuentaBancaria.Enmascarar(cuenta.Ultimos4)}";

    /// <summary>
    /// La validación del tercero con una empresa está por vencer o venció por su vigencia (VV-3): aviso en su campana (el correo con el enlace lo manda el
    /// barrido de vigencias). El enlace lleva a la renovación abierta o, si no hay, a la empresa.
    /// </summary>
    private static async Task<PlanDeNotificaciones> DeLaVigenciaAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct, string titulo,
        Func<string, Vinculo, string> mensaje)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo ||
            vinculo.TerceroId == Guid.Empty)
            return PlanDeNotificaciones.Vacio;

        var empresa = (await contexto.EmpresaAsync(vinculo.EmpresaId, ct))?.Nombre ?? "La empresa";
        var expediente = vinculo.ExpedienteAbiertoId is { } id ? await contexto.ExpedienteAsync(tenantId, id, ct) : null;
        return Plan(Nueva(evento, tenantId, vinculo.TerceroId, vinculo.EmpresaId, vinculo.Id, expediente?.Id, expediente?.Folio, TiposNotificacionTercero.Vigencia,
            titulo, mensaje(empresa, vinculo), expediente is null ? $"/empresas/{vinculo.Id}" : $"/expediente/{expediente.Id}", orden), tenantId);
    }

    /// <summary>
    /// Una empresa invitó a un correo que no es contacto conocido del tercero (acceso aislado): los contactos ven el aviso en su campana y deciden desde su cuenta
    /// (<c>/accesos</c>). Va <b>sin vínculo</b> —el vínculo es de la persona aislada— para que lo vea el tercero entero y no la sesión aislada. Si ya se decidió
    /// (un correo que se había rechazado nace rechazado) no avisa.
    /// </summary>
    private static async Task<PlanDeNotificaciones> DeAccesoSolicitadoAsync(IEvent evento, AccesoAisladoSolicitado solicitado, IContextoDeEventos contexto, long orden,
        CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo ||
            vinculo.TerceroId == Guid.Empty || vinculo.AccesoAislado is not EstadoAccesoAislado.Pendiente)
            return PlanDeNotificaciones.Vacio;

        var empresa = (await contexto.EmpresaAsync(vinculo.EmpresaId, ct))?.Nombre ?? "Una empresa";
        // Un contacto sin verificar desplazado por uno verificado, o una solicitud que decide la plataforma porque ningún contacto está verificado, dicen otra cosa.
        if (solicitado.Motivo == Dominio.Terceros.SolicitudDeAcceso.OrigenContactoDesplazado)
            return Plan(Nueva(evento, tenantId, vinculo.TerceroId, vinculo.EmpresaId, null, null, null, TiposNotificacionTercero.AccesoSolicitado,
                "Un contacto pasó a acceso aislado",
                $"{solicitado.Correo} estaba sin verificar y otro contacto de tu empresa se verificó: el verificado prevalece. Apruébalo de nuevo o recházalo.", "/accesos", orden), tenantId);

        var tercero = await contexto.TerceroAsync(vinculo.TerceroId, ct);
        var mensaje = tercero is not null && Dominio.Terceros.AccesoDelTercero.DecideLaPlataforma(tercero)
            ? $"{empresa} invitó a {solicitado.Correo}, que no está entre los contactos de tu empresa. Ningún contacto está verificado todavía, así que la plataforma revisará el acceso."
            : $"{empresa} invitó a {solicitado.Correo}, que no está entre los contactos de tu empresa. Apruébalo o recházalo.";
        return Plan(Nueva(evento, tenantId, vinculo.TerceroId, vinculo.EmpresaId, null, null, null, TiposNotificacionTercero.AccesoSolicitado,
            "Un correo nuevo pidió acceso", mensaje, "/accesos", orden), tenantId);
    }

    private static async Task<PlanDeNotificaciones> DeInvitacionAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo ||
            vinculo.TerceroId == Guid.Empty)
            return PlanDeNotificaciones.Vacio;

        var empresa = (await contexto.EmpresaAsync(vinculo.EmpresaId, ct))?.Nombre ?? "Una empresa";
        var tipo = (await contexto.TipoVinculoAsync(tenantId, vinculo.TipoVinculoId, ct))?.Nombre;
        var mensaje = $"{empresa} te invitó a validar tu información" + (string.IsNullOrWhiteSpace(tipo) ? "." : $" como {tipo.ToLower(Cultura)}.");
        return Plan(Nueva(evento, tenantId, vinculo.TerceroId, vinculo.EmpresaId, vinculo.Id, null, null, TiposNotificacionTercero.Invitacion,
            "Tienes una invitación", mensaje, $"/empresas/{vinculo.Id}", orden), tenantId);
    }

    /// <summary>
    /// La lectura de un documento propone cambios para la información del tercero (F5, FM-14): con la misma función pura de las
    /// propuestas, sin la validación de catálogos (la pantalla de «Mi información» descarta lo que no pase). No es de ninguna
    /// empresa: va sin empresa ni vínculo, en el tenant del último vínculo con el que se compartió el documento.
    /// </summary>
    private static async Task<PlanDeNotificaciones> PropuestaAsync(IEvent evento, AnalisisDeIaRegistrado analisis, IContextoDeEventos contexto, long orden,
        CancellationToken ct)
    {
        if (!analisis.Legible || await contexto.DocumentoAsync(evento.StreamId, ct) is not
            { Ambito: AmbitoDocumento.Global, TipoCanonico: { } canonico } documento ||
            !MapaDeRespaldo.RespaldaAlguna(canonico) || documento.VersionActual != analisis.Numero ||
            documento.Comparticiones.LastOrDefault() is not { } destino || await contexto.TerceroAsync(documento.TerceroId, ct) is not { } tercero)
            return PlanDeNotificaciones.Vacio;

        var secciones = new List<string>();
        foreach (var codigo in MapaDeRespaldo.Secciones(canonico))
        {
            if (CatalogoDeSecciones.Buscar(codigo) is not { } definicion || !definicion.Obligatoria(tercero.TipoPersona))
                continue;
            var periodo = definicion.PorPeriodo ? MapaDeRespaldo.Periodo(analisis.Campos) : null;
            if (definicion.PorPeriodo && periodo is null)
                continue;
            var seccion = await contexto.SeccionDeFichaAsync(SeccionDeFicha.IdPara(tercero.Id, codigo, periodo), ct);
            if (PropuestaDesdeDocumento.Proponer(codigo, seccion?.Vigente?.Datos, tercero.TipoPersona, periodo, canonico, analisis.Campos).HayCambios)
                secciones.Add(definicion.Nombre);
        }

        if (secciones.Count == 0)
            return PlanDeNotificaciones.Vacio;
        var mensaje = $"Leímos tu {documento.NombreTipo} y trae datos distintos a los de tu información ({string.Join(", ", secciones)}). " +
                      "Revisa la propuesta y confírmala si es correcta.";
        return Plan(Nueva(evento, destino.TenantId, tercero.Id, null, null, null, null, TiposNotificacionTercero.Propuesta,
            "Tu información puede actualizarse", mensaje, "/ficha", orden), destino.TenantId);
    }

    private static Notificacion Nueva(IEvent evento, Guid tenantId, Guid terceroId, Guid? empresaId, Guid? vinculoId, Guid? expedienteId, string? folio,
        string tipo, string titulo, string mensaje, string enlace, long orden) => new()
    {
        Id = Deterministas.Guid($"notificacion-tercero:{evento.Id:N}:{terceroId:N}:{vinculoId:N}:{tipo}"),
        UsuarioId = terceroId,
        TenantId = tenantId,
        EmpresaId = empresaId,
        Tipo = tipo,
        Titulo = titulo,
        // «con Norte S.A.S.» ya termina en punto: no se agrega otro («S.A.S..»).
        Mensaje = mensaje.EndsWith("..", StringComparison.Ordinal) ? mensaje[..^1] : mensaje,
        ExpedienteId = expedienteId,
        VinculoId = vinculoId,
        Folio = folio,
        Enlace = enlace,
        CreadaEn = evento.Timestamp,
        Orden = orden
    };

    private static PlanDeNotificaciones Plan(Notificacion notificacion, Guid tenantId) => new([(tenantId, notificacion)], []);

    /// <summary>Notificación directa (sin evento de dominio) de un recordatorio manual de vencimientos (VE-3).</summary>
    public static Notificacion DeRecordatorioDeVencimiento(Guid tenantId, Guid terceroId, Guid empresaId, Guid vinculoId, string empresa, string documento,
        DateOnly vigenteHasta, DateTimeOffset ahora)
    {
        var dias = vigenteHasta.DayNumber - CalculadoraDeVigencia.Fecha(ahora).DayNumber;
        var cuando = dias < 0 ? $"venció el {Fecha(vigenteHasta)}" : dias == 0 ? "vence hoy" : $"vence el {Fecha(vigenteHasta)} ({Dias(dias)})";
        return new Notificacion
        {
            Id = Deterministas.Guid($"notificacion-tercero:vencimiento:{vinculoId:N}:{documento}:{ahora.UtcTicks}"),
            UsuarioId = terceroId,
            TenantId = tenantId,
            EmpresaId = empresaId,
            Tipo = dias < 0 ? TiposNotificacionTercero.DocumentoVencido : TiposNotificacionTercero.DocumentoPorVencer,
            Titulo = dias < 0 ? "Documento vencido" : "Documento por vencer",
            Mensaje = $"{empresa} te recuerda que tu {documento} {cuando}.",
            VinculoId = vinculoId,
            Enlace = "/boveda",
            CreadaEn = ahora,
            Orden = ahora.UtcTicks
        };
    }

    private static string Fecha(DateTimeOffset fecha) => Fecha(CalculadoraDeVigencia.Fecha(fecha));

    private static string Fecha(DateOnly fecha) => fecha.ToString("d 'de' MMMM 'de' yyyy", Cultura);

    private static string Dias(int dias) => dias == 1 ? "1 día" : $"{dias} días";

    /// <summary>«A, B y C»; con más de tres, «A, B, C y 2 más». Sin nada, <paramref name="vacio"/>.</summary>
    /// <summary>
    /// ¿La actualización la pidió la empresa? Una persona o un sistema de la empresa que la abre le pide información al tercero; la que abre la propia
    /// versión nueva del tercero (lo habitual, RV-1) no le pide nada: ya la recibió y la está revisando.
    /// </summary>
    private static bool PidioLaEmpresa(ActorDeExpediente abiertoPor) => abiertoPor.Tipo is ActorTipo.Usuario or ActorTipo.Integracion;

    private static string De(string lista) => lista.Length == 0 ? string.Empty : $" de {lista}";

    private static string Lista(IReadOnlyList<string> nombres, string vacio) => nombres.Count switch
    {
        0 => vacio,
        1 => nombres[0],
        <= 3 => $"{string.Join(", ", nombres.Take(nombres.Count - 1))} y {nombres[^1]}",
        _ => $"{string.Join(", ", nombres.Take(3))} y {nombres.Count - 3} más"
    };
}

/// <summary>
/// Campana del tercero y sus preferencias de correo (NT-1, sesión de tercero). Entre tenants, solo lo que pertenece a los vínculos
/// de la sesión del tercero (o lo que no es de ningún vínculo, como una propuesta desde un documento): nunca ve lo de otra persona.
/// </summary>
public static class NotificacionesDelTerceroEndpoints
{
    /// <summary>Más recientes primero; <c>?soloNoLeidas=true</c>, <c>?limite=</c> (30 por omisión, máx. 100) y <c>?cursor=</c> de la página anterior.</summary>
    [WolverineGet("/public/v1/notificaciones")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<BandejaNotificacionesTerceroDto> Listar(bool? soloNoLeidas, int? limite, string? cursor, HttpContext http, ISesiones sesiones,
        CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Relacion);
        await using var consulta = sesiones.ConsultaGlobal();
        var propias = Visibles(consulta, tercero);
        var noLeidas = await propias.Where(n => !n.Leida).CountAsync(ct);

        var lista = soloNoLeidas == true ? propias.Where(n => !n.Leida) : propias;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var antesDe = EventosEndpoints.LeerCursor(cursor);
            lista = lista.Where(n => n.Orden < antesDe);
        }

        var tamano = Math.Clamp(limite ?? 30, 1, 100);
        var pagina = await lista.OrderByDescending(n => n.Orden).Take(tamano + 1).ToListAsync(ct);
        var siguiente = pagina.Count > tamano ? pagina[tamano - 1].Orden.ToString(CultureInfo.InvariantCulture) : null;

        var empresaIds = pagina.Take(tamano).Select(n => n.EmpresaId).OfType<Guid>().Distinct().ToArray();
        var empresas = empresaIds.Length == 0 ? [] : (await consulta.LoadManyAsync<Empresa>(ct, empresaIds)).ToDictionary(e => e.Id);
        return new BandejaNotificacionesTerceroDto([.. pagina.Take(tamano).Select(n => ADto(n, empresas))], siguiente, noLeidas);
    }

    /// <summary>404 <c>notificacion-no-encontrada</c> si no es de este tercero (o de uno de sus vínculos).</summary>
    [WolverinePost("/public/v1/notificaciones/{id}/leida")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<NotificacionTerceroDto> MarcarLeida(Guid id, HttpContext http, ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Relacion);
        await using var consulta = sesiones.ConsultaGlobal();
        var notificacion = await Visibles(consulta, tercero).Where(n => n.Id == id).FirstOrDefaultAsync(ct);
        if (notificacion?.TenantId is not { } tenantId)
            throw ReglaDeNegocioException.NoEncontrado("notificacion-no-encontrada", "No existe esa notificación.");

        notificacion.MarcarLeida(reloj.GetUtcNow());
        await using (var sesion = sesiones.ParaTenant(tenantId))
        {
            sesion.Store(notificacion);
            await sesion.SaveChangesAsync(ct);
        }

        var empresa = notificacion.EmpresaId is { } empresaId ? await consulta.LoadAsync<Empresa>(empresaId, ct) : null;
        return ADto(notificacion, empresa is null ? [] : new Dictionary<Guid, Empresa> { [empresa.Id] = empresa });
    }

    /// <summary>Marca todas las no leídas del tercero (de todas sus empresas).</summary>
    [WolverinePost("/public/v1/notificaciones/leidas")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<NotificacionesMarcadasDto> MarcarTodas(HttpContext http, ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Relacion);
        await using var consulta = sesiones.ConsultaGlobal();
        var pendientes = await Visibles(consulta, tercero).Where(n => !n.Leida && n.TenantId != null).ToListAsync(ct);
        var ahora = reloj.GetUtcNow();
        foreach (var grupo in pendientes.GroupBy(n => n.TenantId!.Value))
        {
            await using var sesion = sesiones.ParaTenant(grupo.Key);
            foreach (var notificacion in grupo)
            {
                notificacion.MarcarLeida(ahora);
                sesion.Store(notificacion);
            }

            await sesion.SaveChangesAsync(ct);
        }

        return new NotificacionesMarcadasDto(pendientes.Count);
    }

    [WolverineGet("/public/v1/preferencias-notificacion")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<PreferenciasNotificacionDto> Preferencias(HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        await using var consulta = sesiones.ConsultaGlobal();
        return ADto(await consulta.LoadAsync<PreferenciasDelTercero>(tercero.TerceroId, ct));
    }

    /// <summary>Reemplaza el valor de los tipos indicados (los demás conservan el suyo); 422 <c>tipo-notificacion-invalido</c>. La campana no se apaga.</summary>
    [WolverinePut("/public/v1/preferencias-notificacion")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<PreferenciasNotificacionDto> ActualizarPreferencias(ActualizarPreferenciasNotificacionDto dto, HttpContext http, ISesiones sesiones,
        TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        var cambios = dto.Correo ?? new Dictionary<string, bool>();
        var invalidos = cambios.Keys.Where(k => !TiposNotificacionTercero.ConCorreo.Contains(k)).ToList();
        if (invalidos.Count > 0)
            throw ReglaDeNegocioException.Validacion("tipo-notificacion-invalido",
                $"Estos tipos no van por correo: {string.Join(", ", invalidos)}. Los que admiten correo: {string.Join(", ", TiposNotificacionTercero.ConCorreo)}.");

        await using var sesion = sesiones.Global();
        var preferencias = await sesion.LoadAsync<PreferenciasDelTercero>(tercero.TerceroId, ct) ?? new PreferenciasDelTercero { Id = tercero.TerceroId };
        foreach (var (tipo, valor) in cambios)
            preferencias.Correo[tipo] = valor;
        preferencias.ActualizadasEn = reloj.GetUtcNow();
        sesion.Store(preferencias);
        await sesion.SaveChangesAsync(ct);
        return ADto(preferencias);
    }

    /// <summary>
    /// SSE del portal del tercero (NT-12), con su sesión (<c>Authorization: Bearer</c>): <c>event: notificacion</c> con cada
    /// <see cref="NotificacionTerceroDto"/> nueva (id = su orden), <c>event: no_leidas</c> con el total cuando cambia y
    /// <c>event: cambio</c> (<see cref="CambioEnVivoDto"/>) cuando cambia algo de lo suyo —un expediente, un vínculo, una cuenta, un
    /// documento, su ficha, un certificado— para que lo que tiene abierto se refresque solo. Solo lo de los vínculos de su sesión.
    /// El stream se cierra cuando vence el token de la sesión: el cliente reconecta con el renovado. Consulta cada 2 s.
    /// </summary>
    [WolverineGet("/public/v1/notificaciones/stream")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IResult> Stream(HttpContext http, ISesiones sesiones, TimeProvider reloj)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Relacion);
        var alcance = AlcanceDelStream.De(http.User);
        var ct = http.RequestAborted;
        var ultimo = http.Request.Headers["Last-Event-ID"].ToString() is { Length: > 0 } reanudar ? EventosEndpoints.LeerCursor(reanudar) : reloj.GetUtcNow().UtcTicks;
        var cambios = new CursorDeCambios(CambiosEnVivo.InicioDeUnStream(reloj));
        await Sse.IniciarAsync(http);

        int? noLeidasAnterior = null;
        var ultimoLatido = reloj.GetUtcNow();
        while (!ct.IsCancellationRequested && (alcance.Vence is null || reloj.GetUtcNow() < alcance.Vence))
        {
            await using (var consulta = sesiones.ConsultaGlobal())
            {
                await CambiosEnVivo.EscribirAsync(http, cambios.Nuevos(await CambiosEnVivo.DelTerceroAsync(consulta, alcance, cambios.Desde, ct)));

                var desde = ultimo;
                var nuevas = await Visibles(consulta, tercero).Where(n => n.Orden > desde).OrderBy(n => n.Orden).Take(100).ToListAsync(ct);
                if (nuevas.Count > 0)
                {
                    var empresaIds = nuevas.Select(n => n.EmpresaId).OfType<Guid>().Distinct().ToArray();
                    var empresas = empresaIds.Length == 0 ? [] : (await consulta.LoadManyAsync<Empresa>(ct, empresaIds)).ToDictionary(e => e.Id);
                    foreach (var notificacion in nuevas)
                    {
                        await Sse.EscribirAsync(http, notificacion.Orden.ToString(CultureInfo.InvariantCulture), "notificacion",
                            JsonSerializer.Serialize(ADto(notificacion, empresas), JsonEventos.Opciones));
                        ultimo = notificacion.Orden;
                    }
                }

                var noLeidas = await Visibles(consulta, tercero).Where(n => !n.Leida).CountAsync(ct);
                if (noLeidas != noLeidasAnterior)
                {
                    await Sse.EscribirAsync(http, ultimo.ToString(CultureInfo.InvariantCulture), "no_leidas", noLeidas.ToString(CultureInfo.InvariantCulture));
                    noLeidasAnterior = noLeidas;
                }
            }

            ultimoLatido = await Sse.LatidoAsync(http, ultimoLatido, reloj);
            await Sse.EsperarAsync(ct);
        }

        return Sse.Fin;
    }

    /// <summary>Lo del tercero entre todos los tenants, solo de los vínculos de su sesión (o sin vínculo).</summary>
    private static IQueryable<Notificacion> Visibles(IQuerySession consulta, SesionDeTercero tercero)
    {
        var terceroId = tercero.TerceroId;
        Guid?[] vinculos = [.. tercero.Vinculos.Select(v => (Guid?)v.VinculoId)];
        var propias = consulta.Query<Notificacion>().Where(n => n.AnyTenant() && n.UsuarioId == terceroId);
        // Una sesión aislada solo se entera de lo de sus vínculos: lo que no es de ninguno (propuestas de su ficha, solicitudes de acceso) es del tercero entero.
        return tercero.Aislada
            ? propias.Where(n => n.VinculoId.IsOneOf(vinculos))
            : propias.Where(n => n.VinculoId == null || n.VinculoId.IsOneOf(vinculos));
    }

    private static NotificacionTerceroDto ADto(Notificacion n, IReadOnlyDictionary<Guid, Empresa> empresas) =>
        new(n.Id, n.Tipo, n.Titulo, n.Mensaje, n.EmpresaId is { } id ? empresas.GetValueOrDefault(id).APublica(id) : null, n.ExpedienteId, n.VinculoId, n.Folio,
            n.Enlace, n.CreadaEn, n.LeidaEn);

    private static PreferenciasNotificacionDto ADto(PreferenciasDelTercero? preferencias) =>
        new(TiposNotificacionTercero.ConCorreo.ToDictionary(t => t, t => TiposNotificacionTercero.QuiereCorreo(preferencias, t)), TiposNotificacionTercero.ConCorreo);
}
