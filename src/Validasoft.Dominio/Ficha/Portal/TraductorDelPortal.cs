using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha.Portal;

/// <summary>
/// Algo que pasó al traducir: <b>advertencia</b> (una corrección o normalización aplicada: DV recalculado, calidad sin código,
/// persona duplicada, fecha de corte asumida) o <b>rechazo</b> (un dato del Portal que no se pudo convertir y quedó fuera; va
/// al CSV de rechazos con su acción sugerida). <see cref="Seccion"/> nula = el tercero.
/// </summary>
public sealed record AvisoDeTraduccion(string? Seccion, int? Periodo, string Campo, string Codigo, string Mensaje, string? Dato = null,
    string? AccionSugerida = null);

/// <summary>Datos tipados y normalizados de una sección (y periodo, en la financiera), listos para registrar la versión.</summary>
public sealed record SeccionTraducida(DefinicionDeSeccion Definicion, int? Periodo, object Datos);

/// <summary>Identidad del tercero en Validasoft: identificación normalizada (DV recalculado), tipo de persona y nombre.</summary>
public sealed record TerceroTraducido(Identificacion Identificacion, TipoPersona TipoPersona, string Nombre);

/// <summary>
/// Resultado de traducir un tercero del Portal (FM-40). Sin <see cref="Tercero"/> (identificación inválida, sin información
/// general…) el tercero entero queda rechazado y no se importa nada de él.
/// </summary>
public sealed record ResultadoDeTraduccion(int Terid, string ClaveLegado, TerceroTraducido? Tercero, IReadOnlyList<SeccionTraducida> Secciones,
    IReadOnlyList<AvisoDeTraduccion> Advertencias, IReadOnlyList<AvisoDeTraduccion> Rechazos);

/// <summary>
/// Traductor puro (sin E/S) de un tercero del Portal a las secciones de la ficha, con las correcciones de FM-10/FM-11
/// (tablas en docs/08 §6.6 y FM-40):
/// <list type="bullet">
/// <item>DV recalculado (DIAN módulo 11) aunque el Portal traiga otro (advertencia si difiere).</item>
/// <item>Las calidades S/N ⇒ códigos DIAN (<see cref="CalidadesDelPortal"/>, con <c>NoObligFacturar</c> normalizado).</item>
/// <item>Fechas, lugares y porcentajes de texto ⇒ tipados; lo ilegible queda fuera como rechazo.</item>
/// <item>Ciudades ⇒ DANE, actividades ⇒ CIIU, especialidades ⇒ <c>PE{EspId}</c>, países ⇒ ISO.</item>
/// <item>Personas de la cámara deduplicadas por rol + identificación; una financiera por año.</item>
/// </list>
/// Al final cada sección pasa por la regla única de la ficha (<see cref="ReglasDeFicha"/>): un error de formato no tumba la
/// sección, quita ese dato (rechazo) y la versión queda con sus faltantes. Lo incompleto se importa incompleto: nunca se inventa.
/// </summary>
public static partial class TraductorDelPortal
{
    public const string AccionCorregirEnPortal = "Corregir el dato en el Portal y volver a sincronizar el tercero.";

    [GeneratedRegex(@"^(?<campo>[a-z_]+)(\[(?<indice>\d+)\])?$")]
    private static partial Regex RegexSegmento();

    private sealed class Avisos(string? seccion = null, int? periodo = null)
    {
        public List<AvisoDeTraduccion> Advertencias { get; } = [];

        public List<AvisoDeTraduccion> Rechazos { get; } = [];

        public string? Seccion { get; set; } = seccion;

        public int? Periodo { get; set; } = periodo;

        public void Advertir(string campo, string codigo, string mensaje, string? dato = null) =>
            Advertencias.Add(new AvisoDeTraduccion(Seccion, Periodo, campo, codigo, mensaje, dato));

        public void Rechazar(string campo, string codigo, string mensaje, string? dato = null, string? accion = AccionCorregirEnPortal) =>
            Rechazos.Add(new AvisoDeTraduccion(Seccion, Periodo, campo, codigo, mensaje, dato, accion));
    }

