using System.Security.Cryptography;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Plataforma;

/// <summary>Credencial de arranque de Plataforma (<c>Validasoft:Plataforma:ClaveBootstrap</c>) como actor.</summary>
public static class ActoresDePlataforma
{
    public const string Bootstrap = "plataforma-bootstrap";

    /// <summary>Cabecera de la aserción de identidad delegada (ADR-0011).</summary>
    public const string CabeceraAsercion = "X-Usuario-Delegado";
}

/// <summary>
/// Sistemas del backoffice de Plataforma y sesiones de sus operadores (PT-4): un operador entra a la consola por identidad
/// delegada, como las personas de un sistema integrado, pero sin tenant.
/// </summary>
public static class OperadoresDePlataformaEndpoints
{
    public const string EntidadSistema = "SistemaDePlataforma";

    /// <summary>
    /// Crea o reemplaza el sistema <paramref name="codigo"/> (nombre, clave pública PEM y orígenes que alojan la consola). Reemplazar
    /// la clave invalida al instante las aserciones firmadas con la anterior. 422 <c>codigo-invalido</c>,
    /// <c>clave-publica-invalida</c>, <c>origen-invalido</c>.
    /// </summary>
    [WolverinePut("/api/v1/plataforma/sistemas/{codigo}")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SistemaDePlataformaDto> Fijar(string codigo, FijarSistemaDePlataformaDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        OrigenesDeEmbebido embebido, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var existente = await sesion.LoadAsync<SistemaDePlataforma>((codigo ?? string.Empty).Trim().ToUpperInvariant(), ct);
        var antes = existente?.ADto();
        var sistema = SistemaDePlataforma.Fijar(existente, codigo ?? string.Empty, dto.Nombre, dto.ClavePublicaPem, dto.OrigenesPermitidos,
            embebido.PermiteHttpLocal, ahora);

        sesion.Store(sistema);
        Auditoria.Registrar(sesion, contexto, EntidadSistema, sistema.Id, existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado,
            antes, sistema.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);
        embebido.Invalidar();
        return sistema.ADto();
    }

    [WolverineGet("/api/v1/plataforma/sistemas")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IReadOnlyList<SistemaDePlataformaDto>> Listar(ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var sistemas = await consulta.Query<SistemaDePlataforma>().OrderBy(s => s.Id).ToListAsync(ct);
        return [.. sistemas.Select(s => s.ADto())];
    }

    /// <summary>
    /// Canjea, servidor a servidor, la credencial de Plataforma (<c>X-Api-Key</c> con la clave de arranque) y la aserción
    /// <c>X-Usuario-Delegado</c> de un operador —firmada por un sistema de Plataforma activo, <c>iss</c> = su código— por una
    /// sesión de 30 minutos para la consola (sin tenant, solo <c>plataforma:tenants</c>). El operador queda registrado para la
    /// auditoría. 403 <c>credencial-de-plataforma-requerida</c> con otra credencial (una sesión no se renueva a sí misma);
    /// 422 <c>usuario-delegado-requerido</c>; 401 <c>asercion-*</c>, <c>sistema-inactivo</c>, <c>operador-inactivo</c>.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/sesiones")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SesionUsuarioDto> CrearSesion(HttpContext http, IContextoSolicitud contexto, IVerificadorDeOperadores verificador,
        IEmisorDeSesiones emisor, TimeProvider reloj, CancellationToken ct)
    {
        if (contexto.ActorId != ActoresDePlataforma.Bootstrap)
            throw ReglaDeNegocioException.Prohibido("credencial-de-plataforma-requerida",
                "La sesión de Plataforma se abre con la credencial de Plataforma (X-Api-Key), no con otra sesión.");

        var asercion = http.Request.Headers[ActoresDePlataforma.CabeceraAsercion].ToString().Trim();
        if (asercion.Length == 0)
            throw ReglaDeNegocioException.Validacion("usuario-delegado-requerido",
                "Envía la aserción X-Usuario-Delegado del operador, firmada por un sistema de Plataforma.");

        var operador = await verificador.VerificarAsync(asercion, contexto.CorrelationId, ct);
        return emisor.EmitirPlataforma(operador, reloj.GetUtcNow());
    }

    public static SistemaDePlataformaDto ADto(this SistemaDePlataforma s) =>
        new(s.Codigo, s.Nombre, s.ClavePublicaPem, s.OrigenesPermitidos, s.Activo, s.CreadoEn, s.ActualizadoEn);
}

/// <summary>
/// Secretos en reposo y su clave maestra (PT-3): cuántos hay de cada tipo con la clave vigente y re-cifrado con ella de los
/// que quedaron con una anterior o con el formato heredado (derivado de la clave de sesiones). El valor de un secreto nunca
/// sale de aquí ni va a la auditoría.
/// </summary>
public static class SecretosEndpoints
{
    public const string TipoLlaveOpenAi = "llave-openai";
    public const string TipoSecretoWebhook = "secreto-webhook";
    public const string TipoCuentaBancaria = "cuenta-bancaria";

