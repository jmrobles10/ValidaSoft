using System.Security.Cryptography;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>
/// Plantilla descargable de un tipo de documento (TD-5): la empresa la sube en dos pasos como la carga del tercero (URL
/// prefirmada PUT a la zona temporal del bucket del tenant y confirmación que verifica tamaño, hash y tipo real), queda
/// versionada (vigente e historial) y auditada. El tercero la descarga con URL prefirmada solo desde un requisito de su
/// propio expediente.
/// </summary>
public static class PlantillasEndpoints
{
    public const string Entidad = "PlantillaTipoDocumento";

    /// <summary>Paso 1: 200 con la URL PUT de la versión siguiente. 413 tamaño sobre el global; 415 si no es PDF, DOCX o XLSX.</summary>
    [WolverinePut("/api/v1/tipos-documento/{id}/plantilla")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<CargaDePlantillaDto> Iniciar(Guid id, IniciarPlantillaDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        await using var sesion = sesiones.DelTenant();
        var tipo = await TiposDocumentoEndpoints.CargarAsync(sesion, id, ct);
        var carga = tipo.IniciarPlantilla(ClavesDeAlmacen.BucketDeTenant(tenantId), dto.NombreArchivo, dto.TamanoBytes, dto.TipoMime, dto.Sha256,
            reglas.TamanoMaximoBytes, reglas.VigenciaSubida, reloj.GetUtcNow());
        sesion.Store(tipo);
        await sesion.SaveChangesAsync(ct);

        await almacen.AsegurarBucketAsync(carga.Bucket, ct);
        var url = await almacen.UrlDeSubidaAsync(carga.Bucket, carga.ClaveTemporal, carga.MimeDeclarado, reglas.VigenciaSubida, ct);
        return new CargaDePlantillaDto(tipo.Id, carga.Version, new InstruccionCargaDto(url.Metodo, url.Url, url.Cabeceras, url.Expira));
    }

    /// <summary>
    /// Paso 2: lee el objeto y verifica tamaño (413), SHA-256 declarado (422 <c>hash-no-coincide</c>) y tipo real por sus
    /// bytes (415). La versión queda vigente y en el historial (mismo archivo que la vigente ⇒ <c>duplicado</c>). 404
    /// <c>carga-no-encontrada</c>; 409 <c>archivo-no-cargado</c>.
    /// </summary>
    [WolverinePost("/api/v1/tipos-documento/{id}/plantilla/{version}/confirmar")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<ConfirmacionPlantillaDto> Confirmar(Guid id, int version, ISesiones sesiones, IContextoSolicitud contexto,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipo = await TiposDocumentoEndpoints.CargarAsync(sesion, id, ct);
        var carga = tipo.CargaDePlantillaRequerida(version);
        var antes = tipo.PlantillaDto();

        var objeto = await almacen.ConsultarAsync(carga.Bucket, carga.ClaveTemporal, ct)
                     ?? throw ReglaDeNegocioException.Conflicto("archivo-no-cargado",
                         "Aún no recibimos la plantilla: súbela con la URL de carga y vuelve a confirmar.");
        var limites = new ReglasDeArchivo("La plantilla", FormatosDePlantilla.Permitidos, reglas.TamanoMaximoBytes, int.MaxValue, false);
        var contenido = await CargaEndpoints.ConLimpiezaAsync(almacen, carga.Bucket, carga.ClaveTemporal, async () =>
        {
            limites.ValidarTamano(objeto.Tamano);
            return await CargaEndpoints.LeerAsync(almacen, carga.Bucket, carga.ClaveTemporal, limites, ct);
        }, ct);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(contenido));
        var mime = await CargaEndpoints.ConLimpiezaAsync(almacen, carga.Bucket, carga.ClaveTemporal, () =>
        {
            if (carga.Sha256Declarado is { } declarado && declarado != sha256)
                throw ReglaDeNegocioException.Validacion("hash-no-coincide", "El archivo recibido no coincide con el SHA-256 declarado.");
            return Task.FromResult(FormatosDePlantilla.Detectar(contenido)
                                   ?? throw ReglaDeNegocioException.TipoNoSoportado(
                                       $"La plantilla debe ser {FormatosDePlantilla.Descripcion} (la extensión o el tipo declarado no bastan)."));
        }, ct);

        var clave = ClavesDeAlmacen.Plantilla(tipo.Id, sha256);
        await almacen.CopiarAsync(carga.Bucket, carga.ClaveTemporal, carga.Bucket, clave, mime, new Dictionary<string, string>
        {
            ["nombre-original"] = Uri.EscapeDataString(carga.NombreOriginal),
            ["tipo-documento-id"] = tipo.Id.ToString(),
            ["plantilla-version"] = version.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["mime"] = mime
        }, ct);

        var ahora = reloj.GetUtcNow();
        var (plantilla, duplicado) = tipo.ConfirmarPlantilla(version, sha256, mime, contenido.LongLength, clave, contexto.ActorId, ahora);
        sesion.Store(tipo);
        if (!duplicado)
            Auditoria.Registrar(sesion, contexto, Entidad, tipo.Id.ToString(), AccionConfiguracion.Publicado, antes, tipo.PlantillaDto(), ahora);
        await sesion.SaveChangesAsync(ct);
        await almacen.EliminarAsync(carga.Bucket, carga.ClaveTemporal, ct);
        return new ConfirmacionPlantillaDto(duplicado, plantilla.ADto(tipo.PlantillaVigente));
    }

    [WolverineGet("/api/v1/tipos-documento/{id}/plantilla")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<PlantillaTipoDocumentoDto> Obtener(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        return (await TiposDocumentoEndpoints.CargarAsync(sesion, id, ct)).PlantillaDto();
    }

    /// <summary>URL prefirmada GET de la vigente o, con <c>?version=n</c>, de una del historial. 404 <c>plantilla-no-encontrada</c>.</summary>
    [WolverineGet("/api/v1/tipos-documento/{id}/plantilla/descarga")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<DescargaDto> Descargar(Guid id, int? version, ISesiones sesiones, IAlmacenDeArchivos almacen, ReglasDeCarga reglas,
        CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipo = await TiposDocumentoEndpoints.CargarAsync(sesion, id, ct);
        return await UrlAsync(almacen, reglas, tipo.PlantillaRequerida(version), ct);
    }

    /// <summary>Deja el tipo sin plantilla vigente (el historial se conserva). 404 <c>plantilla-no-encontrada</c>.</summary>
    [WolverineDelete("/api/v1/tipos-documento/{id}/plantilla")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<PlantillaTipoDocumentoDto> Retirar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipo = await TiposDocumentoEndpoints.CargarAsync(sesion, id, ct);
        var antes = tipo.PlantillaDto();
        var ahora = reloj.GetUtcNow();
        tipo.RetirarPlantilla(ahora);
        sesion.Store(tipo);
        var despues = tipo.PlantillaDto();
        Auditoria.Registrar(sesion, contexto, Entidad, tipo.Id.ToString(), AccionConfiguracion.Archivado, antes, despues, ahora);
        await sesion.SaveChangesAsync(ct);
        return despues;
    }

    /// <summary>
    /// El tercero descarga la plantilla vigente del tipo de un requisito de su expediente: el expediente debe ser de un
    /// vínculo de su sesión (si no, 404 <c>expediente-no-encontrado</c>, T-4); sin plantilla ⇒ 404 <c>plantilla-no-encontrada</c>.
    /// </summary>
    [WolverineGet("/public/v1/expedientes/{id}/requisitos/{requisitoId}/plantilla")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<DescargaDto> DescargarDelTercero(Guid id, Guid requisitoId, HttpContext http, ISesiones sesiones,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Relacion);
        await using var consulta = sesiones.ConsultaGlobal();
        var expediente = await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.Id == id).SingleOrDefaultAsync(ct);
        var enSesion = expediente is null || expediente.TerceroId != tercero.TerceroId
            ? null
            : tercero.Vinculos.FirstOrDefault(v => v.VinculoId == expediente.VinculoId);
        if (expediente is null || enSesion is null)
            throw ReglaDeNegocioException.NoEncontrado("expediente-no-encontrado", "No existe ese expediente.");

        var requisito = expediente.BuscarRequisito(requisitoId)
                        ?? throw ReglaDeNegocioException.NoEncontrado("requisito-no-encontrado", "Ese requisito no está en el expediente.");
        var tipoId = requisito.TipoDocumentoTenantId;
        var tenantId = enSesion.TenantId;
        var tipo = await consulta.Query<TipoDocumentoTenant>().Where(t => t.AnyTenant() && t.Id == tipoId && t.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);
        var plantilla = tipo?.Plantilla
                        ?? throw ReglaDeNegocioException.NoEncontrado("plantilla-no-encontrada", "Este documento no tiene plantilla para descargar.");
        return await UrlAsync(almacen, reglas, plantilla, ct);
    }

    private static async Task<DescargaDto> UrlAsync(IAlmacenDeArchivos almacen, ReglasDeCarga reglas, PlantillaDeTipo plantilla, CancellationToken ct)
    {
        var url = await almacen.UrlDeDescargaAsync(plantilla.Bucket, plantilla.ClaveObjeto, plantilla.NombreOriginal, plantilla.Mime,
            reglas.VigenciaDescarga, ct);
        return new DescargaDto(url.Url, url.Expira, plantilla.NombreOriginal, plantilla.Mime);
    }
}
