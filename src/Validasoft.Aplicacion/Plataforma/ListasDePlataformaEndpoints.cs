using System.Security.Cryptography;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Plataforma;

/// <summary>
/// Cuenta de la plataforma con los proveedores de listas restrictivas (G-2, PP-5): <b>Infolaft es uno solo para todos los clientes</b>, con la
/// cuenta de Sinco, no uno por empresa. La configura Plataforma desde su consola; la clave es de solo escritura (vuelve «configurada · …1234») y se
/// cifra con la clave maestra (<see cref="IProtectorDeSecretos.Proteger"/>), como los demás secretos de la plataforma. Las empresas consultan con
/// ella sin verla ni cargarla; cada una conserva su consumo mensual (VE-11) para atribuirle el costo. Stradata y Compliance siguen por empresa.
/// Todo cambio queda en la auditoría, sin la clave.
/// </summary>
public static class ListasDePlataformaEndpoints
{
    public const string Entidad = "ListasRestrictivasDePlataforma";

    /// <summary>Cuentas de la plataforma, si la instalación habilitó cada proveedor y cuántas empresas aún guardan credenciales propias de antes (migración).</summary>
    [WolverineGet("/api/v1/plataforma/listas-restrictivas")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ListasDePlataformaDto> Obtener(ISesiones sesiones, ConsultaDeListas consulta, CancellationToken ct)
    {
        await using var global = sesiones.ConsultaGlobal();
        var cuenta = await global.LoadAsync<ListasRestrictivasDePlataforma>(ListasRestrictivasDePlataforma.IdUnico, ct);
        return await DtoAsync(sesiones, cuenta, consulta, ct);
    }

    /// <summary>
    /// Crea, cambia o quita (<c>clave: ""</c>) la cuenta de Infolaft de la plataforma: usuario, clave, URL del contrato, plan, la cuota mensual de
    /// <b>cada empresa</b> y el modo de búsqueda. Exige confirmar con la passkey (la clave es de todos los clientes). 422 <c>proveedor-invalido</c>
    /// (no es de la plataforma), <c>usuario-requerido</c>, <c>clave-requerida</c>, <c>clave-invalida</c>, <c>url-invalida</c>, <c>cuota-invalida</c>,
    /// <c>modo-busqueda-invalido</c>.
    /// </summary>
    [WolverinePut("/api/v1/plataforma/listas-restrictivas")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ListasDePlataformaDto> Actualizar(ActualizarListasDePlataformaDto dto, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        ConsultaDeListas consulta, IProtectorDeSecretos protector, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "configurar-listas-restrictivas", ct);
        var ahora = reloj.GetUtcNow();
        var c = dto.Credenciales;
        ModoBusquedaListas? modo = string.IsNullOrWhiteSpace(c.ModoBusqueda) ? null
            : Enum.TryParse<ModoBusquedaListas>(c.ModoBusqueda, true, out var m) && Enum.IsDefined(m) ? m
            : throw ReglaDeNegocioException.Validacion("modo-busqueda-invalido", "El modo de búsqueda es PorNombre o PorDocumento.");

        await using var sesion = sesiones.Global();
        var existente = await sesion.LoadAsync<ListasRestrictivasDePlataforma>(ListasRestrictivasDePlataforma.IdUnico, ct);
        var antes = existente is null ? null : await DtoAsync(sesiones, existente, consulta, ct);
        var cuenta = existente ?? new ListasRestrictivasDePlataforma();
        cuenta.CambiarCredenciales(new CambioDeCredencialesDeListas(c.Proveedor, c.Usuario, c.Clave, c.UrlBase, c.Plan, c.CuotaMensual, modo), protector.Proteger,
            contexto.ActorId, ahora);

        sesion.Store(cuenta);
        var despues = await DtoAsync(sesiones, cuenta, consulta, ct);
        Auditoria.Registrar(sesion, contexto, Entidad, ListasRestrictivasDePlataforma.IdUnico, existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado,
            antes, despues, ahora);
        await sesion.SaveChangesAsync(ct);
        return despues;
    }

    /// <summary>
    /// Migración de las credenciales que las empresas tenían de Infolaft antes de G-2: la <b>primera válida</b> (la clave se descifra con la de su
    /// grupo y se vuelve a cifrar con la maestra) pasa a ser la cuenta de la plataforma, si esta aún no tiene una; después se quitan las credenciales
    /// propias de Infolaft de todas las empresas. Si ninguna se puede leer o la plataforma no quedó configurada, no se quita nada. Idempotente:
    /// repetirla no cambia nada. La cuota por empresa no se hereda (la fija Plataforma). Cada empresa queda en la auditoría, sin la clave.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/listas-restrictivas/migrar-credenciales")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResultadoMigracionListasDto> Migrar(HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, IProtectorDeSecretos protector,
        ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "migrar-credenciales-de-listas", ct);
        var ahora = reloj.GetUtcNow();
        const string proveedor = ProveedoresDeListas.Infolaft;

        var halladas = await CredencialesPropiasAsync(sesiones, proveedor, ct);
        await using var global = sesiones.Global();
        var existente = await global.LoadAsync<ListasRestrictivasDePlataforma>(ListasRestrictivasDePlataforma.IdUnico, ct);
        var cuenta = existente ?? new ListasRestrictivasDePlataforma();

