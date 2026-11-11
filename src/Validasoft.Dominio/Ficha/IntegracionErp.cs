using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha;

/// <summary>De dónde sale una calidad tributaria (F3, FM-33): datos tributarios genéricos de Colombia, para cualquier ERP.</summary>
public enum OrigenDeCalidad
{
    /// <summary>De los códigos de la casilla 53 del RUT: con alguno de los códigos ⇒ sí; sin ninguno ⇒ no.</summary>
    CodigoDian,

    /// <summary>De un dato de la sección tributaria que no está en la casilla 53 (ICA es municipal; la exención de CREE se declara).</summary>
    DatoDeLaSeccion,

    /// <summary>No se puede derivar de lo que guarda la ficha: va como <c>null</c> con su nota.</summary>
    NoDerivable
}

/// <summary>
/// Fila de la tabla de las calidades: la calidad (clave camelCase en el contenido), de dónde sale, los códigos DIAN que la dan y la
/// fuente o la nota. A qué campo de cada ERP corresponde es asunto del adaptador de ese ERP (docs/08 §3.4), no del núcleo.
/// </summary>
public sealed record ReglaDeCalidad(string Calidad, OrigenDeCalidad Origen, IReadOnlyList<string> Codigos, string Nota);

/// <summary>
/// Las calidades tributarias sí/no de un tercero en Colombia, derivadas de lo aceptado (códigos de la casilla 53 del RUT y datos
/// declarados de la sección tributaria). <c>null</c> = no se puede derivar ni se declaró (ver <see cref="CalidadesTributarias.Tabla"/>):
/// el sistema que recibe conserva lo que ya tenga.
/// </summary>
public sealed record CalidadesTributarias(
    bool? ResponsableIva,
    bool? Autorretenedor,
    bool? DeclaranteRenta,
    bool? GranContribuyente,
    bool? AutorretenedorIca,
    bool? RegimenSimple,
    bool? GranContribuyenteBogota,
    bool? RegimenEspecial,
    bool? AutorretenedorRft,
    bool? DescuentoSenaFic,
    bool? ProfesionalIndependiente,
    bool? ExentoRetencion,
    bool? AgenteRetenedorIva,
    bool? ObligadoAFacturar,
    bool? ExentoReteCree = null)
{
    private const string Casilla53 =
        "Casilla 53 del RUT (Resolución DIAN 000110 de 2021, anexo de responsabilidades; catálogo responsabilidades-dian).";

    /// <summary>
    /// La tabla de derivación (FM-33). Los códigos son los de la casilla 53 del RUT; 11 y 12 son los históricos de IVA
    /// (régimen común y simplificado), hoy 48 y 49. El 07 (retención en la fuente a título de renta) <b>no</b> hace agente
    /// retenedor de IVA: esa calidad la dan el 09 (retención en la fuente en el impuesto sobre las ventas, art. 437-2 E.T.)
    /// y el 23 (agente de retención en el impuesto sobre las ventas).
    /// </summary>
    public static readonly IReadOnlyList<ReglaDeCalidad> Tabla =
    [
        new("responsableIva", OrigenDeCalidad.CodigoDian, ["48", "11"],
            $"48 Impuesto sobre las ventas (IVA); 11 Ventas régimen común (histórico). Con 49 o 53 (no responsable) ⇒ no. {Casilla53}"),
        new("autorretenedor", OrigenDeCalidad.CodigoDian, ["15"], $"15 Autorretenedor. {Casilla53}"),
        new("declaranteRenta", OrigenDeCalidad.CodigoDian, ["05", "04"],
            $"05 Renta régimen ordinario; 04 Renta régimen tributario especial. {Casilla53}"),
        new("granContribuyente", OrigenDeCalidad.CodigoDian, ["13"], $"13 Gran contribuyente. {Casilla53}"),
        new("autorretenedorIca", OrigenDeCalidad.DatoDeLaSeccion, [],
            "ICA es municipal y no está en la casilla 53: sale de autorretenedor_ica de la sección tributaria (null si no se diligenció)."),
        new("regimenSimple", OrigenDeCalidad.CodigoDian, ["47"], $"47 Régimen simple de tributación (SIMPLE). {Casilla53}"),
        new("granContribuyenteBogota", OrigenDeCalidad.NoDerivable, [],
            "Calidad distrital (Secretaría de Hacienda de Bogotá): no está en la casilla 53 ni en la ficha."),
        new("regimenEspecial", OrigenDeCalidad.CodigoDian, ["04"],
            $"04 Impuesto sobre la renta régimen tributario especial. {Casilla53}"),
        new("autorretenedorRft", OrigenDeCalidad.NoDerivable, [],
            "La autorretención especial a título de renta (Decreto 2201 de 2016) no tiene código propio en la casilla 53."),
        new("descuentoSenaFic", OrigenDeCalidad.NoDerivable, [],
            "Aportes parafiscales (SENA/FIC): no están en la casilla 53 ni en la ficha."),
        new("profesionalIndependiente", OrigenDeCalidad.NoDerivable, [],
            "Condición del pago (servicios personales), no una responsabilidad del RUT."),
        new("exentoRetencion", OrigenDeCalidad.NoDerivable, [],
            "La exención de retención la soporta un certificado o la ley, no la casilla 53."),
        new("agenteRetenedorIva", OrigenDeCalidad.CodigoDian, ["09", "23"],
            $"09 Retención en la fuente en el impuesto sobre las ventas; 23 Agente de retención en el impuesto sobre las ventas. El 07 es retención a título de renta y no cuenta. {Casilla53}"),
        new("obligadoAFacturar", OrigenDeCalidad.CodigoDian, ["52", "16", "37"],
            $"52 Facturador electrónico; 16 Obligación facturar por ingresos excluidos; 37 Obligado a facturar electrónicamente (histórico). {Casilla53}"),
        new("exentoReteCree", OrigenDeCalidad.DatoDeLaSeccion, [],
            "La exención de la retención del CREE no está en la casilla 53: sale de exento_rete_cree de la sección tributaria (null si no se diligenció).")
    ];

    /// <summary>Calidades que no se pueden derivar de la ficha (van como <c>null</c>), con su nota.</summary>
    public static IReadOnlyList<ReglaDeCalidad> NoDerivables => [.. Tabla.Where(r => r.Origen == OrigenDeCalidad.NoDerivable)];

    /// <summary>
    /// Función pura: las calidades de unas responsabilidades (códigos de la casilla 53, con o sin cero a la izquierda) y del
    /// <c>autorretenedor_ica</c> declarado. Lo derivable de un código es sí con el código y no sin él; lo no derivable, <c>null</c>.
    /// </summary>
    public static CalidadesTributarias Derivar(IEnumerable<string>? responsabilidades, bool? autorretenedorIca, bool? exentoReteCree = null)
    {
        var codigos = (responsabilidades ?? []).Select(c => (c ?? string.Empty).Trim().PadLeft(2, '0')).ToHashSet(StringComparer.Ordinal);

        bool? Valor(string calidad)
        {
            var regla = Tabla.First(r => r.Calidad == calidad);
            return regla.Origen switch
            {
                OrigenDeCalidad.CodigoDian => regla.Codigos.Any(codigos.Contains),
                OrigenDeCalidad.DatoDeLaSeccion => regla.Calidad == "exentoReteCree" ? exentoReteCree : autorretenedorIca,
                _ => null
            };
        }

        return new CalidadesTributarias(
            Valor("responsableIva"), Valor("autorretenedor"), Valor("declaranteRenta"), Valor("granContribuyente"), Valor("autorretenedorIca"),
            Valor("regimenSimple"), Valor("granContribuyenteBogota"), Valor("regimenEspecial"), Valor("autorretenedorRft"), Valor("descuentoSenaFic"),
            Valor("profesionalIndependiente"), Valor("exentoRetencion"), Valor("agenteRetenedorIva"), Valor("obligadoAFacturar"), Valor("exentoReteCree"));
    }

    /// <summary>Las calidades de los datos tributarios aceptados.</summary>
    public static CalidadesTributarias Derivar(DatosTributarios datos) => Derivar(datos.Responsabilidades, datos.AutorretenedorIca, datos.ExentoReteCree);

    /// <summary>El valor de una calidad por su clave (la de <see cref="ReglaDeCalidad.Calidad"/>).</summary>
    public bool? ValorDe(string calidad) => calidad switch
    {
        "responsableIva" => ResponsableIva,
        "autorretenedor" => Autorretenedor,
        "declaranteRenta" => DeclaranteRenta,
        "granContribuyente" => GranContribuyente,
        "autorretenedorIca" => AutorretenedorIca,
        "regimenSimple" => RegimenSimple,
        "granContribuyenteBogota" => GranContribuyenteBogota,
        "regimenEspecial" => RegimenEspecial,
        "autorretenedorRft" => AutorretenedorRft,
        "descuentoSenaFic" => DescuentoSenaFic,
        "profesionalIndependiente" => ProfesionalIndependiente,
        "exentoRetencion" => ExentoRetencion,
        "agenteRetenedorIva" => AgenteRetenedorIva,
        "obligadoAFacturar" => ObligadoAFacturar,
        "exentoReteCree" => ExentoReteCree,
        _ => throw new ArgumentOutOfRangeException(nameof(calidad), calidad, "Calidad tributaria desconocida.")
    };
}

