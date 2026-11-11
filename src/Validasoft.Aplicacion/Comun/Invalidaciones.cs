namespace Validasoft.Aplicacion.Comun;

/// <summary>Qué cambió, para que cada servidor olvide lo que guardó en memoria sobre eso (PI-8).</summary>
public static class TiposDeInvalidacion
{
    /// <summary>Una API key revocada, rotada o de un sistema desactivado: el id de la llave.</summary>
    public const string ApiKey = "api-key";

    /// <summary>Una sesión directa cerrada o revocada: el id de la sesión.</summary>
    public const string SesionDirecta = "sesion-directa";

    /// <summary>La política de ingreso de un grupo (modos, sesión, SSO): el id del tenant.</summary>
    public const string PoliticaDeIngreso = "politica-ingreso";

    /// <summary>Una empresa cambiada o desactivada: su id.</summary>
    public const string Empresa = "empresa";

    /// <summary>Un sistema integrador o de Plataforma, o un operador, activado o desactivado: su clave.</summary>
    public const string Sistema = "sistema";

    /// <summary>La configuración global de Plataforma (clave de arranque): siempre <c>global</c>.</summary>
    public const string Plataforma = "plataforma";

    /// <summary>La configuración de SSO de un grupo o de sus dominios: el id del tenant (más sus dominios, separados por coma).</summary>
    public const string Sso = "sso";
}

/// <summary>
/// Avisa a todos los servidores —este incluido— de que algo cambió, sin esperar a que venza su caché (PI-8). La implementación usa
/// PostgreSQL <c>LISTEN/NOTIFY</c> (el mismo motor que ya comparten todos los servidores): publicar no puede fallar la petición; si el
/// aviso no sale, las cachés siguen venciendo por su cuenta como antes.
/// </summary>
public interface IBusDeInvalidaciones
{
    /// <summary>Envía el aviso (sin esperar). Las cachés del propio servidor ya se olvidan por su lado; esto llega a los demás.</summary>
    void Publicar(string tipo, string id);

    /// <summary>Envía el aviso y espera a que salga (pruebas y cierres ordenados).</summary>
    Task PublicarAsync(string tipo, string id, CancellationToken ct = default);
}

/// <summary>Quien guarda algo en memoria que un aviso de otro servidor debe descartar.</summary>
public interface IReceptorDeInvalidaciones
{
    /// <summary>El <see cref="TiposDeInvalidacion"/> que atiende.</summary>
    string Tipo { get; }

    /// <summary>Otro servidor avisó que cambió <paramref name="id"/>.</summary>
    void Recibir(string id);

    /// <summary>
    /// Se perdió la conexión de escucha (y quizá algún aviso): lo guardado antes de ahora ya no es de fiar. Quien no pueda descartar todo a la vez
    /// no hace nada (sus entradas vencen solas).
    /// </summary>
    void Reiniciar();
}

/// <summary>
/// Sistemas y operadores que dejan de valer al desactivarse (PI-7): las sesiones de Validasoft que ya se emitieron para ellos (JWT de la identidad
/// delegada, de la consola de Plataforma o de un embebido del tercero) se cortan en segundos, sin esperar a que venza el token. La verificación se guarda
/// unos segundos en memoria; al desactivar o reactivar se olvida en todos los servidores.
/// </summary>
public interface ICacheDeSistemas
{
    /// <summary>
    /// El sistema integrador <paramref name="codigo"/> del grupo sigue activo (y su grupo también) y no se desactivó desde que nació la sesión: <paramref name="generacion"/> es la que
    /// llevaba la sesión y debe ser la de ahora (revocar es definitivo, aunque el sistema se reactive).
    /// </summary>
    Task<bool> IntegradorActivoAsync(Guid tenantId, string codigo, int generacion, CancellationToken ct);

    /// <summary>El operador de Plataforma está activo, su sistema también (si entró por uno) y no se desactivó desde que nació la sesión (<paramref name="generacion"/>).</summary>
    Task<bool> OperadorActivoAsync(string operadorId, int generacion, CancellationToken ct);

    void OlvidarIntegrador(Guid tenantId, string codigo);

    void OlvidarOperador(string operadorId);
}

/// <summary>
/// Si la clave de arranque de Plataforma está desactivada (PI-17), con una caché corta en memoria que se descarta en todos los servidores al cambiar: desactivarla desde
/// la consola la apaga en todos a la vez, sin reiniciar nada.
/// </summary>
public interface ICacheDeClaveDeArranque
{
    Task<bool> DesactivadaAsync(CancellationToken ct);

    void Olvidar();
}
