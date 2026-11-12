namespace Validasoft.Adaptadores.Portal;

// Modelo de origen del importador (F4, FM-39): las filas del Portal de Proveedores que necesita la ficha, con los nombres de
// tabla y columna del Portal (C:\Fuentes_Git\PortalProveedores\backend\librerias\Code.Model\Entity\dbo y
// Code.Repository\DataBase\SQL\Table). Solo lectura: Validasoft nunca escribe en el Portal. Los textos van tal cual (sin
// recortar ni convertir): la conversión es del traductor (TraductorDelPortal), que no inventa lo que no se pueda convertir.

/// <summary><c>dbo.Terceros</c>. <see cref="TerEstado"/> es el <c>EstadoTercero</c> global del Portal.</summary>
public sealed record TercerosPortal(int Terid, int TerEstado = 0, DateTime? TerFechaRegistro = null);

/// <summary><c>dbo.TerInformacionGeneral</c> (la fila de mayor <c>TigId</c> del tercero). <c>TigTipoPersona</c>: <c>N</c> o <c>J</c>.</summary>
public sealed record TerInformacionGeneral(
    int TigId = 0,
    string? TigNombre = null,
    string? TerApellido = null,
    string? TigTipoPersona = null,
    string? TigTipoDocumento = null,
    string? TigNumeroIdentificacion = null,
    int? TerDigitoVerificacion = null,
    string? TigCorreo = null,
    int TigCiudad = 0,
    string? TigDireccion = null,
    int TigActEconomicaPri = 0,
    string? TigTelefono = null,
    string? TigPaginaWeb = null,
    bool TigCertificadoISO = false,
    string? TigTipoEmpresa = null,
    string? TigPais = null,
    int? TigAnioInicioActividades = null);

/// <summary>
/// <c>dbo.TerDireccionEstructurada</c>: vía (<c>ViaCodigo</c> de <c>dbo.Avenidas</c>: CL, CR, AK…), número de la vía, su
/// intersección (cuadrante), número del cruce, su intersección, placa (<c>SecNumero2</c>) y unidad (<c>dbo.UnidadesIdentidad</c>).
/// </summary>
public sealed record TerDireccionEstructurada(
    string? ViaCodigo = null,
    string? ViaNumero = null,
    string? ViaInterseccion = null,
    string? SecNumero = null,
    string? SecInterseccion = null,
    string? SecNumero2 = null,
    string? UnidadCodigo = null,
    string? UnidadNumero = null);

/// <summary>
/// <c>dbo.TerInfoTributaria</c>: las calidades S/N del Portal (13 más <c>AutoretenedorRFT</c>), las resoluciones de gran
/// contribuyente y autorretenedor (número entero y fecha). <c>NoObligFacturar</c> guarda «obligado a facturar» pese al nombre
/// (el formulario lo rotula así y el SRM envía su negación a AyF).
/// </summary>
public sealed record TerInfoTributaria(
    bool ResponsableIVA = false,
    bool Autorretenedor = false,
    bool Declarante = false,
    bool GranContribuyente = false,
    bool AutoRetenedorICA = false,
    bool RegimenSimple = false,
    bool GranContribuyenteBta = false,
    bool RegimenEspecial = false,
    bool AutoretenedorRFT = false,
    bool DescuentoSENAFIC = false,
    bool ProfesionalIndependiente = false,
    bool ExentoRetencionFuente = false,
    bool AgenteRetenedorIVA = false,
    bool NoObligFacturar = false,
    int? ResolucionGC = null,
    DateTime? FechaGC = null,
    int? ResolucionAutoRete = null,
    DateTime? FechaAutoRete = null);

/// <summary><c>dbo.TerRetencionICA</c>: municipio (<c>dbo.Ciudades</c>) y actividad (<c>dbo.ActividadEconomica</c>) de ICA.</summary>
public sealed record TerRetencionICA(int IdTerIca = 0, int IdCiudad = 0, int IdActividadEconomica = 0);

/// <summary><c>dbo.TerInfoFinanciera</c>: una fila por año (<c>Anio</c>), cifras decimal(18,2).</summary>
public sealed record TerInfoFinanciera(
    int Id = 0,
    int Anio = 0,
    decimal ActivosTotales = 0,
    decimal PasivosTotales = 0,
    decimal Patrimonio = 0,
    decimal IngresosBrutos = 0,
    decimal IngresosNetos = 0,
    decimal GastosOperaciones = 0,
    decimal GastosNoOperaciones = 0,
    decimal OtrosIngresos = 0);

/// <summary>
/// <c>dbo.TerTransaccionMonedaExtranjera</c> (una por tercero, sin año) con sus tipos (<c>TerTransaccionMonedaExtranjeraTipo</c>:
/// <c>TipoTransaccionMonedaExtranjeraEnum</c>, 1 importaciones … 9 otra) y, por tipo, sus países (<c>…Pais</c>, <c>dbo.Paises</c>).
/// </summary>
public sealed record TerTransaccionMonedaExtranjera(bool RealizaTransacciones = false, IReadOnlyList<TerTransaccionMonedaExtranjeraTipo>? Tipos = null);

