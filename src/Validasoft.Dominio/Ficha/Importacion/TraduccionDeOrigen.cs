using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha.Importacion;

/// <summary>
/// Algo que pasó al traducir un tercero de un sistema de origen: <b>advertencia</b> (una corrección o normalización aplicada: DV
/// recalculado, calidad sin código, persona duplicada, fecha de corte asumida) o <b>rechazo</b> (un dato que no se pudo convertir
/// y quedó fuera; va al CSV de rechazos con su acción sugerida). <see cref="Seccion"/> nula = el tercero.
/// </summary>
public sealed record AvisoDeTraduccion(string? Seccion, int? Periodo, string Campo, string Codigo, string Mensaje, string? Dato = null,
    string? AccionSugerida = null);

/// <summary>Datos tipados y normalizados de una sección (y periodo, en la financiera), listos para registrar la versión.</summary>
public sealed record SeccionTraducida(DefinicionDeSeccion Definicion, int? Periodo, object Datos);

/// <summary>Identidad del tercero en Validasoft: identificación normalizada (DV recalculado), tipo de persona y nombre.</summary>
public sealed record TerceroTraducido(Identificacion Identificacion, TipoPersona TipoPersona, string Nombre);

/// <summary>
/// Resultado de traducir un tercero de un sistema de origen a las secciones de la ficha. <see cref="Sistema"/> es el código del
/// sistema de origen (minúsculas, p. ej. el del adaptador) y <see cref="Clave"/> la clave del tercero en él; juntos dan la clave
/// del legado (<see cref="ClaveLegado"/>) y el id determinista del tercero (<see cref="IdDeLegado.Tercero"/>). Sin
/// <see cref="Tercero"/> (identificación inválida, sin información general…) el tercero entero queda rechazado y no se importa
/// nada de él.
/// </summary>
public sealed record ResultadoDeTraduccion(string Sistema, string Clave, TerceroTraducido? Tercero, IReadOnlyList<SeccionTraducida> Secciones,
    IReadOnlyList<AvisoDeTraduccion> Advertencias, IReadOnlyList<AvisoDeTraduccion> Rechazos)
{
    /// <summary><c>{sistema}:{clave}</c> (docs/08 §6.2).</summary>
    public string ClaveLegado => IdDeLegado.Clave(Sistema, Clave);
}
