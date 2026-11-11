using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>Reclamos de las sesiones de ingreso directo (los mismos nombres los lee <c>packages/api-client</c>).</summary>
public static class ReclamosDeIngreso
{
    /// <summary>Familia de la sesión directa (<c>SesionDeUsuario.Id</c>): permite cerrarla y revocarla sin esperar al vencimiento del token.</summary>
    public const string SesionId = "validasoft:sesion-id";

    /// <summary>Cómo se autenticó: <c>Codigo</c>, <c>Passkey</c> o <c>CodigoYPasskey</c> (<see cref="MetodoDeIngreso"/>).</summary>
    public const string Metodo = "validasoft:metodo";
}

/// <summary>
/// Validación de las sesiones directas en cada petición (reclamo <c>validasoft:sesion-id</c>): cerrar o revocar una sesión corta su token de
/// acceso casi de inmediato. La verificación se guarda unos segundos en memoria; al cerrar o revocar se olvida en este nodo (en los demás, al vencer).
/// </summary>
public interface ICacheDeSesionesDirectas
{
    /// <summary>Si la sesión directa sigue vigente (no se cerró, no se revocó ni venció): la respuesta se guarda unos segundos en memoria.</summary>
    Task<bool> VigenteAsync(Guid sesionId, CancellationToken ct);

    void Olvidar(Guid sesionId);
}

/// <summary>Lo que el servidor sabe de quien hace la petición HTTP: IP, navegador y correlación.</summary>
public sealed record ContextoDeCliente(string? Ip, string? Dispositivo, string CorrelationId)
{
    public static ContextoDeCliente De(HttpContext http, string correlationId) =>
        new(http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString(), correlationId);
}

/// <summary>
/// Política de ingreso de un grupo (modos, duración e inactividad de la sesión directa, passkey para todos) con una caché corta en
/// memoria. Los dos modos se verifican aquí: la identidad delegada (<c>Delegado</c>) al autenticar, el ingreso directo (<c>Directo</c>) al
/// pedir el código, al renovar la sesión y al validar cada token. Cambiar la política la invalida en este nodo y se avisa a los demás (PI-8).
/// </summary>
public sealed class PoliticaDeIngreso(IDocumentStore store, IMemoryCache cache, IBusDeInvalidaciones? bus = null) : IReceptorDeInvalidaciones
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(30);

    private static string Clave(Guid tenantId) => "ingreso:tenant:" + tenantId.ToString("N");

    public async Task<Tenant?> TenantAsync(Guid tenantId, CancellationToken ct)
    {
        if (cache.TryGetValue(Clave(tenantId), out Tenant? guardado))
            return guardado;

        await using var consulta = store.QuerySession();
        var tenant = await consulta.LoadAsync<Tenant>(tenantId, ct);
        cache.Set(Clave(tenantId), tenant, Vigencia);
        return tenant;
    }

    /// <summary>El grupo admite ese modo; si no, 403 <c>modo-de-ingreso-no-admitido</c>.</summary>
    public async Task AsegurarModoAsync(Guid tenantId, ModoDeIngreso modo, CancellationToken ct)
    {
        var tenant = await TenantAsync(tenantId, ct);
        if (tenant is null || !tenant.Activo || !tenant.ModosDeIngreso.HasFlag(modo))
            throw ReglaDeNegocioException.Prohibido("modo-de-ingreso-no-admitido",
                modo == ModoDeIngreso.Delegado
                    ? "Este grupo no admite el ingreso por identidad delegada del sistema integrado: sus personas entran directo a Validasoft."
                    : "Este grupo no admite el ingreso directo a Validasoft: sus personas entran desde el sistema integrado.");
    }

    public string Tipo => TiposDeInvalidacion.PoliticaDeIngreso;

    public void Olvidar(Guid tenantId)
    {
        cache.Remove(Clave(tenantId));
        bus?.Publicar(Tipo, tenantId.ToString());
    }

    public void Recibir(string id)
    {
        if (Guid.TryParse(id, out var tenantId))
            cache.Remove(Clave(tenantId));
    }

    /// <summary>Se perdió la escucha de avisos: la política guardada vence sola en 30 segundos.</summary>
    public void Reiniciar()
    {
    }
}

