namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// Última desaprobación de un expediente (DS-1, DS-2): quién, cuándo, desde qué etapa se devolvió a revisión
/// (<see cref="Etapa"/>), si fue a la fase inicial, si el expediente estaba ya aprobado y por qué.
/// </summary>
public sealed record DesaprobacionDeExpediente(int Etapa, bool FaseInicial, bool ExpedienteAprobado, string AdministradorId, string Justificacion,
    DateTimeOffset Fecha);

/// <summary>
/// Un administrador devolvió a revisión desde la etapa <see cref="Orden"/> un expediente que seguía abierto (DS-1): esa etapa
/// y todas las posteriores vuelven a <c>Esperando</c> y los veredictos vigentes de sus áreas (los de área y los de sus
/// requisitos) dejan de contar (<c>Revertido</c>: quedan en el historial). Con <see cref="FaseInicial"/> es la etapa 1: todo
/// el flujo se repite. Le sigue la <c>EtapaActivada</c> de la etapa devuelta y, si el expediente estaba devuelto al tercero,
/// la <c>RevisionReanudada</c>. Equivale a <c>DesaprobarAFlujo</c> y <c>DesaprobarAEstadoInicial</c> del SRM.
/// </summary>
public sealed record EtapaReabierta(int Orden, bool FaseInicial, string AdministradorId, string Justificacion, DateTimeOffset Fecha);

/// <summary>
/// Un administrador desaprobó un expediente ya <c>Aprobado</c> (DS-2, equivale a <c>DesaprobarTercero</c>): vuelve a
/// <c>EnRevision</c> (el mismo expediente, con su historial y su folio) y, como <see cref="EtapaReabierta"/>, devuelve a
/// revisión desde la etapa <see cref="Orden"/>. El proceso de validación lo cuenta en el vínculo (<c>VinculoDesaprobado</c>)
/// y revoca el certificado en la misma transacción.
/// </summary>
public sealed record ExpedienteDesaprobado(int Orden, bool FaseInicial, string AdministradorId, string Justificacion, DateTimeOffset Fecha);
