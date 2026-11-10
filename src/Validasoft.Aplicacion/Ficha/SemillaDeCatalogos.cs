using Validasoft.Dominio.Ficha;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>
/// Filas de la semilla de desarrollo (FM-22). Fuentes: DIVIPOLA del DANE (capitales), CIIU Rev. 4 A.C. (DANE), códigos de
/// compensación CENIT del Banco de la República y responsabilidades de la casilla 53 del RUT (DIAN). Solo para
/// desarrollo y pruebas: los catálogos completos se importan desde la BD del Portal en F4.
/// </summary>
public static class SemillaDeCatalogos
{
    public static IReadOnlyList<(CatalogoDePlataforma Catalogo, IReadOnlyList<FilaDeCatalogo> Filas)> Todas =>
    [
        (CatalogoDePlataforma.Ciudades, Ciudades),
        (CatalogoDePlataforma.ActividadesEconomicas, ActividadesEconomicas),
        (CatalogoDePlataforma.Bancos, Bancos),
        (CatalogoDePlataforma.ResponsabilidadesDian, ResponsabilidadesDian),
        (CatalogoDePlataforma.Especialidades, Especialidades)
    ];

    private static FilaDeCatalogo Ciudad(string codigo, string nombre, string departamento) => new(codigo, nombre, departamento, "CO");

    /// <summary>Las capitales de los 32 departamentos (la de Cundinamarca es Bogotá, D.C.) y algunos municipios del área metropolitana.</summary>
    public static readonly IReadOnlyList<FilaDeCatalogo> Ciudades =
    [
        Ciudad("05001", "Medellín", "Antioquia"),
        Ciudad("08001", "Barranquilla", "Atlántico"),
        Ciudad("11001", "Bogotá, D.C.", "Bogotá, D.C."),
        Ciudad("13001", "Cartagena de Indias", "Bolívar"),
        Ciudad("15001", "Tunja", "Boyacá"),
        Ciudad("17001", "Manizales", "Caldas"),
        Ciudad("18001", "Florencia", "Caquetá"),
        Ciudad("19001", "Popayán", "Cauca"),
        Ciudad("20001", "Valledupar", "Cesar"),
        Ciudad("23001", "Montería", "Córdoba"),
        Ciudad("27001", "Quibdó", "Chocó"),
        Ciudad("41001", "Neiva", "Huila"),
        Ciudad("44001", "Riohacha", "La Guajira"),
        Ciudad("47001", "Santa Marta", "Magdalena"),
        Ciudad("50001", "Villavicencio", "Meta"),
        Ciudad("52001", "Pasto", "Nariño"),
        Ciudad("54001", "San José de Cúcuta", "Norte de Santander"),
        Ciudad("63001", "Armenia", "Quindío"),
        Ciudad("66001", "Pereira", "Risaralda"),
        Ciudad("68001", "Bucaramanga", "Santander"),
        Ciudad("70001", "Sincelejo", "Sucre"),
        Ciudad("73001", "Ibagué", "Tolima"),
        Ciudad("76001", "Cali", "Valle del Cauca"),
        Ciudad("81001", "Arauca", "Arauca"),
        Ciudad("85001", "Yopal", "Casanare"),
        Ciudad("86001", "Mocoa", "Putumayo"),
        Ciudad("88001", "San Andrés", "Archipiélago de San Andrés, Providencia y Santa Catalina"),
        Ciudad("91001", "Leticia", "Amazonas"),
        Ciudad("94001", "Inírida", "Guainía"),
        Ciudad("95001", "San José del Guaviare", "Guaviare"),
        Ciudad("97001", "Mitú", "Vaupés"),
        Ciudad("99001", "Puerto Carreño", "Vichada"),
        Ciudad("05088", "Bello", "Antioquia"),
        Ciudad("05266", "Envigado", "Antioquia"),
        Ciudad("05360", "Itagüí", "Antioquia"),
        Ciudad("25175", "Chía", "Cundinamarca"),
        Ciudad("25754", "Soacha", "Cundinamarca")
    ];

