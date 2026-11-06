using System.Text.Json;
using System.Text.Json.Serialization;
using Marten;
using JasperFx.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
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
    /// <summary>
    /// Crea el vínculo (201). Si la empresa ya tiene ese <c>(tercero, tipo)</c> y lo pide <b>otro</b> sistema integrado, le
    /// agrega su referencia y responde 200 con el vínculo existente: una sola validación compartida (I-0). La misma
    /// referencia es idempotente (200); el mismo sistema con otro id es <c>referencia-en-conflicto</c>; sin referencia,
    /// <c>vinculo-duplicado</c>.
    /// </summary>
    [WolverinePost("/api/v1/vinculos")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<IResult> Crear(CrearVinculoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        empresa.UsarMensajeria(mensajes);
        var sesion = empresa.Sesion;

        var codigoTipo = (dto.TipoVinculo ?? string.Empty).Trim().ToUpperInvariant();
        var tipo = await sesion.Query<TipoVinculo>().FirstOrDefaultAsync(t => t.Codigo == codigoTipo, ct);
        if (tipo is null || !tipo.Activo)
            throw ReglaDeNegocioException.Validacion("tipo-vinculo-invalido", $"El tipo de vínculo '{dto.TipoVinculo}' no existe o está inactivo.");

        var atributos = ValidadorDeAtributos.Validar(tipo, dto.Atributos);
        var referencia = ReferenciaDeLaCredencial(dto.ReferenciaExterna, contexto);
        var tercero = await ResolverTerceroAsync(dto, sesion, contexto, ahora, ct);

        var terceroId = tercero.Id;
        var tipoId = tipo.Id;
        var existente = await empresa.Vinculos().FirstOrDefaultAsync(v => v.TerceroId == terceroId && v.TipoVinculoId == tipoId, ct);
        if (existente is not null)
        {
            if (referencia is null)
                throw new ReglaDeNegocioException(TipoFalla.Conflicto, "vinculo-duplicado",
                    $"La empresa ya tiene un vínculo {tipo.Codigo} con este tercero ({existente.Id}).");

            var agregada = existente.AgregarReferencia(referencia, ahora);
            if (agregada is null)
                return Results.Ok(existente.ADto(tipo));

            await AsegurarReferenciaLibreAsync(empresa, referencia, ct);
            var stream = await empresa.VinculoParaEscribirAsync(existente.Id, null, ct);
            return Results.Ok(await GuardarAsync(empresa, stream, agregada, ct));
        }

        if (referencia is not null)
            await AsegurarReferenciaLibreAsync(empresa, referencia, ct);

        var creado = Vinculo.Crear(empresa.EmpresaId, tercero.Id, tipo.Id, flujoId: null, atributos, OrigenVinculo.Api, referencia,
            dto.CorreoInvitacion, ahora);

        var eventos = new List<object> { creado };
        if (dto.EnviarInvitacion)
        {
            var vinculo = Vinculo.Create(creado);
            var invitacion = vinculo.EnviarInvitacion(null, TimeSpan.FromHours(dto.VigenciaInvitacionHoras), null, ahora);
            eventos.Add(invitacion);
            await EnlacesDeAcceso.InvitarAsync(sesion, mensajes, protector, contexto.TenantRequerido(), vinculo, tipo, invitacion, ct);
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

    /// <summary>Busca el vínculo de la empresa activa por la referencia de un sistema integrado.</summary>
    [WolverineGet("/api/v1/vinculos/por-referencia")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<VinculoDto> PorReferencia(string sistema, string id, SesionDeEmpresa empresa, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sistema) || string.IsNullOrWhiteSpace(id))
            throw ReglaDeNegocioException.Validacion("referencia-externa-invalida", "Indica sistema e id de la referencia.");

        var clave = new ReferenciaExterna(sistema.Trim().ToUpperInvariant(), id.Trim()).Clave();
        var vinculo = await empresa.Vinculos().FirstOrDefaultAsync(v => v.ClavesReferencia.Contains(clave), ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No hay un vínculo con esa referencia en la empresa.");
        return vinculo.ADto(await empresa.Sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct));
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

    /// <summary>
    /// Envía o reenvía la invitación: genera un enlace de acceso nuevo (el anterior no usado deja de servir) y el correo
    /// sale por el outbox después de confirmar la transacción.
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/invitaciones")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<IResult> Invitar(Guid id, InvitacionDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http,
        IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        var evento = stream.Aggregate!.EnviarInvitacion(dto.CorreoDestino, TimeSpan.FromHours(dto.VigenciaHoras), null, reloj.GetUtcNow());
        var tipo = await empresa.Sesion.LoadAsync<TipoVinculo>(stream.Aggregate.TipoVinculoId, ct)
                   ?? throw ReglaDeNegocioException.NoEncontrado("tipo-vinculo-no-encontrado", "El tipo del vínculo ya no existe.");
        await EnlacesDeAcceso.InvitarAsync(empresa.Sesion, mensajes, protector, contexto.TenantRequerido(), stream.Aggregate, tipo, evento, ct);
        // El conteo sale del vínculo recargado tras guardar (el agregado del stream ya aplicó el evento al anexarlo).
        var guardado = await GuardarAsync(empresa, stream, evento, ct);
        return Results.Created($"/api/v1/vinculos/{id}",
            guardado.Invitacion ?? new EnlaceInvitacionDto(evento.CorreoDestino, evento.Expira, 1));
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

    /// <summary>Mismas convenciones JSON que el resto de la API: camelCase y enums como texto.</summary>
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

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
            Cabecera(e, "sistema"),
            JsonSerializer.SerializeToElement(e.Data, e.EventType, OpcionesJson)))];
    }

    /// <summary>
    /// El sistema de la referencia lo fija la API key (ADR-0011): el cuerpo puede omitirlo, pero si lo trae debe coincidir.
    /// Una credencial sin sistema integrado no puede registrar referencias.
    /// </summary>
    private static ReferenciaExterna? ReferenciaDeLaCredencial(ReferenciaExternaDto? dto, IContextoSolicitud contexto)
    {
        if (dto is null)
            return null;

        if (string.IsNullOrWhiteSpace(contexto.Sistema))
            throw ReglaDeNegocioException.Validacion("referencia-sin-sistema",
                "Solo la API key de un sistema integrado puede registrar una referencia externa.");

        if (!string.IsNullOrWhiteSpace(dto.Sistema) && !string.Equals(dto.Sistema.Trim(), contexto.Sistema, StringComparison.OrdinalIgnoreCase))
            throw ReglaDeNegocioException.Validacion("referencia-sistema-invalido",
                $"La referencia externa debe ser del sistema de la credencial ({contexto.Sistema}).");

        return new ReferenciaExterna(contexto.Sistema, (dto.Id ?? string.Empty).Trim());
    }

    /// <summary>Una referencia identifica un solo vínculo en la empresa (la usa <c>por-referencia</c>).</summary>
    private static async Task AsegurarReferenciaLibreAsync(SesionDeEmpresa empresa, ReferenciaExterna referencia, CancellationToken ct)
    {
        var clave = referencia.Clave();
        var otro = await empresa.Vinculos().FirstOrDefaultAsync(v => v.ClavesReferencia.Contains(clave), ct);
        if (otro is not null)
            throw ReglaDeNegocioException.Conflicto("referencia-duplicada",
                $"La referencia {referencia.Id} de {referencia.Sistema} ya identifica otro vínculo ({otro.Id}).");
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