/// <summary>Una cuenta con la que se puede entrar: su documento, su grupo y lo que se le exige.</summary>
public sealed record CuentaResuelta(
    CuentaDeIngreso Cuenta, string Nombre, string Correo, string? Grupo, bool ExigePasskey, Usuario? Usuario, OperadorDePlataforma? Operador, Tenant? Tenant)
{
    /// <summary>Identificador estable que viaja al navegador: <c>u:{grupo}:{usuario}</c> u <c>o:{operador}</c>.</summary>
    public string Id => Identificador(Cuenta);

    public static string Identificador(CuentaDeIngreso cuenta) =>
        cuenta.Tipo == TipoDePropietario.Usuario ? $"u:{cuenta.TenantId:N}:{cuenta.SujetoId}" : $"o:{cuenta.SujetoId}";

    public CuentaDeIngresoDto ADto(bool tienePasskey) =>
        new(Id, Cuenta.Tipo.ToString(), Nombre, Grupo, ExigePasskey, tienePasskey);
}

/// <summary>
/// Busca las cuentas de una persona por su correo en todos los grupos que admiten el ingreso directo y entre los operadores de
/// Plataforma de ingreso directo, y las vuelve a cargar con su estado al día (activa, grupo que admite el modo, roles que exigen
/// passkey). Un operador de Plataforma siempre exige passkey (ING-4).
/// </summary>
public sealed class CuentasDeIngreso(IDocumentStore store)
{
    public async Task<IReadOnlyList<CuentaResuelta>> PorCorreoAsync(string correo, CancellationToken ct)
    {
        var cuentas = new List<CuentaResuelta>();
        await using var consulta = store.QuerySession();

        var usuarios = await consulta.Query<Usuario>().Where(u => u.AnyTenant() && u.Correo == correo && u.Activo && u.TenantId != null).ToListAsync(ct);
        if (usuarios.Count > 0)
        {
            var ids = usuarios.Select(u => u.TenantId!.Value).Distinct().ToArray();
            var tenants = (await consulta.LoadManyAsync<Tenant>(ct, ids)).ToDictionary(t => t.Id);
            cuentas.AddRange(usuarios
                .Where(u => tenants.TryGetValue(u.TenantId!.Value, out var t) && t.Activo && t.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo))
                .Select(u => DeUsuario(u, tenants[u.TenantId!.Value]))
                .OrderBy(c => c.Grupo, StringComparer.CurrentCultureIgnoreCase));
        }

        var operador = await consulta.LoadAsync<OperadorDePlataforma>(OperadorDePlataforma.Clave(OperadorDePlataforma.SistemaDirecto, correo), ct);
        if (operador is { Activo: true })
            cuentas.Add(DeOperador(operador));
        return cuentas;
    }

    /// <summary>La cuenta con su estado actual, o nulo si ya no puede entrar (desactivada, otro grupo, modo apagado).</summary>
    public async Task<CuentaResuelta?> CargarAsync(CuentaDeIngreso cuenta, CancellationToken ct)
    {
        if (cuenta.Tipo == TipoDePropietario.Operador)
        {
            await using var global = store.QuerySession();
            var operador = await global.LoadAsync<OperadorDePlataforma>(cuenta.SujetoId, ct);
            return operador is { Activo: true, EsDirecto: true } ? DeOperador(operador) : null;
        }

        if (cuenta.TenantId is not { } tenantId || !Guid.TryParse(cuenta.SujetoId, out var usuarioId))
            return null;

        await using var consultaGrupo = store.QuerySession();
        var tenant = await consultaGrupo.LoadAsync<Tenant>(tenantId, ct);
        if (tenant is not { Activo: true } || !tenant.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo))
            return null;

        await using var deTenant = store.QuerySession(tenantId.ToString("N"));
        var usuario = await deTenant.LoadAsync<Usuario>(usuarioId, ct);
        return usuario is { Activo: true } ? DeUsuario(usuario, tenant) : null;
    }

    /// <summary>Passkeys activas de la cuenta.</summary>
    public async Task<IReadOnlyList<Passkey>> PasskeysAsync(CuentaDeIngreso cuenta, CancellationToken ct)
    {
        await using var consulta = store.QuerySession();
        var sujeto = cuenta.SujetoId;
        var tipo = cuenta.Tipo;
        return await consulta.Query<Passkey>().Where(p => p.Propietario == tipo && p.SujetoId == sujeto && p.RevocadaEn == null).OrderBy(p => p.CreadaEn).ToListAsync(ct);
    }

    private static CuentaResuelta DeUsuario(Usuario u, Tenant t) =>
        new(new CuentaDeIngreso(TipoDePropietario.Usuario, t.Id, u.Id.ToString()), u.Nombre, u.Correo, t.Nombre, u.ExigePasskey(t.PasskeyParaTodos), u, null, t);

    private static CuentaResuelta DeOperador(OperadorDePlataforma o) =>
        new(new CuentaDeIngreso(TipoDePropietario.Operador, null, o.Id), o.Nombre, o.Correo, null, true, null, o, null);
}

