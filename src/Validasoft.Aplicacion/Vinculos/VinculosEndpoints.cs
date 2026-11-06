using System.Text.Json;
using Marten;
using JasperFx.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>Terceros: datos globales visibles solo para el tenant que los registró o tiene un vínculo con ellos.</summary>
public static class TercerosEndpoints
{
    [WolverinePost("/api/v1/terceros")]
    [Authorize(Policy = Permisos.TercerosEscribir)]
    public static async Task<IResult> Crear(CrearTerceroDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var tercero = ConstruirTercero(dto, contexto.TenantRequerido(), reloj.GetUtcNow());

        await using var sesion = sesiones.DelTenant();
        var existente = await sesion.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == tercero.ClaveIdentificacion, ct);
        if (existente is not null)
            throw new ReglaDeNegocioException(TipoFalla.Conflicto, "tercero-duplicado",
                "Ese tercero ya existe en la plataforma; vincúlalo con POST /vinculos indicando su identificación.");

        sesion.Store(tercero);
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/terceros/{tercero.Id}", tercero.ADto());
    }

    [WolverineGet("/api/v1/terceros/{id}")]
    [Authorize(Policy = Permisos.TercerosLeer)]
    public static async Task<TerceroDto> Obtener(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tercero = await sesion.LoadAsync<Tercero>(id, ct);
        return tercero is not null && await EsVisibleAsync(sesion, tercero, contexto, ct)
            ? tercero.ADto()
            : throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero.");
    }

    /// <summary>Búsqueda exacta por identificación normalizada; 404 si no es visible para el tenant (anti-cosecha).</summary>
    [WolverineGet("/api/v1/terceros/por-identificacion")]
    [Authorize(Policy = Permisos.TercerosLeer)]
    public static async Task<TerceroDto> PorIdentificacion(string pais, string tipo, string numero, ISesiones sesiones,
        IContextoSolicitud contexto, CancellationToken ct)
    {
        var clave = new IdentificacionDto(pais, tipo, numero).ADominio().Clave;
        await using var sesion = sesiones.DelTenant();
        var tercero = await sesion.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == clave, ct);
        return tercero is not null && await EsVisibleAsync(sesion, tercero, contexto, ct)
            ? tercero.ADto()
            : throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero.");
    }

    internal static Tercero ConstruirTercero(CrearTerceroDto dto, Guid tenantId, DateTimeOffset ahora)
    {
        if (!Enum.TryParse<TipoPersona>(dto.TipoPersona, true, out var tipoPersona))
            throw ReglaDeNegocioException.Validacion("tipo-persona-invalido", "El tipo de persona debe ser Natural, Juridica o Consorcio.");

        var tercero = Tercero.Crear(dto.Identificacion.ADominio(), tipoPersona, dto.Nombre, dto.NombreComercial,
            dto.Contactos?.Select(c => new Contacto(c.Nombre, c.Correo.Trim().ToLowerInvariant(), c.Telefono, c.Rol)), ahora);
        tercero.CreadoPorTenant = tenantId;
        return tercero;
    }

    private static async Task<bool> EsVisibleAsync(IDocumentSession sesion, Tercero tercero, IContextoSolicitud contexto, CancellationToken ct) =>
        tercero.CreadoPorTenant == contexto.TenantId || await sesion.Query<Vinculo>().AnyAsync(v => v.TerceroId == tercero.Id, ct);
}

