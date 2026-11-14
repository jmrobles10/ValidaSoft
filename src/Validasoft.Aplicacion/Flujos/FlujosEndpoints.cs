using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Terceros;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Flujos;

/// <summary>
/// Flujos aprobatorios versionados de la empresa activa (<c>X-Empresa</c>): etapas en secuencia, áreas en paralelo,
/// aprobadores por área, áreas automáticas, política de devolución y los documentos que pide (requisitos, RQ-1: se
/// editan en el borrador y se congelan al publicar) y, opcionalmente, la regla con la que se escoge solo (condición y prioridad, FR-1).
/// Todo cambio deja registro de auditoría (R-1).
/// </summary>
public static class FlujosEndpoints
{
    [WolverinePost("/api/v1/flujos")]
    [Authorize(Policy = Permisos.FlujosEscribir)]
    public static async Task<IResult> Crear(CrearFlujoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var codigoTipo = (dto.TipoVinculo ?? string.Empty).Trim().ToUpperInvariant();
        var tipo = await empresa.Sesion.Query<TipoVinculo>().FirstOrDefaultAsync(t => t.Codigo == codigoTipo, ct);
        if (tipo is null || !tipo.Activo)
            throw ReglaDeNegocioException.Validacion("tipo-vinculo-invalido", $"El tipo de vínculo '{dto.TipoVinculo}' no existe o está inactivo.");

        var areas = await empresa.Areas().ToListAsync(ct);
        var tipos = await TiposAsync(empresa, ct);
        var politica = Politica(dto.PoliticaDevolucion) ?? PoliticaDevolucion.Inmediata;
        var definicion = (dto.Etapas is { Count: > 0 }
            ? new DefinicionDeFlujo([.. dto.Etapas.Select(e => e.ADominio())], politica, dto.SeparacionDeFunciones)
            : DefinicionDeFlujo.TodasEnParalelo(areas, politica, dto.SeparacionDeFunciones)) with
        {
            Requisitos = [.. (dto.Requisitos ?? []).Select(r => r.ADominio(tipos))],
            Verificaciones = [.. (dto.Verificaciones ?? []).Select(v => v.ADominio())],
            PlazoCargue = dto.PlazoCargue?.ADominio(),
            Reintento = dto.Reintento?.ADominio(),
            VigenciaVinculo = dto.VigenciaVinculo?.ADominio(),
            RecordatorioObservacionesDias = dto.RecordatorioObservaciones?.ADominio(),
            Seleccion = dto.Seleccion?.ADominio()
        };
        var flujo = Flujo.Crear(empresa.EmpresaId, dto.Codigo, dto.Nombre, tipo.Id, definicion, areas, ahora, tipos, tipo);

        var codigo = flujo.Codigo;
        if (await empresa.Flujos().AnyAsync(f => f.Codigo == codigo, ct))
            throw ReglaDeNegocioException.Conflicto("flujo-duplicado", $"La empresa ya tiene el flujo {codigo}.");
        await ValidarPrioridadAsync(empresa, flujo, flujo.Ultima.Seleccion, ct);

        empresa.Sesion.Store(flujo);
        var resultado = flujo.ADto(tipo, false);
        Auditoria.Registrar(empresa.Sesion, contexto, "Flujo", flujo.Id.ToString(), AccionConfiguracion.Creado, null, resultado, ahora, flujo.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/flujos/{flujo.Id}", resultado);
    }

    [WolverineGet("/api/v1/flujos")]
    [Authorize(Policy = Permisos.FlujosLeer)]
    public static async Task<IReadOnlyList<FlujoDto>> Listar(string? tipoVinculo, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var tipos = await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct);
        var consulta = empresa.Flujos();
        if (!string.IsNullOrWhiteSpace(tipoVinculo))
        {
            var tipoId = tipos.FirstOrDefault(t => t.Codigo == tipoVinculo.Trim().ToUpperInvariant())?.Id ?? Guid.Empty;
            consulta = consulta.Where(f => f.TipoVinculoId == tipoId);
        }

        var flujos = await consulta.OrderBy(f => f.Codigo).ToListAsync(ct);
        var porDefecto = await PorDefectoAsync(empresa, ct);
        return [.. flujos.Select(f => f.ADto(tipos.FirstOrDefault(t => t.Id == f.TipoVinculoId), porDefecto.Any(p => p.FlujoId == f.Id)))];
    }

    /// <summary>Flujo por defecto de cada tipo de vínculo de la empresa.</summary>
    [WolverineGet("/api/v1/flujos/por-defecto")]
    [Authorize(Policy = Permisos.FlujosLeer)]
    public static async Task<IReadOnlyList<FlujoPorDefectoDto>> ListarPorDefecto(SesionDeEmpresa empresa, CancellationToken ct) =>
        [.. (await PorDefectoAsync(empresa, ct)).Select(p => new FlujoPorDefectoDto(p.TipoVinculoId, p.FlujoId, p.EmpresaId))];