public sealed record TerTransaccionMonedaExtranjeraTipo(int Id = 0, int IdTipoTransaccion = 0, string? OtraCual = null, IReadOnlyList<int>? Paises = null);

/// <summary>
/// <c>dbo.TerCamaraComercio</c>: una persona relacionada. <c>TerTipoRegistro</c>: 1 representante legal, 2 socio, 3 revisor
/// fiscal, 4 contador. <c>TerTipoRepresentacion</c>: 1 principal, 2 suplente, 3 apoderado, 4 firma. Fechas, lugares,
/// nacionalidad y porcentaje son <b>texto</b> en el Portal.
/// </summary>
public sealed record TerCamaraComercio(
    int TerCamId = 0,
    int TerTipoRegistro = 0,
    string? TerCamTipoDoc = null,
    string? TerCamDocumento = null,
    string? TerCapNombre = null,
    string? TerCapApellido = null,
    string? TerCamCargo = null,
    string? TerCamPorParticipa = null,
    bool? TerCamEsPep = null,
    bool? TerCamVinculoPep = null,
    int? TerTipoRepresentacion = null,
    string? TerFechaExpedicion = null,
    string? TerLugarExpedicion = null,
    string? TerFechaNacimiento = null,
    string? TerLugarNacimiento = null,
    string? TerLugarResidencia = null,
    string? TerPaisResidencia = null,
    string? TerDireccion = null,
    string? TerNacionalidad = null,
    string? TerRazonSocialNombre = null,
    string? TerRazonSocialTipoDoc = null,
    string? TerRazonSocialDoc = null,
    string? TerCelular = null,
    string? TerCorreo = null);


/// <summary>
/// <c>dbo.TerDeclaracionPep</c> (HU 49093 del Portal, solo existe en un Portal con esa rama desplegada): la declaración jurada PEP de una fila
/// de <c>TerCamaraComercio</c> (<c>TerCamId</c>), con sus hijos. <c>RelacionEmpresa</c>: 1 proveedor, 2 socio, 3 otro; <c>FirmaEnCalidad</c>: 1 nombre
/// propio, 2 representante legal, 3 apoderado. Solo se lee la <b>activa</b>. La imagen de la firma está en el disco del servidor del Portal
/// (<c>FirmaRuta</c>, ruta que guardó el Portal) y su huella SHA-256 en <c>FirmaHash</c>. <c>FirmaImagen</c> no es una columna: el adaptador la llena leyendo
/// la imagen por <see cref="IOrigenDeArchivosDelPortal"/> (o viene en el archivo JSON de pruebas) y el traductor solo la usa si su huella coincide.
/// <c>Vinculos</c> son las PEP relacionadas (<c>dbo.TerDeclaracionPepVinculo</c>, varias por declaración desde la última versión de la HU).
/// </summary>
public sealed record TerDeclaracionPep(
    int TerCamId = 0,
    int RelacionEmpresa = 0,
    string? RelacionOtroCual = null,
    string? Nacionalidad = null,
    string? Direccion = null,
    string? CargoDesempenado = null,
    string? Institucion = null,
    string? PaisInstitucion = null,
    DateTime? FechaVinculacion = null,
    DateTime? FechaDesvinculacion = null,
    bool CargoVigente = false,
    bool TieneCuentasExterior = false,
    string? PaisesCuentasExterior = null,
    string? ConyugeNombre = null,
    string? ConyugeTipoDocumento = null,
    string? ConyugeNumeroDocumento = null,
    bool TieneVinculoPep = false,
    string? VinculoPepNombre = null,
    string? VinculoPepParentesco = null,
    string? VinculoPepCargoInstitucion = null,
    DateTime? VinculoPepFechaVinculacion = null,
    DateTime? VinculoPepFechaDesvinculacion = null,
    bool VinculoPepVigente = false,
    bool AceptaJuramento = false,
    int FirmaEnCalidad = 0,
    string? FirmanteNombre = null,
    string? FirmanteTipoDocumento = null,
    string? FirmanteNumeroDocumento = null,
    DateTime? FechaFirma = null,
    string? FirmaHash = null,
    IReadOnlyList<TerDeclaracionPepFamiliar>? Familiares = null,
    IReadOnlyList<TerDeclaracionPepSociedad>? Sociedades = null,
    string? FirmaRuta = null,
    byte[]? FirmaImagen = null,
    IReadOnlyList<TerDeclaracionPepVinculo>? Vinculos = null);

/// <summary><c>dbo.TerDeclaracionPepFamiliar</c>.</summary>
public sealed record TerDeclaracionPepFamiliar(string? NombreCompleto = null, string? Parentesco = null, string? TipoDocumento = null, string? NumeroDocumento = null);

/// <summary><c>dbo.TerDeclaracionPepSociedad</c>. <c>Calidad</c>: 1 socio, 2 administrador, 3 fideicomitente, 4 otra.</summary>
public sealed record TerDeclaracionPepSociedad(string? NombreRazonSocial = null, string? Identificacion = null, int Calidad = 0, string? CalidadOtraCual = null);