/// <summary>
/// Vínculos de la empresa activa (cabecera <c>X-Empresa</c>). Cada vínculo es un stream de eventos
/// <c>Vinculo-{id}</c>; la lectura sale de su proyección inline.
/// </summary>
public static class VinculosEndpoints
{
    [WolverinePost("/api/v1/vinculos")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<IResult> Crear(CrearVinculoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var sesion = empresa.Sesion;

        var codigoTipo = (dto.TipoVinculo ?? string.Empty).Trim().ToUpperInvariant();
        var tipo = await sesion.Query<TipoVinculo>().FirstOrDefaultAsync(t => t.Codigo == codigoTipo, ct);
        if (tipo is null || !tipo.Activo)
            throw ReglaDeNegocioException.Validacion("tipo-vinculo-invalido", $"El tipo de vínculo '{dto.TipoVinculo}' no existe o está inactivo.");

        var atributos = ValidadorDeAtributos.Validar(tipo, dto.Atributos);
        var tercero = await ResolverTerceroAsync(dto, sesion, contexto, ahora, ct);

        var duplicado = await empresa.Vinculos().FirstOrDefaultAsync(v => v.TerceroId == tercero.Id && v.TipoVinculoId == tipo.Id, ct);
        if (duplicado is not null)
            throw new ReglaDeNegocioException(TipoFalla.Conflicto, "vinculo-duplicado",
                $"La empresa ya tiene un vínculo {tipo.Codigo} con este tercero ({duplicado.Id}).");

        var referencia = dto.ReferenciaExterna is null ? null : new ReferenciaExterna(dto.ReferenciaExterna.Sistema, dto.ReferenciaExterna.Id);
        var creado = Vinculo.Crear(empresa.EmpresaId, tercero.Id, tipo.Id, flujoId: null, atributos, OrigenVinculo.Api, referencia,
            dto.CorreoInvitacion, ahora);

        var eventos = new List<object> { creado };
        if (dto.EnviarInvitacion)
        {
            var vinculo = Vinculo.Create(creado);
            eventos.Add(vinculo.EnviarInvitacion(null, TimeSpan.FromHours(dto.VigenciaInvitacionHoras), null, ahora));
        }

        sesion.Events.StartStream<Vinculo>(creado.VinculoId, eventos);
        await sesion.SaveChangesAsync(ct);

        var resultado = await empresa.CargarVinculoAsync(creado.VinculoId, ct);
        return Results.Created($"/api/v1/vinculos/{resultado.Id}", resultado.ADto(tipo));
    }

    [WolverineGet("/api/v1/vinculos")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<PaginaDto<VinculoDto>> Listar(SesionDeEmpresa empresa, string? estado, string? tipoVinculo,
        Guid? terceroId, int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var consulta = empresa.Vinculos();

        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoVinculo>(estado, true, out var estadoVinculo))
                throw ReglaDeNegocioException.Validacion("estado-invalido", $"'{estado}' no es un estado de vínculo.");
            consulta = consulta.Where(v => v.Estado == estadoVinculo);
        }

        var tipos = await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(tipoVinculo))
        {
            var tipoId = tipos.FirstOrDefault(t => t.Codigo == tipoVinculo.Trim().ToUpperInvariant())?.Id ?? Guid.Empty;
            consulta = consulta.Where(v => v.TipoVinculoId == tipoId);
        }

