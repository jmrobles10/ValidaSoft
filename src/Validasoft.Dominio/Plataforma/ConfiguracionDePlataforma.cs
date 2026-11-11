using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Plataforma;

/// <summary>
/// Configuración global de la instalación que Plataforma cambia desde su consola (PI-17): hoy, si la clave de arranque (<c>Validasoft:Plataforma:ClaveBootstrap</c>)
/// sigue valiendo. Un solo documento (id <see cref="IdUnico"/>) en el esquema <c>plataforma</c>.
/// </summary>
public sealed class ConfiguracionDePlataforma
{
    public const string IdUnico = "plataforma";

    public string Id { get; set; } = IdUnico;

    /// <summary>
    /// La clave de arranque ya no autentica aunque siga en la configuración del servidor: se desactivó desde la consola por un operador con passkey. Quitarla de
    /// la configuración del servidor sigue siendo lo más seguro, pero esto la apaga en todos los servidores sin reiniciar nada.
    /// </summary>
    public bool ClaveDeArranqueDesactivada { get; set; }

    public DateTimeOffset? DesactivadaEn { get; set; }

    public string? DesactivadaPor { get; set; }

    public DateTimeOffset? ReactivadaEn { get; set; }

    public string? ReactivadaPor { get; set; }

    /// <summary>Apaga la clave de arranque. 409 <c>clave-de-arranque-desactivada</c> si ya estaba apagada.</summary>
    public void DesactivarClaveDeArranque(string actor, DateTimeOffset ahora)
    {
        if (ClaveDeArranqueDesactivada)
            throw ReglaDeNegocioException.Conflicto("clave-de-arranque-desactivada", "La clave de arranque ya está desactivada.");
        ClaveDeArranqueDesactivada = true;
        DesactivadaEn = ahora;
        DesactivadaPor = actor;
    }

    /// <summary>Vuelve a permitir la clave de arranque (si el servidor aún la tiene configurada). 409 <c>clave-de-arranque-activa</c> si ya valía.</summary>
    public void ReactivarClaveDeArranque(string actor, DateTimeOffset ahora)
    {
        if (!ClaveDeArranqueDesactivada)
            throw ReglaDeNegocioException.Conflicto("clave-de-arranque-activa", "La clave de arranque no estaba desactivada.");
        ClaveDeArranqueDesactivada = false;
        ReactivadaEn = ahora;
        ReactivadaPor = actor;
    }
}