    /// <summary>CIIU Rev. 4 A.C. comunes en construcción, su cadena de suministro y el comercio.</summary>
    public static readonly IReadOnlyList<FilaDeCatalogo> ActividadesEconomicas =
    [
        new("0810", "Extracción de piedra, arena, arcillas, cal, yeso, caolín, bentonitas y similares"),
        new("1610", "Aserrado, acepillado e impregnación de la madera"),
        new("2229", "Fabricación de artículos de plástico n.c.p."),
        new("2392", "Fabricación de materiales de arcilla para la construcción"),
        new("2394", "Fabricación de cemento, cal y yeso"),
        new("2395", "Fabricación de artículos de hormigón, cemento y yeso"),
        new("2410", "Industrias básicas de hierro y de acero"),
        new("2511", "Fabricación de productos metálicos para uso estructural"),
        new("4111", "Construcción de edificios residenciales"),
        new("4112", "Construcción de edificios no residenciales"),
        new("4210", "Construcción de carreteras y vías de ferrocarril"),
        new("4220", "Construcción de proyectos de servicio público"),
        new("4290", "Construcción de otras obras de ingeniería civil"),
        new("4311", "Demolición"),
        new("4312", "Preparación del terreno"),
        new("4321", "Instalaciones eléctricas"),
        new("4322", "Instalaciones de fontanería, calefacción y aire acondicionado"),
        new("4329", "Otras instalaciones especializadas"),
        new("4330", "Terminación y acabado de edificios y obras de ingeniería civil"),
        new("4390", "Otras actividades especializadas para la construcción de edificios y obras de ingeniería civil"),
        new("4610", "Comercio al por mayor a cambio de una retribución o por contrata"),
        new("4659", "Comercio al por mayor de otros tipos de maquinaria y equipo n.c.p."),
        new("4661", "Comercio al por mayor de combustibles sólidos, líquidos, gaseosos y productos conexos"),
        new("4662", "Comercio al por mayor de metales y productos metalíferos"),
        new("4663", "Comercio al por mayor de materiales de construcción, artículos de ferretería, pinturas, productos de vidrio, equipo y materiales de fontanería y calefacción"),
        new("4664", "Comercio al por mayor de productos químicos básicos, cauchos y plásticos en formas primarias y productos químicos de uso agropecuario"),
        new("4669", "Comercio al por mayor de otros productos n.c.p."),
        new("4690", "Comercio al por mayor no especializado"),
        new("4719", "Comercio al por menor en establecimientos no especializados, con surtido compuesto principalmente por productos diferentes de alimentos (víveres en general), bebidas y tabaco"),
        new("4752", "Comercio al por menor de artículos de ferretería, pinturas y productos de vidrio en establecimientos especializados"),
        new("4753", "Comercio al por menor de tapices, alfombras y cubrimientos para paredes y pisos en establecimientos especializados"),
        new("4754", "Comercio al por menor de electrodomésticos y gasodomésticos de uso doméstico, muebles y equipos de iluminación"),
        new("4759", "Comercio al por menor de otros artículos domésticos en establecimientos especializados"),
        new("4923", "Transporte de carga por carretera"),
        new("5210", "Almacenamiento y depósito"),
        new("6810", "Actividades inmobiliarias realizadas con bienes propios o arrendados"),
        new("6820", "Actividades inmobiliarias realizadas a cambio de una retribución o por contrata"),
        new("6920", "Actividades de contabilidad, teneduría de libros, auditoría financiera y asesoría tributaria"),
        new("7020", "Actividades de consultoría de gestión"),
        new("7110", "Actividades de arquitectura e ingeniería y otras actividades conexas de consultoría técnica"),
        new("7120", "Ensayos y análisis técnicos"),
        new("7730", "Alquiler y arrendamiento de otros tipos de maquinaria, equipo y bienes tangibles n.c.p."),
        new("8010", "Actividades de seguridad privada"),
        new("8121", "Limpieza general interior de edificios")
    ];

    /// <summary>Bancos con su código de compensación CENIT (Banco de la República).</summary>
    public static readonly IReadOnlyList<FilaDeCatalogo> Bancos =
    [
        new("1001", "Banco de Bogotá"),
        new("1002", "Banco Popular"),
        new("1006", "Banco Itaú"),
        new("1007", "Bancolombia"),
        new("1009", "Banco Citibank"),
        new("1012", "Banco GNB Sudameris"),
        new("1013", "Banco BBVA"),
        new("1019", "Scotiabank Colpatria"),
        new("1023", "Banco de Occidente"),
        new("1032", "Banco Caja Social (BCSC)"),
        new("1040", "Banco Agrario de Colombia"),
        new("1047", "Banco Mundo Mujer"),
        new("1051", "Banco Davivienda"),
        new("1052", "Banco AV Villas"),
        new("1059", "Bancamía"),
        new("1060", "Banco Pichincha"),
        new("1061", "Banco Bancoomeva"),
        new("1062", "Banco Falabella"),
        new("1063", "Banco Finandina"),
        new("1065", "Banco Santander de Negocios"),
        new("1066", "Banco Coopcentral"),
        new("1067", "Mibanco"),
        new("1069", "Banco Serfinanza"),
        new("1070", "Lulo Bank"),
        new("1071", "Banco J.P. Morgan")
    ];

