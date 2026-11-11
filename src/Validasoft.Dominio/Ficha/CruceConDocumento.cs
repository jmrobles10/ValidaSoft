using System.Globalization;
using System.Text;
using System.Text.Json;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha;

/// <summary>Resultado del cruce de un campo de la sección con lo que dice el documento (FM-8, docs/14 §6).</summary>
public enum ResultadoDeCruce
{
    /// <summary>Lo declarado y lo extraído son lo mismo (con la normalización de la regla del campo).</summary>
    Coincide,

    /// <summary>Son distintos (o el documento trae el dato y la sección no lo tiene, o al revés en las filas).</summary>
    Discrepa,

    /// <summary>No se puede verificar: el documento no trae el dato, no se lee o es de otro periodo.</summary>
    NoVerificable
}

/// <summary>
/// Un campo cruzado: ruta snake_case de la sección (<c>razon_social</c>, <c>personas[0]</c>), etiqueta legible, lo declarado
/// y lo extraído como texto, el resultado, si es crítico (identificación, razón social, representante legal, matrícula) y
/// una nota para el revisor.
/// </summary>
public sealed record CampoCruzado(string Campo, string Etiqueta, string? Declarado, string? Extraido, ResultadoDeCruce Resultado, bool Critico,
    string? Detalle = null);

/// <summary>
/// Cruce de una versión de la sección con una versión de un documento de la bóveda que la respalda (FM-8): hecho compartido
/// de la versión de la sección, calculado una sola vez con los hechos de IA del documento (ADR-0012), nunca por empresa.
/// </summary>
public sealed record CruceDeSeccion(
    Guid DocumentoId,
    int VersionDocumento,
    string TipoDocumento,
    decimal Confianza,
    bool Legible,
    IReadOnlyList<CampoCruzado> Campos,
    DateTimeOffset Fecha)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int Coincidencias => Campos.Count(c => c.Resultado == ResultadoDeCruce.Coincide);

    [System.Text.Json.Serialization.JsonIgnore]
    public int Discrepancias => Campos.Count(c => c.Resultado == ResultadoDeCruce.Discrepa);

    [System.Text.Json.Serialization.JsonIgnore]
    public int NoVerificables => Campos.Count(c => c.Resultado == ResultadoDeCruce.NoVerificable);

    /// <summary>Un campo crítico que discrepa: no se aprueba en lote ni sin justificación (FM-13).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool DiscrepanciaCritica => Campos.Any(c => c.Critico && c.Resultado == ResultadoDeCruce.Discrepa);

    public bool Es(Guid documentoId, int version) => DocumentoId == documentoId && VersionDocumento == version;
}

/// <summary>Se cruzó la versión <see cref="Numero"/> de la sección con una versión de documento (docs/14 §7).</summary>
public sealed record CruceDeSeccionRegistrado(int Numero, CruceDeSeccion Cruce);

/// <summary>
/// Mapa de plataforma (FM-13, docs/14 §5.1): qué secciones respalda cada tipo canónico de documento. Es el mismo que usan el
/// cruce, la opción «Usar como soporte» de los requisitos de documento y la actualización desde el documento. La cédula respalda la
/// General de la persona natural y, en la jurídica, las personas de la Legal (el representante; FD-6); la certificación bancaria
/// respalda las <b>cuentas bancarias</b> del tercero (FD-5), que no son una sección de la ficha sino su propia sección de cuentas
/// (<see cref="CodigosDeSeccion.CuentasBancarias"/>): por eso <see cref="Secciones"/> solo devuelve secciones de la ficha y la de
/// cuentas va aparte (<see cref="RespaldaCuentas"/>).
/// </summary>
public static class MapaDeRespaldo
{
    private static readonly Dictionary<string, string[]> PorTipo = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RUT"] = [CodigosDeSeccion.General, CodigosDeSeccion.Tributaria, CodigosDeSeccion.Legal],
        ["CAMARA_COMERCIO"] = [CodigosDeSeccion.General, CodigosDeSeccion.Legal],
        ["ESTADOS_FINANCIEROS"] = [CodigosDeSeccion.Financiera],
        ["CEDULA"] = [CodigosDeSeccion.General, CodigosDeSeccion.Legal]
    };

    /// <summary>Tipos de documento que respaldan las cuentas bancarias del tercero (docs/14 §5.1: la certificación, CB-1).</summary>
    private static readonly string[] TiposDeCuentas = ["CERTIFICACION_BANCARIA"];

    /// <summary>Secciones de la ficha que respalda el tipo de documento (sin la de cuentas).</summary>
    public static IReadOnlyList<string> Secciones(string? tipoCanonico) =>
        tipoCanonico is not null && PorTipo.TryGetValue(tipoCanonico, out var secciones) ? secciones : [];

    /// <summary>¿El tipo respalda las cuentas bancarias del tercero (certificación bancaria)?</summary>
    public static bool RespaldaCuentas(string? tipoCanonico) =>
        tipoCanonico is not null && TiposDeCuentas.Contains(tipoCanonico, StringComparer.OrdinalIgnoreCase);

    public static bool RespaldaAlguna(string? tipoCanonico) => Secciones(tipoCanonico).Count > 0 || RespaldaCuentas(tipoCanonico);

    public static bool Respalda(string? tipoCanonico, string seccion) =>
        seccion == CodigosDeSeccion.CuentasBancarias ? RespaldaCuentas(tipoCanonico) : Secciones(tipoCanonico).Contains(seccion, StringComparer.Ordinal);

    /// <summary>Tipos de documento que respaldan la sección.</summary>
    public static IReadOnlyList<string> TiposPara(string seccion) =>
        seccion == CodigosDeSeccion.CuentasBancarias ? TiposDeCuentas : [.. PorTipo.Where(p => p.Value.Contains(seccion)).Select(p => p.Key)];

    /// <summary>
    /// Periodo que cubre el documento (los estados financieros dicen de qué año son); nulo si no aplica o no se leyó. Con él se
    /// elige la financiera que respalda un documento que no fue adjuntado como soporte.
    /// </summary>
    public static int? Periodo(IReadOnlyDictionary<string, JsonElement> extraidos) =>
        LecturaDeHechos.Texto(extraidos, "periodo") is { } texto && texto.Length >= 4 &&
        int.TryParse(texto.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var anio)
            ? anio
            : null;
}

