namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Cifrado a nivel de campo con la <b>clave del grupo</b> (PI-11): el número de cuenta que queda congelado en los eventos de un vínculo se protege con una clave derivada de la
/// maestra y del grupo (<c>vg1.{kid}.…</c>, datos asociados con el grupo), así una copia de un evento de un grupo no se puede leer ni mover a otro, y rotar la clave maestra
/// se resuelve re-cifrando con <c>POST /plataforma/secretos/recifrar</c>. Lo cifrado antes con la clave general de la plataforma (<c>vs1.</c>) o con el formato heredado se sigue
/// leyendo: <see cref="Desproteger"/> acepta los tres.
/// </summary>
public interface IProtectorDeCamposDelGrupo
{
    /// <summary>Cifra <paramref name="secreto"/> con la clave maestra vigente derivada para el grupo.</summary>
    string Proteger(Guid tenantId, string secreto);

    /// <summary>Descifra un valor del grupo (<c>vg1.</c>) o, si es anterior, uno protegido con la clave general (<c>vs1.</c> o heredado).</summary>
    string Desproteger(Guid tenantId, string protegido);

    /// <summary>El valor ya está cifrado con la clave del grupo y la maestra vigente.</summary>
    bool EstaVigente(Guid tenantId, string protegido);
}

/// <summary>Cuántos valores hay y cómo están: <see cref="Vigentes"/> ya con la clave del grupo, <see cref="Recifrados"/> en esta pasada, <see cref="Fallidos"/> ilegibles (se dejan igual).</summary>
public sealed record ConteoDeRecifrado(int Total, int Vigentes, int Recifrados, int Fallidos);

/// <summary>
/// Pasa a la clave del grupo el número de cuenta de las copias congeladas en los eventos de un vínculo (<c>CuentaDesignadaPropuesta</c>, <c>CuentaDesignadaAprobada</c>,
/// <c>CuentaDesignadaRechazada</c>) y en el documento del vínculo que los proyecta (PI-11). Idempotente. Como los eventos son inmutables, esto es una <b>migración
/// controlada</b> del valor protegido (no del hecho): cambia solo el texto cifrado de ese campo, no su significado ni el orden ni la versión del flujo.
/// </summary>
public interface IRecifradorDeCuentasDeVinculos
{
    /// <summary>Recorre un grupo. Con <paramref name="simular"/> solo cuenta. Devuelve el conteo de los eventos y el de los documentos de vínculo.</summary>
    Task<(ConteoDeRecifrado Eventos, ConteoDeRecifrado Vinculos)> RecorrerAsync(Guid tenantId, bool simular, CancellationToken ct);
}