        var tomada = false;
        Guid? origen = null;
        var noLegibles = 0;
        foreach (var (tenantId, empresaId, credenciales) in halladas)
        {
            if (cuenta.CredencialesDe(proveedor) is not null)
                break;
            string claro;
            try
            {
                claro = protector.DesprotegerDeGrupo(tenantId, credenciales.ClaveProtegida);
            }
            catch (CryptographicException)
            {
                noLegibles++;
                continue;
            }

            if (!cuenta.TomarCredencialesDeEmpresa(proveedor, credenciales, protector.Proteger(claro), contexto.ActorId, ahora))
                continue;
            tomada = true;
            origen = empresaId;
        }

        if (tomada)
        {
            global.Store(cuenta);
            Auditoria.Registrar(global, contexto, Entidad, ListasRestrictivasDePlataforma.IdUnico, existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado,
                null, new { Proveedor = proveedor, Migrada = true, EmpresaDeOrigen = origen, Usuario = cuenta.CredencialesDe(proveedor)!.Usuario,
                    cuenta.CredencialesDe(proveedor)!.UltimosCuatroClave }, ahora);
            await global.SaveChangesAsync(ct);
        }

        // Con la cuenta de la plataforma ya configurada se quitan las credenciales propias: una sola verdad. Sin ella, no se toca nada.
        var limpiadas = 0;
        var cuotas = 0;
        if (cuenta.CredencialesDe(proveedor) is not null)
        {
            foreach (var porTenant in halladas.GroupBy(h => h.TenantId))
            {
                await using var sesion = sesiones.ParaTenant(porTenant.Key);
                foreach (var (_, empresaId, _) in porTenant)
                {
                    if (await sesion.LoadAsync<ListasRestrictivasEmpresa>(empresaId, ct) is not { } configuracion)
                        continue;
                    var quitadas = configuracion.QuitarCredencialesDeLaPlataforma(ahora);
                    if (quitadas.Count == 0)
                        continue;
                    limpiadas++;
                    cuotas += quitadas.Values.Count(q => q.CuotaMensual is not null);
                    sesion.Store(configuracion);
                    Auditoria.Registrar(sesion, contexto, ListasRestrictivasEndpoints.Entidad, empresaId.ToString(), AccionConfiguracion.Modificado,
                        new { Proveedor = proveedor, CredencialesPropias = true }, new { Proveedor = proveedor, CredencialesPropias = false, Motivo = "Migradas a la cuenta de la plataforma (G-2)." },
                        ahora, empresaId);
                }

                await sesion.SaveChangesAsync(ct);
            }
        }

        return new ResultadoMigracionListasDto(proveedor, tomada, origen, halladas.Count, limpiadas, noLegibles, cuotas);
    }

    private static async Task<ListasDePlataformaDto> DtoAsync(ISesiones sesiones, ListasRestrictivasDePlataforma? cuenta, ConsultaDeListas consulta, CancellationToken ct)
    {
        var opciones = consulta.Opciones;
        var propias = new Dictionary<string, int>();
        foreach (var nombre in ProveedoresDeListas.DeLaPlataforma)
            propias[nombre] = (await CredencialesPropiasAsync(sesiones, nombre, ct)).Count;

        var proveedores = ProveedoresDeListas.DeLaPlataforma.Select(nombre =>
        {
            var credenciales = cuenta?.CredencialesDe(nombre);
            return new ProveedorDePlataformaDto(nombre, opciones.Proveedor(nombre).Habilitado, credenciales is not null, credenciales?.Usuario, credenciales?.UltimosCuatroClave,
                credenciales?.UrlBase, credenciales?.Plan, credenciales?.CuotaMensual, credenciales?.ModoBusqueda.ToString(), credenciales?.ActualizadasEn, propias[nombre]);
        }).ToList();
        return new ListasDePlataformaDto(proveedores, cuenta?.ActualizadaEn, cuenta?.ActualizadaPor);
    }

    /// <summary>Las empresas que guardan credenciales propias de un proveedor que ahora es de la plataforma (de antes de G-2), en el orden de los grupos.</summary>
    private static async Task<List<(Guid TenantId, Guid EmpresaId, CredencialesDeListas Credenciales)>> CredencialesPropiasAsync(ISesiones sesiones, string proveedor,
        CancellationToken ct)
    {
        List<Guid> tenants;
        await using (var consulta = sesiones.ConsultaGlobal())
            tenants = [.. await consulta.Query<Tenant>().OrderBy(t => t.Id).Select(t => t.Id).ToListAsync(ct)];

        var halladas = new List<(Guid, Guid, CredencialesDeListas)>();
        foreach (var tenantId in tenants)
        {
            await using var sesion = sesiones.ParaTenant(tenantId);
            foreach (var configuracion in await sesion.Query<ListasRestrictivasEmpresa>().ToListAsync(ct))
            {
                if (configuracion.CredencialesDe(proveedor) is { } credenciales)
                    halladas.Add((tenantId, configuracion.EmpresaId, credenciales));
            }
        }

        return halladas;
    }
}
