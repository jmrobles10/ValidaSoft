using Marten;
using JasperFx.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>Terceros: datos globales visibles solo para el tenant que los registró o tiene un vínculo con ellos.</summary>
public static class TercerosEndpoints
{
    /// <summary>
    /// Crea el tercero (201). Con <c>referenciaExterna</c> (el sistema lo fija la API key) la creación es idempotente: si esa clave ya
    /// identifica a un tercero con la misma identificación, responde 200 con él; si identifica a otro, 409 <c>referencia-duplicada</c>.
    /// </summary>
    [WolverinePost("/api/v1/terceros")]
    [Authorize(Policy = Permisos.TercerosEscribir)]
    public static async Task<IResult> Crear(CrearTerceroDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var tercero = ConstruirTercero(dto, contexto.TenantRequerido(), contexto.EsSandbox, ahora);
        var referencia = VinculosEndpoints.ReferenciaDeLaCredencial(dto.ReferenciaExterna, contexto);

        await using var sesion = sesiones.DelTenant();
        if (referencia is not null &&
            await sesion.LoadAsync<ReferenciaDeTercero>(ReferenciaDeTercero.IdPara(referencia.Sistema, referencia.Id), ct) is { } previa)
        {
            var yaRegistrado = await sesion.LoadAsync<Tercero>(previa.TerceroId, ct);
            return yaRegistrado is not null && yaRegistrado.ClaveIdentificacion == tercero.ClaveIdentificacion
                ? Results.Ok(yaRegistrado.ADto())
                : throw ReglaDeNegocioException.Conflicto("referencia-duplicada",
                    $"La clave {referencia.Id} de {referencia.Sistema} ya identifica a otro tercero.");
        }

        var existente = await sesion.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == tercero.ClaveIdentificacion, ct);
        if (existente is not null)
            throw new ReglaDeNegocioException(TipoFalla.Conflicto, "tercero-duplicado",
                "Ese tercero ya existe en la plataforma; vincúlalo con POST /vinculos indicando su identificación.");