/// <summary>
/// Cruce sección ↔ documento (FM-8, docs/14 §6): compara campo por campo lo declarado en una versión de la sección con lo que
/// la IA extrajo del documento. Reglas: identificación exacta (solo dígitos); nombres sin mayúsculas, tildes ni puntuación
/// (S.A.S. = SAS) y sin importar el orden de las palabras; ciudad por código DANE; responsabilidades y CIIU como conjuntos;
/// cifras con diferencia ≤ 0,5 %; representantes declarados contra los del documento y viceversa. Si el documento no es
/// legible, todo queda <see cref="ResultadoDeCruce.NoVerificable"/>. Es evidencia: no decide nada. Función pura.
/// </summary>
public static class CruceConDocumento
{
    /// <summary>Tolerancia de las cifras financieras (0,5 %).</summary>
    public const decimal ToleranciaCifras = 0.005m;

    /// <summary>
    /// Cruza la versión de la sección (<paramref name="datos"/>: JSON canónico) con los campos extraídos del documento. Nulo si
    /// el tipo de documento no respalda la sección o no hay nada que comparar.
    /// </summary>
    public static CruceDeSeccion? Cruzar(string seccion, JsonElement datos, TipoPersona tipoPersona, int? periodo, string tipoDocumento, Guid documentoId,
        int versionDocumento, IReadOnlyDictionary<string, JsonElement> extraidos, decimal confianza, bool legible, DateTimeOffset fecha)
    {
        if (!MapaDeRespaldo.Respalda(tipoDocumento, seccion))
            return null;

        var campos = Campos(seccion, datos, tipoPersona, periodo, tipoDocumento.ToUpperInvariant(), extraidos);
        if (campos.Count == 0)
            return null;
        if (!legible)
            campos = [.. campos.Select(c => c with { Resultado = ResultadoDeCruce.NoVerificable, Detalle = "El documento no se lee con seguridad." })];
        return new CruceDeSeccion(documentoId, versionDocumento, tipoDocumento.ToUpperInvariant(), confianza, legible, campos, fecha);
    }

    private static List<CampoCruzado> Campos(string seccion, JsonElement datos, TipoPersona tipoPersona, int? periodo, string tipo,
        IReadOnlyDictionary<string, JsonElement> x) => seccion switch
    {
        CodigosDeSeccion.General => General(Leer<DatosGenerales>(datos), tipoPersona, tipo, x),
        CodigosDeSeccion.Tributaria when tipo == "RUT" => Tributaria(Leer<DatosTributarios>(datos), x),
        CodigosDeSeccion.Legal when tipoPersona != TipoPersona.Natural => Legal(Leer<DatosLegales>(datos), tipo, x),
        CodigosDeSeccion.Financiera when tipo == "ESTADOS_FINANCIEROS" => Financiera(Leer<DatosFinancieros>(datos), periodo, x),
        _ => []
    };

    private static T Leer<T>(JsonElement datos) where T : class, new() =>
        datos.ValueKind == JsonValueKind.Object ? JsonDeFicha.Deserializar<T>(datos) : new T();

    // ---------- Por sección ----------

    private static List<CampoCruzado> General(DatosGenerales g, TipoPersona tipoPersona, string tipo, IReadOnlyDictionary<string, JsonElement> x)
    {
        var campos = new List<CampoCruzado>();
        var natural = tipoPersona == TipoPersona.Natural;
        switch (tipo)
        {
            case "RUT":
                Agregar(campos, "numero", "Número de identificación", g.Numero, LecturaDeHechos.Texto(x, "nit"), Comparador.Identificacion, true);
                if (g.Dv is not null)
                    Agregar(campos, "dv", "DV", g.Dv, LecturaDeHechos.Texto(x, "dv"), Comparador.Identificacion, false);
                if (natural)
                    Agregar(campos, "nombres", "Nombres y apellidos", NombreCompleto(g.Nombres, g.Apellidos), LecturaDeHechos.Texto(x, "razonSocial"),
                        Comparador.Nombre, true);
                else
                    Agregar(campos, "razon_social", "Razón social", g.RazonSocial, LecturaDeHechos.Texto(x, "razonSocial"), Comparador.Nombre, true);
                Agregar(campos, "tipo_persona", "Tipo de persona", g.TipoPersona is { } tp ? TipoDePersona(tp) : null,
                    TipoDeContribuyente(LecturaDeHechos.Texto(x, "tipoContribuyente")), Comparador.Texto, false);
                Agregar(campos, "direccion", "Dirección", g.Direccion, LecturaDeHechos.Texto(x, "direccion"), Comparador.Direccion, false);
                Agregar(campos, "ciudad", "Ciudad (código DANE)", g.Ciudad, LecturaDeHechos.Texto(x, "codigoMunicipio"), Comparador.Codigo, false);
                Agregar(campos, "correo", "Correo electrónico", g.Correo, LecturaDeHechos.Texto(x, "correo"), Comparador.Correo, false);
                Agregar(campos, "telefono", "Teléfono", g.Telefono, LecturaDeHechos.Texto(x, "telefono"), Comparador.Telefono, false);
                Agregar(campos, "ciiu_principal", "Actividad económica principal (CIIU)", g.CiiuPrincipal,
                    LecturaDeHechos.Texto(x, "actividadEconomicaPrincipal"), Comparador.Codigo, false);
                AgregarConjunto(campos, "ciiu_secundarias", "Actividades económicas secundarias", g.CiiuSecundarias,
                    LecturaDeHechos.Lista(x, "actividadesSecundarias"), 4);
                break;
            case "CAMARA_COMERCIO" when !natural:
                Agregar(campos, "numero", "Número de identificación", g.Numero, LecturaDeHechos.Texto(x, "nit"), Comparador.Identificacion, true);
                Agregar(campos, "razon_social", "Razón social", g.RazonSocial, LecturaDeHechos.Texto(x, "razonSocial"), Comparador.Nombre, true);
                break;
            case "CEDULA" when natural:
                Agregar(campos, "numero", "Número de identificación", g.Numero, LecturaDeHechos.Texto(x, "numero"), Comparador.Identificacion, true);
                Agregar(campos, "nombres", "Nombres", g.Nombres, LecturaDeHechos.Texto(x, "nombres"), Comparador.Nombre, true);
                Agregar(campos, "apellidos", "Apellidos", g.Apellidos, LecturaDeHechos.Texto(x, "apellidos"), Comparador.Nombre, true);
                // La fecha de expedición solo se compara si el tercero la declaró (FD-8): la propuesta desde la cédula es la que invita a llenarla.
                if (g.FechaExpedicion is { } expedicion)
                    Agregar(campos, "fecha_expedicion", "Fecha de expedición", expedicion.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        LecturaDeHechos.Texto(x, "fechaExpedicion"), Comparador.Texto, false);
                break;
        }

        return campos;
    }

