using System.Text.Json;
using JasperFx.Events;
using Marten;
using Marten.Events;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Identidad;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Auditoría de la empresa activa (docs/05 §2.7, docs/06 <c>/auditoria</c>, AU-1): los cambios de configuración de la empresa y
/// los de todo el grupo, con el detalle campo por campo, y los accesos a documentos y a certificados de la empresa. Los
/// eventos de dominio se consultan en el historial de cada vínculo o expediente; la auditoría de identidad aún no se registra.
/// Se arma al consultar, de lo más reciente a lo más antiguo; cursor opaco.
/// </summary>
public static class AuditoriaEndpoints
{
    private const string Configuracion = "Configuracion";
    private const string Acceso = "Acceso";

    [WolverineGet("/api/v1/auditoria")]
    [Authorize(Policy = Permisos.AuditoriaLeer)]
    public static async Task<PaginaDto<RegistroAuditoriaDto>> Listar(SesionDeEmpresa empresa, ISesiones sesiones, string? tipo, string? entidad,
        string? entidadId, string? actor, DateTimeOffset? desde, DateTimeOffset? hasta, int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var salto = Peticiones.LeerDesplazamiento(cursor);
        if (desde is not null && hasta is not null && desde > hasta)
            throw ReglaDeNegocioException.Validacion("rango-invalido", "La fecha inicial es posterior a la final.");

        var registros = TipoDe(tipo) switch
        {
            Configuracion => await ConfiguracionAsync(empresa, entidad, entidadId, actor, desde, hasta, ct),
            _ => await AccesosAsync(empresa, sesiones, entidad, entidadId, actor, desde, hasta, ct)
        };

        var nombres = await NombresDeUsuariosAsync(empresa, registros, ct);
        var pagina = registros.Skip(salto).Take(tamano)
            .Select(r => r.Actor.Tipo == nameof(ActorTipo.Usuario) && r.Actor.Id is { } id && nombres.TryGetValue(id, out var nombre)
                ? r with { Actor = r.Actor with { Nombre = nombre } }
                : r)
            .ToList();
        var siguiente = registros.Count > salto + tamano ? Peticiones.EscribirDesplazamiento(salto + tamano) : null;
        return new PaginaDto<RegistroAuditoriaDto>(pagina, siguiente);
    }

    private static string TipoDe(string? tipo)
    {
        if (string.IsNullOrWhiteSpace(tipo) || tipo.Equals(Configuracion, StringComparison.OrdinalIgnoreCase))
            return Configuracion;
        if (tipo.Equals(Acceso, StringComparison.OrdinalIgnoreCase))
            return Acceso;
        if (tipo.Equals("Dominio", StringComparison.OrdinalIgnoreCase) || tipo.Equals("Identidad", StringComparison.OrdinalIgnoreCase))
            throw ReglaDeNegocioException.Validacion("tipo-no-disponible",
                "Los eventos de dominio se consultan en el historial de cada vínculo o expediente; la auditoría de identidad aún no se registra.");
        throw ReglaDeNegocioException.Validacion("tipo-invalido", $"'{tipo}' no es un tipo de auditoría (Configuracion o Acceso).");
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

        return [.. registros
            .Where(r => string.IsNullOrWhiteSpace(entidadId) || r.Entidad.Id == entidadId || r.VinculoId?.ToString() == entidadId)
            .Where(r => string.IsNullOrWhiteSpace(actor) || r.Actor.Id == actor)
            .Where(r => (desde is null || r.Fecha >= desde) && (hasta is null || r.Fecha <= hasta))
            .OrderByDescending(r => r.Fecha).ThenByDescending(r => r.Id)];
    }

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
