using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Configuracion;

/// <summary>
/// Plantillas de notificación de la empresa activa (docs/05 §2, docs/06 <c>/configuracion/.../plantillas</c>, PL-1): asunto,
/// cabecera y pie de los correos que Validasoft le manda al tercero en nombre de la empresa, con auditoría y vista previa.
/// </summary>
public static class PlantillasNotificacionEndpoints
{
    /// <summary>El catálogo completo con lo personalizado de la empresa (sin personalizar: textos nulos y versión 0).</summary>
    [WolverineGet("/api/v1/plantillas-notificacion")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<IReadOnlyList<PlantillaNotificacionDto>> Listar(SesionDeEmpresa empresa, RedactorDeCorreos redactor, TimeProvider reloj,
        CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        var datos = await DatosAsync(empresa, empresaId, ct);
        var guardadas = (await empresa.Sesion.Query<PlantillaNotificacion>().Where(p => p.EmpresaId == empresaId).ToListAsync(ct))
            .ToDictionary(p => p.Evento, StringComparer.Ordinal);
        var ahora = reloj.GetUtcNow();
        return
        [
            .. CatalogoDePlantillas.Eventos.Select(e =>
                ADto(e, guardadas.GetValueOrDefault(e.Evento), EjemplosDeCorreo.Redactar(redactor, e.Evento, datos.Nombre, datos.Prefijo, ahora).Mensaje.Asunto))
        ];
    }

    /// <summary>
    /// Guarda asunto, cabecera y pie (vacío ⇒ nulo; todo nulo = vuelve al predeterminado) y sube la versión. 404
    /// <c>plantilla-no-encontrada</c> fuera del catálogo; 422 <c>canal-invalido</c>, <c>asunto-invalido</c>,
    /// <c>asunto-demasiado-largo</c>, <c>texto-demasiado-largo</c> o <c>variable-no-permitida</c> (con <c>campo</c>).
    /// </summary>
    [WolverinePut("/api/v1/plantillas-notificacion/{evento}/{canal}")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<PlantillaNotificacionDto> Actualizar(string evento, string canal, ActualizarPlantillaNotificacionDto dto,
        SesionDeEmpresa empresa, IContextoSolicitud contexto, RedactorDeCorreos redactor, TimeProvider reloj, CancellationToken ct)
    {
        var definicion = Evento(evento);
        var textos = PlantillaNotificacion.Normalizar(definicion, canal, dto.Asunto, dto.Cabecera, dto.Pie);
        var empresaId = empresa.EmpresaId;
        var datos = await DatosAsync(empresa, empresaId, ct);
        var ahora = reloj.GetUtcNow();
        var asuntoPorDefecto = EjemplosDeCorreo.Redactar(redactor, definicion.Evento, datos.Nombre, datos.Prefijo, ahora).Mensaje.Asunto;

        var plantilla = await empresa.Sesion.LoadAsync<PlantillaNotificacion>(
            PlantillaNotificacion.Clave(empresaId, definicion.Evento, CatalogoDePlantillas.Correo), ct);
        var antes = plantilla is null ? null : ADto(definicion, plantilla, asuntoPorDefecto);
        plantilla ??= PlantillaNotificacion.Nueva(empresaId, definicion, CatalogoDePlantillas.Correo);
        plantilla.Actualizar(textos, ahora);
        var despues = ADto(definicion, plantilla, asuntoPorDefecto);

        empresa.Sesion.Store(plantilla);
        Auditoria.Registrar(empresa.Sesion, contexto, "PlantillaNotificacion", plantilla.Id,
            antes is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes, despues, ahora, empresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return despues;
    }

    /// <summary>
    /// El correo con lo que se está editando (sin guardar; mismas validaciones) armado con el redactor real y datos de
    /// ejemplo: la empresa activa, un folio, un tercero y un documento de ejemplo.
    /// </summary>
    [WolverinePost("/api/v1/plantillas-notificacion/{evento}/{canal}/vista-previa")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<VistaPreviaCorreoDto> Previsualizar(string evento, string canal, ActualizarPlantillaNotificacionDto dto,
        SesionDeEmpresa empresa, RedactorDeCorreos redactor, TimeProvider reloj, CancellationToken ct)
    {
        var definicion = Evento(evento);
        var textos = PlantillaNotificacion.Normalizar(definicion, canal, dto.Asunto, dto.Cabecera, dto.Pie);
        var datos = await DatosAsync(empresa, empresa.EmpresaId, ct);
        var ejemplo = EjemplosDeCorreo.Redactar(redactor, definicion.Evento, datos.Nombre, datos.Prefijo, reloj.GetUtcNow());
        var mensaje = ComposicionDeCorreo.Aplicar(ejemplo.Mensaje, textos, ejemplo.Variables);
        // Las imágenes de marca que el correo pide por cid: (el GIF de la aprobación) se incrustan para que la vista previa las muestre.
        return new VistaPreviaCorreoDto(mensaje.Asunto, mensaje.Texto, MarcoDeCorreo.EnLinea(mensaje.Html));
    }

    private static EventoDePlantilla Evento(string evento) =>
        CatalogoDePlantillas.Buscar(evento)
        ?? throw ReglaDeNegocioException.NoEncontrado("plantilla-no-encontrada",
            $"'{evento}' no es un correo personalizable ({string.Join(", ", CatalogoDePlantillas.Eventos.Select(e => e.Evento))}).");

    private static async Task<(string Nombre, string Prefijo)> DatosAsync(SesionDeEmpresa empresa, Guid empresaId, CancellationToken ct)
    {
        var datos = await empresa.Sesion.LoadAsync<Empresa>(empresaId, ct);
        return (datos?.Nombre ?? "La empresa", string.IsNullOrWhiteSpace(datos?.PrefijoFolio) ? "EMP" : datos.PrefijoFolio);
    }

    private static PlantillaNotificacionDto ADto(EventoDePlantilla evento, PlantillaNotificacion? plantilla, string asuntoPorDefecto) =>
        new(evento.Evento, CatalogoDePlantillas.Correo, evento.Nombre, evento.Descripcion, asuntoPorDefecto, plantilla?.Asunto, plantilla?.Cabecera,
            plantilla?.Pie, evento.Variables, plantilla?.Version ?? 0, plantilla?.ActualizadaEn);
}