    private static List<CampoCruzado> Tributaria(DatosTributarios t, IReadOnlyDictionary<string, JsonElement> x)
    {
        var campos = new List<CampoCruzado>();
        AgregarConjunto(campos, "responsabilidades", "Responsabilidades (casilla 53 del RUT)", t.Responsabilidades,
            LecturaDeHechos.Lista(x, "responsabilidades"), 2);
        return campos;
    }

    private static List<CampoCruzado> Legal(DatosLegales l, string tipo, IReadOnlyDictionary<string, JsonElement> x)
    {
        var campos = new List<CampoCruzado>();
        var personas = l.Personas ?? [];
        if (tipo == "RUT")
        {
            // El RUT inscribe al representante legal principal: debe ser el principal de la sección (FM-34).
            var principal = RepresentanteLegalPrincipal.Elegir(TipoPersona.Juridica, null, l);
            var nombre = LecturaDeHechos.Texto(x, "representanteLegalNombre");
            var identificacion = LecturaDeHechos.Texto(x, "representanteLegalIdentificacion");
            if (principal is null && nombre is null && identificacion is null)
                return campos;
            var campo = principal?.Indice is { } i ? $"personas[{i}]" : "personas";
            var declarado = principal is null ? null : Resumen(principal.Nombre, principal.Persona.Numero);
            var extraido = nombre is null && identificacion is null ? null : Resumen(nombre, identificacion);
            var resultado = principal is null || extraido is null
                ? (principal is null ? ResultadoDeCruce.Discrepa : ResultadoDeCruce.NoVerificable)
                : MismaPersona(principal.Nombre, principal.Persona.Numero, nombre, identificacion) ? ResultadoDeCruce.Coincide : ResultadoDeCruce.Discrepa;
            campos.Add(new CampoCruzado(campo, "Representante legal principal", declarado, extraido, resultado, true,
                resultado == ResultadoDeCruce.NoVerificable ? "El documento no trae el representante."
                : principal is null ? "La sección no tiene representante legal." : null));
            return campos;
        }

        // La cédula respalda a la persona de la legal a la que pertenece (el representante; FD-6): se cruza con esa fila.
        if (tipo == "CEDULA")
            return LegalConCedula(personas, x);

        if (tipo != "CAMARA_COMERCIO")
            return campos;

        // Cada representante declarado aparece en el certificado y viceversa.
        var enDocumento = LecturaDeHechos.Personas(x, "representantesLegales");
        var declarados = personas.Select((p, i) => (Persona: p, Indice: i))
            .Where(p => p.Persona.Rol is RolPersonaRelacionada.RepresentanteLegal or RolPersonaRelacionada.Suplente)
            .ToList();
        var usados = new HashSet<int>();
        foreach (var (persona, indice) in declarados)
        {
            var nombre = NombreCompleto(persona.Nombres, persona.Apellidos);
            var etiqueta = persona.Rol == RolPersonaRelacionada.Suplente ? "Representante legal suplente" : "Representante legal";
            if (enDocumento is null)
            {
                campos.Add(new CampoCruzado($"personas[{indice}]", etiqueta, Resumen(nombre, persona.Numero), null, ResultadoDeCruce.NoVerificable, true,
                    "El documento no trae representantes."));
                continue;
            }

            var encontrada = enDocumento.Select((p, i) => (p, i))
                .FirstOrDefault(d => !usados.Contains(d.i) && MismaPersona(nombre, persona.Numero, d.p.Nombre, d.p.Identificacion));
            if (encontrada.p is not null)
            {
                usados.Add(encontrada.i);
                campos.Add(new CampoCruzado($"personas[{indice}]", etiqueta, Resumen(nombre, persona.Numero),
                    Resumen(encontrada.p.Nombre, encontrada.p.Identificacion, encontrada.p.Cargo), ResultadoDeCruce.Coincide, true));
            }
            else
            {
                campos.Add(new CampoCruzado($"personas[{indice}]", etiqueta, Resumen(nombre, persona.Numero), null, ResultadoDeCruce.Discrepa, true,
                    "No figura en el documento."));
            }
        }

        foreach (var persona in (enDocumento ?? []).Where((_, i) => !usados.Contains(i)))
        {
            campos.Add(new CampoCruzado("personas", "Representante legal", null, Resumen(persona.Nombre, persona.Identificacion, persona.Cargo),
                ResultadoDeCruce.Discrepa, true, "Figura en el documento y no en la sección."));
        }

        var estado = LecturaDeHechos.Texto(x, "estadoMatricula");
        var activa = estado is null ? (bool?)null : Normalizacion.Texto(estado) is var e && e.Contains("ACTIV", StringComparison.Ordinal) &&
                                                    !e.Contains("INACTIV", StringComparison.Ordinal);
        campos.Add(new CampoCruzado("estado_matricula", "Estado de la matrícula mercantil", "Activa", estado,
            activa switch { null => ResultadoDeCruce.NoVerificable, true => ResultadoDeCruce.Coincide, false => ResultadoDeCruce.Discrepa }, true,
            activa == false ? "La matrícula no está activa." : null));
        return campos;
    }

