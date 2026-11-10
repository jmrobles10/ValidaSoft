using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha;

// Datos tipados de cada sección (docs/14 §4). En JSON las claves van en snake_case; un campo ausente o null es
// «sin diligenciar» (distinto de 0 o false). Fechas DateOnly (aaaa-mm-dd), cifras y porcentajes decimal, sí/no bool.

/// <summary><c>FICHA_GENERAL</c>. Tipo de persona, tipo y número de identificación y DV salen del registro del tercero (no editables).</summary>
public sealed record DatosGenerales
{
    public string? RazonSocial { get; init; }

    public string? Nombres { get; init; }

    public string? Apellidos { get; init; }

    public TipoPersona? TipoPersona { get; init; }

    public string? TipoIdentificacion { get; init; }

    public string? Numero { get; init; }

    /// <summary>Calculado (DIAN módulo 11 para el NIT); nunca lo fija el tercero.</summary>
    public string? Dv { get; init; }

    public string? Correo { get; init; }

    public string? Telefono { get; init; }

    /// <summary>País del domicilio, ISO 3166-1 alfa-2.</summary>
    public string? Pais { get; init; }

    /// <summary>Municipio DANE (5 dígitos); obligatorio si el país es CO.</summary>
    public string? Ciudad { get; init; }

    public string? Direccion { get; init; }

    public DireccionEstructurada? DireccionEstructurada { get; init; }

    public string? CiiuPrincipal { get; init; }

    public IReadOnlyList<string>? CiiuSecundarias { get; init; }

    public string? PaginaWeb { get; init; }

    public bool? CertificadoIso { get; init; }

    public int? AnioInicio { get; init; }
}

/// <summary>Dirección en el formato estructurado de AyF (opcional): «Calle 45 # 12 - 08, Oficina 301».</summary>
public sealed record DireccionEstructurada
{
    /// <summary>Calle, Carrera, Avenida, AvenidaCalle, AvenidaCarrera, Diagonal, Transversal, Circular, Autopista, Kilometro.</summary>
    public string? TipoVia { get; init; }

    public string? NumeroVia { get; init; }

    public string? LetraVia { get; init; }

    public bool? Bis { get; init; }

    /// <summary>Sur, Este, Norte, Oeste.</summary>
    public string? Cuadrante { get; init; }

    public string? NumeroCruce { get; init; }

    public string? LetraCruce { get; init; }

    public string? Placa { get; init; }

    public string? Complemento { get; init; }
}

/// <summary>
/// <c>FICHA_TRIBUTARIA</c>. Responsabilidades = códigos de la casilla 53 del RUT; de ellos se derivan las calidades del
/// Portal (F3). ICA es municipal y no está en la casilla 53: <see cref="ResponsableIca"/> lo declara (FM-21).
/// </summary>
public sealed record DatosTributarios
{
    public IReadOnlyList<string>? Responsabilidades { get; init; }

    /// <summary>Obligatoria con la responsabilidad 13 (gran contribuyente).</summary>
    public Resolucion? ResolucionGranContribuyente { get; init; }

    /// <summary>Obligatoria con la responsabilidad 15 (autorretenedor).</summary>
    public Resolucion? ResolucionAutorretenedor { get; init; }

    /// <summary>¿Contribuyente del impuesto de industria y comercio? Con <c>true</c> exige al menos una fila de <see cref="Ica"/>.</summary>
    public bool? ResponsableIca { get; init; }

    public bool? AutorretenedorIca { get; init; }

    public IReadOnlyList<FilaIca>? Ica { get; init; }
}

public sealed record Resolucion
{
    public string? Numero { get; init; }

    public DateOnly? Fecha { get; init; }
}

/// <summary>Municipio (DANE) donde declara ICA, actividad (CIIU) y tarifa opcional por mil.</summary>
public sealed record FilaIca
{
    public string? Municipio { get; init; }

    public string? Ciiu { get; init; }

    public decimal? TarifaPorMil { get; init; }
}

/// <summary><c>FICHA_FINANCIERA</c> de un periodo (año). Cifras en pesos con hasta dos decimales; el patrimonio puede ser negativo.</summary>
public sealed record DatosFinancieros
{
    public int? Periodo { get; init; }