    /// <summary>
    /// Traduce un tercero. <paramref name="plataforma"/>: los catálogos de plataforma (los de la base más los que la corrida
    /// importará); <paramref name="hoy"/>: la fecha de negocio (fechas futuras, periodos válidos). Con
    /// <paramref name="registrado"/> (el tercero ya existe en Validasoft) manda su tipo de persona: consorcio y jurídica son
    /// compatibles; natural frente a jurídica rechaza el tercero (no se cambia el registro desde la importación).
    /// </summary>
    public static ResultadoDeTraduccion Traducir(TerceroDelPortal origen, CatalogosDelPortal catalogos, ICatalogosDeFicha plataforma, DateOnly hoy,
        TipoPersona? registrado = null)
    {
        var clave = IdDeLegado.ClaveTerceroPortal(origen.Terid);
        var avisos = new Avisos();
        var tercero = Identidad(origen, catalogos, avisos);
        if (tercero is not null && registrado is { } tipo && tipo != tercero.TipoPersona)
        {
            if ((tipo == TipoPersona.Natural) != (tercero.TipoPersona == TipoPersona.Natural))
            {
                avisos.Rechazar("tipo_persona", "tipo-persona-distinto",
                    $"El tercero ya está registrado en Validasoft como persona {tipo.ToString().ToLowerInvariant()} y el Portal dice {tercero.TipoPersona.ToString().ToLowerInvariant()}.",
                    origen.InformacionGeneral?.TigTipoPersona, "Revisar el tipo de persona en el Portal o en Validasoft antes de importar.");
                tercero = null;
            }
            else
            {
                tercero = tercero with { TipoPersona = tipo };
            }
        }

        if (tercero is null)
            return new ResultadoDeTraduccion(origen.Terid, clave, null, [], avisos.Advertencias, avisos.Rechazos);

        var secciones = new List<SeccionTraducida>();
        void Agregar(string codigo, int? periodo, object? datos)
        {
            if (datos is null)
                return;
            var definicion = CatalogoDeSecciones.Requerida(codigo);
            avisos.Seccion = codigo;
            avisos.Periodo = periodo;
            var contexto = new ContextoDeSeccion(tercero.TipoPersona, tercero.Identificacion, periodo, hoy);
            if (Depurar(definicion, datos, contexto, plataforma, avisos) is { } depurados)
                secciones.Add(new SeccionTraducida(definicion, periodo, depurados));
        }

        avisos.Seccion = CodigosDeSeccion.General;
        Agregar(CodigosDeSeccion.General, null, General(origen, tercero, catalogos, avisos));
        avisos.Seccion = CodigosDeSeccion.Tributaria;
        Agregar(CodigosDeSeccion.Tributaria, null, Tributaria(origen, catalogos, avisos));

        var natural = tercero.TipoPersona == TipoPersona.Natural;
        avisos.Seccion = CodigosDeSeccion.Legal;
        if (natural && origen.CamaraComercio is { Count: > 0 })
            avisos.Advertir("personas", "no-aplica-a-persona-natural",
                "La persona natural no diligencia la información legal (FM-17): las filas de TerCamaraComercio no se importan.");
        else
            Agregar(CodigosDeSeccion.Legal, null, Legal(origen, catalogos, avisos));

        avisos.Seccion = CodigosDeSeccion.Financiera;
        if (natural && origen.InfoFinanciera is { Count: > 0 })
            avisos.Advertir("periodo", "no-aplica-a-persona-natural",
                "La persona natural no diligencia la información financiera (FM-17): las filas de TerInfoFinanciera no se importan.");
        else
            foreach (var (periodo, datos) in Financiera(origen, catalogos, hoy, avisos))
                Agregar(CodigosDeSeccion.Financiera, periodo, datos);

        avisos.Seccion = CodigosDeSeccion.Sst;
        Agregar(CodigosDeSeccion.Sst, null, Sst(origen.Siso));
        avisos.Seccion = CodigosDeSeccion.Especialidades;
        Agregar(CodigosDeSeccion.Especialidades, null, Especialidades(origen, catalogos, avisos));

        return new ResultadoDeTraduccion(origen.Terid, clave, tercero, secciones, avisos.Advertencias, avisos.Rechazos);
    }

    // ---------- identidad ----------

