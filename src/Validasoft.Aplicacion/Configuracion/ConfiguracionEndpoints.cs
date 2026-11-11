using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Sandbox;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Configuracion;

/// <summary>Razones sociales del tenant (ADR-0008).</summary>
public static class EmpresasEndpoints
{
    [WolverinePost("/api/v1/empresas")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<IResult> Crear(CrearEmpresaDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        Validasoft.Aplicacion.Plataforma.ControlDePlan plan, TimeProvider reloj, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        await plan.ExigirAsync(tenantId, Validasoft.Dominio.Plataforma.MetricaDePlan.Empresas, 1, ct); // límite de empresas del plan con bloqueo suave (OD-17)
        var ahora = reloj.GetUtcNow();
        var empresa = Empresa.Crear(tenantId, dto.Identificacion.ADominio(), dto.Nombre, dto.PrefijoFolio, dto.LogoUrl, dto.ReplyTo, ahora);

        await using var sesion = sesiones.DelTenant();
        var existentes = await sesion.Query<Empresa>()
            .Where(e => e.TenantId == tenantId && (e.ClaveIdentificacion == empresa.ClaveIdentificacion || e.PrefijoFolio == empresa.PrefijoFolio))
            .ToListAsync(ct);

        if (existentes.Any(e => e.ClaveIdentificacion == empresa.ClaveIdentificacion))
            throw ReglaDeNegocioException.Conflicto("empresa-duplicada", "Ya existe una empresa con esa identificación en el tenant.");
        if (existentes.Any(e => e.PrefijoFolio == empresa.PrefijoFolio))
            throw ReglaDeNegocioException.Conflicto("prefijo-folio-duplicado", $"El prefijo {empresa.PrefijoFolio} ya lo usa otra empresa.");

        sesion.Store(empresa);
        Auditoria.Registrar(sesion, contexto, "Empresa", empresa.Id.ToString(), AccionConfiguracion.Creado, null, empresa.ADto(), ahora, empresa.Id);
        await sesion.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/empresas/{empresa.Id}", empresa.ADto());
    }

    /// <summary>Solo las empresas que la credencial puede usar.</summary>
    [WolverineGet("/api/v1/empresas")]
    [Authorize]
    public static async Task<IReadOnlyList<EmpresaDto>> Listar(ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        await using var consulta = sesiones.ConsultaGlobal();
        var empresas = await consulta.Query<Empresa>().Where(e => e.TenantId == tenantId).OrderBy(e => e.Nombre).ToListAsync(ct);
        return [.. empresas.Where(e => contexto.Empresas.Contains("*") || contexto.Empresas.Contains(e.Id.ToString())).Select(e => e.ADto())];
    }

    [WolverineGet("/api/v1/empresas/{id}")]
    [Authorize]
    public static async Task<EmpresaDto> Obtener(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct) =>
        (await CargarAsync(id, sesiones, contexto, ct)).ADto();

    /// <summary>Datos y marca (contacto de soporte, color y logo). La identificación, la razón social y el prefijo no cambian.</summary>
    [WolverinePut("/api/v1/empresas/{id}")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<EmpresaDto> Actualizar(Guid id, ActualizarEmpresaDto dto, ISesiones sesiones, IContextoSolicitud contexto, CacheDeEmpresas cache,
        TimeProvider reloj, CancellationToken ct)
    {
        var empresa = await CargarAsync(id, sesiones, contexto, ct);
        var antes = empresa.ADto();
        empresa.ActualizarMarca(dto.ContactoSoporte, dto.ColorMarca, dto.LogoUrl);

        await using var sesion = sesiones.DelTenant();
        sesion.Store(empresa);
        Auditoria.Registrar(sesion, contexto, "Empresa", empresa.Id.ToString(), AccionConfiguracion.Modificado, antes, empresa.ADto(), reloj.GetUtcNow(), empresa.Id);
        await sesion.SaveChangesAsync(ct);
        cache.Olvidar(empresa.Id);
        return empresa.ADto();
    }

    [WolverinePost("/api/v1/empresas/{id}/desactivar")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<EmpresaDto> Desactivar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CacheDeEmpresas cache, TimeProvider reloj,
        CancellationToken ct)
    {
        var empresa = await CargarAsync(id, sesiones, contexto, ct);
        var antes = empresa.ADto();
        empresa.Activa = false;

        await using var sesion = sesiones.DelTenant();
        sesion.Store(empresa);
        Auditoria.Registrar(sesion, contexto, "Empresa", empresa.Id.ToString(), AccionConfiguracion.Desactivado, antes, empresa.ADto(), reloj.GetUtcNow(), empresa.Id);
        await sesion.SaveChangesAsync(ct);
        cache.Olvidar(empresa.Id); // el corte vale ya en este nodo (en los demás, a lo sumo 30 s)
        return empresa.ADto();
    }

    internal static async Task<Empresa> CargarAsync(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        await using var consulta = sesiones.ConsultaGlobal();
        var empresa = await consulta.LoadAsync<Empresa>(id, ct);
        var permitida = empresa is not null && empresa.TenantId == tenantId &&
                        (contexto.Empresas.Contains("*") || contexto.Empresas.Contains(id.ToString()));
        return permitida ? empresa! : throw ReglaDeNegocioException.NoEncontrado("empresa-no-encontrada", "No existe esa empresa.");
    }
}

/// <summary>API keys del tenant (docs/07 §4).</summary>
public static class ApiKeysEndpoints
{
    [WolverinePost("/api/v1/api-keys")]
    [Authorize(Policy = Permisos.ApiKeysAdministrar)]
    public static async Task<IResult> Crear(CrearApiKeyDto dto, ISesiones sesiones, IGeneradorDeApiKeys llaves,
        IContextoSolicitud contexto, ServicioDeSandbox sandbox, TimeProvider reloj, CancellationToken ct)
    {
        ExigirProduccion(contexto);
        var tenantId = contexto.TenantRequerido();
        var ahora = reloj.GetUtcNow();

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "La llave necesita un nombre.");

        var invalidos = dto.Scopes.Except(Permisos.DelTenant).ToList();
        if (dto.Scopes.Count == 0 || invalidos.Count > 0)
            throw ReglaDeNegocioException.Validacion("scopes-invalidos", $"Scopes no válidos: {string.Join(", ", invalidos)}.");

        if (dto.ExpiraEn is not null && dto.ExpiraEn <= ahora)
            throw ReglaDeNegocioException.Validacion("expiracion-invalida", "La fecha de expiración debe ser futura.");

        var empresas = dto.Empresas is null || dto.Empresas.Count == 0 ? ["*"] : dto.Empresas.ToList();
        if (!empresas.Contains("*"))
        {
            await using var consulta = sesiones.ConsultaGlobal();
            var ids = empresas.Select(e => Guid.TryParse(e, out var g) ? g : Guid.Empty).ToList();
            var propias = await consulta.Query<Empresa>().Where(e => e.TenantId == tenantId).Select(e => e.Id).ToListAsync(ct);
            if (ids.Any(i => !propias.Contains(i)))
                throw ReglaDeNegocioException.Validacion("empresas-invalidas", "Alguna empresa no pertenece al tenant.");
        }

        ExigirAlcance(contexto, empresas);

        await using var sesion = sesiones.DelTenant();
        var tenant = await sesion.LoadAsync<Tenant>(tenantId, ct)
            ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "El tenant de la credencial no existe.");

        if (dto.SistemaId is { } sistemaId && await sesion.LoadAsync<SistemaIntegrado>(sistemaId, ct) is not { Activo: true })
            throw ReglaDeNegocioException.Validacion("sistema-invalido", "El sistema integrado no existe en el tenant o está desactivado.");

        // Una llave de prueba opera en el sandbox del grupo (SB-2): se crea —o se pone al día con las empresas nuevas— al emitirla.
        if (dto.Sandbox)
            await sandbox.AsegurarAsync(tenantId, ct);

        var (llave, valor) = llaves.Generar(tenant.Slug, dto.Nombre.Trim(), dto.Scopes, empresas, dto.ExpiraEn, dto.Sandbox, ahora);
        llave.SistemaId = dto.SistemaId;
        sesion.Store(llave);
        Auditoria.Registrar(sesion, contexto, "ApiKey", llave.Id.ToString(), AccionConfiguracion.Creado, null, llave.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/api-keys/{llave.Id}", llave.ADto(valor));
    }

    [WolverineGet("/api/v1/api-keys")]
    [Authorize(Policy = Permisos.ApiKeysAdministrar)]
    public static async Task<IReadOnlyList<ApiKeyDto>> Listar(ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        ExigirProduccion(contexto);
        await using var sesion = sesiones.DelTenant();
        var llaves = await sesion.Query<ApiKey>().Where(k => k.RevocadaEn == null).OrderBy(k => k.CreadaEn).ToListAsync(ct);
        return [.. llaves.Select(k => k.ADto())];
    }

    [WolverineDelete("/api/v1/api-keys/{id}")]
    [Authorize(Policy = Permisos.ApiKeysAdministrar)]
    public static async Task<IResult> Revocar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeCredenciales cache, TimeProvider reloj,
        CancellationToken ct)
    {
        ExigirProduccion(contexto);
        await using var sesion = sesiones.DelTenant();
        var llave = await sesion.LoadAsync<ApiKey>(id, ct);
        if (llave is null || llave.RevocadaEn is not null)
            throw ReglaDeNegocioException.NoEncontrado("api-key-no-encontrada", "No existe esa API key.");

        var ahora = reloj.GetUtcNow();
        llave.RevocadaEn = ahora;
        sesion.Store(llave);
        Auditoria.Registrar(sesion, contexto, "ApiKey", llave.Id.ToString(), AccionConfiguracion.Revocado, null, llave.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);
        cache.Olvidar(llave.Id);
        return Results.NoContent();
    }

