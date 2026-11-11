using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Cumplimiento;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Identidad;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Verificaciones;

/// <summary>
/// Verificaciones externas del expediente (H7, docs/05 §2.7; VE-5…VE-13), todo con <c>X-Empresa</c>: consultar o reconsultar
/// listas (<c>verificaciones:escribir</c>), resolver una coincidencia y registrar el estudio SAGRILAFT
/// (<c>cumplimiento:decidir</c>, siempre una persona), descargar la evidencia (auditada) y la bandeja del oficial. Cada
/// decisión pasa por la cascada del expediente: si era lo último que faltaba, el expediente se aprueba en la misma
/// transacción (vínculo activo y certificado).
/// </summary>
public static class VerificacionesEndpoints
{
    /// <summary>
    /// Consulta (o reconsulta) ahora las listas del tercero y de sus personas relacionadas con el proveedor de la empresa. 422
    /// <c>tipo-no-consultable</c> (solo <c>ListasRestrictivas</c>), <c>cuota-listas-agotada</c>, <c>credenciales-requeridas</c>;
    /// 409 <c>estado-invalido</c> con el expediente cerrado; 503 <c>verificacion-no-disponible</c>.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/verificaciones/{tipo}/consultar")]
    [Authorize(Policy = Permisos.VerificacionesEscribir)]
    public static async Task<ExpedienteDto> Consultar(Guid id, string tipo, SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http,
        ConsultaDeListas consulta, IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        if (LeerTipo(tipo) != TipoVerificacion.ListasRestrictivas)
            throw ReglaDeNegocioException.Validacion("tipo-no-consultable", "Solo las listas restrictivas se consultan; el estudio lo registra el oficial.");
        empresa.UsarMensajeria(mensajes);
        var expediente = await empresa.CargarExpedienteAsync(id, ct);
        if (!expediente.EstaAbierto)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"El expediente está {expediente.Estado}: ya no recibe verificaciones.");
        await ExpedientesEndpoints.AsegurarVinculoNoBloqueadoAsync(empresa, expediente, ct);

