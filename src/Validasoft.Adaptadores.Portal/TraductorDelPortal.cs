using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Ficha.Importacion;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Adaptadores.Portal;

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
        var clave = origen.Terid.ToString(CultureInfo.InvariantCulture);
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
            return new ResultadoDeTraduccion(AdaptadorPortal.CodigoDelSistema, clave, null, [], avisos.Advertencias, avisos.Rechazos);

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
        // La fila propia de la persona natural (la que crea la invitación del SRM) ya se importó en la General: su PEP y su expedición (FD-8).
        if (natural && origen.CamaraComercio?.Count(c => !EsFilaPropia(c, tercero)) is > 0)
            avisos.Advertir("personas", "no-aplica-a-persona-natural",
                "La persona natural no diligencia la información legal (FM-17): las filas de TerCamaraComercio no se importan.");
        else if (!natural)
            Agregar(CodigosDeSeccion.Legal, null, Legal(origen, catalogos, avisos)); // la fila propia del natural ya se tradujo en la General: traducirla otra vez repetiría sus avisos

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

        return new ResultadoDeTraduccion(AdaptadorPortal.CodigoDelSistema, clave, tercero, secciones, avisos.Advertencias, avisos.Rechazos);
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

        // Lo propio de la persona natural (FD-8): el Portal lo guarda en su fila de TerCamaraComercio (representante legal de sí misma).
        var propia = natural ? origen.CamaraComercio?.OrderBy(c => c.TerCamId).FirstOrDefault(c => EsFilaPropia(c, tercero)) : null;

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
            AnioInicio = ig.TigAnioInicioActividades,
            Pep = propia?.TerCamEsPep,
            VinculoPep = propia?.TerCamVinculoPep,
            FechaExpedicion = propia is null ? null : Fecha(propia.TerFechaExpedicion, "fecha_expedicion", avisos),
            LugarExpedicion = propia?.TerLugarExpedicion,
            DeclaracionPep = natural ? Declaracion(origen, propia, string.Empty, catalogos, avisos) : null
        };
    }

    /// <summary>La fila de TerCamaraComercio que es la propia persona natural: representante legal con su mismo número de documento.</summary>
    private static bool EsFilaPropia(TerCamaraComercio c, TerceroTraducido tercero) =>
        c.TerTipoRegistro == 1 && !string.IsNullOrWhiteSpace(c.TerCamDocumento) &&
        new string(c.TerCamDocumento.Where(char.IsLetterOrDigit).ToArray()).TrimStart('0').Equals(tercero.Identificacion.Numero.TrimStart('0'), StringComparison.OrdinalIgnoreCase);

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
                Telefono = c.TerCelular,
                DeclaracionPep = Declaracion(origen, c, campo, catalogos, avisos)
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

    // ---------- declaración jurada PEP (HU 49093 del Portal) ----------

    /// <summary>
    /// La declaración PEP <b>activa</b> de un contacto que responde «Sí» a «¿Es PEP?». Se importa con la firma (la imagen que el adaptador leyó del disco del
    /// Portal, si su huella coincide; ver <see cref="FirmaImportada"/>) o, si no hay imagen utilizable, solo con la huella que guardó el Portal. Lo que no se pueda convertir (un país sin código ISO, un tipo
    /// de documento sin equivalente) queda vacío con una advertencia y la declaración como faltante de la sección.
    /// </summary>
    private static DeclaracionPep? Declaracion(TerceroDelPortal origen, TerCamaraComercio? c, string prefijo, CatalogosDelPortal catalogos, Avisos avisos)
    {
        if (c is not { TerCamEsPep: true } || origen.DeclaracionesPep?.FirstOrDefault(x => x.TerCamId == c.TerCamId) is not { } d)
            return null;

        string Ruta(string r) => prefijo.Length == 0 ? r : $"{prefijo}.{r}";

        string? Pais(string? texto, string ruta)
        {
            var iso = CodigosDelPortal.Pais(texto, catalogos.Paises);
            if (iso is null && !string.IsNullOrWhiteSpace(texto))
                avisos.Advertir(Ruta(ruta), "pais-no-reconocido", "El país de la declaración PEP no corresponde a un código ISO 3166-1: queda sin diligenciar.", texto);
            return iso;
        }

        string? Documento(string? tipo, string ruta)
        {
            var codigo = CodigosDelPortal.TipoDeIdentificacion(tipo);
            if (codigo is null && !string.IsNullOrWhiteSpace(tipo))
                avisos.Advertir(Ruta(ruta), "tipo-documento-sin-equivalente", "El tipo de documento de la declaración PEP no tiene equivalente: queda sin diligenciar.", tipo);
            return codigo;
        }

        static DateOnly? Dia(DateTime? fecha) => fecha is { } f ? DateOnly.FromDateTime(f) : null;

        var paises = new List<string>();
        if (d.TieneCuentasExterior && !string.IsNullOrWhiteSpace(d.PaisesCuentasExterior))
        {
            string[] nombres;
            try
            {
                nombres = System.Text.Json.JsonSerializer.Deserialize<string[]>(d.PaisesCuentasExterior) ?? [];
            }
            catch (System.Text.Json.JsonException)
            {
                avisos.Advertir(Ruta("declaracion_pep.paises_cuentas_exterior"), "paises-ilegibles", "Los países de las cuentas en el exterior no son una lista legible.", d.PaisesCuentasExterior);
                nombres = [];
            }

            paises.AddRange(nombres.Select(n => Pais(n, "declaracion_pep.paises_cuentas_exterior")).OfType<string>());
        }

        var firma = FirmaImportada(d, Ruta, avisos);

        return new DeclaracionPep
        {
            Nacionalidad = Pais(d.Nacionalidad, "declaracion_pep.nacionalidad"),
            Direccion = d.Direccion,
            Cargo = d.CargoDesempenado,
            Institucion = d.Institucion,
            Pais = Pais(d.PaisInstitucion, "declaracion_pep.pais"),
            FechaVinculacion = Dia(d.FechaVinculacion),
            Vigente = d.CargoVigente,
            FechaDesvinculacion = Dia(d.FechaDesvinculacion),
            CuentasExterior = d.TieneCuentasExterior,
            PaisesCuentasExterior = paises.Count == 0 ? null : paises,
            Conyuge = string.IsNullOrWhiteSpace(d.ConyugeNombre + d.ConyugeTipoDocumento + d.ConyugeNumeroDocumento)
                ? null
                : new ConyugePep { Nombre = d.ConyugeNombre, TipoIdentificacion = Documento(d.ConyugeTipoDocumento, "declaracion_pep.conyuge.tipo_identificacion"), Numero = d.ConyugeNumeroDocumento },
            Familiares = d.Familiares is { Count: > 0 } familiares
                ? [.. familiares.Select(f => new FamiliarPep { Nombre = f.NombreCompleto, Parentesco = f.Parentesco, TipoIdentificacion = Documento(f.TipoDocumento, "declaracion_pep.familiares"), Numero = f.NumeroDocumento })]
                : null,
            Sociedades = d.Sociedades is { Count: > 0 } sociedades
                ? [.. sociedades.Select(s => new SociedadPep
                {
                    Nombre = s.NombreRazonSocial,
                    Identificacion = s.Identificacion,
                    Calidad = s.Calidad switch { 1 => CalidadEnSociedad.Socio, 2 => CalidadEnSociedad.Administrador, 3 => CalidadEnSociedad.Fideicomitente, 4 => CalidadEnSociedad.Otra, _ => null },
                    CalidadOtra = s.CalidadOtraCual
                })]
                : null,
            VinculoConPep = VinculoPep(d, Ruta, avisos, Pais),
            AceptaJuramento = d.AceptaJuramento,
            FirmaEnCalidad = FirmaEnCalidadDe(d.FirmaEnCalidad),
            FirmanteNombre = d.FirmanteNombre,
            FirmanteTipoIdentificacion = Documento(d.FirmanteTipoDocumento, "declaracion_pep.firmante_tipo_identificacion"),
            FirmanteNumero = d.FirmanteNumeroDocumento,
            FirmaPng = firma.Png,
            FirmaHuella = firma.Huella,
            FechaFirma = Dia(d.FechaFirma)
        };
    }

    /// <summary>
    /// La firma de una declaración importada. Con la imagen leída del disco del Portal y <b>solo si su SHA-256 es el que el Portal guardó</b> (y es un PNG
    /// que Validasoft sabe dibujar) se importa la imagen, y la huella es la de esa imagen. Si falta, no coincide o no sirve, la declaración se importa igual
    /// pero <b>sin imagen</b> (con la huella del Portal como constancia de que allá estaba firmada) y con una advertencia que la corrida muestra: no se rechaza.
    /// </summary>
    private static (string? Png, string? Huella) FirmaImportada(TerDeclaracionPep d, Func<string, string> ruta, Avisos avisos)
    {
        var campo = ruta("declaracion_pep.firma_png");
        var huellaDelPortal = string.IsNullOrWhiteSpace(d.FirmaHash) ? null : d.FirmaHash.Trim().ToLowerInvariant();
        if (d.FirmaImagen is not { Length: > 0 } imagen)
        {
            avisos.Advertir(campo, "firma-sin-imagen",
                "No se pudo leer la imagen de la firma del Portal: la declaración se importa sin imagen (solo queda la huella de la firma que el Portal guardó).", d.FirmaRuta);
            return (null, huellaDelPortal);
        }

        var huella = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(imagen));
        if (huellaDelPortal is null || huella != huellaDelPortal)
        {
            avisos.Advertir(campo, "firma-huella-no-coincide",
                "La imagen de la firma que se leyó no coincide con la huella que guardó el Portal: la declaración se importa sin imagen, para no dar por firmada otra imagen.", d.FirmaRuta);
            return (null, huellaDelPortal);
        }

        if (FirmaPng.Error(imagen) is { } error)
        {
            avisos.Advertir(campo, "firma-imagen-invalida", $"La imagen de la firma del Portal no se puede usar ({error}): la declaración se importa sin imagen.", d.FirmaRuta);
            return (null, huellaDelPortal);
        }

        return (Convert.ToBase64String(imagen), huella);
    }

    /// <summary>Firma a nombre propio (1), como representante legal (2) o por apoderado (3), como en la HU del Portal.</summary>
    private static FirmaEnCalidad FirmaEnCalidadDe(int calidad) => calidad switch
    {
        2 => Dominio.Ficha.FirmaEnCalidad.RepresentanteLegal,
        3 => Dominio.Ficha.FirmaEnCalidad.Apoderado,
        _ => Dominio.Ficha.FirmaEnCalidad.NombrePropio
    };

    /// <summary>
    /// El vínculo con otras PEP (sección 3). Las versiones nuevas del Portal guardan varias PEP relacionadas (<c>TerDeclaracionPepVinculo</c>, tipo familiar o asociado cercano,
    /// con relación, cargo, entidad, país y periodo) y dejan en blanco las columnas del vínculo único de la fase 1: se importan todas. Sin filas vale lo de la fase 1
    /// (una sola, de tipo familiar, sin país ni entidad: queda como faltante lo que no traía).
    /// </summary>
    private static VinculoConPep VinculoPep(TerDeclaracionPep d, Func<string, string> ruta, Avisos avisos, Func<string?, string, string?> pais)
    {
        static DateOnly? Dia(DateTime? fecha) => fecha is { } f ? DateOnly.FromDateTime(f) : null;

        if (d.Vinculos is { Count: > 0 } vinculos)
            return new VinculoConPep
            {
                Tiene = true,
                Vinculos =
                [
                    .. vinculos.Select((v, i) => new PepRelacionada
                    {
                        Tipo = v.TipoVinculo == 2 ? TipoVinculoPep.AsociadoCercano : TipoVinculoPep.Familiar,
                        Relacion = v.Relacion?.Trim(),
                        Nombre = v.NombreCompleto,
                        Cargo = v.Cargo,
                        Entidad = v.Entidad,
                        Pais = pais(v.Pais, $"declaracion_pep.vinculo_con_pep.vinculos[{i}].pais"),
                        FechaVinculacion = Dia(v.FechaVinculacion),
                        FechaDesvinculacion = v.Actual ? null : Dia(v.FechaRetiro),
                        Vigente = v.Actual
                    })
                ]
            };

        if (!d.TieneVinculoPep)
            return new VinculoConPep { Tiene = false };
        if (string.IsNullOrWhiteSpace(d.VinculoPepNombre + d.VinculoPepParentesco + d.VinculoPepCargoInstitucion))
            return new VinculoConPep { Tiene = true };
        var parentesco = d.VinculoPepParentesco?.Trim();
        var enLista = parentesco is not null && DeclaracionPepReglas.RelacionesFamiliar.Contains(parentesco);
        if (parentesco is not null && !enLista)
            avisos.Advertir(ruta("declaracion_pep.vinculo_con_pep.vinculos[0].relacion"), "parentesco-sin-equivalente",
                "El parentesco de la PEP relacionada no es uno de la lista de la HU: queda sin diligenciar.", parentesco);
        return new VinculoConPep
        {
            Tiene = true,
            Vinculos =
            [
                new PepRelacionada
                {
                    Tipo = TipoVinculoPep.Familiar,
                    Relacion = enLista ? parentesco : null,
                    Nombre = d.VinculoPepNombre,
                    Cargo = d.VinculoPepCargoInstitucion,
                    FechaVinculacion = Dia(d.VinculoPepFechaVinculacion),
                    FechaDesvinculacion = d.VinculoPepVigente ? null : Dia(d.VinculoPepFechaDesvinculacion),
                    Vigente = d.VinculoPepVigente
                }
            ]
        };
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