    /// <summary>Responsabilidades de la casilla 53 del RUT con su nombre oficial (las 11 y 12 son códigos históricos, hoy 48 y 49).</summary>
    public static readonly IReadOnlyList<FilaDeCatalogo> ResponsabilidadesDian =
    [
        new("01", "Aporte especial para la administración de justicia"),
        new("02", "Gravamen a los movimientos financieros"),
        new("03", "Impuesto al patrimonio"),
        new("04", "Impuesto sobre la renta y complementarios régimen tributario especial"),
        new("05", "Impuesto sobre la renta y complementarios régimen ordinario"),
        new("06", "Ingresos y patrimonio"),
        new("07", "Retención en la fuente a título de renta"),
        new("08", "Retención timbre nacional"),
        new("09", "Retención en la fuente en el impuesto sobre las ventas"),
        new("10", "Usuario aduanero"),
        new("11", "Ventas régimen común"),
        new("12", "Ventas régimen simplificado"),
        new("13", "Gran contribuyente"),
        new("14", "Informante de exógena"),
        new("15", "Autorretenedor"),
        new("16", "Obligación facturar por ingresos bienes y/o servicios excluidos"),
        new("17", "Profesionales de compra y venta de divisas"),
        new("18", "Precios de transferencia"),
        new("19", "Productor de bienes y/o servicios exentos"),
        new("20", "Obtención NIT"),
        new("21", "Declarar ingreso o salida del país de divisas o moneda legal colombiana"),
        new("22", "Obligado a cumplir deberes formales a nombre de terceros"),
        new("23", "Agente de retención en el impuesto sobre las ventas"),
        new("26", "Declaración informativa individual precios de transferencia"),
        new("32", "Impuesto nacional a la gasolina y al ACPM"),
        new("33", "Impuesto nacional al consumo"),
        new("36", "Establecimiento permanente"),
        new("39", "Proveedor de servicios tecnológicos (PST)"),
        new("41", "Declaración anual de activos en el exterior"),
        new("42", "Obligado a llevar contabilidad"),
        new("45", "Autorretenedor de rendimientos financieros"),
        new("46", "IVA prestadores de servicios desde el exterior"),
        new("47", "Régimen simple de tributación (SIMPLE)"),
        new("48", "Impuesto sobre las ventas (IVA)"),
        new("49", "No responsable de IVA"),
        new("50", "No responsable de consumo restaurantes y bares"),
        new("52", "Facturador electrónico"),
        new("53", "Persona jurídica no responsable de IVA"),
        new("54", "Intercambio automático de información CRS"),
        new("55", "Informante de beneficiarios finales"),
        new("56", "Impuesto nacional al carbono")
    ];

    private static FilaDeCatalogo Especialidad(string codigo, string nombre, int nivel, string? padre = null) => new(codigo, nombre, Padre: padre, Nivel: nivel);

    /// <summary>Árbol de ejemplo grupo → categoría → especialidad (el real llega del Portal en F4).</summary>
    public static readonly IReadOnlyList<FilaDeCatalogo> Especialidades =
    [
        Especialidad("OG", "Obra gris", 1),
        Especialidad("OG.CON", "Concretos", 2, "OG"),
        Especialidad("OG.CON.PRE", "Concreto premezclado", 3, "OG.CON"),
        Especialidad("OG.CON.BOM", "Bombeo de concreto", 3, "OG.CON"),
        Especialidad("OG.ACE", "Acero de refuerzo", 2, "OG"),
        Especialidad("OG.ACE.FIG", "Figurado y armado de acero", 3, "OG.ACE"),
        Especialidad("OG.MAM", "Mampostería", 2, "OG"),
        Especialidad("OG.MAM.BLO", "Bloque y ladrillo", 3, "OG.MAM"),
        Especialidad("AC", "Acabados", 1),
        Especialidad("AC.PIS", "Pisos y enchapes", 2, "AC"),
        Especialidad("AC.PIS.CER", "Cerámica y porcelanato", 3, "AC.PIS"),
        Especialidad("AC.PIN", "Pinturas", 2, "AC"),
        Especialidad("AC.PIN.INT", "Pintura interior y exterior", 3, "AC.PIN"),
        Especialidad("IN", "Instalaciones", 1),
        Especialidad("IN.ELE", "Instalaciones eléctricas", 2, "IN"),
        Especialidad("IN.ELE.RED", "Redes eléctricas de baja tensión", 3, "IN.ELE"),
        Especialidad("IN.HID", "Instalaciones hidrosanitarias", 2, "IN"),
        Especialidad("IN.HID.RED", "Redes hidrosanitarias y de gas", 3, "IN.HID"),
        Especialidad("MQ", "Maquinaria y equipo", 1),
        Especialidad("MQ.ALQ", "Alquiler de equipo", 2, "MQ"),
        Especialidad("MQ.ALQ.AND", "Andamios y formaletas", 3, "MQ.ALQ"),
        Especialidad("MQ.ALQ.PES", "Maquinaria pesada", 3, "MQ.ALQ"),
        Especialidad("SP", "Servicios profesionales", 1),
        Especialidad("SP.ING", "Ingeniería", 2, "SP"),
        Especialidad("SP.ING.EST", "Diseño estructural", 3, "SP.ING"),
        Especialidad("SP.ING.SUE", "Estudios de suelos", 3, "SP.ING")
    ];
}