    private static List<CampoCruzado> Financiera(DatosFinancieros f, int? periodo, IReadOnlyDictionary<string, JsonElement> x)
    {
        var campos = new List<CampoCruzado>();
        var delDocumento = MapaDeRespaldo.Periodo(x);
        var periodoSeccion = f.Periodo ?? periodo;
        if (delDocumento is { } p && periodoSeccion is { } s && p != s)
        {
            campos.Add(new CampoCruzado("periodo", "Periodo", s.ToString(CultureInfo.InvariantCulture), p.ToString(CultureInfo.InvariantCulture),
                ResultadoDeCruce.Discrepa, true, "Los estados financieros son de otro periodo."));
            return campos;
        }

        Agregar(campos, "activos", "Activos", Cifra(f.Activos), LecturaDeHechos.Texto(x, "activoTotal"), Comparador.Cifra, false);
        Agregar(campos, "pasivos", "Pasivos", Cifra(f.Pasivos), LecturaDeHechos.Texto(x, "pasivoTotal"), Comparador.Cifra, false);
        Agregar(campos, "patrimonio", "Patrimonio", Cifra(f.Patrimonio), LecturaDeHechos.Texto(x, "patrimonio"), Comparador.Cifra, false);
        Agregar(campos, "ingresos_brutos", "Ingresos brutos", Cifra(f.IngresosBrutos), LecturaDeHechos.Texto(x, "ingresosBrutos"), Comparador.Cifra,
            false);
        Agregar(campos, "fecha_corte", "Fecha de corte", f.FechaCorte?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            LecturaDeHechos.Texto(x, "fechaCorte"), Comparador.Texto, false);
        return campos;
    }

    /// <summary>
    /// La cédula frente a las personas de la información legal (FD-6): se busca la persona a la que pertenece (por número; si alguno no
    /// lo trae, por nombre) y se cruzan su número y nombre (críticos) y, si la persona los declaró, las fechas de expedición y de
    /// nacimiento. Si no corresponde a ninguna persona, discrepa en un solo campo (la cédula es de alguien que no figura).
    /// </summary>
    private static List<CampoCruzado> LegalConCedula(IReadOnlyList<PersonaRelacionada> personas, IReadOnlyDictionary<string, JsonElement> x)
    {
        var campos = new List<CampoCruzado>();
        var numero = LecturaDeHechos.Texto(x, "numero");
        var nombreCedula = NombreCompleto(LecturaDeHechos.Texto(x, "nombres"), LecturaDeHechos.Texto(x, "apellidos"));
        if (numero is null && nombreCedula is null)
            return campos;

        var encontrada = personas.Select((p, i) => (Persona: p, Indice: i))
            .FirstOrDefault(d => MismaPersona(NombreCompleto(d.Persona.Nombres, d.Persona.Apellidos), d.Persona.Numero, nombreCedula, numero));
        if (encontrada.Persona is null)
        {
            campos.Add(new CampoCruzado("personas", "Persona de la cédula", null, Resumen(nombreCedula, numero), ResultadoDeCruce.Discrepa, true,
                "La cédula no corresponde a ninguna persona de la información legal."));
            return campos;
        }

        var (persona, indice) = encontrada;
        var rol = persona.Rol is { } r ? RolLegible(r) : "Persona";
        Agregar(campos, $"personas[{indice}].numero", $"{rol} · Número de identificación", persona.Numero, numero, Comparador.Identificacion, true);
        Agregar(campos, $"personas[{indice}]", $"{rol} · Nombre", NombreCompleto(persona.Nombres, persona.Apellidos), nombreCedula, Comparador.Nombre, true);
        if (persona.FechaExpedicion is { } expedicion)
            Agregar(campos, $"personas[{indice}].fecha_expedicion", $"{rol} · Fecha de expedición", expedicion.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LecturaDeHechos.Texto(x, "fechaExpedicion"), Comparador.Texto, false);
        if (persona.FechaNacimiento is { } nacimiento)
            Agregar(campos, $"personas[{indice}].fecha_nacimiento", $"{rol} · Fecha de nacimiento", nacimiento.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                LecturaDeHechos.Texto(x, "fechaNacimiento"), Comparador.Texto, false);
        return campos;
    }

