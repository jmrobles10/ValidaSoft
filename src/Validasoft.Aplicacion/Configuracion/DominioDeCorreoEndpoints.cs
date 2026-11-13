using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Configuracion;

/// <summary>
/// El proveedor que registra el dominio de correo de una empresa y comprueba su DNS (OD-19): quien da los registros SPF, DKIM y DMARC
/// que la empresa debe crear y dice si ya están. En desarrollo y pruebas es el <b>simulado</b>: no consulta ningún DNS ni ningún
/// proveedor real; uno real (SES, Postmark…) se conecta implementando esta interfaz y eligiéndolo en
/// <c>Validasoft:Correo:Dominios:Proveedor</c>.
/// </summary>
public interface IProveedorDeDominiosDeCorreo
{
    /// <summary>Nombre con el que se elige en la configuración (<c>Simulado</c>).</summary>
    string Nombre { get; }

    /// <summary>No habla con ningún proveedor ni DNS reales: la verificación la decide una regla local.</summary>
    bool EsSimulado { get; }

    /// <summary>Registra el dominio y devuelve los registros DNS que la empresa debe crear (SPF, DKIM y DMARC).</summary>
    Task<IReadOnlyList<RegistroDns>> RegistrarAsync(string dominio, CancellationToken ct);

    /// <summary>Comprueba en el DNS cada uno de los registros esperados.</summary>
    Task<IReadOnlyList<ComprobacionDeRegistroDns>> VerificarAsync(string dominio, IReadOnlyList<RegistroDns> esperados, CancellationToken ct);
}

/// <summary>
/// Dominio de correo propio de cada empresa (OD-18…OD-22, docs/11 §H8). La empresa registra su dominio, crea los registros DNS que se
/// le muestran (SPF, DKIM, DMARC) y pide verificarlos; con el dominio verificado sus correos a terceros salen desde su dirección, y
/// mientras no lo esté —o si lo quita— salen con el remitente de la plataforma. <c>empresas:administrar</c>.
/// </summary>
public static class DominioDeCorreoEndpoints
{
    public const string Entidad = "DominioDeCorreo";

    /// <summary>La clave con la que el enviador recuerda 30 segundos el remitente de la empresa: al cambiar el dominio se olvida (OD-20).</summary>
    public static string LlaveDelRemitente(Guid empresaId) => "remitente-de-empresa:" + empresaId.ToString("N");

    /// <summary>La clave con la que el enviador recuerda 30 segundos el correo de soporte de la empresa (su <c>Reply-To</c>): al cambiar el contacto se olvida.</summary>
    public static string LlaveDelResponderA(Guid empresaId) => "responder-a-de-empresa:" + empresaId.ToString("N");

    /// <summary>La dirección con la que sale el correo de la plataforma (<c>Validasoft:Correo:Remitente</c>).</summary>
    public static string RemitenteDeLaPlataforma(IConfiguration configuracion) =>
        configuracion["Validasoft:Correo:Remitente"] is { Length: > 0 } remitente ? remitente : "Validasoft <no-responder@validasoft.local>";

    public static DominioDeCorreoDto ADto(Empresa empresa, DominioDeCorreoEmpresa? dominio, IProveedorDeDominiosDeCorreo proveedor, IConfiguration configuracion)
    {
        var plataforma = RemitenteDeLaPlataforma(configuracion);
        if (dominio is null)
            return new DominioDeCorreoDto(empresa.Id, false, null, DominioDeCorreoEmpresa.ParteLocalPorDefecto, null, null, [], [], null, null, plataforma, plataforma,
                proveedor.EsSimulado);

        var propio = dominio.RemitenteSiVerificado(empresa.Nombre);
        return new DominioDeCorreoDto(empresa.Id, true, dominio.Dominio, dominio.ParteLocal, dominio.NombreRemitente, dominio.Estado.ToString(),
            [.. dominio.Registros.Select(r => new RegistroDnsDto(r.Finalidad.ToString(), r.Tipo, r.Nombre, r.Valor))],
            [.. dominio.UltimaVerificacion.Select(c => new ComprobacionDeRegistroDnsDto(c.Finalidad.ToString(), c.Correcto, c.Encontrado))], dominio.VerificadoEn,
            dominio.UltimaVerificacionEn, propio is null ? plataforma : $"{propio.Nombre} <{propio.Direccion}>", plataforma, proveedor.EsSimulado);
    }