    public DateOnly? FechaCorte { get; init; }

    public decimal? Activos { get; init; }

    public decimal? Pasivos { get; init; }

    public decimal? Patrimonio { get; init; }

    public decimal? IngresosBrutos { get; init; }

    public decimal? IngresosNetos { get; init; }

    public decimal? GastosOperacionales { get; init; }

    public decimal? GastosNoOperacionales { get; init; }

    public decimal? OtrosIngresos { get; init; }

    public MonedaExtranjera? MonedaExtranjera { get; init; }
}

public enum TipoOperacionExtranjera
{
    Importaciones,
    Exportaciones,
    Inversiones,
    Prestamos,
    Giros,
    PagoDeServicios,
    Otras
}

public sealed record MonedaExtranjera
{
    public bool? Realiza { get; init; }

    public IReadOnlyList<TipoOperacionExtranjera>? Tipos { get; init; }

    /// <summary>Países ISO 3166-1 alfa-2.</summary>
    public IReadOnlyList<string>? Paises { get; init; }
}

/// <summary>Rol de una persona relacionada en la sección legal (FM-7, fase 1).</summary>
public enum RolPersonaRelacionada
{
    RepresentanteLegal,
    Suplente,
    Apoderado,
    Socio,
    JuntaDirectiva,
    RevisorFiscal,
    Contador
}

/// <summary><c>FICHA_LEGAL</c>: personas relacionadas como filas tipadas (fase 1) y la firma de revisoría fiscal, si la hay.</summary>
public sealed record DatosLegales
{
    public IReadOnlyList<PersonaRelacionada>? Personas { get; init; }

    public FirmaRevisora? RevisorFiscalFirma { get; init; }
}

public sealed record PersonaRelacionada
{
    public RolPersonaRelacionada? Rol { get; init; }

    /// <summary>Texto libre del certificado (p. ej. «Principal», «Primer suplente»).</summary>
    public string? TipoRepresentacion { get; init; }

    /// <summary>Tipo de identificación de Colombia (CC, CE, NIT, PA, PPT, TI) o genérico (PASAPORTE, ID_FISCAL).</summary>
    public string? TipoIdentificacion { get; init; }

    public string? Numero { get; init; }

    /// <summary>Calculado si es NIT; nunca lo fija el tercero.</summary>
    public string? Dv { get; init; }

    public string? Nombres { get; init; }

    public string? Apellidos { get; init; }

    public string? Cargo { get; init; }

    /// <summary>Participación (0–100); obligatoria para los socios.</summary>
    public decimal? Porcentaje { get; init; }

    public bool? Pep { get; init; }

    public bool? VinculoPep { get; init; }

    public DateOnly? FechaExpedicion { get; init; }

    public DateOnly? FechaNacimiento { get; init; }

    /// <summary>ISO 3166-1 alfa-2.</summary>
    public string? Nacionalidad { get; init; }

    /// <summary>País de residencia, ISO 3166-1 alfa-2.</summary>
    public string? Residencia { get; init; }

    public string? Correo { get; init; }

    public string? Telefono { get; init; }
}

/// <summary>Firma de revisoría fiscal cuando el revisor actúa por una firma.</summary>
public sealed record FirmaRevisora
{
    public string? Nit { get; init; }

    public string? RazonSocial { get; init; }
}

/// <summary><c>FICHA_SST</c>: las ocho preguntas sí/no del Portal (SISO).</summary>
public sealed record DatosSst
{
    public bool? ProgramaSaludOcupacional { get; init; }

    public bool? IdentificaFactoresDeRiesgo { get; init; }

    public bool? ComiteParitario { get; init; }

    public bool? ProgramaHigieneYSeguridad { get; init; }

    public bool? GestionAmbiental { get; init; }

    public bool? ReglamentoInterno { get; init; }

    public bool? PoliticaHseq { get; init; }

    public bool? LicenciasYPermisos { get; init; }
}

/// <summary><c>FICHA_ESPECIALIDADES</c>: códigos de nivel 3 del catálogo de especialidades.</summary>
public sealed record DatosEspecialidades
{
    public IReadOnlyList<string>? Especialidades { get; init; }
}
