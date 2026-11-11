using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// Cuentas bancarias del tercero (CB-1) y la cuenta que designa para cobrarle a cada empresa (CB-2, docs/02 §4, P-3-3):
/// el tercero registra sus cuentas en la bóveda (número protegido) y elige una por vínculo; la empresa la aprueba con dos
/// personas distintas que tengan <c>cuentas:aprobar</c>. Cambiarla con una empresa no afecta a las demás.
/// </summary>
public static class CuentasPublicasEndpoints
{
    [WolverineGet("/public/v1/boveda/cuentas-bancarias")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IReadOnlyList<CuentaBancariaPublicaDto>> Listar(HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var boveda = sesiones.ConsultaBoveda();
        var terceroId = tercero.TerceroId;
        var cuentas = await boveda.Query<CuentaBancaria>().Where(c => c.TerceroId == terceroId).OrderBy(c => c.RegistradaEn).ToListAsync(ct);
        await using var consulta = sesiones.ConsultaGlobal();
        var vistos = await VistaDelTercero.CargarAsync(consulta, tercero, [.. tercero.Vinculos.Select(v => v.VinculoId)], ct);
        return [.. cuentas.Select(c => APublica(c, vistos))];
    }

    [WolverinePost("/public/v1/boveda/cuentas-bancarias")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<Created<CuentaBancariaPublicaDto>> Registrar(RegistrarCuentaBancariaDto dto, HttpContext http, ISesiones sesiones, IProtectorDeSecretos protector,
        TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var datos = new DatosDeCuentaBancaria(dto.Pais, dto.Banco, dto.TipoCuenta, dto.Numero, dto.Titular, dto.IdentificacionTitular, dto.Moneda,
            dto.BancoCodigo, dto.BancoSwift, dto.ContactoPagos is null ? null : new ContactoDePagos(dto.ContactoPagos.Nombre, dto.ContactoPagos.Correo, dto.ContactoPagos.Telefono));
        // El banco se toma del catálogo de plataforma (IO-4): su nombre, su código de compensación y, si el tercero no lo dio, su SWIFT.
        if (!string.IsNullOrWhiteSpace(dto.BancoCodigo))
        {
            var codigo = ReglasDeCatalogo.NormalizarCodigo(CatalogoDePlataforma.Bancos, dto.BancoCodigo)
                         ?? throw ReglaDeNegocioException.Validacion("banco-no-encontrado", "El código del banco no es un código de compensación válido (4 dígitos).");
            await using var global = sesiones.ConsultaGlobal();
            var banco = await global.LoadAsync<EntradaDeCatalogo>(EntradaDeCatalogo.IdPara(CatalogoDePlataforma.Bancos, codigo), ct);
            if (banco is not { Activo: true })
                throw ReglaDeNegocioException.Validacion("banco-no-encontrado", $"El banco {codigo} no está en el catálogo.");
            datos = datos with { Banco = banco.Nombre, BancoCodigo = banco.Codigo, BancoSwift = string.IsNullOrWhiteSpace(dto.BancoSwift) ? banco.Swift : dto.BancoSwift, Pais = string.IsNullOrWhiteSpace(dto.Pais) ? banco.Pais : dto.Pais };
        }

        var cuenta = CuentaBancaria.Registrar(tercero.TerceroId, datos, protector.Proteger, reloj.GetUtcNow());
        await using var boveda = sesiones.Boveda();
        boveda.Store(cuenta);
        await boveda.SaveChangesAsync(ct);
        return TypedResults.Created($"/public/v1/boveda/cuentas-bancarias/{cuenta.Id}", APublica(cuenta, []));
    }

    /// <summary>Retira una cuenta. Si está designada o propuesta con alguna empresa, 409 <c>cuenta-en-uso</c>: primero designa otra.</summary>
    [WolverinePost("/public/v1/boveda/cuentas-bancarias/{id}/retirar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<CuentaBancariaPublicaDto> Retirar(Guid id, HttpContext http, ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var consulta = sesiones.ConsultaGlobal();
        var vistos = await VistaDelTercero.CargarAsync(consulta, tercero, [.. tercero.Vinculos.Select(v => v.VinculoId)], ct);
        if (vistos.Any(v => v.Vinculo.CuentaDesignada?.Cuenta.CuentaId == id || v.Vinculo.CuentaPropuesta?.Cuenta.CuentaId == id))
            throw ReglaDeNegocioException.Conflicto("cuenta-en-uso", "Esa cuenta está designada con una empresa: primero designa otra.");

        await using var boveda = sesiones.Boveda();
        var cuenta = await boveda.LoadAsync<CuentaBancaria>(id, ct);
        if (cuenta is null || cuenta.TerceroId != tercero.TerceroId)
            throw ReglaDeNegocioException.NoEncontrado("cuenta-no-encontrada", "No existe esa cuenta.");
        cuenta.Retirar(reloj.GetUtcNow());
        boveda.Store(cuenta);
        await boveda.SaveChangesAsync(ct);
        return APublica(cuenta, vistos);
    }

    /// <summary>
    /// El tercero designa una cuenta suya, activa, para cobrarle a la empresa de este vínculo. Exige tener la certificación
    /// bancaria en la bóveda (422 <c>certificacion-requerida</c>). Queda propuesta hasta que la empresa la apruebe.
    /// </summary>
    [WolverinePost("/public/v1/vinculos/{id}/cuenta-designada")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<CuentaBancariaPublicaDto> Designar(Guid id, DesignarCuentaDto dto, HttpContext http, ISesiones sesiones, IProtectorDeSecretos protector,
        IProtectorDeCamposDelGrupo campos, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var enSesion = tercero.VinculoRequerido(id);
        CuentaBancaria cuenta;
        await using (var boveda = sesiones.ConsultaBoveda())
        {
            cuenta = await boveda.LoadAsync<CuentaBancaria>(dto.CuentaId, ct) is { } propia && propia.TerceroId == tercero.TerceroId
                ? propia
                : throw ReglaDeNegocioException.NoEncontrado("cuenta-no-encontrada", "No existe esa cuenta.");
            if (cuenta.Estado != EstadoCuentaBancaria.Activa)
                throw ReglaDeNegocioException.Conflicto("cuenta-retirada", "Esa cuenta está retirada.");
            var terceroId = tercero.TerceroId;
            var certificacion = await boveda.Query<Documento>()
                .Where(d => d.TerceroId == terceroId && d.TipoCanonico == "CERTIFICACION_BANCARIA").FirstOrDefaultAsync(ct);
            if (certificacion?.VersionActual is null)
                throw ReglaDeNegocioException.Validacion("certificacion-requerida", "Sube tu certificación bancaria antes de designar una cuenta.");
        }

        await Peticiones.ConReintentosAsync(async () =>
        {
            await using var sesion = sesiones.ParaTenant(enSesion.TenantId);
            var stream = await sesion.Events.FetchForWriting<Vinculo>(id, ct);
            if (stream.Aggregate is not { } vinculo || vinculo.TerceroId != tercero.TerceroId)
                throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");
            if (vinculo.ProponerCuenta(ParaDesignar(cuenta, enSesion.TenantId, protector, campos), reloj.GetUtcNow()) is { } propuesta)
            {
                stream.AppendOne(propuesta);
                await sesion.SaveChangesAsync(ct);
            }

            return true;
        }, 5);

        await using var consulta = sesiones.ConsultaGlobal();
        var vistos = await VistaDelTercero.CargarAsync(consulta, tercero, [.. tercero.Vinculos.Select(v => v.VinculoId)], ct);
        return APublica(cuenta, vistos);
    }

    /// <summary>
    /// La copia congelada que viaja en los eventos del vínculo (PI-11): el número de la cuenta se descifra con la clave general de la bóveda y se vuelve a cifrar con la
    /// <b>clave del grupo</b> de la empresa (un evento de un grupo no se puede leer con la clave de otro).
    /// </summary>
    private static CuentaParaDesignar ParaDesignar(CuentaBancaria c, Guid tenantId, IProtectorDeSecretos protector, IProtectorDeCamposDelGrupo campos) =>
        new(c.Id, c.Banco, c.TipoCuenta.ToString(), campos.Proteger(tenantId, protector.Desproteger(c.NumeroProtegido)), c.Ultimos4, c.Titular, c.IdentificacionTitular, c.Moneda, c.BancoCodigo, c.BancoSwift,
            c.ContactoPagos);

    private static ContactoDePagosDto? ADto(ContactoDePagos? c) => c is null ? null : new ContactoDePagosDto(c.Nombre, c.Correo, c.Telefono);

    private static CuentaBancariaPublicaDto APublica(CuentaBancaria c, IReadOnlyList<VinculoVisto> vistos) =>
        new(c.Id, c.Banco, c.TipoCuenta.ToString(), c.NumeroEnmascarado, c.Titular, c.IdentificacionTitular, c.Moneda, c.Estado.ToString(),
            c.RegistradaEn,
            [
                .. vistos.SelectMany(v => new[] { v.Vinculo.CuentaDesignada, v.Vinculo.CuentaPropuesta, v.Vinculo.CuentaRechazada }
                    .Where(d => d?.Cuenta.CuentaId == c.Id)
                    .Select(d => new DesignacionDeCuentaDto(v.Vinculo.Id, v.Vinculo.EmpresaId, v.Empresa?.Nombre ?? string.Empty, d!.Estado.ToString(),
                        d.MotivoRechazo)))
            ], c.BancoCodigo, c.BancoSwift, ADto(c.ContactoPagos));
}

/// <summary>La empresa ve y decide la cuenta designada de un vínculo (CB-2).</summary>
public static class CuentaDesignadaEndpoints
{
    [WolverineGet("/api/v1/vinculos/{id}/cuenta-designada")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<CuentaDesignadaDelVinculoDto> Obtener(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IProtectorDeCamposDelGrupo campos, CancellationToken ct) =>
        await DtoAsync(empresa, await empresa.CargarVinculoAsync(id, ct), contexto, campos, ct);

    /// <summary>Aprueba la cuenta propuesta (una persona con <c>cuentas:aprobar</c>); con la segunda persona queda designada.</summary>
    [WolverinePost("/api/v1/vinculos/{id}/cuenta-designada/aprobar")]
    [Authorize(Policy = Permisos.CuentasAprobar)]
    public static async Task<CuentaDesignadaDelVinculoDto> Aprobar(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IProtectorDeCamposDelGrupo campos, TimeProvider reloj, CancellationToken ct)
    {
        var usuario = UsuarioRequerido(contexto);
        var stream = await empresa.VinculoParaEscribirAsync(id, null, ct);
        stream.AppendMany(stream.Aggregate!.AprobarCuenta(usuario, reloj.GetUtcNow()));
        await empresa.Sesion.SaveChangesAsync(ct);
        return await DtoAsync(empresa, await empresa.CargarVinculoAsync(id, ct), contexto, campos, ct);
    }

    [WolverinePost("/api/v1/vinculos/{id}/cuenta-designada/rechazar")]
    [Authorize(Policy = Permisos.CuentasAprobar)]
    public static async Task<CuentaDesignadaDelVinculoDto> Rechazar(Guid id, MotivoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IProtectorDeCamposDelGrupo campos, TimeProvider reloj, CancellationToken ct)
    {
        var usuario = UsuarioRequerido(contexto);
        var stream = await empresa.VinculoParaEscribirAsync(id, null, ct);
        stream.AppendOne(stream.Aggregate!.RechazarCuenta(usuario, dto.Motivo, reloj.GetUtcNow()));
        await empresa.Sesion.SaveChangesAsync(ct);
        return await DtoAsync(empresa, await empresa.CargarVinculoAsync(id, ct), contexto, campos, ct);
    }

    /// <summary>Las aprobaciones son de personas: una integración sin identidad delegada no aprueba cuentas.</summary>
    private static string UsuarioRequerido(IContextoSolicitud contexto) =>
        contexto.ActorTipo == ActorTipo.Usuario && Guid.TryParse(contexto.ActorId, out var usuario)
            ? usuario.ToString()
            : throw ReglaDeNegocioException.Validacion("usuario-delegado-requerido",
                "Las cuentas las aprueba una persona: actúa con la identidad delegada (X-Usuario-Delegado) o con su sesión.");

    internal static async Task<CuentaDesignadaDelVinculoDto> DtoAsync(SesionDeEmpresa empresa, Vinculo vinculo, IContextoSolicitud contexto,
        IProtectorDeCamposDelGrupo campos, CancellationToken ct)
    {
        var puedeAprobar = contexto.Scopes.Contains(Permisos.CuentasAprobar);
        var ids = new[] { vinculo.CuentaDesignada, vinculo.CuentaPropuesta }
            .SelectMany(c => c?.Aprobaciones ?? [])
            .Select(a => Guid.TryParse(a.UsuarioId, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .Distinct()
            .ToArray();
        var nombres = ids.Length == 0
            ? new Dictionary<string, string>()
            : (await empresa.Sesion.LoadManyAsync<Usuario>(ct, ids)).ToDictionary(u => u.Id.ToString(), u => u.Nombre);

        CuentaDesignadaDto? Dto(CuentaDesignada? c) => c is null
            ? null
            : new CuentaDesignadaDto(c.Cuenta.CuentaId, c.Cuenta.Banco, c.Cuenta.TipoCuenta, CuentaBancaria.Enmascarar(c.Cuenta.Ultimos4),
                puedeAprobar ? campos.Desproteger(contexto.TenantRequerido(), c.Cuenta.NumeroProtegido) : null, c.Cuenta.Titular, c.Cuenta.IdentificacionTitular,
                c.Cuenta.Moneda, c.Estado.ToString(), c.PropuestaEn,
                [.. c.Aprobaciones.Select(a => new AprobacionDeCuentaDto(a.UsuarioId, nombres.GetValueOrDefault(a.UsuarioId), a.Fecha))],
                c.AprobadaEn, c.MotivoRechazo, c.Cuenta.BancoCodigo, c.Cuenta.BancoSwift,
                c.Cuenta.ContactoPagos is null ? null : new ContactoDePagosDto(c.Cuenta.ContactoPagos.Nombre, c.Cuenta.ContactoPagos.Correo, c.Cuenta.ContactoPagos.Telefono));

        var usuario = contexto.ActorTipo == ActorTipo.Usuario ? contexto.ActorId : null;
        var puedes = puedeAprobar && usuario is not null && vinculo.CuentaPropuesta is { } propuesta &&
                     propuesta.Aprobaciones.All(a => !string.Equals(a.UsuarioId, usuario, StringComparison.OrdinalIgnoreCase));
        return new CuentaDesignadaDelVinculoDto(Dto(vinculo.CuentaDesignada), Dto(vinculo.CuentaPropuesta), Dto(vinculo.CuentaRechazada),
            Vinculo.AprobadoresDeCuenta, puedes);
    }
}