    private static string RolLegible(RolPersonaRelacionada rol) => rol switch
    {
        RolPersonaRelacionada.RepresentanteLegal => "Representante legal",
        RolPersonaRelacionada.Suplente => "Suplente",
        RolPersonaRelacionada.Apoderado => "Apoderado",
        RolPersonaRelacionada.Socio => "Socio",
        RolPersonaRelacionada.JuntaDirectiva => "Junta directiva",
        RolPersonaRelacionada.RevisorFiscal => "Revisor fiscal",
        _ => "Contador"
    };

    // ---------- Cuentas bancarias contra la certificación (FD-5, docs/14 §6) ----------

    /// <summary>
    /// Cruza una cuenta bancaria del tercero con lo que dice su certificación bancaria (docs/14 §6, fila «Bancaria»): el número debe
    /// ser exacto, el titular debe ser el tercero y la cuenta debe estar activa (críticos); el banco y el tipo de cuenta son evidencia.
    /// El número completo solo existe en memoria: en el resultado va <b>siempre enmascarado</b> (CB-2). Función pura.
    /// </summary>
    public static CruceDeSeccion CruzarCuenta(DatosDeCuentaParaCruce cuenta, string terceroNombre, string terceroIdentificacion, Guid documentoId,
        int versionDocumento, IReadOnlyDictionary<string, JsonElement> extraidos, decimal confianza, bool legible, DateTimeOffset fecha)
    {
        var campos = new List<CampoCruzado>();
        var numeroDocumento = LecturaDeHechos.Texto(extraidos, "numeroCuenta");
        var enmascarado = Enmascarar(cuenta.Numero);

        if (numeroDocumento is null)
            campos.Add(new CampoCruzado("numero", "Número de la cuenta", enmascarado, null, ResultadoDeCruce.NoVerificable, true, "El documento no trae el dato."));
        else
            campos.Add(new CampoCruzado("numero", "Número de la cuenta", enmascarado, Enmascarar(numeroDocumento),
                Normalizacion.Identificacion(cuenta.Numero) == Normalizacion.Identificacion(numeroDocumento) ? ResultadoDeCruce.Coincide : ResultadoDeCruce.Discrepa,
                true));

        Agregar(campos, "titular", "Titular de la cuenta", cuenta.Titular, LecturaDeHechos.Texto(extraidos, "titular"), Comparador.Nombre, true);
        var identificacionDocumento = LecturaDeHechos.Texto(extraidos, "identificacionTitular");
        if (identificacionDocumento is null)
        {
            campos.Add(new CampoCruzado("identificacion_titular", "Identificación del titular", cuenta.IdentificacionTitular, null, ResultadoDeCruce.NoVerificable, true,
                "El documento no trae el dato."));
        }
        else
        {
            campos.Add(new CampoCruzado("identificacion_titular", "Identificación del titular", cuenta.IdentificacionTitular, identificacionDocumento,
                MismaIdentificacion(cuenta.IdentificacionTitular, identificacionDocumento) ? ResultadoDeCruce.Coincide : ResultadoDeCruce.Discrepa, true));
            // El titular debe ser el propio tercero: la certificación dice de quién es la cuenta, no lo que el tercero declaró de ella.
            var esDelTercero = MismaIdentificacion(terceroIdentificacion, identificacionDocumento);
            campos.Add(new CampoCruzado("titular_es_el_tercero", "El titular es el tercero", $"{terceroNombre} ({terceroIdentificacion})", identificacionDocumento,
                esDelTercero ? ResultadoDeCruce.Coincide : ResultadoDeCruce.Discrepa, true,
                esDelTercero ? null : "La cuenta que certifica el banco no es del tercero."));
        }

        var estado = LecturaDeHechos.Texto(extraidos, "estadoCuenta");
        var activa = estado is null ? (bool?)null : EstaActiva(estado);
        campos.Add(new CampoCruzado("estado_cuenta", "Estado de la cuenta", "Activa", estado,
            activa switch { null => ResultadoDeCruce.NoVerificable, true => ResultadoDeCruce.Coincide, false => ResultadoDeCruce.Discrepa }, true,
            activa == false ? "La cuenta no está activa." : null));

        AgregarBanco(campos, cuenta.Banco, LecturaDeHechos.Texto(extraidos, "banco"));
        AgregarTipoDeCuenta(campos, cuenta.TipoCuenta, LecturaDeHechos.Texto(extraidos, "tipoCuenta"));

        if (!legible)
            campos = [.. campos.Select(c => c with { Resultado = ResultadoDeCruce.NoVerificable, Detalle = "El documento no se lee con seguridad." })];
        return new CruceDeSeccion(documentoId, versionDocumento, "CERTIFICACION_BANCARIA", confianza, legible, campos, fecha);
    }

    /// <summary>«****1234»: del número completo solo se muestran los últimos cuatro dígitos (CB-2).</summary>
    public static string Enmascarar(string numero)
    {
        var digitos = new string(numero.Where(char.IsDigit).ToArray());
        return $"****{(digitos.Length <= 4 ? digitos : digitos[^4..])}";
    }

    /// <summary>
    /// La misma identificación, con o sin el dígito de verificación («900123456» y «900.123.456-1») y con o sin el tipo escrito delante
    /// («NIT 900123456»): se comparan los dígitos; si alguna no tiene (un pasaporte), los caracteres.
    /// </summary>
    private static bool MismaIdentificacion(string a, string b)
    {
        var x = Normalizacion.Identificacion(new string(a.Where(char.IsDigit).ToArray()));
        var y = Normalizacion.Identificacion(new string(b.Where(char.IsDigit).ToArray()));
        if (x.Length == 0 || y.Length == 0)
        {
            x = Normalizacion.Identificacion(a);
            y = Normalizacion.Identificacion(b);
        }

        return x.Length > 0 && y.Length > 0 && (x == y || (y.Length == x.Length + 1 && y.StartsWith(x, StringComparison.Ordinal)) ||
                                              (x.Length == y.Length + 1 && x.StartsWith(y, StringComparison.Ordinal)));
    }