    private static TerceroTraducido? Identidad(TerceroDelPortal origen, CatalogosDelPortal catalogos, Avisos avisos)
    {
        if (origen.InformacionGeneral is not { } ig)
        {
            avisos.Rechazar("tercero", "sin-informacion-general", "El tercero no tiene TerInformacionGeneral: sin identificación no se importa.",
                accion: "Diligenciar la información general en el Portal o descartar el tercero.");
            return null;
        }

        TipoPersona? tipoPersona = (ig.TigTipoPersona ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "N" => TipoPersona.Natural,
            "J" => TipoPersona.Juridica,
            _ => null
        };
        if (tipoPersona is null)
        {
            avisos.Rechazar("tipo_persona", "tipo-persona-invalido", "TigTipoPersona debe ser N o J.", ig.TigTipoPersona);
            return null;
        }

        var tipo = CodigosDelPortal.TipoDeIdentificacion(ig.TigTipoDocumento);
        if (tipo is null)
        {
            avisos.Rechazar("tipo_identificacion", "tipo-identificacion-invalido", "El tipo de documento del Portal no tiene equivalente.",
                ig.TigTipoDocumento);
            return null;
        }

        var paisDomicilio = CodigosDelPortal.Pais(ig.TigPais, catalogos.Paises);
        var pais = tipo is "ID_FISCAL" or "PASAPORTE" ? paisDomicilio : "CO";
        if (pais is null || (tipo == "ID_FISCAL" && pais == "CO"))
        {
            avisos.Rechazar("pais", "pais-de-identificacion-invalido", "Un documento extranjero (EXT) necesita un país distinto de Colombia.", ig.TigPais);
            return null;
        }

        if (tipo == "ID_FISCAL")
            avisos.Advertir("tipo_identificacion", "documento-extranjero", "El documento EXT del Portal se importa como ID_FISCAL del país.", ig.TigTipoDocumento);

        var (numero, dvDelNumero) = SepararDv(ig.TigNumeroIdentificacion, tipo);
        Identificacion identificacion;
        try
        {
            identificacion = Identificacion.Crear(pais, tipo, numero);
        }
        catch (ReglaDeNegocioException e)
        {
            avisos.Rechazar("numero", e.Codigo, e.Message, ig.TigNumeroIdentificacion);
            return null;
        }

        var aplica = CatalogoIdentificacion.Buscar(identificacion.Pais, identificacion.Tipo)!.AplicaA;
        if ((aplica == AplicaA.Natural && tipoPersona != TipoPersona.Natural) || (aplica == AplicaA.Juridica && tipoPersona == TipoPersona.Natural))
        {
            avisos.Rechazar("tipo_identificacion", "tipo-identificacion-no-aplica",
                $"{identificacion.Tipo} no aplica a una persona {tipoPersona.ToString()!.ToLowerInvariant()}.", ig.TigTipoDocumento);
            return null;
        }

        var dvDeclarado = ig.TerDigitoVerificacion?.ToString(CultureInfo.InvariantCulture) ?? dvDelNumero;
        if (identificacion.DigitoVerificacion is { } dv && dvDeclarado is not null && dvDeclarado != dv)
        {
            avisos.Seccion = CodigosDeSeccion.General;
            avisos.Advertir("dv", "dv-recalculado",
                $"El Portal trae el DV {dvDeclarado}; el de {identificacion.Numero} (DIAN módulo 11) es {dv}: se usa el calculado.", dvDeclarado);
            avisos.Seccion = null;
        }

        var nombre = tipoPersona == TipoPersona.Natural
            ? string.Join(' ', new[] { ig.TigNombre, ig.TerApellido }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()))
            : (ig.TigNombre ?? string.Empty).Trim();
        if (nombre.Length == 0)
        {
            avisos.Rechazar("razon_social", "nombre-requerido", "El tercero no tiene nombre ni razón social en el Portal.");
            return null;
        }

