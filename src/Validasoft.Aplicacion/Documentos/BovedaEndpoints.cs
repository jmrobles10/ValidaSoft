using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>
/// La bóveda vista por su dueño (sesión <c>Tercero</c>): sus documentos, la versión actual y, por cada empresa de su sesión,
/// si se le compartió. Un documento de otro tercero responde 404.
/// </summary>
public static class BovedaEndpoints
{
    [WolverineGet("/public/v1/boveda")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<BovedaDto> Obtener(HttpContext http, ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var terceroId = tercero.TerceroId;
        var hoy = reloj.Hoy();
        await using var consulta = sesiones.ConsultaBoveda();
        var documentos = await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId).OrderBy(d => d.CreadoEn).ToListAsync(ct);
        var vinculos = await VinculosAsync(sesiones, tercero, ct);

        return new BovedaDto(terceroId,
        [
            .. documentos.Where(d => d.Ambito == AmbitoDocumento.Global || tercero.Vinculos.Any(v => v.VinculoId == d.VinculoId)).Select(d => new DocumentoBovedaDto(d.Id, d.Resumen(), d.Ambito.ToString(), d.Clase.ToString(), d.VinculoId,
                d.VersionActual is { } actual ? d.VersionRequerida(actual).ADto(d.Id, null, hoy) : null, d.Versiones.Count, d.PorEmpresa(vinculos, hoy)))
        ]);
    }