        var meses = expediente.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)?.FrecuenciaReconsultaMeses ?? Expediente.MesesReconsultaPorOmision;
        var resultado = await consulta.ConsultarAsync(empresa.Sesion, contexto.TenantRequerido(), empresa.EmpresaId, expediente.TerceroId, expediente.Folio,
            await QuienAsync(empresa, contexto, ct), meses, ct);
        return await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var stream = await empresa.ExpedienteParaEscribirAsync(id, null, ct);
            var eventos = stream.Aggregate!.RegistrarConsultaDeListas(resultado, contexto.ActorId, reloj.GetUtcNow());
            return await GuardarAsync(empresa, stream, eventos, mensajes, protector, http, reloj, ct);
        });
    }

    /// <summary>
    /// Cumplimiento descarta o confirma una coincidencia con justificación (P-9-4). Confirmada ⇒ la verificación queda
    /// <c>Fallida</c> y el expediente no cierra (f-4). 404 <c>coincidencia-no-encontrada</c>; 409 <c>coincidencia-ya-resuelta</c>;
    /// 403 <c>conflicto-de-intereses</c>; 422 <c>justificacion-requerida</c>, <c>decision-invalida</c>.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/verificaciones/{tipo}/coincidencias/{coincidenciaId}/resolver")]
    [Authorize(Policy = Permisos.CumplimientoDecidir)]
    public static async Task<ExpedienteDto> ResolverCoincidencia(Guid id, string tipo, Guid coincidenciaId, ResolverCoincidenciaDto dto, SesionDeEmpresa empresa,
        IContextoSolicitud contexto, HttpContext http, IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        if (LeerTipo(tipo) != TipoVerificacion.ListasRestrictivas)
            throw ReglaDeNegocioException.Validacion("tipo-no-consultable", "Solo las listas restrictivas tienen coincidencias.");
        var decision = LeerDecision(dto.Decision);
        empresa.UsarMensajeria(mensajes);
        return await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
            var oficial = await OficialAsync(empresa, contexto, ct);
            var eventos = stream.Aggregate!.ResolverCoincidencia(coincidenciaId, decision, dto.Justificacion, oficial, reloj.GetUtcNow());
            return await GuardarAsync(empresa, stream, eventos, mensajes, protector, http, reloj, ct);
        });
    }

    /// <summary>
    /// Registra el estudio del Oficial de Cumplimiento (SAGRILAFT, VE-9) con la vigencia del flujo o la de la configuración
    /// (30 días). Las evidencias deben ser versiones de documentos del expediente (422 <c>evidencia-invalida</c>).
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/verificaciones/estudio-cumplimiento")]
    [Authorize(Policy = Permisos.CumplimientoDecidir)]
    public static async Task<ExpedienteDto> RegistrarEstudio(Guid id, RegistrarEstudioCumplimientoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        HttpContext http, OpcionesDeVerificaciones opciones, IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        DebidaDiligencia? debidaPedida = string.IsNullOrWhiteSpace(dto.DebidaDiligencia) ? null
            : Enum.TryParse<DebidaDiligencia>(dto.DebidaDiligencia, true, out var d) && Enum.IsDefined(d)
                ? d
                : throw ReglaDeNegocioException.Validacion("debida-diligencia-invalida", "La debida diligencia es Simple, Ordinaria o Ampliada.");
        var concepto = Enum.TryParse<ConceptoCumplimiento>(dto.Concepto, true, out var c) && Enum.IsDefined(c)
            ? c
            : throw ReglaDeNegocioException.Validacion("concepto-invalido", "El concepto es Favorable o Desfavorable.");
        empresa.UsarMensajeria(mensajes);
        var matriz = await empresa.Sesion.LoadAsync<MatrizDeRiesgoEmpresa>(empresa.EmpresaId, ct);
        return await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
            var expediente = stream.Aggregate!;
            var oficial = await OficialAsync(empresa, contexto, ct);
            // Con la matriz de la empresa activa (CU-17) el nivel y la debida diligencia salen del cálculo; el oficial solo puede ajustarlos con justificación.
            var calculo = matriz is { Activa: true }
                ? await MatrizEndpoints.CalcularAsync(empresa, matriz, expediente, dto.Respuestas, ct)
                : null;
            var nivel = dto.NivelRiesgo ?? calculo?.Nivel
                        ?? throw ReglaDeNegocioException.Validacion("nivel-riesgo-requerido", "Indica el nivel de riesgo (1 a 3).");
            var debida = debidaPedida ?? (calculo is null ? (DebidaDiligencia?)null : matriz!.Niveles.FirstOrDefault(n => n.Nivel == nivel)?.DebidaDiligencia ?? calculo.DebidaDiligencia)
                         ?? throw ReglaDeNegocioException.Validacion("debida-diligencia-requerida", "Indica la debida diligencia (Simple, Ordinaria o Ampliada).");
            if (calculo is not null && matriz!.Niveles.FirstOrDefault(n => n.Nivel == nivel) is { } deNivel && debida < deNivel.DebidaDiligencia)
                throw ReglaDeNegocioException.Validacion("debida-diligencia-insuficiente",
                    $"El nivel de riesgo {nivel} exige debida diligencia {deNivel.DebidaDiligencia} o más intensa.");
            var evidencias = new List<EvidenciaDeEstudio>();
            foreach (var evidencia in dto.Evidencias ?? [])
            {
                var requisito = expediente.Requisitos.FirstOrDefault(r => r.VersionActual?.DocumentoId == evidencia.DocumentoId ||
                                                                          r.Historial.Any(h => h.DocumentoId == evidencia.DocumentoId))
                                ?? throw ReglaDeNegocioException.Validacion("evidencia-invalida", "Una evidencia no es un documento del expediente.");
                evidencias.Add(new EvidenciaDeEstudio(evidencia.DocumentoId, evidencia.Version, requisito.Nombre));
            }

            var ahora = reloj.GetUtcNow();
            var dias = expediente.BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento)?.VigenciaDias ?? opciones.VigenciaEstudioDias;
            var eventos = expediente.RegistrarEstudio(new DatosDeEstudio(nivel, dto.ConflictoDeInteres, debida, dto.Observaciones, evidencias, concepto, calculo,
                    dto.JustificacionDeNivel, dto.Adjuntos), oficial, ahora.AddDays(dias), ahora);
            return await GuardarAsync(empresa, stream, eventos, mensajes, protector, http, reloj, ct);
        });
    }

    /// <summary>
    /// URL prefirmada (5 min) del PDF de evidencia de la consulta vigente o de una anterior (<c>referencia</c>). La descarga
    /// queda en el historial del expediente (<c>EvidenciaDeVerificacionDescargada</c> con actor, IP y user-agent).
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/verificaciones/{tipo}/evidencia/descarga")]
    [Authorize(Policy = Permisos.DocumentosDescargar)]
    public static async Task<DescargaEvidenciaDto> DescargarEvidencia(Guid id, string tipo, string? referencia, SesionDeEmpresa empresa,
        IContextoSolicitud contexto, HttpContext http, IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var tipoVerificacion = LeerTipo(tipo);
        var stream = await empresa.ExpedienteParaEscribirAsync(id, null, ct);
        var (evidencia, evento) = stream.Aggregate!.DescargarEvidencia(tipoVerificacion, referencia, contexto.ActorTipo.ToString(), contexto.ActorId,
            BovedaEndpoints.Ip(http), BovedaEndpoints.UserAgent(http), reloj.GetUtcNow());
        stream.AppendOne(evento);
        await empresa.Sesion.SaveChangesAsync(ct);
        return await UrlAsync(almacen, reglas, evidencia, ct);
    }

    /// <summary>
    /// Bandeja del oficial de cumplimiento: expedientes abiertos con alguna verificación que no está satisfecha (pendiente, con
    /// coincidencias, fallida o vencida) y vínculos activos cuyo monitoreo encontró coincidencias nuevas. Lo más antiguo primero.
    /// </summary>
    [WolverineGet("/api/v1/cumplimiento/bandeja")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<IReadOnlyList<FilaCumplimientoDto>> Bandeja(SesionDeEmpresa empresa, CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        var expedientes = await empresa.Expedientes().Where(e => e.PendienteDeCumplimiento).OrderBy(e => e.AbiertoEn).Take(500).ToListAsync(ct);
        var monitoreos = await empresa.Sesion.Query<MonitoreoDeListas>().Where(m => m.EmpresaId == empresaId && m.PendienteDeCumplimiento)
            .Take(500).ToListAsync(ct);

        var filas = expedientes.Select(e => new FilaCumplimientoDto("Expediente", e.Id, e.Folio, e.VinculoId,
                new TerceroResumenDto(e.TerceroId, e.TerceroNombre, e.TerceroIdentificacion), e.TipoVinculoNombre, e.Estado.ToString(),
                [.. e.Verificaciones.Where(v => v.RequiereAtencion).Select(v => new VerificacionPendienteDto(v.Tipo.ToString(), v.Estado.ToString(), v.Bloqueante))],
                e.Verificaciones.Sum(v => v.Coincidencias.Count(x => !x.Resuelta)), e.EsperandoVerificaciones,
                e.Verificaciones.Where(v => v.RequiereAtencion).Select(v => v.ExpiroEn ?? v.RegistradaEn ?? e.AbiertoEn).Min()))
            .Concat(monitoreos.Select(m => new FilaCumplimientoDto("Monitoreo", null, m.Folio, m.VinculoId,
                new TerceroResumenDto(m.TerceroId, m.TerceroNombre, m.TerceroIdentificacion), null, null,
                [new VerificacionPendienteDto(nameof(TipoVerificacion.ListasRestrictivas), m.Listas.Estado.ToString(), false)],
                m.Listas.Coincidencias.Count(x => !x.Resuelta), false, m.Listas.RegistradaEn ?? m.ActualizadoEn)))
            .OrderBy(f => f.Desde)
            .ToList();
        return filas;
    }

    /// <summary>Monitoreo de listas del vínculo (VE-12): última reconsulta periódica y la próxima. 404 <c>monitoreo-no-encontrado</c>.</summary>
    [WolverineGet("/api/v1/vinculos/{id}/monitoreo-listas")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<MonitoreoListasDto> Monitoreo(Guid id, SesionDeEmpresa empresa, CancellationToken ct) =>
        (await CargarMonitoreoAsync(empresa, id, ct)).ADto();

    /// <summary>
    /// Resuelve una coincidencia del monitoreo con las mismas reglas que en el expediente; queda en la auditoría
    /// (<c>MonitoreoDeListas</c>). Confirmada: el administrador decide bloquear o desvincular (nunca automático).
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/monitoreo-listas/coincidencias/{coincidenciaId}/resolver")]
    [Authorize(Policy = Permisos.CumplimientoDecidir)]
    public static async Task<MonitoreoListasDto> ResolverCoincidenciaDeMonitoreo(Guid id, Guid coincidenciaId, ResolverCoincidenciaDto dto,
        SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var decision = LeerDecision(dto.Decision);
        var monitoreo = await CargarMonitoreoAsync(empresa, id, ct);
        var oficial = await OficialAsync(empresa, contexto, ct);
        var ahora = reloj.GetUtcNow();
        var coincidencia = monitoreo.ResolverCoincidencia(coincidenciaId, decision, dto.Justificacion, oficial, ahora);
        empresa.Sesion.Store(monitoreo);
        Auditoria.Registrar(empresa.Sesion, contexto, "MonitoreoDeListas", $"{id}/{coincidenciaId}", Dominio.Auditoria.AccionConfiguracion.Modificado, null,
            new { coincidencia.Lista, coincidencia.Rol, Decision = decision.ToString(), dto.Justificacion }, ahora, empresa.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return monitoreo.ADto();
    }

    /// <summary>URL prefirmada de la evidencia de la última reconsulta del monitoreo (la entrega queda en la auditoría).</summary>
    [WolverinePost("/api/v1/vinculos/{id}/monitoreo-listas/evidencia/descarga")]
    [Authorize(Policy = Permisos.DocumentosDescargar)]
    public static async Task<DescargaEvidenciaDto> DescargarEvidenciaDeMonitoreo(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var monitoreo = await CargarMonitoreoAsync(empresa, id, ct);
        var evidencia = monitoreo.Listas.Evidencia
                        ?? throw ReglaDeNegocioException.NoEncontrado("evidencia-no-encontrada", "El monitoreo no tiene evidencia guardada.");
        Auditoria.Registrar(empresa.Sesion, contexto, "EvidenciaDeListas", $"{id}/{monitoreo.Listas.ReferenciaExterna}",
            Dominio.Auditoria.AccionConfiguracion.Modificado, null, new { Descargada = evidencia.NombreArchivo }, reloj.GetUtcNow(), empresa.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return await UrlAsync(almacen, reglas, evidencia, ct);
    }

    internal static TipoVerificacion LeerTipo(string tipo) =>
        Enum.TryParse<TipoVerificacion>(tipo, true, out var valor) && Enum.IsDefined(valor)
            ? valor
            : throw ReglaDeNegocioException.NoEncontrado("verificacion-no-encontrada",
                $"«{tipo}» no es una verificación ({string.Join(", ", Enum.GetNames<TipoVerificacion>())}).");

    private static DecisionCoincidencia LeerDecision(string? decision) =>
        Enum.TryParse<DecisionCoincidencia>(decision, true, out var valor) && Enum.IsDefined(valor)
            ? valor
            : throw ReglaDeNegocioException.Validacion("decision-invalida", "La decisión es Descartada o Confirmada.");

    /// <summary>Quien decide como Cumplimiento: una persona activa con rol en la empresa (con su identificación, I-21).</summary>
    internal static async Task<Revisor> OficialAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, CancellationToken ct)
    {
        if (contexto.ActorTipo != ActorTipo.Usuario || !Guid.TryParse(contexto.ActorId, out var usuarioId))
            throw ReglaDeNegocioException.Validacion("usuario-delegado-requerido",
                "Las decisiones de cumplimiento las toma una persona: actúa con la identidad delegada (X-Usuario-Delegado) o con su sesión.");
        var usuario = await empresa.Sesion.LoadAsync<Usuario>(usuarioId, ct);
        if (usuario is not { Activo: true } || !usuario.TieneRolEn(empresa.EmpresaId))
            throw ReglaDeNegocioException.Prohibido("usuario-sin-rol", "No tienes un rol activo en esta empresa.");
        return new Revisor(usuarioId, usuario.Identificacion?.Clave);
    }

    /// <summary>Nombre de quien consulta para el PDF de evidencia (la persona o la credencial del sistema).</summary>
    internal static async Task<string> QuienAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, CancellationToken ct)
    {
        if (contexto.ActorTipo == ActorTipo.Usuario && Guid.TryParse(contexto.ActorId, out var usuarioId) &&
            await empresa.Sesion.LoadAsync<Usuario>(usuarioId, ct) is { } usuario)
            return $"{usuario.Nombre} ({usuario.Correo})";
        return contexto.Sistema is { } sistema ? $"Sistema integrado {sistema}" : $"Credencial {contexto.ActorId}";
    }

    private static async Task<MonitoreoDeListas> CargarMonitoreoAsync(SesionDeEmpresa empresa, Guid vinculoId, CancellationToken ct)
    {
        await empresa.CargarVinculoAsync(vinculoId, ct);
        var monitoreo = await empresa.Sesion.LoadAsync<MonitoreoDeListas>(vinculoId, ct);
        return monitoreo is not null && monitoreo.EmpresaId == empresa.EmpresaId
            ? monitoreo
            : throw ReglaDeNegocioException.NoEncontrado("monitoreo-no-encontrado", "El vínculo no tiene monitoreo de listas.");
    }

    internal static async Task<DescargaEvidenciaDto> UrlAsync(IAlmacenDeArchivos almacen, ReglasDeCarga reglas, EvidenciaDeVerificacion evidencia,
        CancellationToken ct)
    {
        var url = await almacen.UrlDeDescargaAsync(evidencia.Bucket, evidencia.Clave, evidencia.NombreArchivo, evidencia.TipoContenido, reglas.VigenciaDescarga, ct);
        return new DescargaEvidenciaDto(url.Url, url.Expira, evidencia.NombreArchivo);
    }

    internal static async Task<ExpedienteDto> GuardarAsync(SesionDeEmpresa empresa, JasperFx.Events.IEventStream<Expediente> stream, IReadOnlyList<object> eventos,
        IMessageBus mensajes, IProtectorDeSecretos protector, HttpContext http, TimeProvider reloj, CancellationToken ct)
    {
        if (eventos.Count > 0)
            await ExpedientesEndpoints.AnexarYGuardarAsync(empresa, stream, eventos, mensajes, protector, reloj, ct);
        var detalle = await ExpedientesEndpoints.DetalleAsync(empresa, stream.Id, ct);
        Peticiones.EscribirETag(http, detalle.Version);
        return detalle;
    }
}