        return new TerceroTraducido(identificacion, tipoPersona.Value, nombre);
    }

    /// <summary>Número sin el DV que el Portal a veces pega con guion (<c>900123456-7</c>) y ese DV.</summary>
    private static (string Numero, string? Dv) SepararDv(string? numero, string tipo)
    {
        var crudo = (numero ?? string.Empty).Trim();
        if (tipo != "NIT" || !crudo.Contains('-', StringComparison.Ordinal))
            return (crudo, null);
        var corte = crudo.LastIndexOf('-');
        return (crudo[..corte], crudo[(corte + 1)..].Trim());
    }

    // ---------- secciones ----------

    private static DatosGenerales General(TerceroDelPortal origen, TerceroTraducido tercero, CatalogosDelPortal catalogos, Avisos avisos)
    {
        var ig = origen.InformacionGeneral!;
        var pais = CodigosDelPortal.Pais(ig.TigPais, catalogos.Paises);
        if (pais is null && !string.IsNullOrWhiteSpace(ig.TigPais))
            avisos.Rechazar("pais", "pais-no-reconocido", "El país del Portal no corresponde a un código ISO 3166-1.", ig.TigPais);

        string? ciudad = null;
        if (ig.TigCiudad > 0)
        {
            var fila = (catalogos.Ciudades ?? []).FirstOrDefault(c => c.CiudID == ig.TigCiudad);
            ciudad = CodigosDelPortal.Dane(fila);
            if (ciudad is null)
                avisos.Rechazar("ciudad", "ciudad-sin-dane", "La ciudad del Portal no tiene un código DANE válido (CiuCodDepartamento + CiuCodMunicipio).",
                    fila is null ? $"CiudID {ig.TigCiudad} (no existe)" : $"{fila.CiuNombre} ({fila.CiuCodDepartamento}/{fila.CiuCodMunicipio})");
        }

        if (pais is null && ciudad is not null && string.IsNullOrWhiteSpace(ig.TigPais))
        {
            pais = "CO";
            avisos.Advertir("pais", "pais-de-la-ciudad", "El Portal no trae el país: es Colombia por la ciudad DANE.");
        }

        string? ciiu = null;
        if (ig.TigActEconomicaPri > 0)
        {
            var actividad = (catalogos.ActividadesEconomicas ?? []).FirstOrDefault(a => a.ActEcId == ig.TigActEconomicaPri);
            ciiu = CodigosDelPortal.Ciiu(actividad);
            if (ciiu is null)
                avisos.Rechazar("ciiu_principal", "ciiu-invalido", "La actividad económica del Portal no tiene un CIIU de 4 dígitos.",
                    actividad?.ActEcCodigo ?? $"ActEcId {ig.TigActEconomicaPri} (no existe)");
        }

        var natural = tercero.TipoPersona == TipoPersona.Natural;
        if (natural && string.IsNullOrWhiteSpace(ig.TerApellido))
            avisos.Advertir("apellidos", "apellidos-vacios", "El Portal no separa los apellidos (TerApellido vacío): quedan por diligenciar.", ig.TigNombre);

        return new DatosGenerales
        {
            RazonSocial = natural ? null : ig.TigNombre,
            Nombres = natural ? ig.TigNombre : null,
            Apellidos = natural ? ig.TerApellido : null,
            Correo = ig.TigCorreo,
            Telefono = ig.TigTelefono,
            Pais = pais,
            Ciudad = pais == "CO" ? ciudad : null,
            Direccion = ig.TigDireccion,
            DireccionEstructurada = Direccion(origen.DireccionEstructurada, avisos),
            CiiuPrincipal = ciiu,
            PaginaWeb = ig.TigPaginaWeb,
            CertificadoIso = ig.TigCertificadoISO,
            AnioInicio = ig.TigAnioInicioActividades
        };
    }

    /// <summary>La dirección estructurada de AyF, solo si se puede armar entera (vía conocida, números válidos); si no, nula con advertencia.</summary>
    private static DireccionEstructurada? Direccion(TerDireccionEstructurada? d, Avisos avisos)
    {
        if (d is null || string.IsNullOrWhiteSpace(d.ViaCodigo))
            return null;
        var via = CodigosDelPortal.TiposDeVia.GetValueOrDefault(d.ViaCodigo.Trim());
        var numero = CodigosDelPortal.NumeroDeVia(d.ViaNumero);
        var cruce = CodigosDelPortal.NumeroDeVia(d.SecNumero);
        var placa = CodigosDelPortal.SoloDigitos(d.SecNumero2);
        if (via is null || numero is null || (!string.IsNullOrWhiteSpace(d.SecNumero) && cruce is null) ||
            (!string.IsNullOrWhiteSpace(d.SecNumero2) && placa is not { Length: <= 4 }))
        {
            avisos.Advertir("direccion_estructurada", "direccion-estructurada-no-convertible",
                "La dirección estructurada del Portal no cabe en el formato de AyF: se importa solo la dirección en texto.",
                string.Join(' ', new[] { d.ViaCodigo, d.ViaNumero, d.ViaInterseccion, d.SecNumero, d.SecInterseccion, d.SecNumero2 }
                    .Where(x => !string.IsNullOrWhiteSpace(x))));
            return null;
        }

        var cuadrante = new[] { d.ViaInterseccion, d.SecInterseccion }
            .Select(x => CodigosDelPortal.Cuadrantes.GetValueOrDefault((x ?? string.Empty).Trim()))
            .FirstOrDefault(x => x is not null);
        var unidad = string.Join(' ', new[] { d.UnidadCodigo, d.UnidadNumero }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        return new DireccionEstructurada
        {
            TipoVia = via,
            NumeroVia = numero.Value.Numero,
            LetraVia = numero.Value.Letra,
            Bis = numero.Value.Bis ? true : null,
            Cuadrante = cuadrante,
            NumeroCruce = cruce?.Numero,
            LetraCruce = cruce?.Letra,
            Placa = placa,
            Complemento = unidad.Length == 0 ? null : unidad
        };
    }

    private static DatosTributarios? Tributaria(TerceroDelPortal origen, CatalogosDelPortal catalogos, Avisos avisos)
    {
        var t = origen.InfoTributaria;
        var ica = new List<FilaIca>();
        foreach (var fila in origen.RetencionesIca ?? [])
        {
            var ciudad = (catalogos.Ciudades ?? []).FirstOrDefault(c => c.CiudID == fila.IdCiudad);
            var municipio = CodigosDelPortal.Dane(ciudad);
            var ciiu = CodigosDelPortal.Ciiu((catalogos.ActividadesEconomicas ?? []).FirstOrDefault(a => a.ActEcId == fila.IdActividadEconomica));
            if (municipio is null || ciiu is null)
            {
                avisos.Rechazar($"ica[{ica.Count}]", municipio is null ? "ciudad-sin-dane" : "ciiu-invalido",
                    "La fila de ICA del Portal no tiene municipio DANE o CIIU válidos: no se importa.",
                    $"IdTerIca {fila.IdTerIca}: ciudad {fila.IdCiudad}, actividad {fila.IdActividadEconomica}");
                continue;
            }

            var nueva = new FilaIca { Municipio = municipio, Ciiu = ciiu };
            if (ica.Contains(nueva))
                avisos.Advertir("ica", "fila-duplicada", $"El municipio {municipio} con la actividad {ciiu} está repetido en el Portal: se importa una vez.");
            else
                ica.Add(nueva);
        }

        if (t is null)
        {
            if (ica.Count > 0)
                avisos.Advertir("responsabilidades", "sin-info-tributaria", "Hay municipios de ICA pero no TerInfoTributaria: se importan sin responsabilidades.");
            return ica.Count == 0 ? null : new DatosTributarios { ResponsableIca = true, Ica = ica };
        }

        var traduccion = CalidadesDelPortal.Traducir(t);
        foreach (var (campo, mensaje) in traduccion.Notas)
            avisos.Advertir("responsabilidades", "calidad-sin-codigo", mensaje, campo);
        if (ica.Count == 0)
            avisos.Advertir("responsable_ica", "ica-sin-municipios",
                "El Portal no tiene municipios de ICA (TerRetencionICA): «¿contribuyente de ICA?» queda por diligenciar.");

        return new DatosTributarios
        {
            Responsabilidades = traduccion.Responsabilidades,
            ResolucionGranContribuyente = Resolucion(t.ResolucionGC, t.FechaGC),
            ResolucionAutorretenedor = Resolucion(t.ResolucionAutoRete, t.FechaAutoRete),
            ResponsableIca = ica.Count > 0 ? true : null,
            AutorretenedorIca = traduccion.AutorretenedorIca,
            Ica = ica.Count == 0 ? null : ica
        };
    }

    /// <summary>Resolución del Portal (número entero y fecha); 0 o una fecha anterior a 1900 son el valor por omisión del legado, no un dato.</summary>
    private static Resolucion? Resolucion(int? numero, DateTime? fecha)
    {
        var n = numero is > 0 ? numero.Value.ToString(CultureInfo.InvariantCulture) : null;
        DateOnly? f = fecha is { Year: > 1900 } valor ? DateOnly.FromDateTime(valor) : null;
        return n is null && f is null ? null : new Resolucion { Numero = n, Fecha = f };
    }

    private static IEnumerable<(int Periodo, DatosFinancieros Datos)> Financiera(TerceroDelPortal origen, CatalogosDelPortal catalogos, DateOnly hoy,
        Avisos avisos)
    {
        var filas = origen.InfoFinanciera ?? [];
        if (filas.Count == 0)
            yield break;

        var moneda = MonedaExtranjera(origen.MonedaExtranjera, catalogos, avisos);
        if (moneda is not null && filas.Select(f => f.Anio).Distinct().Count() > 1)
            avisos.Advertir("moneda_extranjera", "moneda-extranjera-sin-anio",
                "El Portal guarda la moneda extranjera por tercero, sin año: se lleva a cada periodo importado.");

        foreach (var grupo in filas.GroupBy(f => f.Anio).OrderBy(g => g.Key))
        {
            avisos.Periodo = grupo.Key;
            var fila = grupo.MaxBy(f => f.Id)!;
            if (grupo.Count() > 1)
                avisos.Advertir("periodo", "periodo-duplicado", $"El Portal tiene {grupo.Count()} filas del año {grupo.Key}: se importa la última (Id {fila.Id}).");
            if (grupo.Key < 1990 || grupo.Key > hoy.Year)
            {
                avisos.Rechazar("periodo", "periodo-invalido", $"El año debe estar entre 1990 y {hoy.Year}.", grupo.Key.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            decimal[] cifras = [fila.ActivosTotales, fila.PasivosTotales, fila.Patrimonio, fila.IngresosBrutos, fila.IngresosNetos, fila.GastosOperaciones,
                fila.GastosNoOperaciones, fila.OtrosIngresos];
            if (cifras.All(c => c == 0))
            {
                // El GET de la financiera del Portal insertaba filas en cero al abrirla (FM-16): no son cifras declaradas.
                avisos.Advertir("periodo", "fila-en-ceros", $"La fila del año {grupo.Key} está toda en cero (la creaba el Portal al abrir la sección): no se importa.");
                continue;
            }

            var corte = new DateOnly(grupo.Key, 12, 31);
            DateOnly? fechaCorte = corte <= hoy ? corte : null;
            avisos.Advertir("fecha_corte", "fecha-de-corte-asumida", fechaCorte is null
                ? "El Portal no guarda la fecha de corte y el año está en curso: queda por diligenciar."
                : $"El Portal no guarda la fecha de corte: se asume el cierre fiscal {corte:yyyy-MM-dd}.");

            yield return (grupo.Key, new DatosFinancieros
            {
                Periodo = grupo.Key,
                FechaCorte = fechaCorte,
                Activos = fila.ActivosTotales,
                Pasivos = fila.PasivosTotales,
                Patrimonio = fila.Patrimonio,
                IngresosBrutos = fila.IngresosBrutos,
                IngresosNetos = fila.IngresosNetos,
                GastosOperacionales = fila.GastosOperaciones,
                GastosNoOperacionales = fila.GastosNoOperaciones,
                OtrosIngresos = fila.OtrosIngresos,
                MonedaExtranjera = moneda
            });
        }

        avisos.Periodo = null;
    }

    private static MonedaExtranjera? MonedaExtranjera(TerTransaccionMonedaExtranjera? m, CatalogosDelPortal catalogos, Avisos avisos)
    {
        if (m is null)
            return null;
        var tipos = new List<TipoOperacionExtranjera>();
        var paises = new List<string>();
        foreach (var tipo in m.Tipos ?? [])
        {
            if (!CodigosDelPortal.OperacionesExtranjeras.TryGetValue(tipo.IdTipoTransaccion, out var operacion))
            {
                avisos.Rechazar("moneda_extranjera.tipos", "operacion-desconocida", "Tipo de transacción en moneda extranjera desconocido.",
                    tipo.IdTipoTransaccion.ToString(CultureInfo.InvariantCulture));
                continue;
            }

            if (operacion == TipoOperacionExtranjera.Otras)
                avisos.Advertir("moneda_extranjera.tipos", "operacion-como-otras",
                    "Leasing, redescuento y «otra» van como «Otras» (la ficha no tiene ese tipo ni el detalle «¿cuál?»).",
                    tipo.OtraCual ?? tipo.IdTipoTransaccion.ToString(CultureInfo.InvariantCulture));
            tipos.Add(operacion);
            foreach (var idPais in tipo.Paises ?? [])
            {
                var fila = (catalogos.Paises ?? []).FirstOrDefault(p => p.Id == idPais);
                var iso = CodigosDelPortal.Pais(fila?.Sigla, catalogos.Paises) ?? CodigosDelPortal.Pais(fila?.Nombre, catalogos.Paises);
                if (iso is null)
                    avisos.Rechazar("moneda_extranjera.paises", "pais-no-reconocido", "El país de la transacción no corresponde a un código ISO 3166-1.",
                        fila is null ? $"IdPais {idPais} (no existe)" : $"{fila.Nombre} ({fila.Sigla})");
                else
                    paises.Add(iso);
            }
        }

        return new MonedaExtranjera { Realiza = m.RealizaTransacciones, Tipos = tipos.Count == 0 ? null : tipos, Paises = paises.Count == 0 ? null : paises };
    }

    private static DatosLegales? Legal(TerceroDelPortal origen, CatalogosDelPortal catalogos, Avisos avisos)
    {
        var filas = (origen.CamaraComercio ?? []).OrderBy(c => c.TerCamId).ToList();
        if (filas.Count == 0)
            return null;

        var personas = new List<PersonaRelacionada>();
        var vistas = new Dictionary<(RolPersonaRelacionada, string?, string), int>();
        FirmaRevisora? firma = null;
        foreach (var c in filas)
        {
            var campo = $"personas[{personas.Count}]";
            var (rol, representacion) = Rol(c);
            if (rol is null)
            {
                avisos.Rechazar(campo, "rol-desconocido", "TerTipoRegistro no es 1 (representante), 2 (socio), 3 (revisor fiscal) ni 4 (contador).",
                    $"TerCamId {c.TerCamId}: {c.TerTipoRegistro}");
                continue;
            }

            var tipo = CodigosDelPortal.TipoDeIdentificacion(c.TerCamTipoDoc);
            if (tipo is null && !string.IsNullOrWhiteSpace(c.TerCamTipoDoc))
                avisos.Rechazar($"{campo}.tipo_identificacion", "tipo-identificacion-invalido", "El tipo de documento del Portal no tiene equivalente.",
                    c.TerCamTipoDoc);
            var (numero, dvDeclarado) = SepararDv(c.TerCamDocumento, tipo ?? string.Empty);
            if (tipo == "NIT" && dvDeclarado is not null && IdentificacionCo("NIT", numero) is { } nit && nit.DigitoVerificacion != dvDeclarado)
                avisos.Advertir($"{campo}.dv", "dv-recalculado", $"El Portal trae el DV {dvDeclarado}; el de {nit.Numero} es {nit.DigitoVerificacion}.",
                    c.TerCamDocumento);
            var numeroNormal = IdentificacionCo(tipo, numero)?.Numero ?? (numero.Length == 0 ? null : numero.ToUpperInvariant());

            if (numeroNormal is not null && vistas.TryGetValue((rol.Value, tipo, numeroNormal), out var primera))
            {
                avisos.Advertir(campo, "persona-duplicada",
                    $"{rol} {tipo} {numeroNormal} está repetido en TerCamaraComercio: se conserva la primera fila (TerCamId {primera}).",
                    $"TerCamId {c.TerCamId}");
                continue;
            }

            if (numeroNormal is not null)
                vistas[(rol.Value, tipo, numeroNormal)] = c.TerCamId;

            var porcentaje = CodigosDelPortal.Porcentaje(c.TerCamPorParticipa, out var redondeado);
            if (porcentaje is null && !string.IsNullOrWhiteSpace(c.TerCamPorParticipa))
                avisos.Rechazar($"{campo}.porcentaje", "porcentaje-invalido", "El porcentaje de participación no es un número entre 0 y 100.", c.TerCamPorParticipa);
            if (redondeado)
                avisos.Advertir($"{campo}.porcentaje", "porcentaje-redondeado", "El porcentaje se redondea a cuatro decimales.", c.TerCamPorParticipa);

            var lugares = new[] { ("lugar de expedición", c.TerLugarExpedicion), ("lugar de nacimiento", c.TerLugarNacimiento), ("ciudad de residencia", c.TerLugarResidencia), ("dirección", c.TerDireccion) }
                .Where(l => !string.IsNullOrWhiteSpace(l.Item2)).ToList();
            if (lugares.Count > 0)
                avisos.Advertir(campo, "lugar-sin-campo",
                    $"La ficha (fase 1) no guarda {string.Join(", ", lugares.Select(l => l.Item1))} de la persona (FM-38): no se importan.",
                    string.Join(" | ", lugares.Select(l => l.Item2)));

            var esFirma = c.TerTipoRepresentacion == 4;
            if (rol == RolPersonaRelacionada.RevisorFiscal && esFirma && firma is null &&
                !string.IsNullOrWhiteSpace(c.TerRazonSocialDoc + c.TerRazonSocialNombre))
                firma = new FirmaRevisora { Nit = SepararDv(c.TerRazonSocialDoc, "NIT").Numero, RazonSocial = c.TerRazonSocialNombre };

            personas.Add(new PersonaRelacionada
            {
                Rol = rol,
                TipoRepresentacion = representacion,
                TipoIdentificacion = tipo,
                Numero = numeroNormal,
                Nombres = tipo == "NIT" ? c.TerCapNombre ?? c.TerRazonSocialNombre : c.TerCapNombre,
                Apellidos = tipo == "NIT" ? null : c.TerCapApellido,
                Cargo = c.TerCamCargo,
                Porcentaje = porcentaje,
                Pep = c.TerCamEsPep,
                VinculoPep = c.TerCamVinculoPep,
                FechaExpedicion = Fecha(c.TerFechaExpedicion, $"{campo}.fecha_expedicion", avisos),
                FechaNacimiento = Fecha(c.TerFechaNacimiento, $"{campo}.fecha_nacimiento", avisos),
                Nacionalidad = PaisDePersona(c.TerNacionalidad, $"{campo}.nacionalidad", catalogos, avisos),
                Residencia = PaisDePersona(c.TerPaisResidencia, $"{campo}.residencia", catalogos, avisos),
                Correo = c.TerCorreo,
                Telefono = c.TerCelular
            });
        }

        return personas.Count == 0 && firma is null ? null : new DatosLegales { Personas = personas, RevisorFiscalFirma = firma };
    }

    /// <summary>
    /// Rol y representación de una fila de la cámara: representante legal principal (o sin representación) ⇒ <c>RepresentanteLegal</c>,
    /// suplente ⇒ <c>Suplente</c>, apoderado ⇒ <c>Apoderado</c>; 2 ⇒ <c>Socio</c>; 3 ⇒ <c>RevisorFiscal</c>; 4 ⇒ <c>Contador</c>.
    /// </summary>
    private static (RolPersonaRelacionada? Rol, string? Representacion) Rol(TerCamaraComercio c)
    {
        string? representacion = c.TerTipoRepresentacion switch
        {
            1 => "Principal",
            2 => "Suplente",
            3 => "Apoderado",
            4 => "Firma",
            _ => null
        };
        RolPersonaRelacionada? rol = c.TerTipoRegistro switch
        {
            1 => c.TerTipoRepresentacion switch
            {
                2 => RolPersonaRelacionada.Suplente,
                3 => RolPersonaRelacionada.Apoderado,
                _ => RolPersonaRelacionada.RepresentanteLegal
            },
            2 => RolPersonaRelacionada.Socio,
            3 => RolPersonaRelacionada.RevisorFiscal,
            4 => RolPersonaRelacionada.Contador,
            _ => null
        };
        return (rol, representacion);
    }

    private static Identificacion? IdentificacionCo(string? tipo, string numero)
    {
        if (tipo is null || numero.Length == 0)
            return null;
        try
        {
            return Identificacion.Crear("CO", tipo, numero);
        }
        catch (ReglaDeNegocioException)
        {
            return null;
        }
    }

    private static DateOnly? Fecha(string? texto, string campo, Avisos avisos)
    {
        var fecha = CodigosDelPortal.Fecha(texto);
        if (fecha is null && !string.IsNullOrWhiteSpace(texto))
            avisos.Rechazar(campo, "fecha-invalida", "La fecha del Portal no se puede leer (aaaa-mm-dd o dd/mm/aaaa).", texto);
        return fecha;
    }

    private static string? PaisDePersona(string? texto, string campo, CatalogosDelPortal catalogos, Avisos avisos)
    {
        var iso = CodigosDelPortal.Pais(texto, catalogos.Paises);
        if (iso is null && !string.IsNullOrWhiteSpace(texto))
            avisos.Rechazar(campo, "pais-no-reconocido", "El país del Portal no corresponde a un código ISO 3166-1.", texto);
        return iso;
    }

    private static DatosSst? Sst(TerSISO? s) =>
        s is null
            ? null
            : new DatosSst
            {
                ProgramaSaludOcupacional = s.ProgramaSaludOcupacional,
                IdentificaFactoresDeRiesgo = s.ProgramaFactoresRiesgo,
                ComiteParitario = s.TieneComiteSO,
                ProgramaHigieneYSeguridad = s.ProgramaSeguridadEhigiene,
                GestionAmbiental = s.ProgramaAmbiental,
                ReglamentoInterno = s.ReglaIntVigente,
                PoliticaHseq = s.PoliticaHSEQ,
                LicenciasYPermisos = s.LicPermAutorizaciones
            };

    private static DatosEspecialidades? Especialidades(TerceroDelPortal origen, CatalogosDelPortal catalogos, Avisos avisos)
    {
        var codigos = new List<string>();
        foreach (var id in (origen.Especialidades ?? []).Distinct())
        {
            if ((catalogos.Especialidades ?? []).Any(e => e.EspId == id))
                codigos.Add(CodigosDelPortal.Especialidad(id));
            else
                avisos.Rechazar("especialidades", "especialidad-desconocida", "La especialidad no existe en EspecialidadesTercero del Portal.",
                    id.ToString(CultureInfo.InvariantCulture));
        }

        return codigos.Count == 0 ? null : new DatosEspecialidades { Especialidades = codigos };
    }

    // ---------- la regla única ----------

    /// <summary>
    /// Normaliza con el esquema y pasa la regla única (FM-16): cada error de formato quita ese dato (rechazo) y se vuelve a
    /// evaluar, hasta que no quede ninguno. Una fila duplicada o inválida sale entera; un error dentro de la dirección
    /// estructurada la quita completa (es un formato de AyF: a medias no sirve). Nulo si un error no se puede quitar (la
    /// sección entera queda rechazada).
    /// </summary>
    private static object? Depurar(DefinicionDeSeccion definicion, object datos, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, Avisos avisos)
    {
        object actual;
        try
        {
            actual = definicion.Esquema.Normalizar(datos, contexto);
        }
        catch (ReglaDeNegocioException e)
        {
            avisos.Rechazar(e.Datos?.GetValueOrDefault("campo") as string ?? "datos", e.Codigo, $"La sección no se importa: {e.Message}");
            return null;
        }

        for (var intento = 0; intento < 200; intento++)
        {
            var errores = ReglasDeFicha.Evaluar(definicion, actual, contexto, catalogos, cambio: false, soportes: 0).Errores;
            if (errores.Count == 0)
                return actual;

            var json = JsonNode.Parse(definicion.Esquema.Canonico(actual).GetRawText())!;
            // Primero los campos (no corren índices); de las filas, una por vuelta (quitarla corre las demás).
            var rutas = errores
                .Select(e => (Error: e, Ruta: e.Campo.StartsWith("direccion_estructurada", StringComparison.Ordinal) ? "direccion_estructurada" : e.Campo))
                .ToList();
            var campos = rutas.Where(r => !r.Ruta.EndsWith(']')).ToList();
            var aQuitar = campos.Count > 0 ? campos : [rutas.First(r => r.Ruta.EndsWith(']'))];
            var quitados = 0;
            foreach (var (error, ruta) in aQuitar.DistinctBy(r => r.Ruta))
            {
                var (existia, valor) = Quitar(json, ruta);
                if (existia)
                    quitados++;
                avisos.Rechazar(error.Campo, error.Codigo, error.Mensaje, valor);
            }

            if (quitados == 0)
            {
                avisos.Rechazar("datos", "seccion-no-importable", "La sección tiene errores de formato que no se pueden quitar dato a dato: no se importa.");
                return null;
            }

            actual = definicion.Esquema.Normalizar(definicion.Esquema.Deserializar(System.Text.Json.JsonSerializer.SerializeToElement(json)), contexto);
        }

        avisos.Rechazar("datos", "seccion-no-importable", "Los errores de formato de la sección no convergen: no se importa.");
        return null;
    }

    /// <summary>Quita el nodo de la ruta (<c>a.b</c>, <c>a[2].b</c>, <c>a[2]</c>): si estaba y su valor en texto.</summary>
    private static (bool Existia, string? Valor) Quitar(JsonNode raiz, string ruta)
    {
        var segmentos = ruta.Split('.');
        JsonNode? nodo = raiz;
        for (var i = 0; i < segmentos.Length; i++)
        {
            var m = RegexSegmento().Match(segmentos[i]);
            if (!m.Success || nodo is not JsonObject objeto || objeto[m.Groups["campo"].Value] is not { } hijo)
                return (false, null);
            var ultimo = i == segmentos.Length - 1;
            if (!m.Groups["indice"].Success)
            {
                if (ultimo)
                {
                    objeto.Remove(m.Groups["campo"].Value);
                    return (true, Texto(hijo));
                }

                nodo = hijo;
                continue;
            }

            var indice = int.Parse(m.Groups["indice"].Value, CultureInfo.InvariantCulture);
            if (hijo is not JsonArray lista || indice >= lista.Count)
                return (false, null);
            if (ultimo)
            {
                var elemento = lista[indice];
                lista.RemoveAt(indice);
                if (lista.Count == 0)
                    objeto.Remove(m.Groups["campo"].Value);
                return (true, Texto(elemento));
            }

            nodo = lista[indice];
        }

        return (false, null);
    }

    private static string? Texto(JsonNode? nodo) => nodo switch
    {
        null => null,
        JsonValue valor when valor.TryGetValue<string>(out var texto) => texto,
        _ => nodo.ToJsonString()
    };
}