    [WolverineGet("/public/v1/boveda/documentos/{documentoId}")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<DocumentoDto> ObtenerDocumento(Guid documentoId, HttpContext http, ISesiones sesiones, TimeProvider reloj,
        CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var consulta = sesiones.ConsultaBoveda();
        var documento = DelTercero(await consulta.LoadAsync<Documento>(documentoId, ct), tercero);
        return documento.ADto(await VinculosAsync(sesiones, tercero, ct), reloj.Hoy());
    }

    /// <summary>URL prefirmada GET de una versión propia; queda como <c>DocumentoAccedido</c> con el tercero como actor.</summary>
    [WolverinePost("/public/v1/boveda/documentos/{documentoId}/versiones/{numero}/descarga")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<DescargaDto> Descargar(Guid documentoId, int numero, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        // El acceso se anota en el stream del documento, donde también escriben las vigencias y la compartición en segundo
        // plano: si una de ellas gana la carrera, se reintenta en lugar de responder 412 a quien solo quería descargar.
        var documento = await Peticiones.ConReintentosAsync(async () =>
        {
            await using var sesion = sesiones.Boveda();
            var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
            var delTercero = DelTercero(stream.Aggregate, tercero);
            stream.AppendOne(delTercero.RegistrarAcceso(numero, null, ActorTipo.Tercero.ToString(), contexto.ActorId, Ip(http), UserAgent(http),
                "Descarga del tercero", reloj.GetUtcNow()));
            await sesion.SaveChangesAsync(ct);
            return delTercero;
        });
        return await UrlDeDescargaAsync(almacen, reglas, documento.VersionRequerida(numero), ct);
    }

    /// <summary>
    /// El tercero retira una versión (no se borra si alguna empresa la recibió o la descargó, docs/02 §10.3). Si era la
    /// actual vuelve la anterior, se comparte con quien corresponda y los expedientes abiertos que la tenían la reciben de
    /// vuelta (RQ-2). Devuelta o retirada ⇒ 409 <c>estado-invalido</c>.
    /// </summary>
    [WolverinePost("/public/v1/boveda/documentos/{documentoId}/versiones/{numero}/retirar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<RetiroDto> Retirar(Guid documentoId, int numero, RetirarVersionDto dto, HttpContext http, ISesiones sesiones,
        IContextoSolicitud contexto, IMessageBus mensajes, IAlmacenDeArchivos almacen, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Boveda(mensajes);
        var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
        var documento = DelTercero(stream.Aggregate, tercero);
        var version = documento.VersionRequerida(numero);

        var eventos = documento.RetirarVersion(numero, dto.Motivo, ahora);
        foreach (var evento in eventos)
            stream.AppendOne(evento);
        var proyectado = documento.Proyectar(eventos);
        var compartidos = proyectado.VersionActual is not null && proyectado.VersionActual != documento.VersionActual
            ? await ComparticionDeDocumentos.CompartirActualAsync(sesion, stream, proyectado, OrigenComparticion.Automatica, ahora, ct, mensajes, contexto)
            : [];
        // Quienes ya tenían la versión restituida también deben volver a ella en sus requisitos.
        if (proyectado.VersionActual is { } restituida && restituida != documento.VersionActual)
        {
            await ComparticionDeDocumentos.PedirSincronizacionAsync(mensajes,
                proyectado.Comparticiones.Where(c => c.Numero == restituida).Select(c => (c.TenantId, c.VinculoId)), contexto);
            await mensajes.PublishAsync(new CalcularVigenciasDelDocumento(documento.Id));
        }

        // El objeto es content-addressed: solo se elimina si nadie lo vio y ninguna otra versión del tercero apunta a él (una
        // versión solo de formulario no tiene binario).
        var nadieLoVio = version.TieneArchivo && !documento.Comparticiones.Any(c => c.Numero == numero) && !version.AccedidaPorEmpresa;
        var terceroId = tercero.TerceroId;
        var otros = nadieLoVio
            ? await sesion.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct)
            : [];
        var eliminar = nadieLoVio && !otros.SelectMany(d => d.Versiones.Select(v => (d.Id, v)))
            .Any(x => (x.Id != documento.Id || x.v.Numero != numero) && x.v.Bucket == version.Bucket && x.v.ClaveObjeto == version.ClaveObjeto);

        await sesion.SaveChangesAsync(ct);
        if (eliminar)
            await almacen.EliminarAsync(version.Bucket, version.ClaveObjeto, ct);

        return new RetiroDto(proyectado.ADto(await VinculosAsync(sesiones, tercero, ct)), eliminar, compartidos);
    }

    /// <summary>El documento existe y es del tercero de la sesión (un <c>PorVinculo</c>, además, de un vínculo de su sesión); si no, 404.</summary>
    internal static Documento DelTercero(Documento? documento, SesionDeTercero tercero) =>
        documento is not null && documento.TerceroId == tercero.TerceroId &&
        (documento.Ambito == AmbitoDocumento.Global || tercero.Vinculos.Any(v => v.VinculoId == documento.VinculoId))
            ? documento
            : throw ReglaDeNegocioException.NoEncontrado("documento-no-encontrado", "No existe ese documento en tu bóveda.");

    internal static async Task<DescargaDto> UrlDeDescargaAsync(IAlmacenDeArchivos almacen, ReglasDeCarga reglas, VersionDocumento version,
        CancellationToken ct)
    {
        var mime = version.Hechos?.MimeReal ?? version.MimeDeclarado;
        var url = await almacen.UrlDeDescargaAsync(version.Bucket, version.ClaveObjeto, version.NombreOriginal, mime, reglas.VigenciaDescarga, ct);
        return new DescargaDto(url.Url, url.Expira, version.NombreOriginal, mime);
    }

    internal static string Ip(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    internal static string UserAgent(HttpContext http) => http.Request.Headers.UserAgent.ToString();

    private static async Task<IReadOnlyList<(Guid VinculoId, Empresa? Empresa, Guid EmpresaId)>> VinculosAsync(ISesiones sesiones,
        SesionDeTercero tercero, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var vistos = await VistaDelTercero.CargarAsync(consulta, tercero, [.. tercero.Vinculos.Select(v => v.VinculoId)], ct);
        return [.. vistos.Select(v => (v.Vinculo.Id, v.Empresa, v.Vinculo.EmpresaId))];
    }
}

/// <summary>
/// Documentos de un vínculo vistos por la empresa (<c>X-Empresa</c>): solo las versiones compartidas con ese vínculo, que
/// existen únicamente tras la aceptación de la invitación (B-1). Lo no compartido responde 404 (T-4), nunca 403.
/// </summary>
public static class DocumentosDeVinculoEndpoints
{
    [WolverineGet("/api/v1/vinculos/{id}/documentos")]
    [Authorize(Policy = Permisos.DocumentosLeer)]
    public static async Task<IReadOnlyList<DocumentoVistaEmpresaDto>> Listar(Guid id, SesionDeEmpresa empresa, ISesiones sesiones,
        TimeProvider reloj, CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        if (!Visible(vinculo))
            return [];

        var terceroId = vinculo.TerceroId;
        await using var consulta = sesiones.ConsultaBoveda();
        var documentos = await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId).OrderBy(d => d.CreadoEn).ToListAsync(ct);
        var hoy = reloj.Hoy();
        return [.. documentos.Select(d => Vista(d, id, hoy)).OfType<DocumentoVistaEmpresaDto>()];
    }

    [WolverineGet("/api/v1/vinculos/{id}/documentos/{documentoId}")]
    [Authorize(Policy = Permisos.DocumentosLeer)]
    public static async Task<DocumentoVistaEmpresaDto> Obtener(Guid id, Guid documentoId, SesionDeEmpresa empresa, ISesiones sesiones,
        TimeProvider reloj, CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        await using var consulta = sesiones.ConsultaBoveda();
        var documento = await consulta.LoadAsync<Documento>(documentoId, ct);
        return Visible(vinculo) && documento is not null && documento.TerceroId == vinculo.TerceroId && Vista(documento, id, reloj.Hoy()) is { } vista
            ? vista
            : throw ReglaDeNegocioException.NoEncontrado("documento-no-encontrado", "No existe ese documento para el vínculo.");
    }

    /// <summary>URL prefirmada GET (5 min) de una versión compartida con el vínculo; registra <c>DocumentoAccedido</c> con el actor.</summary>
    [WolverinePost("/api/v1/vinculos/{id}/documentos/{documentoId}/versiones/{numero}/descarga")]
    [Authorize(Policy = Permisos.DocumentosDescargar)]
    public static async Task<DescargaDto> Descargar(Guid id, Guid documentoId, int numero, HttpContext http, SesionDeEmpresa empresa,
        ISesiones sesiones, IContextoSolicitud contexto, IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        if (!Visible(vinculo))
            throw ReglaDeNegocioException.NoEncontrado("documento-no-encontrado", "No existe ese documento para el vínculo.");

        var destino = new DestinoDeComparticion(contexto.TenantRequerido(), vinculo.EmpresaId, vinculo.Id);
        // Mismo criterio que la descarga del tercero: el acceso se reintenta si un proceso en segundo plano escribió antes.
        var documento = await Peticiones.ConReintentosAsync(async () =>
        {
            await using var sesion = sesiones.Boveda();
            var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
            var encontrado = stream.Aggregate;
            if (encontrado is null || encontrado.TerceroId != vinculo.TerceroId)
                throw ReglaDeNegocioException.NoEncontrado("documento-no-encontrado", "No existe ese documento para el vínculo.");
            stream.AppendOne(encontrado.RegistrarAcceso(numero, destino, contexto.ActorTipo.ToString(), contexto.ActorId, BovedaEndpoints.Ip(http),
                BovedaEndpoints.UserAgent(http), "Descarga", reloj.GetUtcNow()));
            await sesion.SaveChangesAsync(ct);
            return encontrado;
        });
        return await BovedaEndpoints.UrlDeDescargaAsync(almacen, reglas, documento.VersionRequerida(numero), ct);
    }

    /// <summary>docs/07 §5.2: invitación aceptada y vínculo no anulado (desvinculado o bloqueado conserva lo ya compartido).</summary>
    internal static bool Visible(Dominio.Vinculos.Vinculo vinculo) =>
        vinculo.InvitacionAceptada && vinculo.Estado != Dominio.Vinculos.EstadoVinculo.Anulado;

    /// <summary>Solo las versiones compartidas con el vínculo, cada una con sus datos declarados y la vigencia de este vínculo.</summary>
    internal static DocumentoVistaEmpresaDto? Vista(Documento documento, Guid vinculoId, DateOnly hoy)
    {
        var numeros = documento.Comparticiones.Where(c => c.VinculoId == vinculoId).Select(c => c.Numero).ToHashSet();
        if (numeros.Count == 0)
            return null;

        var actual = documento.VersionActual is { } n && numeros.Contains(n) ? n : (int?)null;
        return new DocumentoVistaEmpresaDto(documento.Id, documento.Resumen(), documento.Ambito.ToString(), actual,
            [.. documento.Versiones.Where(v => numeros.Contains(v.Numero)).OrderBy(v => v.Numero).Select(v => v.ADto(documento.Id, vinculoId, hoy))]);
    }
}