    [WolverineGet("/api/v1/empresas/{id}/dominio-correo")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<DominioDeCorreoDto> Obtener(Guid id, ISesiones sesiones, IContextoSolicitud contexto, IProveedorDeDominiosDeCorreo proveedor, IConfiguration configuracion,
        CancellationToken ct)
    {
        var empresa = await EmpresasEndpoints.CargarAsync(id, sesiones, contexto, ct);
        await using var consulta = sesiones.DelTenant();
        return ADto(empresa, await consulta.LoadAsync<DominioDeCorreoEmpresa>(id, ct), proveedor, configuracion);
    }

    /// <summary>
    /// Registra (o cambia) el dominio: queda <c>Pendiente</c> con los registros DNS que debe crear la empresa. Si el dominio es el mismo,
    /// solo cambia el nombre o la parte local y conserva su estado. 422 <c>dominio-invalido</c>, <c>parte-local-invalida</c>,
    /// <c>nombre-remitente-invalido</c>; 503 <c>proveedor-de-dominios-no-configurado</c>.
    /// </summary>
    [WolverinePut("/api/v1/empresas/{id}/dominio-correo")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<DominioDeCorreoDto> Fijar(Guid id, ActualizarDominioDeCorreoDto dto, ISesiones sesiones, IContextoSolicitud contexto, IProveedorDeDominiosDeCorreo proveedor,
        IConfiguration configuracion, IMemoryCache cache, TimeProvider reloj, CancellationToken ct)
    {
        var empresa = await EmpresasEndpoints.CargarAsync(id, sesiones, contexto, ct);
        var ahora = reloj.GetUtcNow();
        var dominio = DominioDeCorreoEmpresa.NormalizarDominio(dto.Dominio);
        await using var sesion = sesiones.DelTenant();
        var actual = await sesion.LoadAsync<DominioDeCorreoEmpresa>(id, ct);
        var registros = actual is not null && actual.Dominio == dominio ? actual.Registros : await proveedor.RegistrarAsync(dominio, ct);
        var antes = actual is null ? null : ADto(empresa, actual, proveedor, configuracion);
        var nuevo = DominioDeCorreoEmpresa.Registrar(actual, id, dominio, dto.ParteLocal, dto.NombreRemitente, registros, ahora);

        sesion.Store(nuevo);
        var despues = ADto(empresa, nuevo, proveedor, configuracion);
        Auditoria.Registrar(sesion, contexto, Entidad, id.ToString(), actual is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes, despues, ahora, id);
        await sesion.SaveChangesAsync(ct);
        cache.Remove(LlaveDelRemitente(id));
        return despues;
    }

    /// <summary>
    /// Verifica ya los registros DNS (SPF, DKIM y DMARC) contra el proveedor. Con todos correctos el dominio queda <c>Verificado</c> y los
    /// correos de la empresa salen desde él; si falta alguno queda <c>Fallido</c> con lo que se encontró y el correo sigue saliendo con el
    /// remitente de la plataforma. 409 <c>dominio-sin-registrar</c>.
    /// </summary>
    [WolverinePost("/api/v1/empresas/{id}/dominio-correo/verificar")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<DominioDeCorreoDto> Verificar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, IProveedorDeDominiosDeCorreo proveedor, IConfiguration configuracion,
        IMemoryCache cache, TimeProvider reloj, CancellationToken ct)
    {
        var empresa = await EmpresasEndpoints.CargarAsync(id, sesiones, contexto, ct);
        await using var sesion = sesiones.DelTenant();
        var dominio = await sesion.LoadAsync<DominioDeCorreoEmpresa>(id, ct)
                      ?? throw ReglaDeNegocioException.Conflicto("dominio-sin-registrar", "Registra primero el dominio de correo de la empresa.");
        var antes = ADto(empresa, dominio, proveedor, configuracion);
        var ahora = reloj.GetUtcNow();

        dominio.RegistrarVerificacion(await proveedor.VerificarAsync(dominio.Dominio, dominio.Registros, ct), ahora);
        sesion.Store(dominio);
        var despues = ADto(empresa, dominio, proveedor, configuracion);
        Auditoria.Registrar(sesion, contexto, Entidad, id.ToString(), AccionConfiguracion.Modificado, antes, despues, ahora, id);
        await sesion.SaveChangesAsync(ct);
        cache.Remove(LlaveDelRemitente(id));
        return despues;
    }

    /// <summary>Quita el dominio: los correos de la empresa vuelven a salir con el remitente de la plataforma. 404 si no tenía.</summary>
    [WolverineDelete("/api/v1/empresas/{id}/dominio-correo")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<DominioDeCorreoDto> Quitar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, IProveedorDeDominiosDeCorreo proveedor, IConfiguration configuracion,
        IMemoryCache cache, TimeProvider reloj, CancellationToken ct)
    {
        var empresa = await EmpresasEndpoints.CargarAsync(id, sesiones, contexto, ct);
        await using var sesion = sesiones.DelTenant();
        var dominio = await sesion.LoadAsync<DominioDeCorreoEmpresa>(id, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("dominio-sin-registrar", "La empresa no tiene un dominio de correo.");
        var antes = ADto(empresa, dominio, proveedor, configuracion);
        sesion.Delete(dominio);
        Auditoria.Registrar(sesion, contexto, Entidad, id.ToString(), AccionConfiguracion.Desactivado, antes, null, reloj.GetUtcNow(), id);
        await sesion.SaveChangesAsync(ct);
        cache.Remove(LlaveDelRemitente(id));
        return ADto(empresa, null, proveedor, configuracion);
    }
}
