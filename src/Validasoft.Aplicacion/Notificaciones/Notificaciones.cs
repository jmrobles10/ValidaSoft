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

    public static readonly IReadOnlyList<string> Todos =
        [NuevoEnArea, TerceroRespondio, VersionNueva, Aprobado, Rechazado, PorVencer, Vencido, WebhookDeshabilitado, CupoIa, CoincidenciaListas,
            EstudioPendiente, VerificacionExpirada];

    /// <summary>Los que además de la campana van por correo (si la persona no lo apagó).</summary>
    public static readonly IReadOnlyList<string> ConCorreo =
        [NuevoEnArea, TerceroRespondio, VersionNueva, WebhookDeshabilitado, CupoIa, CoincidenciaListas, EstudioPendiente];

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
public sealed record PlanDeNotificaciones(IReadOnlyList<(Guid TenantId, Notificacion Notificacion)> Campana, IReadOnlyList<object> Correos)
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
    public async Task<PlanDeNotificaciones> PlanearAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        switch (evento.Data)
        {
            case ConsentimientosRegistrados consentimientos:
                return await ConstanciaAsync(evento, consentimientos, contexto, ct);
            case EtapaActivada etapa:
                return await AAreasAsync(evento, contexto, TiposNotificacion.NuevoEnArea, etapa.Areas, orden, ct);
            case ExpedienteReenviado reenviado:
                return await AAreasAsync(evento, contexto, TiposNotificacion.TerceroRespondio, reenviado.AreasReabiertas, orden, ct);
            case VersionAdjuntadaARequisito { NumeroAnterior: not null } adjuntada:
                return await VersionNuevaAsync(evento, adjuntada, contexto, orden, ct);
            case ExpedienteAprobado:
                return await AAreasAsync(evento, contexto, TiposNotificacion.Aprobado, null, orden, ct);
            case ExpedienteRechazado:
                return await AAreasAsync(evento, contexto, TiposNotificacion.Rechazado, null, orden, ct);
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
            case VerificacionExpirada expirada:
                return await ACumplimientoAsync(evento, contexto, TiposNotificacion.VerificacionExpirada, "Verificación vencida",
                    t => expirada.Tipo == TipoVerificacion.ListasRestrictivas
                        ? $"Venció la consulta de listas de {t}: se reconsulta sola."
                        : $"Venció el estudio de cumplimiento de {t}: hay que volver a hacerlo antes de cerrar el expediente.", orden, ct);
            default:
                return PlanDeNotificaciones.Vacio;
        }
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

    private static async Task<PlanDeNotificaciones> AAreasAsync(IEvent evento, IContextoDeEventos contexto, string tipo, IReadOnlyList<Guid>? areas,
        long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct) is not { } expediente)
            return PlanDeNotificaciones.Vacio;

        var ids = areas ?? expediente.AreaIds;
        var deLasAreas = (await contexto.AreasAsync(tenantId, [.. ids], ct)).Where(a => a.EmpresaId == expediente.EmpresaId).ToList();
        var empresa = await contexto.EmpresaAsync(expediente.EmpresaId, ct);
        var (titulo, mensaje) = tipo switch
        {
            TiposNotificacion.NuevoEnArea => ("Nuevo expediente en tu área", $"{expediente.TerceroNombre} ({expediente.TipoVinculoNombre}) espera tu revisión."),
            TiposNotificacion.TerceroRespondio => ("El tercero respondió", $"{expediente.TerceroNombre} respondió las observaciones; el expediente vuelve a tu área."),
            TiposNotificacion.Aprobado => ("Expediente aprobado", $"Se aprobó la validación de {expediente.TerceroNombre}."),
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
            certificado.EmpresaId)]);
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
                destino.TenantId, vinculo.EmpresaId)];

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
    Guid? EmpresaId = null);

/// <summary>
/// El certificado del tercero dejó de valer: revocado (definitivo) o suspendido (mientras la empresa lo revisa). Cola
/// <c>correo</c>.
/// </summary>
public sealed record CorreoDeCertificadoPendiente(string Para, string Empresa, string Folio, bool Revocado, DateTimeOffset Fecha, Guid? TenantId = null,
    Guid? EmpresaId = null);

// Los correos al tercero de una empresa llevan la plantilla de esa empresa (PL-1) si la tiene.
public static class CorreoDeCertificadoPendienteHandler
{
    public static async Task Handle(CorreoDeCertificadoPendiente correo, Comun.IEnviadorDeCorreo enviador, Comun.RedactorDeCorreos redactor,
        Comun.PlantillasDeCorreo plantillas, CancellationToken ct) =>
        await enviador.EnviarAsync(await plantillas.AplicarAsync(redactor.RedactarCertificado(correo), CatalogoDePlantillas.Certificado,
            correo.TenantId, correo.EmpresaId, Comun.EjemplosDeCorreo.VariablesDe(correo.Empresa, correo.Folio), ct), ct);
}

public static class CorreoDeVencidoPendienteHandler
{
    public static async Task Handle(CorreoDeVencidoPendiente correo, Comun.IEnviadorDeCorreo enviador, Comun.RedactorDeCorreos redactor,
        Comun.PlantillasDeCorreo plantillas, CancellationToken ct) =>
        await enviador.EnviarAsync(await plantillas.AplicarAsync(redactor.RedactarVencido(correo), CatalogoDePlantillas.Vencido,
            correo.TenantId, correo.EmpresaId, Comun.EjemplosDeCorreo.VariablesDe(correo.Empresa, documento: correo.Documento), ct), ct);
}

public static class CorreoDeConstanciaPendienteHandler
{
    public static async Task Handle(CorreoDeConstanciaPendiente correo, Comun.IEnviadorDeCorreo enviador, Comun.RedactorDeCorreos redactor,
        Comun.PlantillasDeCorreo plantillas, CancellationToken ct) =>
        await enviador.EnviarAsync(await plantillas.AplicarAsync(redactor.RedactarConstancia(correo), CatalogoDePlantillas.ConsentimientoEmpresa,
            correo.TenantId, correo.EmpresaId, Comun.EjemplosDeCorreo.VariablesDe(correo.Empresa), ct), ct);
}