    private static bool EstaActiva(string estado)
    {
        var texto = Normalizacion.Texto(estado);
        return texto.Contains("ACTIV", StringComparison.Ordinal) &&
               !new[] { "INACTIV", "CANCEL", "CERRAD", "BLOQUE", "EMBARG" }.Any(m => texto.Contains(m, StringComparison.Ordinal));
    }

    /// <summary>El banco: el nombre más corto está en el más largo («Bancolombia» y «Bancolombia S.A.»). Evidencia, no crítico.</summary>
    private static void AgregarBanco(List<CampoCruzado> campos, string declarado, string? extraido)
    {
        if (extraido is null)
        {
            campos.Add(new CampoCruzado("banco", "Banco", declarado, null, ResultadoDeCruce.NoVerificable, false, "El documento no trae el dato."));
            return;
        }

        var a = Normalizacion.Palabras(declarado).Where(p => p.Length > 1).ToHashSet();
        var b = Normalizacion.Palabras(extraido).Where(p => p.Length > 1).ToHashSet();
        var coincide = a.Count > 0 && b.Count > 0 && (a.IsSubsetOf(b) || b.IsSubsetOf(a));
        campos.Add(new CampoCruzado("banco", "Banco", declarado, extraido, coincide ? ResultadoDeCruce.Coincide : ResultadoDeCruce.Discrepa, false));
    }

    /// <summary>El tipo de cuenta: «Ahorros» está en «Cuenta de ahorros». Evidencia, no crítico.</summary>
    private static void AgregarTipoDeCuenta(List<CampoCruzado> campos, string declarado, string? extraido)
    {
        if (extraido is null)
        {
            campos.Add(new CampoCruzado("tipo_cuenta", "Tipo de cuenta", declarado, null, ResultadoDeCruce.NoVerificable, false, "El documento no trae el dato."));
            return;
        }

        var coincide = Normalizacion.Texto(extraido).Contains(Normalizacion.Texto(declarado), StringComparison.Ordinal);
        campos.Add(new CampoCruzado("tipo_cuenta", "Tipo de cuenta", declarado, extraido, coincide ? ResultadoDeCruce.Coincide : ResultadoDeCruce.Discrepa, false));
    }

    // ---------- Comparación de un campo ----------

    internal enum Comparador
    {
        Identificacion,
        Nombre,
        Texto,
        Direccion,
        Codigo,
        Correo,
        Telefono,
        Cifra
    }

    /// <summary>
    /// Agrega el campo si hay algo que comparar: declarado y extraído ⇒ coincide o discrepa; solo extraído ⇒ discrepa (la
    /// sección no lo tiene); solo declarado ⇒ no verificable (el documento no lo trae); ninguno ⇒ no se agrega.
    /// </summary>
    private static void Agregar(List<CampoCruzado> campos, string campo, string etiqueta, string? declarado, string? extraido, Comparador comparador,
        bool critico)
    {
        declarado = string.IsNullOrWhiteSpace(declarado) ? null : declarado.Trim();
        extraido = string.IsNullOrWhiteSpace(extraido) ? null : extraido.Trim();
        if (declarado is null && extraido is null)
            return;
        if (extraido is null)
        {
            campos.Add(new CampoCruzado(campo, etiqueta, declarado, null, ResultadoDeCruce.NoVerificable, critico, "El documento no trae el dato."));
            return;
        }

        if (declarado is null)
        {
            campos.Add(new CampoCruzado(campo, etiqueta, null, extraido, ResultadoDeCruce.Discrepa, critico, "La sección no lo tiene diligenciado."));
            return;
        }

        var (coincide, detalle) = Comparar(declarado, extraido, comparador);
        campos.Add(new CampoCruzado(campo, etiqueta, declarado, extraido, coincide ? ResultadoDeCruce.Coincide : ResultadoDeCruce.Discrepa, critico,
            detalle));
    }

    private static void AgregarConjunto(List<CampoCruzado> campos, string campo, string etiqueta, IReadOnlyList<string>? declarados,
        IReadOnlyList<string>? extraidos, int digitos)
    {
        // Una lista vacía del documento es «no trae ninguno»: no se puede verificar (no es una discrepancia).
        var a = Normalizacion.Codigos(declarados, digitos);
        var b = Normalizacion.Codigos(extraidos, digitos);
        if (a.Count == 0 && b.Count == 0)
            return;
        var declarado = a.Count == 0 ? null : string.Join(", ", a);
        var extraido = b.Count == 0 ? null : string.Join(", ", b);
        if (b.Count == 0)
        {
            campos.Add(new CampoCruzado(campo, etiqueta, declarado, null, ResultadoDeCruce.NoVerificable, false, "El documento no trae el dato."));
            return;
        }

        if (a.SetEquals(b))
        {
            campos.Add(new CampoCruzado(campo, etiqueta, declarado, extraido, ResultadoDeCruce.Coincide, false));
            return;
        }

        var faltan = b.Except(a).ToList();
        var sobran = a.Except(b).ToList();
        var detalle = string.Join("; ", new[]
        {
            faltan.Count > 0 ? $"El documento trae {string.Join(", ", faltan)}" : null,
            sobran.Count > 0 ? $"El documento no trae {string.Join(", ", sobran)}" : null
        }.OfType<string>()) + ".";
        campos.Add(new CampoCruzado(campo, etiqueta, declarado, extraido, ResultadoDeCruce.Discrepa, false, detalle));
    }

