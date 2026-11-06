using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;
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
    public static async Task<BovedaDto> Obtener(HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var terceroId = tercero.TerceroId;
        await using var consulta = sesiones.ConsultaBoveda();
        var documentos = await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId).OrderBy(d => d.CreadoEn).ToListAsync(ct);
        var vinculos = await VinculosAsync(sesiones, tercero, ct);

        return new BovedaDto(terceroId,
        [
            .. documentos.Where(d => d.Ambito == AmbitoDocumento.Global || tercero.Vinculos.Any(v => v.VinculoId == d.VinculoId)).Select(d => new DocumentoBovedaDto(d.Id, d.Resumen(), d.Ambito.ToString(), d.Clase.ToString(), d.VinculoId,
                d.VersionActual is { } actual ? d.VersionRequerida(actual).ADto(d.Id) : null, d.Versiones.Count, d.PorEmpresa(vinculos)))
        ]);
    }

    [WolverineGet("/public/v1/boveda/documentos/{documentoId}")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<DocumentoDto> ObtenerDocumento(Guid documentoId, HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var consulta = sesiones.ConsultaBoveda();
        var documento = DelTercero(await consulta.LoadAsync<Documento>(documentoId, ct), tercero);
        return documento.ADto(await VinculosAsync(sesiones, tercero, ct));
    }

    /// <summary>URL prefirmada GET de una versión propia; queda como <c>DocumentoAccedido</c> con el tercero como actor.</summary>
    [WolverinePost("/public/v1/boveda/documentos/{documentoId}/versiones/{numero}/descarga")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<DescargaDto> Descargar(Guid documentoId, int numero, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var sesion = sesiones.Boveda();
        var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
        var documento = DelTercero(stream.Aggregate, tercero);

        stream.AppendOne(documento.RegistrarAcceso(numero, null, ActorTipo.Tercero.ToString(), contexto.ActorId, Ip(http), UserAgent(http),
            "Descarga del tercero", reloj.GetUtcNow()));
        await sesion.SaveChangesAsync(ct);
        return await UrlDeDescargaAsync(almacen, reglas, documento.VersionRequerida(numero), ct);
    }

    /// <summary>
    /// El tercero retira una versión (no se borra si alguna empresa la recibió o la descargó, docs/02 §10.3). Si era la
    /// actual vuelve la anterior y se comparte con quien corresponda. Devuelta o retirada ⇒ 409 <c>estado-invalido</c>.
    /// </summary>
    [WolverinePost("/public/v1/boveda/documentos/{documentoId}/versiones/{numero}/retirar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<RetiroDto> Retirar(Guid documentoId, int numero, RetirarVersionDto dto, HttpContext http, ISesiones sesiones,
        IAlmacenDeArchivos almacen, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Boveda();
        var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
        var documento = DelTercero(stream.Aggregate, tercero);
        var version = documento.VersionRequerida(numero);

        var eventos = documento.RetirarVersion(numero, dto.Motivo, ahora);
        foreach (var evento in eventos)
            stream.AppendOne(evento);
        var proyectado = documento.Proyectar(eventos);
        var compartidos = proyectado.VersionActual is not null && proyectado.VersionActual != documento.VersionActual
            ? await ComparticionDeDocumentos.CompartirActualAsync(sesion, stream, proyectado, OrigenComparticion.Automatica, ahora, ct)
            : [];

        // El objeto es content-addressed: solo se elimina si nadie lo vio y ninguna otra versión del tercero apunta a él.
        var nadieLoVio = !documento.Comparticiones.Any(c => c.Numero == numero) && !version.AccedidaPorEmpresa;
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
        CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        if (!Visible(vinculo))
            return [];

        var terceroId = vinculo.TerceroId;
        await using var consulta = sesiones.ConsultaBoveda();
        var documentos = await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId).OrderBy(d => d.CreadoEn).ToListAsync(ct);
        return [.. documentos.Select(d => Vista(d, id)).OfType<DocumentoVistaEmpresaDto>()];
    }

    [WolverineGet("/api/v1/vinculos/{id}/documentos/{documentoId}")]
    [Authorize(Policy = Permisos.DocumentosLeer)]
    public static async Task<DocumentoVistaEmpresaDto> Obtener(Guid id, Guid documentoId, SesionDeEmpresa empresa, ISesiones sesiones,
        CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        await using var consulta = sesiones.ConsultaBoveda();
        var documento = await consulta.LoadAsync<Documento>(documentoId, ct);
        return Visible(vinculo) && documento is not null && documento.TerceroId == vinculo.TerceroId && Vista(documento, id) is { } vista
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

        await using var sesion = sesiones.Boveda();
        var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
        var documento = stream.Aggregate;
        if (documento is null || documento.TerceroId != vinculo.TerceroId)
            throw ReglaDeNegocioException.NoEncontrado("documento-no-encontrado", "No existe ese documento para el vínculo.");

        var destino = new DestinoDeComparticion(contexto.TenantRequerido(), vinculo.EmpresaId, vinculo.Id);
        stream.AppendOne(documento.RegistrarAcceso(numero, destino, contexto.ActorTipo.ToString(), contexto.ActorId, BovedaEndpoints.Ip(http),
            BovedaEndpoints.UserAgent(http), "Descarga", reloj.GetUtcNow()));
        await sesion.SaveChangesAsync(ct);
        return await BovedaEndpoints.UrlDeDescargaAsync(almacen, reglas, documento.VersionRequerida(numero), ct);
    }

    /// <summary>docs/07 §5.2: invitación aceptada y vínculo no anulado (desvinculado o bloqueado conserva lo ya compartido).</summary>
    private static bool Visible(Dominio.Vinculos.Vinculo vinculo) =>
        vinculo.InvitacionAceptada && vinculo.Estado != Dominio.Vinculos.EstadoVinculo.Anulado;

    private static DocumentoVistaEmpresaDto? Vista(Documento documento, Guid vinculoId)
    {
        var numeros = documento.Comparticiones.Where(c => c.VinculoId == vinculoId).Select(c => c.Numero).ToHashSet();
        if (numeros.Count == 0)
            return null;

        var actual = documento.VersionActual is { } n && numeros.Contains(n) ? n : (int?)null;
        return new DocumentoVistaEmpresaDto(documento.Id, documento.Resumen(), documento.Ambito.ToString(), actual,
            [.. documento.Versiones.Where(v => numeros.Contains(v.Numero)).OrderBy(v => v.Numero).Select(v => v.ADto(documento.Id))]);
    }
}