/// <summary>Auditoría de identidad (ING-6): cada ingreso, passkey y cierre de sesión, con su dispositivo y su IP.</summary>
public sealed class RegistroDeIngresos(IDocumentStore store, TimeProvider reloj)
{
    /// <summary>Agrega el registro a una sesión que ya se va a guardar (misma transacción que el cambio).</summary>
    public static void Agregar(IDocumentSession sesion, RegistroDeIngreso registro) => sesion.Store(registro);

    public static RegistroDeIngreso Crear(EventoDeIngreso evento, bool exitoso, ContextoDeCliente? cliente, DateTimeOffset fecha, Guid? tenantId = null,
        TipoDePropietario? propietario = null, string? sujetoId = null, string? correo = null, MetodoDeIngreso? metodo = null, string? motivo = null,
        ActorTipo? actorTipo = null, string? actorId = null, Guid? sesionId = null, Guid? passkeyId = null, string? detalle = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Fecha = fecha,
        Evento = evento,
        Exitoso = exitoso,
        TenantId = tenantId,
        Propietario = propietario,
        SujetoId = sujetoId,
        Correo = correo,
        Metodo = metodo,
        Motivo = motivo,
        Ip = cliente?.Ip,
        Dispositivo = DesafioDeIngreso.Recortar(cliente?.Dispositivo, 300),
        ActorTipo = actorTipo ?? (sujetoId is null ? ActorTipo.Sistema : propietario == TipoDePropietario.Operador ? ActorTipo.Plataforma : ActorTipo.Usuario),
        ActorId = actorId ?? sujetoId ?? "anonimo",
        SesionId = sesionId,
        PasskeyId = passkeyId,
        CorrelationId = cliente?.CorrelationId,
        Detalle = detalle
    };

    /// <summary>Registra en su propia transacción (los fallos de un ingreso se guardan aunque la petición termine en error).</summary>
    public async Task RegistrarAsync(EventoDeIngreso evento, bool exitoso, ContextoDeCliente? cliente, Guid? tenantId = null,
        TipoDePropietario? propietario = null, string? sujetoId = null, string? correo = null, MetodoDeIngreso? metodo = null, string? motivo = null,
        ActorTipo? actorTipo = null, string? actorId = null, Guid? sesionId = null, Guid? passkeyId = null, string? detalle = null, CancellationToken ct = default)
    {
        await using var sesion = store.LightweightSession();
        sesion.Store(Crear(evento, exitoso, cliente, reloj.GetUtcNow(), tenantId, propietario, sujetoId, correo, metodo, motivo, actorTipo, actorId, sesionId,
            passkeyId, detalle));
        await sesion.SaveChangesAsync(ct);
    }
}

/// <summary>Traducción de los documentos de ingreso a DTO.</summary>
public static class MapeosDeIngreso
{
    public static PasskeyDto ADto(this Passkey p) =>
        new(p.Id, p.Nombre, p.CreadaEn, p.UltimoUso, p.Sincronizada, p.Transportes, p.Activa, p.RevocadaEn, p.RevocadaPor, p.MotivoRevocacion);

    public static SesionDeUsuarioDto ADto(this SesionDeUsuario s, Guid? actual, DateTimeOffset ahora) =>
        new(s.Id, s.Metodo.ToString(), s.CreadaEn, s.UltimaActividad, s.ExpiraEn, s.UltimaIp ?? s.Ip, s.Dispositivo, s.Id == actual, s.VigenteEn(ahora), s.RevocadaEn,
            s.MotivoRevocacion);

    public static IngresoAuditadoDto ADto(this RegistroDeIngreso r) =>
        new(r.Id, r.Fecha, r.Evento.ToString(), r.Exitoso, r.Correo, r.Metodo?.ToString(), r.Motivo, r.Ip, r.Dispositivo, r.ActorTipo.ToString(), r.ActorId,
            r.SesionId, r.PasskeyId);

    public static JsonElement AJson(string json) => JsonDocument.Parse(json).RootElement.Clone();

    public static OperadorDePlataformaDto ADto(this OperadorDePlataforma o, int passkeys) =>
        new(o.Id, o.Sistema, o.Nombre, o.Correo, o.Activo, o.EsDirecto, o.CreadoEn, o.UltimoAcceso, passkeys);
}
