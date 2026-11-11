using System.Text.Json;
using JasperFx.Events;
using Marten;
using Marten.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Auditoría de la empresa activa (docs/05 §2.7, docs/06 <c>/auditoria</c>, AU-1): los cambios de configuración de la empresa y
/// los de todo el grupo, con el detalle campo por campo, y los accesos a documentos y a certificados de la empresa. Los
/// eventos de dominio se consultan en el historial de cada vínculo o expediente; la auditoría de identidad (<c>tipo=Identidad</c>, ING-6) son los
/// ingresos exitosos y fallidos, las passkeys y las sesiones del grupo, con su IP y navegador.
/// Se arma al consultar, de lo más reciente a lo más antiguo; cursor opaco.
/// </summary>
public static class AuditoriaEndpoints
{
    private const string Configuracion = "Configuracion";
    private const string Acceso = "Acceso";
    private const string Identidad = "Identidad";

    /// <summary>Filas que admite la exportación a Excel (IF-4); más ⇒ 422 <c>exportacion-demasiado-grande</c>.</summary>
    public const int MaximoExportacion = 50_000;

    [WolverineGet("/api/v1/auditoria")]
    [Authorize(Policy = Permisos.AuditoriaLeer)]
    public static async Task<PaginaDto<RegistroAuditoriaDto>> Listar(SesionDeEmpresa empresa, ISesiones sesiones, IContextoSolicitud contexto, string? tipo,
        string? entidad, string? entidadId, string? actor, DateTimeOffset? desde, DateTimeOffset? hasta, int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var salto = Peticiones.LeerDesplazamiento(cursor);
        var registros = await RegistrosAsync(empresa, sesiones, contexto, tipo, entidad, entidadId, actor, desde, hasta, ct);
        var pagina = await ConNombresAsync(empresa, [.. registros.Skip(salto).Take(tamano)], ct);
        var siguiente = registros.Count > salto + tamano ? Peticiones.EscribirDesplazamiento(salto + tamano) : null;
        return new PaginaDto<RegistroAuditoriaDto>(pagina, siguiente);
    }