        sesion.Store(tercero);
        if (referencia is not null)
            await ReferenciasDeTerceroEndpoints.RegistrarAsync(sesion, tercero.Id, referencia.Sistema, referencia.Id, ahora, ct);
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
        var clave = contexto.ClaveDeTercero(new IdentificacionDto(pais, tipo, numero).ADominio().Clave);
        await using var sesion = sesiones.DelTenant();
        var tercero = await sesion.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == clave, ct);
        return tercero is not null && await EsVisibleAsync(sesion, tercero, contexto, ct)
            ? tercero.ADto()
            : throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero.");
    }

    /// <summary>
    /// El tercero que se registra desde un tenant. En un sandbox (<paramref name="esSandbox"/>, SB-4) queda aislado: su clave lleva el
    /// tenant sandbox por delante y no choca con el tercero real que tenga la misma identificación.
    /// </summary>
    internal static Tercero ConstruirTercero(CrearTerceroDto dto, Guid tenantId, bool esSandbox, DateTimeOffset ahora)
    {
        if (!Enum.TryParse<TipoPersona>(dto.TipoPersona, true, out var tipoPersona))
            throw ReglaDeNegocioException.Validacion("tipo-persona-invalido", "El tipo de persona debe ser Natural, Juridica o Consorcio.");

        var tercero = Tercero.Crear(dto.Identificacion.ADominio(), tipoPersona, dto.Nombre, dto.NombreComercial,
            dto.Contactos?.Select(c => new Contacto(c.Nombre, c.Correo.Trim().ToLowerInvariant(), c.Telefono, c.Rol)), ahora);
        tercero.CreadoPorTenant = tenantId;
        if (esSandbox)
            tercero.AislarEnSandbox(tenantId);
        return tercero;
    }

    private static async Task<bool> EsVisibleAsync(IQuerySession sesion, Tercero tercero, IContextoSolicitud contexto, CancellationToken ct) =>
        tercero.CreadoPorTenant == contexto.TenantId || await sesion.Query<Vinculo>().AnyAsync(v => v.TerceroId == tercero.Id, ct);

    /// <summary>El tercero si es visible para el tenant (lo registró o tiene un vínculo con él); si no, 404 (anti-cosecha).</summary>
    internal static async Task<Tercero> CargarVisibleAsync(IQuerySession sesion, Guid id, IContextoSolicitud contexto, CancellationToken ct)
    {
        var tercero = await sesion.LoadAsync<Tercero>(id, ct);
        return tercero is not null && await EsVisibleAsync(sesion, tercero, contexto, ct)
            ? tercero
            : throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero.");
    }
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
        IMessageBus mensajes, IProtectorDeSecretos protector, Validasoft.Aplicacion.Plataforma.ControlDePlan plan, TimeProvider reloj, CancellationToken ct)
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

        // Un vínculo nuevo cuenta para el límite de vínculos activos del plan; con el bloqueo suave y el límite alcanzado no se crean más (OD-17).
        await plan.ExigirAsync(contexto.TenantRequerido(), Validasoft.Dominio.Plataforma.MetricaDePlan.VinculosActivos, 1, ct);
        var creado = Vinculo.Crear(empresa.EmpresaId, tercero.Id, tipo.Id, await FlujoExplicitoAsync(empresa, dto.FlujoId, tipo, ct), atributos,
            OrigenVinculo.Api, referencia, dto.CorreoInvitacion, ahora);

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

    /// <summary>
    /// Busca el vínculo de la empresa activa por la clave externa de un sistema integrador: <c>?sistema=SRM&amp;clave=ADP-00123</c>
    /// (<c>id</c> es el nombre anterior del mismo parámetro y se sigue aceptando).
    /// </summary>
    [WolverineGet("/api/v1/vinculos/por-referencia")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<VinculoDto> PorReferencia(string sistema, string? clave, string? id, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var claveExterna = string.IsNullOrWhiteSpace(clave) ? id : clave;
        if (string.IsNullOrWhiteSpace(sistema) || string.IsNullOrWhiteSpace(claveExterna))
            throw ReglaDeNegocioException.Validacion("referencia-externa-invalida", "Indica el sistema y la clave de la referencia.");

        var clavePlana = new ReferenciaExterna(sistema.Trim().ToUpperInvariant(), claveExterna.Trim()).Clave();
        var vinculo = await empresa.Vinculos().FirstOrDefaultAsync(v => v.ClavesReferencia.Contains(clavePlana), ct)
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

    /// <summary>
    /// Bloquear suspende el certificado en la misma transacción (B-5); no lo revoca. Con fecha de fin programa en la misma
    /// transacción el desbloqueo automático (<see cref="DesbloquearAlLlegarLaFecha"/>, VV-5), que hace lo mismo que el manual.
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/bloquear")]
    [Authorize(Policy = Permisos.VinculosBloquear)]
    public static async Task<VinculoDto> Bloquear(Guid id, BloquearVinculoDto dto, SesionDeEmpresa empresa, HttpContext http,
        IContextoSolicitud contexto, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var ahora = reloj.GetUtcNow();
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        var evento = stream.Aggregate!.Bloquear(dto.Motivo, dto.Hasta, ahora);
        await CicloDeCertificados.AlBloquearAsync(empresa.Sesion, id, evento.Motivo, ahora, ct);
        if (evento.Hasta is { } hasta)
            await mensajes.ScheduleAsync(new DesbloquearAlLlegarLaFecha(contexto.TenantRequerido(), id, hasta), hasta);
        return await GuardarAsync(empresa, stream, evento, ct);
    }

    /// <summary>Desbloquear restablece el certificado suspendido (o lo deja vencido si su vigencia pasó durante el bloqueo).</summary>
    [WolverinePost("/api/v1/vinculos/{id}/desbloquear")]
    [Authorize(Policy = Permisos.VinculosBloquear)]
    public static async Task<VinculoDto> Desbloquear(Guid id, JustificacionDto dto, SesionDeEmpresa empresa, HttpContext http,
        TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        var evento = stream.Aggregate!.Desbloquear(dto.Justificacion, ahora);
        await CicloDeCertificados.AlDesbloquearAsync(empresa.Sesion, id, evento.Justificacion, ahora, ct);
        return await GuardarAsync(empresa, stream, evento, ct);
    }

    /// <summary>Si tenía un expediente abierto, se anula en la misma transacción; su certificado se revoca (por el sistema).</summary>
    [WolverinePost("/api/v1/vinculos/{id}/desvincular")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Desvincular(Guid id, MotivoDto dto, SesionDeEmpresa empresa, HttpContext http,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        var abierto = stream.Aggregate!.ExpedienteAbiertoId;
        var evento = stream.Aggregate.Desvincular(dto.Motivo, ahora);
        if (abierto is { } expedienteId)
            await ProcesoDeValidacion.AnularPorTerminacionDelVinculoAsync(empresa.Sesion, expedienteId, evento.Motivo, ahora, ct);
        await CicloDeCertificados.AlTerminarAsync(empresa.Sesion, id, "Vínculo desvinculado", evento.Motivo, contexto.ActorId, ahora, ct);
        return await GuardarAsync(empresa, stream, evento, ct);
    }

    /// <summary>
    /// Reintegra un vínculo desvinculado (DS-3). Por omisión <b>revalida</b>: vuelve a validación y se abre una renovación con
    /// todos los documentos del flujo (si nunca fue aprobado, vuelve a <c>Invitado</c> para invitarlo de nuevo). Con
    /// <c>revalidar: false</c> restituye el estado que tenía al desvincularse y emite su certificado de nuevo con lo ya aprobado,
    /// solo si esas versiones siguen vigentes (409 <c>reintegro-requiere-revalidacion</c> con los motivos si no).
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/reintegrar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Reintegrar(Guid id, ReintegrarVinculoDto dto, SesionDeEmpresa empresa, HttpContext http, IContextoSolicitud contexto,
        IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var version = VersionEsperada(http);
        var abierto = false;
        var resultado = await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            var stream = await empresa.VinculoParaEscribirAsync(id, version, ct);
            var vinculo = stream.Aggregate!;
            var evento = vinculo.Reintegrar(dto.Justificacion, ahora, sinRevalidar: !dto.Revalidar);

            if (evento.SinRevalidar)
            {
                var viabilidad = await ReintegroDeVinculos.EvaluarAsync(empresa, vinculo, ahora, ct);
                if (!viabilidad.PuedeSinRevalidar)
                    throw new ReglaDeNegocioException(TipoFalla.Conflicto, "reintegro-requiere-revalidacion",
                        $"No se puede reintegrar sin revalidar: {string.Join(" ", viabilidad.Motivos)}")
                    {
                        Datos = new Dictionary<string, object?> { ["motivos"] = viabilidad.Motivos }
                    };
            }

            stream.AppendOne(evento);
            if (vinculo.Estado != evento.EstadoResultante)
                vinculo.Apply(evento); // El agregado del stream decide lo que sigue con el estado ya reintegrado.

            if (evento.SinRevalidar)
            {
                // Lo ya aprobado sigue en pie: el certificado revocado al desvincular se emite de nuevo con ese expediente.
                var aprobado = await ReintegroDeVinculos.UltimoAprobadoAsync(empresa.Sesion, id, ct)
                               ?? throw ReglaDeNegocioException.Conflicto("reintegro-requiere-revalidacion", "No hay un expediente aprobado que restituir.");
                await empresa.Certificados.EmitirAsync(empresa.Sesion, aprobado, vinculo, ahora, ct,
                    $"Vínculo reintegrado sin revalidar: {evento.Justificacion}");
            }
            else if (evento.EstadoResultante == EstadoVinculo.EnValidacion)
            {
                // Con aprobación previa se revalida todo: una renovación con todos los documentos del flujo, abierta por quien reintegra.
                var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(empresa.Sesion, vinculo, null, ct)
                            ?? throw ReglaDeNegocioException.Validacion("falta-flujo-por-defecto",
                                "Para revalidar al reintegrar, el vínculo necesita un flujo o el tipo un flujo por defecto publicado.");
                await ProcesoDeValidacion.AbrirAsync(empresa.Sesion, stream, flujo, contexto, empresa.Certificados, ahora, ct,
                    tipoExpediente: TipoExpediente.Renovacion);
                abierto = true;
            }

            await empresa.Sesion.SaveChangesAsync(ct);
            var guardado = await empresa.CargarVinculoAsync(id, ct);
            return guardado.ADto(await empresa.Sesion.LoadAsync<TipoVinculo>(guardado.TipoVinculoId, ct));
        });

        if (abierto)
        {
            // La renovación pudo compartir documentos con el vínculo (sin outbox de la bóveda): vigencias e IA, como al abrir un expediente.
            await mensajes.PublishAsync(new Documentos.CalcularVigenciasDelVinculo(resultado.TerceroId, resultado.Id));
            await mensajes.PublishAsync(new Ia.AnalizarIaDelVinculo(resultado.TerceroId, resultado.Id));
        }

        return resultado;
    }

    /// <summary>
    /// Si el vínculo desvinculado se puede reintegrar sin revalidar (DS-3) y, si no, por qué: una versión cambió en la bóveda del
    /// tercero sin que la empresa la revisara, una aprobada ya venció o el vínculo no estaba aprobado y vigente al desvincularse.
    /// </summary>
    [WolverineGet("/api/v1/vinculos/{id}/reintegro")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<ViabilidadDeReintegroDto> ViabilidadDeReintegro(Guid id, SesionDeEmpresa empresa, TimeProvider reloj, CancellationToken ct) =>
        await ReintegroDeVinculos.EvaluarAsync(empresa, await empresa.CargarVinculoAsync(id, ct), reloj.GetUtcNow(), ct);

    /// <summary>Terminal; si tenía un expediente abierto, se anula en la misma transacción; su certificado se revoca (por el sistema).</summary>
    [WolverinePost("/api/v1/vinculos/{id}/anular")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Anular(Guid id, MotivoDto dto, SesionDeEmpresa empresa, HttpContext http,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);
        var abierto = stream.Aggregate!.ExpedienteAbiertoId;
        var evento = stream.Aggregate.Anular(dto.Motivo, ahora);
        if (abierto is { } expedienteId)
            await ProcesoDeValidacion.AnularPorTerminacionDelVinculoAsync(empresa.Sesion, expedienteId, evento.Motivo, ahora, ct);
        await CicloDeCertificados.AlTerminarAsync(empresa.Sesion, id, "Vínculo anulado", evento.Motivo, contexto.ActorId, ahora, ct);
        return await GuardarAsync(empresa, stream, evento, ct);
    }

    /// <summary>Línea de tiempo del vínculo, directamente del stream de eventos.</summary>
    [WolverineGet("/api/v1/vinculos/{id}/historial")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<IReadOnlyList<EventoHistorialDto>> Historial(Guid id, SesionDeEmpresa empresa, CancellationToken ct)
    {
        await empresa.CargarVinculoAsync(id, ct);
        return Peticiones.Historial(await empresa.Sesion.Events.FetchStreamAsync(id, token: ct));
    }

    /// <summary>
    /// El sistema de la referencia lo fija la API key (ADR-0011): el cuerpo puede omitirlo, pero si lo trae debe coincidir.
    /// Una credencial sin sistema integrado no puede registrar referencias.
    /// </summary>
    internal static ReferenciaExterna? ReferenciaDeLaCredencial(ReferenciaExternaDto? dto, IContextoSolicitud contexto)
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

    /// <summary>Flujo explícito del vínculo (opcional): de la empresa y del mismo tipo de vínculo (FL-4).</summary>
    private static async Task<Guid?> FlujoExplicitoAsync(SesionDeEmpresa empresa, Guid? flujoId, TipoVinculo tipo, CancellationToken ct)
    {
        if (flujoId is not { } id)
            return null;
        var flujo = await empresa.Sesion.LoadAsync<Flujo>(id, ct);
        return flujo is not null && flujo.EmpresaId == empresa.EmpresaId && flujo.TipoVinculoId == tipo.Id
            ? flujo.Id
            : throw ReglaDeNegocioException.Validacion("flujo-invalido", "El flujo no existe en la empresa o es de otro tipo de vínculo.");
    }

    /// <summary>Una referencia identifica un solo vínculo en la empresa (la usa <c>por-referencia</c>).</summary>
    internal static async Task AsegurarReferenciaLibreAsync(SesionDeEmpresa empresa, ReferenciaExterna referencia, CancellationToken ct)
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

        var nuevo = TercerosEndpoints.ConstruirTercero(dto.Tercero, contexto.TenantRequerido(), contexto.EsSandbox, ahora);
        var existente = await sesion.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == nuevo.ClaveIdentificacion, ct);
        if (existente is not null)
            return existente; // Ya existe en la plataforma: se reutiliza sin tocar sus datos maestros (P-1-7).

        sesion.Store(nuevo);
        return nuevo;
    }

    internal static async Task<VinculoDto> GuardarAsync(SesionDeEmpresa empresa, IEventStream<Vinculo> stream, object evento, CancellationToken ct)
    {
        stream.AppendOne(evento);
        await empresa.Sesion.SaveChangesAsync(ct);
        var vinculo = await empresa.CargarVinculoAsync(stream.Id, ct);
        return vinculo.ADto(await empresa.Sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct));
    }

    /// <summary><c>If-Match: "7"</c> o <c>If-Match: 7</c> ⇒ concurrencia optimista sobre la versión del stream.</summary>
    private static long? VersionEsperada(HttpContext http) => Peticiones.VersionEsperada(http);

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