/// <summary>De qué sección sale el representante legal principal.</summary>
public enum OrigenDelRepresentante
{
    /// <summary>Persona natural: el propio tercero, de la información general.</summary>
    General,

    /// <summary>Persona jurídica (o consorcio): una persona de la sección legal.</summary>
    Legal
}

/// <summary>El representante legal principal que recibe el ERP y de dónde salió (<see cref="Indice"/>: posición en <c>personas[]</c> de la legal).</summary>
public sealed record RepresentanteLegalPrincipal(OrigenDelRepresentante Origen, PersonaRelacionada Persona, int? Indice)
{
    /// <summary>
    /// La regla que ya usaban los sistemas de origen para el representante de un tercero: persona natural ⇒ el propio tercero (nombres, apellidos, identificación y contacto de la General); jurídica o consorcio
    /// ⇒ de las personas con rol <c>RepresentanteLegal</c>, la primera (menor orden) cuya representación es «Principal»; si
    /// ninguna lo es, la primera. Sin representantes ⇒ <c>null</c>. Función pura.
    /// </summary>
    public static RepresentanteLegalPrincipal? Elegir(TipoPersona? tipoPersona, DatosGenerales? general, DatosLegales? legal)
    {
        if ((tipoPersona ?? general?.TipoPersona) == TipoPersona.Natural)
        {
            return general is null
                ? null
                : new RepresentanteLegalPrincipal(OrigenDelRepresentante.General, new PersonaRelacionada
                {
                    Rol = RolPersonaRelacionada.RepresentanteLegal,
                    TipoIdentificacion = general.TipoIdentificacion,
                    Numero = general.Numero,
                    Dv = general.Dv,
                    Nombres = general.Nombres,
                    Apellidos = general.Apellidos,
                    Correo = general.Correo,
                    Telefono = general.Telefono
                }, null);
        }

        var representantes = (legal?.Personas ?? [])
            .Select((persona, indice) => (persona, indice))
            .Where(p => p.persona.Rol == RolPersonaRelacionada.RepresentanteLegal)
            .ToList();
        if (representantes.Count == 0)
            return null;

        var elegido = representantes.FirstOrDefault(p => EsPrincipal(p.persona.TipoRepresentacion));
        if (elegido.persona is null)
            elegido = representantes[0];
        return new RepresentanteLegalPrincipal(OrigenDelRepresentante.Legal, elegido.persona, elegido.indice);
    }

    /// <summary>«Principal» (sin importar mayúsculas ni tildes) al comienzo de la representación: «Principal», «principal (gerente)».</summary>
    public static bool EsPrincipal(string? tipoRepresentacion) =>
        ReglasDeCatalogo.NormalizarTexto(tipoRepresentacion).StartsWith("principal", StringComparison.Ordinal);

    /// <summary>Nombre completo (nombres y apellidos con un espacio).</summary>
    public string Nombre => string.Join(' ', new[] { Persona.Nombres, Persona.Apellidos }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
}