        if (terceroId is not null)
            consulta = consulta.Where(v => v.TerceroId == terceroId);

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var desde = Cursor.Leer(cursor);
            consulta = consulta.Where(v => v.CreadoEn > desde);
        }

        var pagina = await consulta.OrderBy(v => v.CreadoEn).Take(tamano + 1).ToListAsync(ct);
        var siguiente = pagina.Count > tamano ? Cursor.Escribir(pagina[tamano - 1].CreadoEn) : null;
        return new PaginaDto<VinculoDto>([.. pagina.Take(tamano).Select(v => v.ADto(tipos.FirstOrDefault(t => t.Id == v.TipoVinculoId)))], siguiente);
    }

    [WolverineGet("/api/v1/vinculos/{id}")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<VinculoDto> Obtener(Guid id, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        return vinculo.ADto(await empresa.Sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct));
    }

    [WolverinePatch("/api/v1/vinculos/{id}/atributos")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> ActualizarAtributos(Guid id, ActualizarAtributosDto dto, SesionDeEmpresa empresa,
        HttpContext http, TimeProvider reloj, CancellationToken ct)
    {
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        var tipo = await empresa.Sesion.LoadAsync<TipoVinculo>(stream.Aggregate!.TipoVinculoId, ct);
        var atributos = ValidadorDeAtributos.Validar(tipo!, dto.Atributos);
        return await GuardarAsync(empresa, stream, stream.Aggregate.ActualizarAtributos(atributos, reloj.GetUtcNow()), ct);
    }

    /// <summary>Envía o reenvía la invitación. El enlace de acceso del tercero (magic link) llega con H4.</summary>
    [WolverinePost("/api/v1/vinculos/{id}/invitaciones")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<IResult> Invitar(Guid id, InvitacionDto dto, SesionDeEmpresa empresa, HttpContext http,
        TimeProvider reloj, CancellationToken ct)
    {
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        var evento = stream.Aggregate!.EnviarInvitacion(dto.CorreoDestino, TimeSpan.FromHours(dto.VigenciaHoras), null, reloj.GetUtcNow());
        await GuardarAsync(empresa, stream, evento, ct);
        return Results.Created($"/api/v1/vinculos/{id}", new EnlaceInvitacionDto(evento.CorreoDestino, evento.Expira, stream.Aggregate.Invitacion?.Envios + 1 ?? 1));
    }

    [WolverinePost("/api/v1/vinculos/{id}/bloquear")]
    [Authorize(Policy = Permisos.VinculosBloquear)]
    public static async Task<VinculoDto> Bloquear(Guid id, BloquearVinculoDto dto, SesionDeEmpresa empresa, HttpContext http,
        TimeProvider reloj, CancellationToken ct)
    {
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        return await GuardarAsync(empresa, stream, stream.Aggregate!.Bloquear(dto.Motivo, dto.Hasta, reloj.GetUtcNow()), ct);
    }

    [WolverinePost("/api/v1/vinculos/{id}/desbloquear")]
    [Authorize(Policy = Permisos.VinculosBloquear)]
    public static async Task<VinculoDto> Desbloquear(Guid id, JustificacionDto dto, SesionDeEmpresa empresa, HttpContext http,
        TimeProvider reloj, CancellationToken ct)
    {
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        return await GuardarAsync(empresa, stream, stream.Aggregate!.Desbloquear(dto.Justificacion, reloj.GetUtcNow()), ct);
    }

    [WolverinePost("/api/v1/vinculos/{id}/desvincular")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Desvincular(Guid id, MotivoDto dto, SesionDeEmpresa empresa, HttpContext http,
        TimeProvider reloj, CancellationToken ct)
    {
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        return await GuardarAsync(empresa, stream, stream.Aggregate!.Desvincular(dto.Motivo, reloj.GetUtcNow()), ct);
    }

    [WolverinePost("/api/v1/vinculos/{id}/reintegrar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Reintegrar(Guid id, JustificacionDto dto, SesionDeEmpresa empresa, HttpContext http,
        TimeProvider reloj, CancellationToken ct)
    {
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        return await GuardarAsync(empresa, stream, stream.Aggregate!.Reintegrar(dto.Justificacion, reloj.GetUtcNow()), ct);
    }

    [WolverinePost("/api/v1/vinculos/{id}/anular")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Anular(Guid id, MotivoDto dto, SesionDeEmpresa empresa, HttpContext http,
        TimeProvider reloj, CancellationToken ct)
    {
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        return await GuardarAsync(empresa, stream, stream.Aggregate!.Anular(dto.Motivo, reloj.GetUtcNow()), ct);
    }

    /// <summary>Línea de tiempo del vínculo, directamente del stream de eventos.</summary>
    [WolverineGet("/api/v1/vinculos/{id}/historial")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<IReadOnlyList<EventoHistorialDto>> Historial(Guid id, SesionDeEmpresa empresa, CancellationToken ct)
    {
        await empresa.CargarVinculoAsync(id, ct);
        var eventos = await empresa.Sesion.Events.FetchStreamAsync(id, token: ct);
        return [.. eventos.Select(e => new EventoHistorialDto(
            e.Version,
            e.EventTypeName,
            e.Timestamp,
            Cabecera(e, "actorTipo"),
            Cabecera(e, "actorId"),
            JsonSerializer.SerializeToElement(e.Data, e.EventType)))];
    }

    private static async Task<Tercero> ResolverTerceroAsync(CrearVinculoDto dto, IDocumentSession sesion, IContextoSolicitud contexto,
        DateTimeOffset ahora, CancellationToken ct)
    {
        if (dto.TerceroId is not null)
        {
            var porId = await sesion.LoadAsync<Tercero>(dto.TerceroId.Value, ct);
            var visible = porId is not null &&
                          (porId.CreadoPorTenant == contexto.TenantId || await sesion.Query<Vinculo>().AnyAsync(v => v.TerceroId == porId.Id, ct));
            return visible ? porId! : throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero.");
        }

        if (dto.Tercero is null)
            throw ReglaDeNegocioException.Validacion("tercero-requerido", "Indica terceroId o los datos del tercero.");

        var nuevo = TercerosEndpoints.ConstruirTercero(dto.Tercero, contexto.TenantRequerido(), ahora);
        var existente = await sesion.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == nuevo.ClaveIdentificacion, ct);
        if (existente is not null)
            return existente; // Ya existe en la plataforma: se reutiliza sin tocar sus datos maestros (P-1-7).

        sesion.Store(nuevo);
        return nuevo;
    }

    private static async Task<VinculoDto> GuardarAsync(SesionDeEmpresa empresa, IEventStream<Vinculo> stream, object evento, CancellationToken ct)
    {
        stream.AppendOne(evento);
        await empresa.Sesion.SaveChangesAsync(ct);
        var vinculo = await empresa.CargarVinculoAsync(stream.Id, ct);
        return vinculo.ADto(await empresa.Sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct));
    }

    /// <summary><c>If-Match: "7"</c> o <c>If-Match: 7</c> ⇒ concurrencia optimista sobre la versión del stream.</summary>
    private static long? VersionEsperada(HttpContext http)
    {
        var valor = http.Request.Headers.IfMatch.ToString().Trim('"', ' ', 'W', '/');
        if (string.IsNullOrEmpty(valor))
            return null;
        return long.TryParse(valor, out var version)
            ? version
            : throw ReglaDeNegocioException.Validacion("if-match-invalido", "If-Match debe ser la versión numérica del vínculo.");
    }

    private static string? Cabecera(IEvent evento, string nombre) =>
        evento.Headers is not null && evento.Headers.TryGetValue(nombre, out var valor) ? valor?.ToString() : null;

    private static class Cursor
    {
        public static string Escribir(DateTimeOffset fecha) =>
            Convert.ToBase64String(BitConverter.GetBytes(fecha.UtcTicks));

        public static DateTimeOffset Leer(string cursor)
        {
            try
            {
                return new DateTimeOffset(BitConverter.ToInt64(Convert.FromBase64String(cursor)), TimeSpan.Zero);
            }
            catch (Exception)
            {
                throw ReglaDeNegocioException.Validacion("cursor-invalido", "El cursor no es válido.");
            }
        }
    }
}
