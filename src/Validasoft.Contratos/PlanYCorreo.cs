namespace Validasoft.Contratos;

// Plan y límites por grupo (OD-14…OD-17) y dominio de correo propio por empresa (OD-18…OD-22).

/// <summary>Límites del plan: nulo = sin límite; <c>0</c> = no se permite nada. El almacenamiento va en megabytes y las llamadas, por mes.</summary>
public sealed record LimitesDePlanDto(int? Usuarios, int? Empresas, int? VinculosActivos, long? AlmacenamientoMb, long? LlamadasApiMes);

/// <summary>
/// Plan de un grupo. <see cref="Configurado"/> falso = nunca se configuró: sin límites. <see cref="BloqueoSuave"/>: al llegar a un
/// límite se bloquean las altas de eso; lo que existe sigue funcionando.
/// </summary>
public sealed record PlanDelGrupoDto(Guid TenantId, string Codigo, LimitesDePlanDto Limites, bool BloqueoSuave, bool Configurado, DateTimeOffset? ActualizadoEn);

public sealed record ActualizarPlanDelGrupoDto(string Codigo, LimitesDePlanDto Limites, bool BloqueoSuave);

/// <summary>
/// Una métrica del plan con lo usado y su límite (almacenamiento en bytes). <see cref="Nivel"/>: <c>Normal</c>, <c>PorAgotar</c> (80 % o
/// más) o <c>Agotado</c> (100 % o más).
/// </summary>
public sealed record MedicionDePlanDto(string Metrica, long Usado, long? Limite, int? Porcentaje, string Nivel);

/// <summary>
/// Uso del grupo en un mes frente a su plan. <see cref="Alertas"/>: lo que está al 80 % o al 100 %, en palabras.
/// <see cref="Bloqueado"/>: hay un bloqueo suave activo porque alguna métrica llegó al límite.
/// </summary>
public sealed record UsoDelGrupoDto(
    string Mes, PlanDelGrupoDto Plan, IReadOnlyList<MedicionDePlanDto> Mediciones, IReadOnlyList<string> Alertas, bool Bloqueado,
    DateTimeOffset? AlmacenamientoMedidoEn);

/// <summary>Resultado de revisar los planes ya (Plataforma): grupos revisados y avisos que salieron.</summary>
public sealed record ResultadoDeRevisionDePlanesDto(int Grupos, int Avisos);

/// <summary>Revisar los planes ya (Plataforma): de un grupo o, sin <see cref="TenantId"/>, de todos.</summary>
public sealed record RevisarPlanesDto(Guid? TenantId = null);

/// <summary>Revisar los plazos del habeas data ya. <see cref="Fecha"/> simula «ahora» y solo se admite en Development.</summary>
public sealed record RevisarPlazosDeHabeasDto(DateTimeOffset? Fecha = null);

/// <summary>Resultado de revisar los plazos del habeas data ya (Plataforma): solicitudes revisadas y avisos enviados.</summary>
public sealed record ResultadoDeRevisionDeHabeasDto(int Revisadas, int PorVencer, int Vencidas);

// ---------- Dominio de correo ----------

/// <summary>Un registro DNS a crear. <see cref="Finalidad"/>: <c>Spf</c>, <c>Dkim</c> o <c>Dmarc</c>.</summary>
public sealed record RegistroDnsDto(string Finalidad, string Tipo, string Nombre, string Valor);

/// <summary>Lo que encontró la verificación en un registro: si coincide y el valor hallado (nulo si no hay registro).</summary>
public sealed record ComprobacionDeRegistroDnsDto(string Finalidad, bool Correcto, string? Encontrado);

/// <summary>
/// Dominio de correo propio de una empresa. <see cref="Estado"/>: <c>Pendiente</c>, <c>Verificado</c> o <c>Fallido</c> (sin dominio,
/// <see cref="Configurado"/> es falso). <see cref="RemitenteEnUso"/> es la dirección desde la que sale hoy el correo de la empresa: la
/// propia si está verificada; si no, <see cref="RemitenteDeLaPlataforma"/>.
/// </summary>
public sealed record DominioDeCorreoDto(
    Guid EmpresaId, bool Configurado, string? Dominio, string ParteLocal, string? NombreRemitente, string? Estado, IReadOnlyList<RegistroDnsDto> Registros,
    IReadOnlyList<ComprobacionDeRegistroDnsDto> Verificacion, DateTimeOffset? VerificadoEn, DateTimeOffset? UltimaVerificacionEn, string RemitenteEnUso,
    string RemitenteDeLaPlataforma, bool Simulado);

public sealed record ActualizarDominioDeCorreoDto(string Dominio, string? ParteLocal = null, string? NombreRemitente = null);