    /// <summary>
    /// Rotación con solapamiento (CN-3): emite una llave nueva con los mismos permisos, alcance, sistema y expiración, y deja
    /// la anterior válida hasta <see cref="RotarApiKeyDto.AnteriorValidaHasta"/> (24 h por defecto) para que el sistema
    /// cambie la clave sin cortar el servicio. Devuelve la nueva con su <c>valor</c> (una sola vez; la respuesta no se guarda
    /// para idempotencia: rotar dos veces la misma responde 409 <c>api-key-ya-rotada</c>).
    /// </summary>
    [WolverinePost("/api/v1/api-keys/{id}/rotar")]
    [Authorize(Policy = Permisos.ApiKeysAdministrar)]
    public static async Task<IResult> Rotar(Guid id, RotarApiKeyDto dto, ISesiones sesiones, IGeneradorDeApiKeys llaves, IContextoSolicitud contexto,
        ICacheDeCredenciales cache, TimeProvider reloj, CancellationToken ct)
    {
        ExigirProduccion(contexto);
        var tenantId = contexto.TenantRequerido();
        await using var sesion = sesiones.DelTenant();
        var anterior = await sesion.LoadAsync<ApiKey>(id, ct);
        if (anterior is null || anterior.RevocadaEn is not null)
            throw ReglaDeNegocioException.NoEncontrado("api-key-no-encontrada", "No existe esa API key.");

        ExigirAlcance(contexto, anterior.Empresas);
        var tenant = await sesion.LoadAsync<Tenant>(tenantId, ct)
            ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "El tenant de la credencial no existe.");