/// <summary><c>dbo.TerDeclaracionPepVinculo</c>: una PEP relacionada. <c>TipoVinculo</c>: 1 familiar, 2 asociado cercano; <c>Actual</c>: sigue vigente.</summary>
public sealed record TerDeclaracionPepVinculo(
    int TipoVinculo = 0,
    string? Relacion = null,
    string? NombreCompleto = null,
    string? Cargo = null,
    string? Entidad = null,
    string? Pais = null,
    DateTime? FechaVinculacion = null,
    DateTime? FechaRetiro = null,
    bool Actual = false);

/// <summary><c>dbo.TerSISO</c>: las ocho preguntas S/N.</summary>
public sealed record TerSISO(
    bool ProgramaSaludOcupacional = false,
    bool ProgramaFactoresRiesgo = false,
    bool TieneComiteSO = false,
    bool ProgramaSeguridadEhigiene = false,
    bool ProgramaAmbiental = false,
    bool ReglaIntVigente = false,
    bool PoliticaHSEQ = false,
    bool LicPermAutorizaciones = false);

/// <summary><c>dbo.TercerosConstructora</c>: el estado del tercero en cada constructora (<c>EstadoTercero</c>: 3 Completada, 4 Actualizacion…).</summary>
public sealed record TercerosConstructora(int IdConstructora = 0, int Estado = 0, DateTime? FechaRegistro = null);

/// <summary>
/// Un tercero del Portal con lo que la ficha necesita de cada tabla. <see cref="Especialidades"/>: los <c>EspId</c> de
/// <c>dbo.TerEspecialidad</c>.
/// </summary>
public sealed record TerceroDelPortal(
    TercerosPortal Tercero,
    TerInformacionGeneral? InformacionGeneral = null,
    TerDireccionEstructurada? DireccionEstructurada = null,
    TerInfoTributaria? InfoTributaria = null,
    IReadOnlyList<TerRetencionICA>? RetencionesIca = null,
    IReadOnlyList<TerInfoFinanciera>? InfoFinanciera = null,
    TerTransaccionMonedaExtranjera? MonedaExtranjera = null,
    IReadOnlyList<TerCamaraComercio>? CamaraComercio = null,
    TerSISO? Siso = null,
    IReadOnlyList<int>? Especialidades = null,
    IReadOnlyList<TercerosConstructora>? Constructoras = null,
    IReadOnlyList<TerDeclaracionPep>? DeclaracionesPep = null)
{
    public int Terid => Tercero.Terid;
}

// ---------- catálogos del Portal ----------

/// <summary><c>dbo.Ciudades</c>: el DANE es <c>CiuCodDepartamento</c> (2) + <c>CiuCodMunicipio</c> (3).</summary>
public sealed record Ciudades(int CiudID, string? CiuNombre = null, string? CiuCodDepartamento = null, string? CiuCodMunicipio = null);

/// <summary><c>dbo.ActividadEconomica</c>: CIIU en <c>ActEcCodigo</c>.</summary>
public sealed record ActividadEconomica(int ActEcId, string? ActEcCodigo = null, string? ActECtexto = null);

/// <summary><c>dbo.Bancos</c>: <c>Codigo</c> es lo que guarda <c>TerCuentaBancaria.IdBanco</c>.</summary>
public sealed record Bancos(string Id, string? Codigo = null, string? Texto = null, bool Estado = true);

/// <summary><c>dbo.GruposTercero</c> (nivel 1 de especialidades).</summary>
public sealed record GruposTercero(int GruId, string? GruTexto = null);

/// <summary><c>dbo.CategoriasTercero</c> (nivel 2; la entidad está en <c>EspecialidadesTercero.cs</c> del Portal: nombres cruzados).</summary>
public sealed record CategoriasTercero(int CatId, int CatIdGrupo = 0, string? CatTexto = null);

/// <summary><c>dbo.EspecialidadesTercero</c> (nivel 3; la entidad está en <c>CategoriasTercero.cs</c> del Portal: nombres cruzados).</summary>
public sealed record EspecialidadesTercero(int EspId, int EspIdCategoria = 0, int EspIdGrupo = 0, string? EspTexto = null);

/// <summary><c>dbo.Paises</c>: <c>Sigla</c> (ISO 3166-1) y nombre (lo que guarda <c>TigPais</c>).</summary>
public sealed record Paises(int Id, string? Sigla = null, string? Nombre = null);

/// <summary>Los catálogos del Portal que usa la ficha.</summary>
public sealed record CatalogosDelPortal(
    IReadOnlyList<Ciudades>? Ciudades = null,
    IReadOnlyList<ActividadEconomica>? ActividadesEconomicas = null,
    IReadOnlyList<Bancos>? Bancos = null,
    IReadOnlyList<GruposTercero>? Grupos = null,
    IReadOnlyList<CategoriasTercero>? Categorias = null,
    IReadOnlyList<EspecialidadesTercero>? Especialidades = null,
    IReadOnlyList<Paises>? Paises = null)
{
    public static readonly CatalogosDelPortal Vacios = new();
}