    internal static (bool Coincide, string? Detalle) Comparar(string declarado, string extraido, Comparador comparador)
    {
        switch (comparador)
        {
            case Comparador.Identificacion:
                return (Normalizacion.Identificacion(declarado) == Normalizacion.Identificacion(extraido), null);
            case Comparador.Nombre:
                return (Normalizacion.MismasPalabras(declarado, extraido), null);
            case Comparador.Direccion:
                return (Normalizacion.Direccion(declarado) == Normalizacion.Direccion(extraido), null);
            case Comparador.Codigo:
                return (Normalizacion.Identificacion(declarado) == Normalizacion.Identificacion(extraido), null);
            case Comparador.Correo:
                return (string.Equals(declarado.Trim(), extraido.Trim(), StringComparison.OrdinalIgnoreCase), null);
            case Comparador.Telefono:
                return (Normalizacion.Telefono(declarado) == Normalizacion.Telefono(extraido), null);
            case Comparador.Cifra:
                if (!Normalizacion.Decimal(declarado, out var a) || !Normalizacion.Decimal(extraido, out var b))
                    return (false, "La cifra del documento no se pudo leer.");
                var diferencia = Math.Abs(a - b);
                var base_ = Math.Max(Math.Abs(a), Math.Abs(b));
                if (diferencia == 0)
                    return (true, null);
                var porcentaje = base_ == 0 ? 1m : diferencia / base_;
                var texto = $"Diferencia de {(porcentaje * 100).ToString("0.##", CultureInfo.GetCultureInfo("es-CO"))} %.";
                return (porcentaje <= ToleranciaCifras, texto);
            default:
                return (Normalizacion.Texto(declarado) == Normalizacion.Texto(extraido), null);
        }
    }

    // ---------- Utilidades ----------

    internal static string? NombreCompleto(string? nombres, string? apellidos)
    {
        var texto = string.Join(' ', new[] { nombres, apellidos }.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim()));
        return texto.Length == 0 ? null : texto;
    }

    private static string TipoDePersona(TipoPersona tipo) => tipo == TipoPersona.Natural ? "Natural" : "Jurídica";

    /// <summary>«Persona jurídica» ⇒ Jurídica; «Persona natural o sucesión ilíquida» ⇒ Natural.</summary>
    private static string? TipoDeContribuyente(string? texto) =>
        texto is null ? null
        : Normalizacion.Texto(texto).Contains("JURIDIC", StringComparison.Ordinal) ? "Jurídica"
        : Normalizacion.Texto(texto).Contains("NATURAL", StringComparison.Ordinal) ? "Natural"
        : texto;

    private static string? Cifra(decimal? valor) => valor?.ToString(CultureInfo.InvariantCulture);

    internal static string? Resumen(string? nombre, string? identificacion, string? cargo = null)
    {
        var partes = new StringBuilder(nombre?.Trim() ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(identificacion))
            partes.Append(partes.Length > 0 ? " " : string.Empty).Append('(').Append(identificacion.Trim()).Append(')');
        if (!string.IsNullOrWhiteSpace(cargo))
            partes.Append(" — ").Append(cargo.Trim());
        return partes.Length == 0 ? null : partes.ToString();
    }

    /// <summary>La misma persona: por identificación si las dos la tienen; si no, por nombre (mismas palabras).</summary>
    internal static bool MismaPersona(string? nombreA, string? idA, string? nombreB, string? idB)
    {
        var a = Normalizacion.Identificacion(idA);
        var b = Normalizacion.Identificacion(idB);
        if (a.Length > 0 && b.Length > 0)
            return a == b;
        return nombreA is not null && nombreB is not null && Normalizacion.MismasPalabras(nombreA, nombreB);
    }
}

/// <summary>Persona que extrajo la IA de un documento (nombre, identificación y cargo).</summary>
public sealed record PersonaExtraida(string? Nombre, string? Identificacion, string? Cargo);

/// <summary>Lectura tolerante de los campos extraídos por la IA (JSON del esquema del tipo, IA-12).</summary>
public static class LecturaDeHechos
{
    /// <summary>Texto (o número como texto); nulo si no viene, es nulo o vacío.</summary>
    public static string? Texto(IReadOnlyDictionary<string, JsonElement> campos, string clave)
    {
        if (!campos.TryGetValue(clave, out var valor))
            return null;
        var texto = valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    /// <summary>Lista de textos: un arreglo JSON o un texto separado por comas; nula si no viene.</summary>
    public static IReadOnlyList<string>? Lista(IReadOnlyDictionary<string, JsonElement> campos, string clave)
    {
        if (!campos.TryGetValue(clave, out var valor))
            return null;
        return valor.ValueKind switch
        {
            JsonValueKind.Array => [.. valor.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText())
                .Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim())],
            JsonValueKind.String => [.. (valor.GetString() ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            _ => null
        };
    }

    /// <summary>Lista de personas (<c>nombre</c>, <c>identificacion</c>, <c>cargo</c>); nula si no viene.</summary>
    public static IReadOnlyList<PersonaExtraida>? Personas(IReadOnlyDictionary<string, JsonElement> campos, string clave)
    {
        if (!campos.TryGetValue(clave, out var valor) || valor.ValueKind != JsonValueKind.Array)
            return null;
        return
        [
            .. valor.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.Object).Select(p => new PersonaExtraida(Propiedad(p, "nombre"),
                Propiedad(p, "identificacion"), Propiedad(p, "cargo"))).Where(p => p.Nombre is not null || p.Identificacion is not null)
        ];
    }

    private static string? Propiedad(JsonElement objeto, string clave) =>
        objeto.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()!.Trim() : null;
}