        var ahora = reloj.GetUtcNow();
        var antes = anterior.ADto();
        var expiraOriginal = anterior.ExpiraEn;
        var (nueva, valor) = llaves.Generar(tenant.Slug, anterior.Nombre, anterior.Scopes, anterior.Empresas, expiraOriginal, anterior.Sandbox, ahora);
        nueva.SistemaId = anterior.SistemaId;
        nueva.RotadaDe = anterior.Id;
        anterior.RotarHacia(nueva.Id, dto.AnteriorValidaHasta, ahora);

        sesion.Store(anterior);
        sesion.Store(nueva);
        Auditoria.Registrar(sesion, contexto, "ApiKey", anterior.Id.ToString(), AccionConfiguracion.Rotado, antes, anterior.ADto(), ahora);
        Auditoria.Registrar(sesion, contexto, "ApiKey", nueva.Id.ToString(), AccionConfiguracion.Creado, null, nueva.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);
        cache.Olvidar(anterior.Id);

        return Results.Created($"/api/v1/api-keys/{nueva.Id}", nueva.ADto(valor));
    }

    /// <summary>
    /// Las API keys —también las de prueba— viven en el grupo de producción y se administran con sus credenciales: una credencial del
    /// sandbox (SB-2) no las crea, lista, rota ni revoca. 422 <c>no-disponible-en-sandbox</c>.
    /// </summary>
    private static void ExigirProduccion(IContextoSolicitud contexto)
    {
        if (contexto.EsSandbox)
            throw ReglaDeNegocioException.Validacion("no-disponible-en-sandbox",
                "Las API keys se administran con las credenciales de producción del grupo (también las de prueba); esta credencial opera en el sandbox.");
    }

    /// <summary>Una credencial no puede emitir (ni rotar) una llave con más empresas que las suyas (anti-escalada).</summary>
    private static void ExigirAlcance(IContextoSolicitud contexto, IReadOnlyCollection<string> empresas)
    {
        if (!contexto.Empresas.Contains("*") && (empresas.Contains("*") || empresas.Any(e => !contexto.Empresas.Contains(e))))
            throw new ReglaDeNegocioException(TipoFalla.Prohibido, "alcance-insuficiente", "No puedes emitir una llave con más empresas que la tuya.");
    }
}