    /// <summary>
    /// La auditoría con los mismos filtros de la pantalla en Excel (IF-4), de lo más reciente a lo más antiguo, con el
    /// detalle de cada cambio en una celda. Pide <c>auditoria:exportar</c> y la exportación misma queda en la auditoría.
    /// </summary>
    [WolverineGet("/api/v1/auditoria/excel")]
    [Authorize(Policy = Permisos.AuditoriaLeer)]
    [Authorize(Policy = Permisos.AuditoriaExportar)]
    public static async Task<IResult> Excel(SesionDeEmpresa empresa, ISesiones sesiones, IContextoSolicitud contexto, HttpContext http, TimeProvider reloj,
        string? tipo, string? entidad, string? entidadId, string? actor, DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        var registros = await RegistrosAsync(empresa, sesiones, contexto, tipo, entidad, entidadId, actor, desde, hasta, ct);
        if (registros.Count > MaximoExportacion)
            throw ReglaDeNegocioException.Validacion("exportacion-demasiado-grande",
                $"La exportación admite hasta {MaximoExportacion} registros: acota el rango de fechas o agrega filtros.");
        var conNombres = await ConNombresAsync(empresa, registros, ct);
        var bytes = HojaDeCalculo.Crear(TipoDe(tipo) switch { Configuracion => "Configuración", Identidad => "Identidad", _ => "Accesos" },
            ["Fecha", "Tipo", "Acción", "Entidad", "Id de la entidad", "Nombre", "Tipo de actor", "Actor", "Id del actor", "Sistema", "Cambios",
                "Justificación", "Detalle", "Vínculo", "IP", "Navegador", "Correlación"],
            conNombres.Select(r => (IReadOnlyList<object?>)
            [
                Informes.Exportaciones.Local(r.Fecha), r.Tipo == Configuracion ? "Configuración" : r.Tipo == Identidad ? "Identidad" : "Acceso", r.Accion, r.Entidad.Tipo, r.Entidad.Id,
                r.Entidad.Nombre, r.Actor.Tipo, r.Actor.Nombre, r.Actor.Id, r.Actor.Sistema, Cambios(r.Diff), r.Justificacion, r.Detalle,
                r.VinculoId?.ToString(), r.Ip, r.UserAgent, r.CorrelationId
            ]));
        var nombreEmpresa = (await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct))?.Nombre ?? "empresa";
        var archivo = $"auditoria-{Informes.Exportaciones.Slug(nombreEmpresa)}-{CalculadoraDeVigencia.Fecha(reloj.GetUtcNow()):yyyy-MM-dd}.xlsx";
        return await Informes.Exportaciones.EntregarAsync(empresa, contexto, http, reloj,
            new Informes.ArchivoExportado("Auditoria", "Excel", bytes, HojaDeCalculo.TipoContenido, archivo, conNombres.Count), ct);
    }

    private static async Task<List<RegistroAuditoriaDto>> RegistrosAsync(SesionDeEmpresa empresa, ISesiones sesiones, IContextoSolicitud contexto, string? tipo,
        string? entidad, string? entidadId, string? actor, DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        if (desde is not null && hasta is not null && desde > hasta)
            throw ReglaDeNegocioException.Validacion("rango-invalido", "La fecha inicial es posterior a la final.");
        return TipoDe(tipo) switch
        {
            Configuracion => await ConfiguracionAsync(empresa, entidad, entidadId, actor, desde, hasta, ct),
            Identidad => await IdentidadAsync(sesiones, contexto.TenantRequerido(), entidad, entidadId, actor, desde, hasta, ct),
            _ => await AccesosAsync(empresa, sesiones, entidad, entidadId, actor, desde, hasta, ct)
        };
    }

    /// <summary>
    /// Auditoría de identidad del grupo (ING-6): ingresos exitosos y fallidos, passkeys registradas o revocadas y sesiones cerradas, de las personas
    /// del grupo. <c>entidadId</c> filtra por la persona a la que se refiere el registro; <c>actor</c>, por quien hizo la acción.
    /// </summary>
    private static async Task<List<RegistroAuditoriaDto>> IdentidadAsync(ISesiones sesiones, Guid tenantId, string? entidad, string? entidadId, string? actor,
        DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        IQueryable<RegistroDeIngreso> filas = consulta.Query<RegistroDeIngreso>().Where(r => r.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(entidadId))
            filas = filas.Where(r => r.SujetoId == entidadId);
        if (!string.IsNullOrWhiteSpace(actor))
            filas = filas.Where(r => r.ActorId == actor);
        if (desde is not null)
            filas = filas.Where(r => r.Fecha >= desde);
        if (hasta is not null)
            filas = filas.Where(r => r.Fecha <= hasta);

        var lista = await filas.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id).Take(MaximoExportacion + 1).ToListAsync(ct);
        return [.. lista
            .Select(r => (Registro: r, Entidad: EntidadDeIdentidad(r.Evento)))
            .Where(x => string.IsNullOrWhiteSpace(entidad) || x.Entidad.Equals(entidad, StringComparison.OrdinalIgnoreCase))
            .Select(x => new RegistroAuditoriaDto(x.Registro.Id, Identidad, x.Registro.Fecha, null,
                new ActorAuditoriaDto(x.Registro.ActorTipo.ToString(), x.Registro.ActorId, null, null),
                new EntidadAuditadaDto(x.Entidad, x.Registro.SujetoId ?? string.Empty, x.Registro.Correo),
                x.Registro.Evento.ToString(), null, null, [], null,
                string.Join(" · ", new[] { x.Registro.Exitoso ? null : "Fallido", x.Registro.Metodo?.ToString(), x.Registro.Motivo, x.Registro.Detalle }
                    .Where(t => !string.IsNullOrWhiteSpace(t))),
                null, x.Registro.Ip, x.Registro.Dispositivo, x.Registro.CorrelationId))];
    }

    /// <summary>Sobre qué recayó el evento de identidad: la passkey, la sesión o el ingreso (códigos, ingresos e invitaciones).</summary>
    private static string EntidadDeIdentidad(EventoDeIngreso evento) => evento switch
    {
        EventoDeIngreso.PasskeyRegistrada or EventoDeIngreso.PasskeyRevocada or EventoDeIngreso.PasskeyEliminada or EventoDeIngreso.CodigosDeRecuperacionGenerados or
            EventoDeIngreso.CodigoDeRecuperacionUsado => "Passkey",
        EventoDeIngreso.SesionCerrada or EventoDeIngreso.SesionRevocada or EventoDeIngreso.ReusoDeSesion or EventoDeIngreso.ReautenticacionExitosa or
            EventoDeIngreso.ReautenticacionFallida => "Sesion",
        _ => "Ingreso"
    };

    private static async Task<List<RegistroAuditoriaDto>> ConNombresAsync(SesionDeEmpresa empresa, IReadOnlyList<RegistroAuditoriaDto> registros,
        CancellationToken ct)
    {
        var nombres = await NombresDeUsuariosAsync(empresa, registros, ct);
        return [.. registros.Select(r => r.Actor.Tipo == nameof(ActorTipo.Usuario) && r.Actor.Id is { } id && nombres.TryGetValue(id, out var nombre)
            ? r with { Actor = r.Actor with { Nombre = nombre } }
            : r)];
    }

    /// <summary><c>campo: antes → después</c> por cambio, uno por línea.</summary>
    private static string? Cambios(IReadOnlyList<DiffCampoDto> diff) =>
        diff.Count == 0 ? null : string.Join('\n', diff.Select(d => $"{d.Campo}: {Valor(d.Antes)} → {Valor(d.Despues)}"));

    private static string Valor(JsonElement? valor) => valor switch
    {
        null => "—",
        { ValueKind: JsonValueKind.String } texto => texto.GetString() ?? string.Empty,
        { } otro => otro.GetRawText()
    };

    private static string TipoDe(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo) || tipo.Equals(Configuracion, StringComparison.OrdinalIgnoreCase))
            return Configuracion;
        if (tipo.Equals(Acceso, StringComparison.OrdinalIgnoreCase))
            return Acceso;
        if (tipo.Equals(Identidad, StringComparison.OrdinalIgnoreCase))
            return Identidad;
        if (tipo.Equals("Dominio", StringComparison.OrdinalIgnoreCase))
            throw ReglaDeNegocioException.Validacion("tipo-no-disponible",
                "Los eventos de dominio se consultan en el historial de cada vínculo o expediente.");
        throw ReglaDeNegocioException.Validacion("tipo-invalido", $"'{tipo}' no es un tipo de auditoría (Configuracion, Acceso o Identidad).");
    }

    private static async Task<List<RegistroAuditoriaDto>> ConfiguracionAsync(SesionDeEmpresa empresa, string? entidad, string? entidadId, string? actor,
        DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        IQueryable<RegistroDeCambioDeConfiguracion> consulta = empresa.Sesion.Query<RegistroDeCambioDeConfiguracion>()
            .Where(r => r.EmpresaId == null || r.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(entidad))
            consulta = consulta.Where(r => r.Entidad == entidad);
        if (!string.IsNullOrWhiteSpace(entidadId))
            consulta = consulta.Where(r => r.EntidadId == entidadId);
        if (!string.IsNullOrWhiteSpace(actor))
            consulta = consulta.Where(r => r.ActorId == actor);
        if (desde is not null)
            consulta = consulta.Where(r => r.Fecha >= desde);
        if (hasta is not null)
            consulta = consulta.Where(r => r.Fecha <= hasta);

        var filas = await consulta.OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id).ToListAsync(ct);
        return [.. filas.Select(r =>
        {
            var antes = AJson(r.Antes);
            var despues = AJson(r.Despues);
            return new RegistroAuditoriaDto(r.Id, Configuracion, r.Fecha, r.EmpresaId,
                new ActorAuditoriaDto(r.ActorTipo.ToString(), r.ActorId, null, r.Sistema),
                new EntidadAuditadaDto(r.Entidad, r.EntidadId, NombreDe(despues) ?? NombreDe(antes)),
                r.Accion.ToString(), antes, despues, DiffDeAuditoria.Calcular(antes, despues), r.Justificacion, null, null, null, null,
                r.CorrelationId);
        })];
    }

    /// <summary>Descargas de versiones de documentos compartidos con los vínculos de la empresa y de los PDF de sus certificados.</summary>
    private static async Task<List<RegistroAuditoriaDto>> AccesosAsync(SesionDeEmpresa empresa, ISesiones sesiones, string? entidad, string? entidadId,
        string? actor, DateTimeOffset? desde, DateTimeOffset? hasta, CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        var registros = new List<RegistroAuditoriaDto>();
        var verDocumentos = string.IsNullOrWhiteSpace(entidad) || entidad.Equals("Documento", StringComparison.OrdinalIgnoreCase);
        var verCertificados = string.IsNullOrWhiteSpace(entidad) || entidad.Equals("Certificado", StringComparison.OrdinalIgnoreCase);

        if (verDocumentos)
        {
            var terceros = (await empresa.Vinculos().Select(v => v.TerceroId).ToListAsync(ct)).Distinct().ToArray();
            await using var boveda = sesiones.ConsultaBoveda();
            var documentos = terceros.Length == 0
                ? []
                : (await boveda.Query<Documento>().Where(d => d.TerceroId.IsOneOf(terceros)).ToListAsync(ct)).ToDictionary(d => d.Id);
            if (documentos.Count > 0)
            {
                var ids = documentos.Keys.ToArray();
                var eventos = await boveda.Events.QueryAllRawEvents()
                    .Where(e => e.StreamId.IsOneOf(ids) && e.EventTypesAre(typeof(DocumentoAccedido)))
                    .ToListAsync(ct);
                registros.AddRange(eventos
                    .Where(e => e.Data is DocumentoAccedido { EmpresaId: { } id } && id == empresaId)
                    .Select(e =>
                    {
                        var acceso = (DocumentoAccedido)e.Data;
                        var documento = documentos[e.StreamId];
                        return new RegistroAuditoriaDto(e.Id, Acceso, acceso.Fecha, acceso.EmpresaId,
                            new ActorAuditoriaDto(acceso.ActorTipo, acceso.ActorId, null, null),
                            new EntidadAuditadaDto("Documento", documento.Id.ToString(), $"{documento.NombreTipo} · versión {acceso.Numero}"),
                            "Descargado", null, null, [], null, acceso.Motivo, acceso.VinculoId, acceso.Ip, acceso.UserAgent, null);
                    }));
            }
        }

        if (verCertificados)
        {
            var certificados = (await empresa.Sesion.Query<Certificado>().Where(c => c.EmpresaId == empresaId).ToListAsync(ct))
                .ToDictionary(c => c.Id);
            if (certificados.Count > 0)
            {
                var ids = certificados.Keys.ToArray();
                var eventos = await empresa.Sesion.Events.QueryAllRawEvents()
                    .Where(e => e.StreamId.IsOneOf(ids) && e.EventTypesAre(typeof(CertificadoDescargado)))
                    .ToListAsync(ct);
                registros.AddRange(eventos.Select(e =>
                {
                    var descarga = (CertificadoDescargado)e.Data;
                    var certificado = certificados[e.StreamId];
                    var folio = certificado.Emisiones.FirstOrDefault(x => x.Numero == descarga.Emision)?.Folio ?? certificado.FolioRaiz;
                    return new RegistroAuditoriaDto(e.Id, Acceso, descarga.Fecha, certificado.EmpresaId,
                        new ActorAuditoriaDto(descarga.ActorTipo, descarga.ActorId, null, null),
                        new EntidadAuditadaDto("Certificado", certificado.Id.ToString(), folio),
                        "Descargado", null, null, [], null, descarga.Publico ? "Verificación pública" : null, certificado.VinculoId,
                        descarga.Ip, descarga.UserAgent, null);
                }));
            }
        }

        // Exportaciones de la empresa (informes, auditoría y expedientes, IF-6).
        var verExportaciones = string.IsNullOrWhiteSpace(entidad) || entidad.Equals("Exportacion", StringComparison.OrdinalIgnoreCase) ||
                               entidad.Equals("Expediente", StringComparison.OrdinalIgnoreCase);
        if (verExportaciones)
        {
            var exportaciones = await empresa.Sesion.Query<Informes.ExportacionRegistrada>().Where(x => x.EmpresaId == empresaId).ToListAsync(ct);
            registros.AddRange(exportaciones
                .Where(x => string.IsNullOrWhiteSpace(entidad) || (x.EntidadId is null) == entidad.Equals("Exportacion", StringComparison.OrdinalIgnoreCase))
                .Select(x => new RegistroAuditoriaDto(x.Id, Acceso, x.Fecha, x.EmpresaId, new ActorAuditoriaDto(x.ActorTipo, x.ActorId, null, null),
                    x.EntidadId is null
                        ? new EntidadAuditadaDto("Exportacion", x.Id.ToString(), NombreDeExportacion(x.Tipo))
                        : new EntidadAuditadaDto("Expediente", x.EntidadId, x.EntidadNombre),
                    "Exportado", null, null, [], null,
                    $"{x.Formato} · {x.Filas} {(x.Tipo == "Expediente" ? "archivos" : "filas")} · SHA-256 {x.Sha256}{(x.Filtros.Length > 0 ? $" · {x.Filtros}" : string.Empty)}",
                    x.VinculoId, x.Ip, x.UserAgent, null)));
        }

        return [.. registros
            .Where(r => string.IsNullOrWhiteSpace(entidadId) || r.Entidad.Id == entidadId || r.VinculoId?.ToString() == entidadId)
            .Where(r => string.IsNullOrWhiteSpace(actor) || r.Actor.Id == actor)
            .Where(r => (desde is null || r.Fecha >= desde) && (hasta is null || r.Fecha <= hasta))
            .OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id)];
    }

    private static string NombreDeExportacion(string tipo) => tipo switch
    {
        "InformeEstadoExpedientes" => "Informe de estado de expedientes",
        "InformeVinculos" => "Informe de terceros y vínculos",
        "Auditoria" => "Auditoría",
        _ => tipo
    };

    private static async Task<Dictionary<string, string>> NombresDeUsuariosAsync(SesionDeEmpresa empresa, IEnumerable<RegistroAuditoriaDto> registros,
        CancellationToken ct)
    {
        var ids = registros.Where(r => r.Actor.Tipo == nameof(ActorTipo.Usuario))
            .Select(r => Guid.TryParse(r.Actor.Id, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
            return [];
        var usuarios = await empresa.Sesion.LoadManyAsync<Usuario>(ct, ids);
        return usuarios.ToDictionary(u => u.Id.ToString(), u => u.Nombre);
    }

    private static JsonElement? AJson(object? valor) => valor switch
    {
        null => null,
        JsonElement elemento => elemento.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : elemento.Clone(),
        _ => JsonSerializer.SerializeToElement(valor, valor.GetType(), JsonSerializerOptions.Web)
    };

    /// <summary>Nombre legible de la entidad: <c>nombre</c>, si no <c>codigo</c>, si no <c>etiqueta</c> del objeto auditado.</summary>
    private static string? NombreDe(JsonElement? json)
    {
        if (json is not { ValueKind: JsonValueKind.Object } objeto)
            return null;
        foreach (var propiedad in objeto.EnumerateObject())
            if (propiedad.Name.Equals("nombre", StringComparison.OrdinalIgnoreCase) && propiedad.Value.ValueKind == JsonValueKind.String)
                return propiedad.Value.GetString();
        foreach (var clave in new[] { "codigo", "etiqueta", "url" })
            foreach (var propiedad in objeto.EnumerateObject())
                if (propiedad.Name.Equals(clave, StringComparison.OrdinalIgnoreCase) && propiedad.Value.ValueKind == JsonValueKind.String)
                    return propiedad.Value.GetString();
        return null;
    }
}

/// <summary>Diferencias campo por campo entre el antes y el después de un cambio de configuración.</summary>
public static class DiffDeAuditoria
{
    public static IReadOnlyList<DiffCampoDto> Calcular(JsonElement? antes, JsonElement? despues)
    {
        var lista = new List<DiffCampoDto>();
        Comparar(string.Empty, antes, despues, lista);
        return lista;
    }

    private static void Comparar(string ruta, JsonElement? antes, JsonElement? despues, List<DiffCampoDto> lista)
    {
        var a = Normalizar(antes);
        var d = Normalizar(despues);
        if (a is null && d is null)
            return;
        if (a is { ValueKind: JsonValueKind.Object } objetoA && d is { ValueKind: JsonValueKind.Object } objetoD)
        {
            var nombres = objetoA.EnumerateObject().Select(p => p.Name)
                .Concat(objetoD.EnumerateObject().Select(p => p.Name))
                .Distinct(StringComparer.Ordinal);
            foreach (var nombre in nombres)
                Comparar(ruta.Length == 0 ? nombre : $"{ruta}.{nombre}", Propiedad(objetoA, nombre), Propiedad(objetoD, nombre), lista);
            return;
        }

        // Un objeto que aparece o desaparece entero se desglosa en sus campos.
        if (a is null && d is { ValueKind: JsonValueKind.Object } nuevo)
        {
            foreach (var p in nuevo.EnumerateObject())
                Comparar(ruta.Length == 0 ? p.Name : $"{ruta}.{p.Name}", null, p.Value, lista);
            return;
        }
        if (d is null && a is { ValueKind: JsonValueKind.Object } viejo)
        {
            foreach (var p in viejo.EnumerateObject())
                Comparar(ruta.Length == 0 ? p.Name : $"{ruta}.{p.Name}", p.Value, null, lista);
            return;
        }

        if (a is not null && d is not null && JsonElement.DeepEquals(a.Value, d.Value))
            return;
        lista.Add(new DiffCampoDto(ruta.Length == 0 ? "(valor)" : ruta, a, d,
            a is null ? "Agregado" : d is null ? "Eliminado" : "Modificado"));
    }

    private static JsonElement? Normalizar(JsonElement? valor) =>
        valor is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) } v ? v : null;

    private static JsonElement? Propiedad(JsonElement objeto, string nombre) =>
        objeto.TryGetProperty(nombre, out var valor) ? valor : null;
}