    /// <summary>Define el flujo por defecto de <c>(empresa, tipo de vínculo)</c>: debe ser de la empresa, de ese tipo y estar publicado.</summary>
    [WolverinePut("/api/v1/flujos/por-defecto")]
    [Authorize(Policy = Permisos.FlujosEscribir)]
    public static async Task<FlujoPorDefectoDto> DefinirPorDefecto(FlujoPorDefectoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        if (dto.EmpresaId is { } empresaId && empresaId != empresa.EmpresaId)
            throw ReglaDeNegocioException.Validacion("empresa-invalida", "La empresa del cuerpo no coincide con X-Empresa.");

        var flujo = await empresa.CargarFlujoAsync(dto.FlujoId, ct);
        if (flujo.TipoVinculoId != dto.TipoVinculoId)
            throw ReglaDeNegocioException.Validacion("tipo-vinculo-invalido", "El flujo es de otro tipo de vínculo.");
        if (flujo.VersionPublicada is null)
            throw ReglaDeNegocioException.Validacion("flujo-sin-version-publicada", "Publica el flujo antes de marcarlo por defecto.");

        var clave = FlujoPorDefecto.Clave(empresa.EmpresaId, dto.TipoVinculoId);
        var actual = await empresa.Sesion.LoadAsync<FlujoPorDefecto>(clave, ct);
        var antes = actual is null ? null : new FlujoPorDefectoDto(actual.TipoVinculoId, actual.FlujoId, actual.EmpresaId);
        var nuevo = new FlujoPorDefecto { Id = clave, EmpresaId = empresa.EmpresaId, TipoVinculoId = dto.TipoVinculoId, FlujoId = flujo.Id };
        var despues = new FlujoPorDefectoDto(nuevo.TipoVinculoId, nuevo.FlujoId, nuevo.EmpresaId);

        empresa.Sesion.Store(nuevo);
        Auditoria.Registrar(empresa.Sesion, contexto, "FlujoPorDefecto", clave, actual is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado,
            antes, despues, reloj.GetUtcNow(), empresa.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return despues;
    }

    [WolverineGet("/api/v1/flujos/{id}")]
    [Authorize(Policy = Permisos.FlujosLeer)]
    public static async Task<FlujoDto> Obtener(Guid id, SesionDeEmpresa empresa, CancellationToken ct) =>
        await DtoAsync(empresa, await empresa.CargarFlujoAsync(id, ct), ct);

    /// <summary>Edita el borrador (o crea uno nuevo con la versión siguiente); la versión publicada no cambia.</summary>
    [WolverinePut("/api/v1/flujos/{id}")]
    [Authorize(Policy = Permisos.FlujosEscribir)]
    public static async Task<FlujoDto> Editar(Guid id, EditarFlujoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var flujo = await empresa.CargarFlujoAsync(id, ct);
        var antes = await DtoAsync(empresa, flujo, ct);
        var ultima = flujo.Ultima;
        var tipos = await TiposAsync(empresa, ct);
        var definicion = new DefinicionDeFlujo(
            dto.Etapas is null ? ultima.Etapas : [.. dto.Etapas.Select(e => e.ADominio())],
            Politica(dto.PoliticaDevolucion) ?? ultima.PoliticaDevolucion,
            dto.SeparacionDeFunciones ?? ultima.SeparacionDeFunciones,
            dto.Requisitos is null ? ultima.Requisitos : [.. dto.Requisitos.Select(r => r.ADominio(tipos))],
            dto.Verificaciones is null ? ultima.Verificaciones : [.. dto.Verificaciones.Select(v => v.ADominio())],
            dto.PlazoCargue?.ADominio() ?? ultima.PlazoEfectivo(),
            dto.Reintento?.ADominio() ?? ultima.ReintentoEfectivo(),
            dto.VigenciaVinculo?.ADominio() ?? ultima.VigenciaEfectiva(),
            dto.RecordatorioObservaciones is { } recordatorio ? recordatorio.ADominio() : ultima.RecordatorioObservacionesDias,
            dto.Seleccion is { } seleccion ? seleccion.ADominio() : ultima.Seleccion);

        var borrador = flujo.EditarBorrador(dto.Nombre, definicion, await empresa.Areas().ToListAsync(ct), ahora, tipos,
            await empresa.Sesion.LoadAsync<TipoVinculo>(flujo.TipoVinculoId, ct));
        await ValidarPrioridadAsync(empresa, flujo, borrador.Seleccion, ct);
        return await GuardarAsync(empresa, contexto, flujo, "FlujoVersion", $"{flujo.Id}/v{borrador.Version}", AccionConfiguracion.Modificado, antes, ahora, ct);
    }

    /// <summary>Publica el borrador y retira la versión anterior. Si el tipo de vínculo no tenía flujo por defecto, este lo es.</summary>
    [WolverinePost("/api/v1/flujos/{id}/publicar")]
    [Authorize(Policy = Permisos.FlujosEscribir)]
    public static async Task<FlujoDto> Publicar(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var flujo = await empresa.CargarFlujoAsync(id, ct);
        var antes = await DtoAsync(empresa, flujo, ct);
        var publicada = flujo.Publicar(await empresa.Areas().ToListAsync(ct), ahora, await TiposAsync(empresa, ct),
            await empresa.Sesion.LoadAsync<TipoVinculo>(flujo.TipoVinculoId, ct));
        await ValidarPrioridadAsync(empresa, flujo, publicada.Seleccion, ct);

        var clave = FlujoPorDefecto.Clave(flujo.EmpresaId, flujo.TipoVinculoId);
        if (await empresa.Sesion.LoadAsync<FlujoPorDefecto>(clave, ct) is null)
        {
            var porDefecto = new FlujoPorDefecto { Id = clave, EmpresaId = flujo.EmpresaId, TipoVinculoId = flujo.TipoVinculoId, FlujoId = flujo.Id };
            empresa.Sesion.Store(porDefecto);
            Auditoria.Registrar(empresa.Sesion, contexto, "FlujoPorDefecto", clave, AccionConfiguracion.Creado, null,
                new FlujoPorDefectoDto(porDefecto.TipoVinculoId, porDefecto.FlujoId, porDefecto.EmpresaId), ahora, flujo.EmpresaId);
        }

        return await GuardarAsync(empresa, contexto, flujo, "FlujoVersion", $"{flujo.Id}/v{publicada.Version}", AccionConfiguracion.Publicado, antes,
            ahora, ct);
    }

    [WolverineGet("/api/v1/flujos/{id}/versiones")]
    [Authorize(Policy = Permisos.FlujosLeer)]
    public static async Task<IReadOnlyList<FlujoVersionDto>> Versiones(Guid id, SesionDeEmpresa empresa, CancellationToken ct) =>
        [.. (await empresa.CargarFlujoAsync(id, ct)).Versiones.OrderBy(v => v.Version).Select(v => v.ADto())];

    [WolverineGet("/api/v1/flujos/{id}/versiones/{n}")]
    [Authorize(Policy = Permisos.FlujosLeer)]
    public static async Task<FlujoVersionDto> Version(Guid id, int n, SesionDeEmpresa empresa, CancellationToken ct) =>
        (await empresa.CargarFlujoAsync(id, ct)).Versiones.FirstOrDefault(v => v.Version == n)?.ADto()
        ?? throw ReglaDeNegocioException.NoEncontrado("version-no-encontrada", $"El flujo no tiene la versión {n}.");

    /// <summary>
    /// «¿Qué flujo le tocaría a este tercero?» (FR-1): evalúa, sin abrir nada, las reglas de selección de los flujos publicados del tipo
    /// de vínculo con los datos del tercero (<c>terceroId</c>, visible para el tenant) y/o los indicados, y responde el flujo que
    /// escogería el sistema con su origen (<c>Regla</c> o <c>PorDefecto</c>) y todas las reglas con su resultado, en orden de evaluación.
    /// Es lo que usa el editor para probar una condición y lo que puede consultar el sistema integrado antes de crear el vínculo.
    /// </summary>
    [WolverinePost("/api/v1/flujos/seleccion/vista-previa")]
    [Authorize(Policy = Permisos.FlujosLeer)]
    public static async Task<SeleccionPrevistaDto> VistaPrevia(VistaPreviaDeSeleccionDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        CancellationToken ct)
    {
        var codigoTipo = (dto.TipoVinculo ?? string.Empty).Trim().ToUpperInvariant();
        var tipo = await empresa.Sesion.Query<TipoVinculo>().FirstOrDefaultAsync(t => t.Codigo == codigoTipo, ct)
                   ?? throw ReglaDeNegocioException.Validacion("tipo-vinculo-invalido", $"El tipo de vínculo '{dto.TipoVinculo}' no existe.");

        var tercero = dto.TerceroId is { } terceroId ? await TercerosEndpoints.CargarVisibleAsync(empresa.Sesion, terceroId, contexto, ct) : null;
        TipoPersona? persona = string.IsNullOrWhiteSpace(dto.TipoPersona) ? null
            : Enum.TryParse<TipoPersona>(dto.TipoPersona, true, out var indicada) && Enum.IsDefined(indicada) ? indicada
            : throw ReglaDeNegocioException.Validacion("tipo-persona-invalido",
                $"'{dto.TipoPersona}' no es un tipo de persona ({string.Join(", ", Enum.GetNames<TipoPersona>())}).");
        var pais = Texto(dto.Pais) ?? tercero?.Identificacion.Pais ?? "CO";
        var evaluado = new ContextoDeExigencia(persona ?? tercero?.TipoPersona ?? TipoPersona.Juridica, pais,
            Texto(dto.TipoIdentificacion) ?? tercero?.Identificacion.Tipo ?? "NIT",
            dto.EsExtranjero ?? (dto.Pais is null && tercero is not null ? tercero.EsExtranjero : pais != "CO"), tipo.Codigo,
            dto.Atributos ?? new Dictionary<string, System.Text.Json.JsonElement>());

        var tipoId = tipo.Id;
        var flujos = await empresa.Flujos().Where(f => f.TipoVinculoId == tipoId).ToListAsync(ct);
        var defecto = await empresa.Sesion.LoadAsync<FlujoPorDefecto>(FlujoPorDefecto.Clave(empresa.EmpresaId, tipo.Id), ct);
        var seleccion = EvaluadorDeSeleccion.Seleccionar(flujos, defecto is null ? null : flujos.FirstOrDefault(f => f.Id == defecto.FlujoId), evaluado);

        return new SeleccionPrevistaDto(
            seleccion is { Flujo: { } flujo, Version: { } version }
                ? new FlujoDeExpedienteDto(flujo.Id, version.Id, flujo.Codigo, flujo.Nombre, version.Version, seleccion.Origen.ToString(), seleccion.Regla?.ADto())
                : null,
            [.. seleccion.Reglas.Select(r => new ReglaDeSeleccionDto(r.Flujo.Id, r.Version.Id, r.Flujo.Codigo, r.Flujo.Nombre, r.Version.Version, r.Prioridad,
                r.Condicion.ADto(), r.Cumple))]);
    }

    private static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToUpperInvariant();

    /// <summary>Una prioridad de selección no se repite entre los flujos publicados del mismo tipo de vínculo (422 <c>prioridad-duplicada</c>).</summary>
    private static async Task ValidarPrioridadAsync(SesionDeEmpresa empresa, Flujo flujo, SeleccionDeFlujo? seleccion, CancellationToken ct)
    {
        if (seleccion is not { Participa: true })
            return;
        var tipoId = flujo.TipoVinculoId;
        EvaluadorDeSeleccion.ValidarPrioridadUnica(flujo, seleccion, await empresa.Flujos().Where(f => f.TipoVinculoId == tipoId).ToListAsync(ct));
    }

    private static async Task<FlujoDto> GuardarAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, Flujo flujo, string entidad, string entidadId,
        AccionConfiguracion accion, FlujoDto antes, DateTimeOffset ahora, CancellationToken ct)
    {
        empresa.Sesion.Store(flujo);
        Auditoria.Registrar(empresa.Sesion, contexto, entidad, entidadId, accion, antes, await DtoAsync(empresa, flujo, ct), ahora, flujo.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return await DtoAsync(empresa, flujo, ct);
    }

    private static async Task<FlujoDto> DtoAsync(SesionDeEmpresa empresa, Flujo flujo, CancellationToken ct)
    {
        var tipo = await empresa.Sesion.LoadAsync<TipoVinculo>(flujo.TipoVinculoId, ct);
        var porDefecto = await empresa.Sesion.LoadAsync<FlujoPorDefecto>(FlujoPorDefecto.Clave(flujo.EmpresaId, flujo.TipoVinculoId), ct);
        return flujo.ADto(tipo, porDefecto?.FlujoId == flujo.Id);
    }

    /// <summary>Tipos de documento del tenant (los requisitos se validan contra ellos: activos y de clase <c>Archivo</c>).</summary>
    private static async Task<IReadOnlyList<TipoDocumentoTenant>> TiposAsync(SesionDeEmpresa empresa, CancellationToken ct) =>
        await empresa.Sesion.Query<TipoDocumentoTenant>().ToListAsync(ct);

    private static async Task<IReadOnlyList<FlujoPorDefecto>> PorDefectoAsync(SesionDeEmpresa empresa, CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        return await empresa.Sesion.Query<FlujoPorDefecto>().Where(p => p.EmpresaId == empresaId).ToListAsync(ct);
    }

    private static PoliticaDevolucion? Politica(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null
        : Enum.TryParse<PoliticaDevolucion>(valor, true, out var politica) && Enum.IsDefined(politica) ? politica
        : throw ReglaDeNegocioException.Validacion("politica-invalida",
            $"'{valor}' no es una política de devolución ({string.Join(", ", Enum.GetNames<PoliticaDevolucion>())}).");
}