/// <summary>Tipos de vínculo del tenant: proveedor, cliente, empleado, usuario… y los que la empresa defina.</summary>
public static class TiposVinculoEndpoints
{
    [WolverinePost("/api/v1/tipos-vinculo")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<IResult> Crear(CrearTipoVinculoDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var tipo = TipoVinculo.Crear(dto.Codigo, dto.Nombre, dto.AtributosPermitidos?.Select(a => a.ADominio()), dto.ExigeCuentaDesignada,
            dto.ExigeOtpEnEnlace);

        await using var sesion = sesiones.DelTenant();
        if (await sesion.Query<TipoVinculo>().AnyAsync(t => t.Codigo == tipo.Codigo, ct))
            throw ReglaDeNegocioException.Conflicto("tipo-vinculo-duplicado", $"Ya existe el tipo de vínculo {tipo.Codigo}.");

        sesion.Store(tipo);
        Auditoria.Registrar(sesion, contexto, "TipoVinculo", tipo.Id.ToString(), AccionConfiguracion.Creado, null, tipo.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/tipos-vinculo/{tipo.Id}", tipo.ADto());
    }

    [WolverineGet("/api/v1/tipos-vinculo")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<IReadOnlyList<TipoVinculoDto>> Listar(ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipos = await sesion.Query<TipoVinculo>().OrderBy(t => t.Codigo).ToListAsync(ct);
        return [.. tipos.Select(t => t.ADto())];
    }

    [WolverineGet("/api/v1/tipos-vinculo/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<TipoVinculoDto> Obtener(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        return (await sesion.LoadAsync<TipoVinculo>(id, ct)
                ?? throw ReglaDeNegocioException.NoEncontrado("tipo-vinculo-no-encontrado", "No existe ese tipo de vínculo.")).ADto();
    }

    /// <summary>El código es inmutable; se editan nombre, atributos, cuenta designada, OTP en el enlace y estado.</summary>
    [WolverinePut("/api/v1/tipos-vinculo/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<TipoVinculoDto> Actualizar(Guid id, CrearTipoVinculoDto dto, bool? activo, ISesiones sesiones,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipo = await sesion.LoadAsync<TipoVinculo>(id, ct)
                   ?? throw ReglaDeNegocioException.NoEncontrado("tipo-vinculo-no-encontrado", "No existe ese tipo de vínculo.");

        if (!string.Equals(dto.Codigo?.Trim(), tipo.Codigo, StringComparison.OrdinalIgnoreCase))
            throw ReglaDeNegocioException.Validacion("codigo-inmutable", "El código de un tipo de vínculo no se puede cambiar.");

        var antes = tipo.ADto();
        var validado = TipoVinculo.Crear(tipo.Codigo, dto.Nombre, dto.AtributosPermitidos?.Select(a => a.ADominio()), dto.ExigeCuentaDesignada,
            dto.ExigeOtpEnEnlace);
        tipo.Nombre = validado.Nombre;
        tipo.AtributosPermitidos = validado.AtributosPermitidos;
        tipo.ExigeCuentaDesignada = validado.ExigeCuentaDesignada;
        tipo.ExigeOtpEnEnlace = validado.ExigeOtpEnEnlace;
        tipo.Activo = activo ?? tipo.Activo;

        sesion.Store(tipo);
        Auditoria.Registrar(sesion, contexto, "TipoVinculo", tipo.Id.ToString(), AccionConfiguracion.Modificado, antes, tipo.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return tipo.ADto();
    }
}

/// <summary>Áreas revisoras de la empresa activa (cabecera <c>X-Empresa</c>) y sus miembros.</summary>
public static class AreasEndpoints
{
    [WolverinePost("/api/v1/areas")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<IResult> Crear(CrearAreaDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var area = Area.Crear(empresa.EmpresaId, dto.Codigo, dto.Nombre);

        var codigo = area.Codigo;
        if (await empresa.Areas().AnyAsync(a => a.Codigo == codigo, ct))
            throw ReglaDeNegocioException.Conflicto("area-duplicada", $"La empresa ya tiene el área {codigo}.");

        empresa.Sesion.Store(area);
        Auditoria.Registrar(empresa.Sesion, contexto, "Area", area.Id.ToString(), AccionConfiguracion.Creado, null, area.ADto(), reloj.GetUtcNow(), area.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/areas/{area.Id}", area.ADto());
    }

    [WolverineGet("/api/v1/areas")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<IReadOnlyList<AreaDto>> Listar(SesionDeEmpresa empresa, CancellationToken ct)
    {
        var areas = await empresa.Areas().OrderBy(a => a.Codigo).ToListAsync(ct);
        return [.. areas.Select(a => a.ADto())];
    }

    [WolverineGet("/api/v1/areas/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<AreaDto> Obtener(Guid id, SesionDeEmpresa empresa, CancellationToken ct) =>
        (await empresa.CargarAreaAsync(id, ct)).ADto();

    /// <summary>El código es inmutable; se editan nombre y estado.</summary>
    [WolverinePut("/api/v1/areas/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<AreaDto> Actualizar(Guid id, ActualizarAreaDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var area = await empresa.CargarAreaAsync(id, ct);
        var antes = area.ADto();
        area.Actualizar(dto.Nombre, dto.Activa);
        return await GuardarAsync(empresa, contexto, area, antes, reloj, ct);
    }

    /// <summary>Reemplaza los miembros: cada uno debe ser un usuario activo con algún rol en esta empresa o en todo el tenant.</summary>
    [WolverinePut("/api/v1/areas/{id}/miembros")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<AreaDto> DefinirMiembros(Guid id, MiembrosAreaDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var area = await empresa.CargarAreaAsync(id, ct);
        var ids = (dto.Usuarios ?? []).Distinct().ToArray();
        var usuarios = ids.Length == 0 ? [] : await empresa.Sesion.LoadManyAsync<Usuario>(ct, ids);

        var invalidos = ids.Where(i => usuarios.FirstOrDefault(u => u.Id == i) is not { Activo: true } u || !u.TieneRolEn(area.EmpresaId)).ToList();
        if (invalidos.Count > 0)
            throw ReglaDeNegocioException.Validacion("miembros-invalidos",
                $"Estos usuarios no existen, están inactivos o no tienen rol en la empresa: {string.Join(", ", invalidos)}.");

        var antes = area.ADto();
        area.DefinirMiembros(ids);
        return await GuardarAsync(empresa, contexto, area, antes, reloj, ct);
    }

    private static async Task<AreaDto> GuardarAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, Area area, AreaDto antes,
        TimeProvider reloj, CancellationToken ct)
    {
        empresa.Sesion.Store(area);
        Auditoria.Registrar(empresa.Sesion, contexto, "Area", area.Id.ToString(), AccionConfiguracion.Modificado, antes, area.ADto(), reloj.GetUtcNow(), area.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return area.ADto();
    }
}