    /// <summary>
    /// Usuario y clave de los proveedores de listas: los de cada empresa (Stradata, Compliance) cifrados con la clave del grupo (CU-20) y la cuenta
    /// de la plataforma (Infolaft, G-2) con la clave maestra.
    /// </summary>
    public const string TipoCredencialDeListas = "credenciales-listas";

    /// <summary>El número de cuenta congelado en los eventos de un vínculo (PI-11): se re-cifra con la clave del grupo.</summary>
    public const string TipoCuentaEnEventos = "cuenta-en-eventos";

    /// <summary>El mismo número en el documento del vínculo que proyecta esos eventos (PI-11).</summary>
    public const string TipoCuentaEnVinculos = "cuenta-en-vinculos";

    /// <summary>Solo cuenta (no re-cifra): clave vigente, claves anteriores y, por tipo, cuántos secretos ya usan la vigente.</summary>
    [WolverineGet("/api/v1/plataforma/secretos")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static Task<EstadoDeSecretosDto> Estado(ISesiones sesiones, IProtectorDeSecretos protector, IRecifradorDeCuentasDeVinculos recifrador, CancellationToken ct) =>
        RecorrerAsync(sesiones, protector, recifrador, simular: true, ct);

    /// <summary>
    /// Re-cifra con la clave maestra vigente las llaves de OpenAI de los planes de IA, los secretos de los webhooks (vigente y
    /// anterior en solapamiento) y los números de las cuentas bancarias de la bóveda. Idempotente: lo que ya está con la vigente
    /// no se toca. Lo que no se puede descifrar (su clave ya no está configurada) se cuenta en <c>fallidos</c> y se deja igual.
    /// El número de cuenta congelado en los eventos de un vínculo (y en el documento que los proyecta) también pasa a la clave del grupo (PI-11): migración controlada del
    /// valor protegido, idempotente.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/secretos/recifrar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<EstadoDeSecretosDto> Recifrar(ISesiones sesiones, IProtectorDeSecretos protector, IRecifradorDeCuentasDeVinculos recifrador, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var resultado = await RecorrerAsync(sesiones, protector, recifrador, simular: false, ct);
        await using var sesion = sesiones.Global();
        Auditoria.Registrar(sesion, contexto, "Secretos", "clave-maestra", AccionConfiguracion.Rotado, null, resultado, reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return resultado;
    }

    private sealed class Conteo(string tipo)
    {
        public int Total, Vigentes, Recifrados, Fallidos;

        /// <summary>Devuelve el texto re-cifrado, o nulo si no cambia (ya vigente, simulación o ilegible).</summary>
        public string? Procesar(string? protegido, IProtectorDeSecretos protector, bool simular, Guid? grupoId = null)
        {
            if (string.IsNullOrEmpty(protegido))
                return null;
            Total++;
            // Con grupo: vigente solo si está cifrado con la clave derivada del grupo (lo guardado con la de la instalación migra).
            if (grupoId is null ? protector.EstaConClaveActual(protegido) : protector.EstaCifradoParaGrupoConClaveActual(protegido))
            {
                Vigentes++;
                return null;
            }

            try
            {
                var nuevo = grupoId is { } grupo ? protector.RecifrarDeGrupo(grupo, protegido) : protector.Recifrar(protegido);
                if (simular)
                    return null;
                Recifrados++;
                return nuevo;
            }
            catch (CryptographicException)
            {
                Fallidos++;
                return null;
            }
        }

        public SecretosPorTipoDto ADto() => new(tipo, Total, Vigentes, Recifrados, Fallidos);
    }

    private static async Task<EstadoDeSecretosDto> RecorrerAsync(ISesiones sesiones, IProtectorDeSecretos protector, IRecifradorDeCuentasDeVinculos recifrador, bool simular,
        CancellationToken ct)
    {
        var llaves = new Conteo(TipoLlaveOpenAi);
        var webhooks = new Conteo(TipoSecretoWebhook);
        var cuentas = new Conteo(TipoCuentaBancaria);
        var listas = new Conteo(TipoCredencialDeListas);
        var enEventos = new ConteoDeRecifrado(0, 0, 0, 0);
        var enVinculos = new ConteoDeRecifrado(0, 0, 0, 0);

        List<Guid> tenants;
        await using (var consulta = sesiones.ConsultaGlobal())
            tenants = [.. await consulta.Query<Tenant>().Select(t => t.Id).ToListAsync(ct)];

        foreach (var tenantId in tenants)
        {
            await using var sesion = sesiones.ParaTenant(tenantId);
            var cambios = false;
            foreach (var plan in await sesion.Query<PlanDeIaEmpresa>().Where(p => p.ApiKeyOpenAiProtegida != null).ToListAsync(ct))
            {
                if (llaves.Procesar(plan.ApiKeyOpenAiProtegida, protector, simular) is not { } nuevo)
                    continue;
                plan.ApiKeyOpenAiProtegida = nuevo;
                sesion.Store(plan);
                cambios = true;
            }

            foreach (var configuracion in await sesion.Query<ListasRestrictivasEmpresa>().ToListAsync(ct))
            {
                var cambioLista = false;
                foreach (var credenciales in configuracion.Credenciales.Values)
                {
                    if (listas.Procesar(credenciales.ClaveProtegida, protector, simular, tenantId) is not { } recifrada)
                        continue;
                    credenciales.ClaveProtegida = recifrada;
                    cambioLista = true;
                }

                if (!cambioLista)
                    continue;
                sesion.Store(configuracion);
                cambios = true;
            }

            foreach (var suscripcion in await sesion.Query<SuscripcionWebhook>().ToListAsync(ct))
            {
                var vigente = webhooks.Procesar(suscripcion.SecretoProtegido, protector, simular);
                var anterior = webhooks.Procesar(suscripcion.SecretoAnteriorProtegido, protector, simular);
                if (vigente is null && anterior is null)
                    continue;
                suscripcion.SecretoProtegido = vigente ?? suscripcion.SecretoProtegido;
                suscripcion.SecretoAnteriorProtegido = anterior ?? suscripcion.SecretoAnteriorProtegido;
                sesion.Store(suscripcion);
                cambios = true;
            }

            if (cambios)
                await sesion.SaveChangesAsync(ct);

            // El número de cuenta de los eventos de un vínculo pasa de la clave general a la del grupo (PI-11).
            var (eventos, vinculos) = await recifrador.RecorrerAsync(tenantId, simular, ct);
            enEventos = Sumar(enEventos, eventos);
            enVinculos = Sumar(enVinculos, vinculos);
        }

        // La cuenta de la plataforma con los proveedores de listas (G-2) se cifra con la clave maestra, sin clave de grupo.
        await using (var global = sesiones.Global())
        {
            if (await global.LoadAsync<ListasRestrictivasDePlataforma>(ListasRestrictivasDePlataforma.IdUnico, ct) is { } cuentaDeListas)
            {
                var cambioCuenta = false;
                foreach (var credenciales in cuentaDeListas.Credenciales.Values)
                {
                    if (listas.Procesar(credenciales.ClaveProtegida, protector, simular) is not { } recifrada)
                        continue;
                    credenciales.ClaveProtegida = recifrada;
                    cambioCuenta = true;
                }

                if (cambioCuenta)
                {
                    global.Store(cuentaDeListas);
                    await global.SaveChangesAsync(ct);
                }
            }
        }

        await using (var boveda = sesiones.Boveda())
        {
            var cambios = false;
            foreach (var cuenta in await boveda.Query<CuentaBancaria>().ToListAsync(ct))
            {
                if (cuentas.Procesar(cuenta.NumeroProtegido, protector, simular) is not { } nuevo)
                    continue;
                cuenta.NumeroProtegido = nuevo;
                boveda.Store(cuenta);
                cambios = true;
            }

            if (cambios)
                await boveda.SaveChangesAsync(ct);
        }

        return new EstadoDeSecretosDto(protector.ClaveVigente, protector.ClavesAnteriores, simular,
            [llaves.ADto(), webhooks.ADto(), cuentas.ADto(), listas.ADto(), Dto(TipoCuentaEnEventos, enEventos), Dto(TipoCuentaEnVinculos, enVinculos)]);
    }

    private static ConteoDeRecifrado Sumar(ConteoDeRecifrado a, ConteoDeRecifrado b) => new(a.Total + b.Total, a.Vigentes + b.Vigentes, a.Recifrados + b.Recifrados, a.Fallidos + b.Fallidos);

    private static SecretosPorTipoDto Dto(string tipo, ConteoDeRecifrado c) => new(tipo, c.Total, c.Vigentes, c.Recifrados, c.Fallidos);
}
