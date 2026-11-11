namespace Validasoft.Dominio.Vinculos;

// Vigencia periódica del vínculo completo (VV-1): hechos del stream Vinculo-{id}. El vínculo vence a los N días de su última
// aprobación (los N y los avisos los fija el flujo con el que se aprobó) y las secciones de la ficha pueden tener su propia
// vigencia. Quién y desde dónde (el sistema, el job de vigencias) viaja en los metadatos del evento.

/// <summary>Por qué venció un vínculo: un documento obligatorio (RN-1) o su vigencia periódica / la de una sección (VV-1).</summary>
public enum CausaDeVencimiento
{
    Documento,
    Vigencia
}

/// <summary>
/// Al aprobarse un expediente quedó fijada la vigencia del vínculo: <see cref="VigenteHasta"/> (inclusive; nula = sin
/// vencimiento periódico) con los <see cref="DiasAviso"/> y la <see cref="DiasGracia"/> del flujo. Reinicia los avisos.
/// </summary>
public sealed record VigenciaDelVinculoFijada(DateOnly? VigenteHasta, IReadOnlyList<int> DiasAviso, int DiasGracia, DateTimeOffset Fecha);

/// <summary>
/// Llegó un día de aviso: el vínculo <c>Activo</c> pasa a <c>PorVencer</c> (los avisos siguientes lo mantienen así).
/// <see cref="AvisosCumplidos"/> son los días de aviso que ya pasaron con este evento (puede haber varios si el job se atrasó).
/// </summary>
public sealed record VinculoPorVencer(DateOnly VigenteHasta, int DiasRestantes, IReadOnlyList<int> AvisosCumplidos, DateTimeOffset Fecha);