/// <summary>Normalizaciones del cruce (docs/14 §6).</summary>
public static class Normalizacion
{
    private static readonly Dictionary<string, string> AbreviaturasDeVia = new(StringComparer.Ordinal)
    {
        ["CARRERA"] = "KR", ["CRA"] = "KR", ["CR"] = "KR", ["KRA"] = "KR", ["CRR"] = "KR",
        ["CALLE"] = "CL", ["CLL"] = "CL", ["CALL"] = "CL",
        ["AVENIDA"] = "AV", ["AVE"] = "AV", ["AK"] = "AV KR", ["AC"] = "AV CL",
        ["DIAGONAL"] = "DG", ["DIAG"] = "DG",
        ["TRANSVERSAL"] = "TV", ["TRANSV"] = "TV", ["TR"] = "TV", ["TRV"] = "TV",
        ["CIRCULAR"] = "CQ", ["AUTOPISTA"] = "AU", ["KILOMETRO"] = "KM",
        ["NUMERO"] = string.Empty, ["NO"] = string.Empty, ["NRO"] = string.Empty, ["N"] = string.Empty,
        ["OFICINA"] = "OF", ["OFC"] = "OF", ["APARTAMENTO"] = "AP", ["APTO"] = "AP", ["APT"] = "AP", ["LOCAL"] = "LC", ["LOC"] = "LC",
        ["BODEGA"] = "BG", ["PISO"] = "PI", ["TORRE"] = "TO", ["INTERIOR"] = "IN", ["INT"] = "IN", ["EDIFICIO"] = "ED", ["EDIF"] = "ED"
    };

    /// <summary>Mayúsculas, sin tildes, la puntuación como espacio y los espacios repetidos colapsados.</summary>
    public static string Texto(string? texto)
    {
        var sinTildes = new StringBuilder();
        foreach (var c in (texto ?? string.Empty).ToUpperInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            sinTildes.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(' ', sinTildes.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// Palabras de un nombre o razón social: las letras sueltas seguidas se unen («S.A.S.» ⇒ «SAS», «S. A.» ⇒ «SA») y se
    /// ignoran el orden y la «Y»/«DE»/«LA» que algunos documentos omiten.
    /// </summary>
    public static IReadOnlyList<string> Palabras(string? texto)
    {
        var palabras = new List<string>();
        var sueltas = new StringBuilder();
        foreach (var palabra in Texto(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (palabra.Length == 1 && char.IsLetter(palabra[0]))
            {
                sueltas.Append(palabra);
                continue;
            }

            if (sueltas.Length > 0)
            {
                palabras.Add(sueltas.ToString());
                sueltas.Clear();
            }

            palabras.Add(palabra);
        }

        if (sueltas.Length > 0)
            palabras.Add(sueltas.ToString());
        return [.. palabras.Where(p => p is not ("Y" or "DE" or "DEL" or "LA" or "LAS" or "LOS" or "EL")).Order(StringComparer.Ordinal)];
    }

    public static bool MismasPalabras(string a, string b) => Palabras(a).SequenceEqual(Palabras(b));

    /// <summary>Solo letras y dígitos, sin ceros a la izquierda (identificaciones, DV, códigos).</summary>
    public static string Identificacion(string? texto)
    {
        var limpio = new string((texto ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var sinCeros = limpio.TrimStart('0');
        return sinCeros.Length == 0 && limpio.Length > 0 ? "0" : sinCeros;
    }

    /// <summary>Dirección con las vías y complementos abreviados como en la DIAN («Carrera 10 # 20-30» = «KR 10 20 30»).</summary>
    public static string Direccion(string? texto)
    {
        var palabras = new List<string>();
        foreach (var palabra in Texto(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            // «12A» y «12 A» son lo mismo.
            var partes = SepararNumeroYLetra(palabra);
            foreach (var parte in partes)
            {
                var abreviada = AbreviaturasDeVia.TryGetValue(parte, out var a) ? a : parte;
                if (abreviada.Length > 0)
                    palabras.AddRange(abreviada.Split(' '));
            }
        }

        return string.Join(' ', palabras);
    }

    private static IEnumerable<string> SepararNumeroYLetra(string palabra)
    {
        var i = 0;
        while (i < palabra.Length && char.IsDigit(palabra[i]))
            i++;
        if (i == 0 || i == palabra.Length)
            return [palabra];
        return [palabra[..i], palabra[i..]];
    }

    /// <summary>Los últimos diez dígitos (sin el indicativo del país).</summary>
    public static string Telefono(string? texto)
    {
        var digitos = new string((texto ?? string.Empty).Where(char.IsDigit).ToArray());
        return digitos.Length > 10 ? digitos[^10..] : digitos;
    }

    /// <summary>Códigos (CIIU de 4, responsabilidades de 2) completados con ceros a la izquierda, sin repetidos.</summary>
    public static HashSet<string> Codigos(IEnumerable<string>? codigos, int digitos) =>
        [.. (codigos ?? []).Select(c => new string(c.Where(char.IsDigit).ToArray())).Where(c => c.Length > 0 && c.Length <= digitos)
            .Select(c => c.PadLeft(digitos, '0'))];

    /// <summary>Cifra en texto (punto decimal; acepta separadores de miles con coma).</summary>
    public static bool Decimal(string? texto, out decimal valor) =>
        decimal.TryParse((texto ?? string.Empty).Replace(",", string.Empty, StringComparison.Ordinal).Replace("$", string.Empty, StringComparison.Ordinal)
            .Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
}

/// <summary>Lo declarado de una cuenta bancaria para cruzarla con su certificación; el número va en claro y solo en memoria (FD-5).</summary>
public sealed record DatosDeCuentaParaCruce(string Banco, string TipoCuenta, string Numero, string Titular, string IdentificacionTitular);
