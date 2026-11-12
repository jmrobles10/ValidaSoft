namespace Validasoft.Contratos;

// Habeas data (Ley 1581 de 2012) y exportación de la bóveda del tercero (OD-1…OD-12, docs/07 §8).

/// <summary>
/// Solicitud del titular desde su portal. <see cref="Tipo"/>: <c>Consulta</c>, <c>Correccion</c> o <c>Supresion</c>.
/// <see cref="Alcance"/> (solo en la supresión): <c>Todo</c> (por omisión), <c>Documentos</c> o <c>Datos</c>. Con
/// <see cref="EmpresaId"/> se dirige a esa empresa (debe ser una con la que tiene un vínculo en su sesión); sin ella, a la plataforma,
/// que responde por la bóveda global. <see cref="Detalle"/> es obligatorio en la corrección.
/// </summary>
public sealed record CrearSolicitudHabeasDto(string Tipo, string? Alcance = null, Guid? EmpresaId = null, string? Detalle = null);

/// <summary>A quién se dirige la solicitud: la plataforma (sin empresa) o una empresa.</summary>
public sealed record DestinoDeSolicitudHabeasDto(Guid? EmpresaId, string Nombre);

/// <summary>Quién presentó la solicitud, solo para quien responde.</summary>
public sealed record TitularDeSolicitudDto(string Nombre, string Identificacion, string Correo);

/// <summary>
/// Una solicitud de habeas data. <see cref="Estado"/>: <c>Recibida</c> o <c>Resuelta</c> (<see cref="Resultado"/>: <c>Atendida</c>,
/// <c>Parcial</c> —supresión con retenciones por ley— o <c>Rechazada</c>). <see cref="Plazo"/>: <c>EnPlazo</c>, <c>PorVencer</c> (dos días
/// hábiles o menos), <c>Vencida</c> o <c>Resuelta</c>. <see cref="DiasHabilesRestantes"/> es negativo si ya venció.
/// <see cref="ContenidoSuprimido"/>: el detalle y la respuesta ya no se pueden leer porque se destruyó la llave del titular
/// (crypto-shredding). <see cref="Titular"/> solo lo ve quien responde.
/// </summary>
public sealed record SolicitudHabeasDto(
    Guid Id, string Radicado, string Tipo, string? Alcance, string Estado, string? Resultado, DestinoDeSolicitudHabeasDto Destino,
    DateTimeOffset RecibidaEn, DateOnly VenceEl, int DiasHabiles, int DiasHabilesRestantes, string Plazo, bool? DentroDelPlazo,
    DateTimeOffset? ResueltaEn, string? Detalle, string? Respuesta, bool ContenidoSuprimido, string? ConstanciaHash, TitularDeSolicitudDto? Titular);

/// <summary>Cuántas solicitudes abiertas hay, cuántas vencen pronto y cuántas ya vencieron.</summary>
public sealed record ResumenDeSolicitudesHabeasDto(int Abiertas, int PorVencer, int Vencidas, int Resueltas);

/// <summary>
/// Respuesta de quien atiende. <see cref="Decision"/>: <c>Atender</c> o <c>Rechazar</c>. La respuesta llega al titular (mínimo 10
/// caracteres); al rechazar es el motivo y conviene citar <see cref="BaseLegal"/>. En la supresión de la plataforma, <c>Atender</c>
/// ejecuta el plan: lo no referenciado se elimina y lo referenciado se conserva con su base legal.
/// </summary>
public sealed record ResolverSolicitudHabeasDto(string Decision, string Respuesta, string? BaseLegal = null);

/// <summary>Qué hará (o hizo) la supresión con cada elemento: <c>Suprimir</c>, <c>Retener</c> o <c>Anonimizar</c>.</summary>
public sealed record ElementoDePlanDeSupresionDto(
    string Tipo, string Referencia, string Descripcion, string Accion, string? BaseLegal, DateOnly? RetenidaHasta);

/// <summary>Lo que ejecutaría la supresión (sin cambiar nada): se muestra a quien la va a confirmar.</summary>
public sealed record PlanDeSupresionDto(
    Guid SolicitudId, string Alcance, int Suprimir, int Retener, int Anonimizar, IReadOnlyList<ElementoDePlanDeSupresionDto> Elementos);

/// <summary>Constancia de la respuesta (sin datos personales). <see cref="Hash"/> es el SHA-256 de su JSON canónico.</summary>
public sealed record ConstanciaHabeasDto(
    string Radicado, string Tipo, string? Alcance, string Destino, DateOnly RecibidaEl, DateOnly ResueltaEl, bool DentroDelPlazo, string Resultado,
    IReadOnlyList<ElementoSuprimidoDto> Suprimidos, IReadOnlyList<ElementoRetenidoDto> Retenidos, IReadOnlyList<string> Anonimizados, string Hash);

public sealed record ElementoSuprimidoDto(string Tipo, string Referencia, string Descripcion);

public sealed record ElementoRetenidoDto(string Tipo, string Referencia, string Descripcion, string BaseLegal, DateOnly? RetenidaHasta);

/// <summary>
/// Exportación de la bóveda del tercero. <see cref="Estado"/>: <c>Pendiente</c>, <c>Generando</c>, <c>Lista</c>, <c>Fallida</c> o
/// <c>Vencida</c>. Lista: <see cref="Sha256"/> del ZIP, <see cref="HashManifiesto"/> y <see cref="Kid"/> de la clave que lo firmó
/// (publicada en <c>/.well-known/validasoft-certificados.json</c>).
/// </summary>
public sealed record ExportacionBovedaDto(
    Guid Id, string Estado, DateTimeOffset SolicitadaEn, DateTimeOffset? TerminadaEn, DateTimeOffset? ExpiraEn, int Archivos, long? Bytes, string? Sha256,
    string? HashManifiesto, string? Kid, string? CodigoDeError, int Descargas);
