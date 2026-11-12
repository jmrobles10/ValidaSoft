using System.Text.Json;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Identificaciones;

namespace Validasoft.Dominio.Ficha;

// Declaración jurada PEP (HU 49093 del SRM y el Portal, decisiones DP-1…DP-14). Vive dentro de los datos de la sección: en la
// General de una persona natural (`declaracion_pep`) y en cada persona de la información legal (`personas[i].declaracion_pep`).
// Así se versiona, se hashea y la revisa la empresa como el resto de la ficha. Con `pep = true` es obligatoria; con `pep = false`
// se conserva «inactiva» (se valida igual, pero nada la muestra).

/// <summary>Relación de quien declara con la empresa, como la rotula el formulario (se asigna sola según el rol).</summary>
public enum RelacionConLaEmpresa
{
    Proveedor,
    Socio,
    Otro
}

/// <summary>Calidad en que participa en una sociedad, fiducia o patrimonio autónomo.</summary>
public enum CalidadEnSociedad
{
    Socio,
    Administrador,
    Fideicomitente,
    Otra
}

/// <summary>En calidad de quién firma: la persona misma o su representante legal (que escribe su nombre y documento).</summary>
public enum FirmaEnCalidad
{
    NombrePropio,
    RepresentanteLegal
}

/// <summary>Cónyuge o compañero(a) permanente: todo o nada.</summary>
public sealed record ConyugePep
{
    public string? Nombre { get; init; }

    public string? TipoIdentificacion { get; init; }

    public string? Numero { get; init; }
}

/// <summary>Familiar hasta el 2.° grado de consanguinidad o afinidad y 1.° civil: la fila va completa o no va.</summary>
public sealed record FamiliarPep
{
    public string? Nombre { get; init; }

    public string? Parentesco { get; init; }

    public string? TipoIdentificacion { get; init; }

    public string? Numero { get; init; }
}

/// <summary>Sociedad, fiducia o patrimonio autónomo en que participa.</summary>
public sealed record SociedadPep
{
    public string? Nombre { get; init; }

    public string? Identificacion { get; init; }

    public CalidadEnSociedad? Calidad { get; init; }

    /// <summary>Obligatoria con la calidad <see cref="CalidadEnSociedad.Otra"/>.</summary>
    public string? CalidadOtra { get; init; }
}

/// <summary>Sección 3: vínculo familiar o asociados cercanos a otra persona expuesta políticamente.</summary>
public sealed record VinculoConPep
{
    public bool? Tiene { get; init; }

    public string? Nombre { get; init; }

    public string? Parentesco { get; init; }

    public string? CargoInstitucion { get; init; }

    public DateOnly? FechaVinculacion { get; init; }

    public DateOnly? FechaDesvinculacion { get; init; }

    public bool? Vigente { get; init; }
}

/// <summary>
/// Declaración jurada de una persona expuesta políticamente. <b>Fija el servidor</b> (lo que envíe el cliente se ignora): los datos
/// básicos (relación, nombre y documento; y la dirección en la persona natural) y los sellos (<see cref="FirmaHuella"/>,
/// <see cref="FechaFirma"/>, <see cref="FirmaIp"/>). La firma es la imagen PNG en base64 (<see cref="FirmaPng"/>); una declaración que
/// llega de un sistema de origen puede traer solo la huella de la firma que allá se guardó.
/// </summary>
public sealed record DeclaracionPep
{
    // 1. Datos básicos (copia de lo que se firmó).
    public RelacionConLaEmpresa? RelacionEmpresa { get; init; }

    public string? RelacionOtra { get; init; }

    public string? Nombre { get; init; }

    public string? TipoIdentificacion { get; init; }

    public string? Numero { get; init; }

    /// <summary>ISO 3166-1 alfa-2.</summary>
    public string? Nacionalidad { get; init; }

    public string? Direccion { get; init; }

    // 2. Condición PEP.
    public string? Cargo { get; init; }

    public string? Institucion { get; init; }

    /// <summary>País de la institución, ISO 3166-1 alfa-2.</summary>
    public string? Pais { get; init; }

    public DateOnly? FechaVinculacion { get; init; }

    /// <summary>Con <c>true</c> no hay fecha de desvinculación.</summary>
    public bool? Vigente { get; init; }

    public DateOnly? FechaDesvinculacion { get; init; }

    public bool? CuentasExterior { get; init; }

    /// <summary>Países ISO de las cuentas financieras en el exterior; al menos uno con <see cref="CuentasExterior"/>.</summary>
    public IReadOnlyList<string>? PaisesCuentasExterior { get; init; }

    public ConyugePep? Conyuge { get; init; }

    public IReadOnlyList<FamiliarPep>? Familiares { get; init; }

    public IReadOnlyList<SociedadPep>? Sociedades { get; init; }

    // 3. Vínculo con otra PEP.
    public VinculoConPep? VinculoConPep { get; init; }

    // 4. Juramento y firma.
    public bool? AceptaJuramento { get; init; }

    public FirmaEnCalidad? FirmaEnCalidad { get; init; }

    public string? FirmanteNombre { get; init; }

    public string? FirmanteTipoIdentificacion { get; init; }

    public string? FirmanteNumero { get; init; }

    /// <summary>PNG en base64, sin el prefijo <c>data:</c>.</summary>
    public string? FirmaPng { get; init; }

    // Sellados por el servidor.

    /// <summary>SHA-256 (hex) del PNG; en una declaración importada, el de la firma que guardó el sistema de origen.</summary>
    public string? FirmaHuella { get; init; }

    public DateOnly? FechaFirma { get; init; }

    public string? FirmaIp { get; init; }
}

/// <summary>De quién es la declaración: lo que el servidor copia de la persona (o del propio tercero) a los datos básicos.</summary>
/// <param name="EsNatural">Es el propio tercero persona natural (su General); sigue firmando a nombre propio y su dirección es la de la General.</param>
public sealed record OrigenDeDeclaracionPep(bool EsNatural, RolPersonaRelacionada? Rol, string? Nombre, string? TipoIdentificacion, string? Numero,
    string? Nacionalidad, string? Direccion);

/// <summary>Las reglas de la declaración jurada PEP: cómo se normaliza, se sella y se revisa (formato y completitud).</summary>
public static partial class DeclaracionPepReglas
{
    public const int MaximoFilas = 30;
    public const string RelacionRevisorFiscal = "Revisor fiscal";
    public const string RelacionContador = "Contador";
    public const string CodigoRequerida = "declaracion-pep-requerida";

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacios();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex RegexPais();

    private static string? Texto(string? valor)
    {
        if (valor is null)
            return null;
        var limpio = RegexEspacios().Replace(valor.Trim(), " ");
        return limpio.Length == 0 ? null : limpio;
    }

    private static string? Mayusculas(string? valor) => Texto(valor)?.ToUpperInvariant();

    // ---------- Normalización ----------

    /// <summary>
    /// Recorta los textos, fija lo que no escribe el tercero (datos básicos, firmante a nombre propio), descarta lo que no aplica a la
    /// respuesta (desvinculación si está vigente, países si no hay cuentas, el vínculo si responde que no) y quita las filas vacías.
    /// Deja la firma con su prefijo <c>data:</c> limpio. No sella (ver <see cref="Sellar"/>).
    /// </summary>
    public static DeclaracionPep? Normalizar(DeclaracionPep? d, OrigenDeDeclaracionPep origen)
    {
        if (d is null)
            return null;

        var (relacion, otra) = RelacionDe(origen);
        var calidad = origen.EsNatural ? FirmaEnCalidad.NombrePropio : d.FirmaEnCalidad;
        var firmaPropia = calidad is FirmaEnCalidad.NombrePropio;
        // Una fecha de desvinculación sin la marca «vigente» dice que ya no lo está (la casilla sin marcar no llega como «no»).
        var vigente = d.Vigente ?? (d.FechaDesvinculacion is not null ? false : null);
        return d with
        {
            RelacionEmpresa = relacion,
            RelacionOtra = otra,
            Nombre = origen.Nombre,
            TipoIdentificacion = origen.TipoIdentificacion,
            Numero = origen.Numero,
            Nacionalidad = origen.Nacionalidad ?? Mayusculas(d.Nacionalidad),
            Direccion = origen.EsNatural ? origen.Direccion : Texto(d.Direccion),
            Cargo = Texto(d.Cargo),
            Institucion = Texto(d.Institucion),
            Pais = Mayusculas(d.Pais),
            Vigente = vigente,
            FechaDesvinculacion = vigente == true ? null : d.FechaDesvinculacion,
            PaisesCuentasExterior = d.CuentasExterior == true
                ? Lista(d.PaisesCuentasExterior)
                : null,
            Conyuge = NormalizarConyuge(d.Conyuge),
            Familiares = Filas(d.Familiares, f => f with
            {
                Nombre = Texto(f.Nombre), Parentesco = Texto(f.Parentesco), TipoIdentificacion = Mayusculas(f.TipoIdentificacion), Numero = Mayusculas(f.Numero)
            }, new FamiliarPep()),
            Sociedades = Filas(d.Sociedades, s => s with
            {
                Nombre = Texto(s.Nombre),
                Identificacion = Mayusculas(s.Identificacion),
                CalidadOtra = s.Calidad == CalidadEnSociedad.Otra ? Texto(s.CalidadOtra) : null
            }, new SociedadPep()),
            VinculoConPep = NormalizarVinculo(d.VinculoConPep),
            FirmaEnCalidad = calidad,
            FirmanteNombre = firmaPropia ? origen.Nombre : Texto(d.FirmanteNombre),
            FirmanteTipoIdentificacion = firmaPropia ? origen.TipoIdentificacion : Mayusculas(d.FirmanteTipoIdentificacion),
            FirmanteNumero = firmaPropia ? origen.Numero : Mayusculas(d.FirmanteNumero),
            FirmaPng = LimpiarPng(d.FirmaPng)
        };
    }

    /// <summary>Relación con la empresa según el rol: la persona natural y los representantes, Proveedor; socio y junta, Socio; el resto, Otro con el cargo.</summary>
    public static (RelacionConLaEmpresa? Relacion, string? Otra) RelacionDe(OrigenDeDeclaracionPep origen) => origen.Rol switch
    {
        null when origen.EsNatural => (RelacionConLaEmpresa.Proveedor, null),
        RolPersonaRelacionada.RepresentanteLegal or RolPersonaRelacionada.Suplente or RolPersonaRelacionada.Apoderado => (RelacionConLaEmpresa.Proveedor, null),
        RolPersonaRelacionada.Socio or RolPersonaRelacionada.JuntaDirectiva => (RelacionConLaEmpresa.Socio, null),
        RolPersonaRelacionada.RevisorFiscal => (RelacionConLaEmpresa.Otro, RelacionRevisorFiscal),
        RolPersonaRelacionada.Contador => (RelacionConLaEmpresa.Otro, RelacionContador),
        _ => (null, null)
    };

    private static List<string>? Lista(IReadOnlyList<string>? valores)
    {
        var lista = (valores ?? []).Select(Mayusculas).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
        return lista.Count == 0 ? null : lista;
    }

    /// <summary>Fila a fila: normaliza y descarta las que quedan totalmente vacías; sin filas es <c>null</c>.</summary>
    private static List<T>? Filas<T>(IReadOnlyList<T?>? filas, Func<T, T> normalizar, T vacia) where T : class
    {
        var lista = (filas ?? []).OfType<T>().Select(normalizar).Where(f => !f.Equals(vacia)).ToList();
        return lista.Count == 0 ? null : lista;
    }

    private static ConyugePep? NormalizarConyuge(ConyugePep? c)
    {
        if (c is null)
            return null;
        var normal = c with { Nombre = Texto(c.Nombre), TipoIdentificacion = Mayusculas(c.TipoIdentificacion), Numero = Mayusculas(c.Numero) };
        return normal == new ConyugePep() ? null : normal;
    }

    private static VinculoConPep? NormalizarVinculo(VinculoConPep? v)
    {
        if (v is null)
            return null;
        if (v.Tiene == false)
            return new VinculoConPep { Tiene = false };
        var vigente = v.Vigente ?? (v.FechaDesvinculacion is not null ? false : null);
        var normal = v with
        {
            Nombre = Texto(v.Nombre),
            Parentesco = Texto(v.Parentesco),
            CargoInstitucion = Texto(v.CargoInstitucion),
            Vigente = vigente,
            FechaDesvinculacion = vigente == true ? null : v.FechaDesvinculacion
        };
        return normal == new VinculoConPep() ? null : normal;
    }

    private static string? LimpiarPng(string? firma)
    {
        var texto = Texto(firma);
        if (texto is null)
            return null;
        var coma = texto.IndexOf(',', StringComparison.Ordinal);
        return texto.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && coma > 0 ? texto[(coma + 1)..] : texto;
    }

    // ---------- Sello ----------

    /// <summary>Huella (SHA-256 en hex) de la imagen de la firma, o nulo si no hay o no es base64.</summary>
    public static string? HuellaDeLaFirma(string? firmaPng) =>
        firmaPng is not null && FirmaPng.Bytes(firmaPng) is { } bytes ? Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)) : null;

    /// <summary>El contenido que se declaró (sin los sellos): lo que decide si la declaración cambió.</summary>
    private static string Contenido(DeclaracionPep d) =>
        JsonSerializer.Serialize(d with { FirmaHuella = null, FechaFirma = null, FirmaIp = null }, JsonDeFicha.Canonica);

    /// <summary>
    /// Fija los sellos de la firma. Lo que guarda el tercero: la huella sale de la imagen; la fecha y la IP son las del momento si la
    /// declaración es nueva, cambió o se reactivó (<paramref name="pep"/> pasó de no a sí) y, si sigue igual, las que ya tenía. Lo que
    /// llega de un sistema de origen conserva sus sellos (solo se calcula la huella si trae la imagen).
    /// </summary>
    public static DeclaracionPep? Sellar(DeclaracionPep? nueva, DeclaracionPep? anterior, bool pep, bool pepAnterior, bool delTercero, DateOnly hoy, string? ip)
    {
        if (nueva is null)
            return null;
        var huella = HuellaDeLaFirma(nueva.FirmaPng) ?? (delTercero ? null : nueva.FirmaHuella);
        if (!delTercero)
            return nueva with { FirmaHuella = huella };

        var igual = anterior is not null && Contenido(anterior) == Contenido(nueva) && !(pep && !pepAnterior);
        return igual
            ? nueva with { FirmaHuella = huella, FechaFirma = anterior!.FechaFirma, FirmaIp = anterior.FirmaIp }
            : nueva with { FirmaHuella = huella, FechaFirma = hoy, FirmaIp = Texto(ip) };
    }

    // ---------- Revisión ----------

    /// <summary>
    /// Revisa la declaración de <paramref name="ruta"/> (<c>declaracion_pep</c> o <c>personas[i].declaracion_pep</c>). Falta con
    /// <paramref name="pep"/> ⇒ <c>declaracion-pep-requerida</c>. Cuando guarda el tercero (<see cref="ContextoDeSeccion.ExigeDeclaracionPep"/>) todo
    /// incumplimiento impide guardar (422); lo que llega de un sistema de origen se guarda y queda como faltante de la completitud.
    /// </summary>
    public static void Revisar(DeclaracionPep? d, bool pep, string ruta, ContextoDeSeccion contexto, RevisionDeSeccion r)
    {
        var revisor = new Revisor(r, contexto.ExigeDeclaracionPep, ruta, contexto.Hoy);
        if (d is null)
        {
            if (pep)
                revisor.Error(string.Empty, CodigoRequerida, "Falta la declaración jurada PEP: diligénciala o cambia la respuesta a «No».");
            return;
        }

        revisor.Basicos(d);
        revisor.Condicion(d);
        revisor.Vinculo(d.VinculoConPep);
        revisor.Juramento(d);
    }

    /// <summary>Acumula los incumplimientos de una declaración con su ruta; según quién guarda, impiden guardar o son faltantes.</summary>
    private sealed class Revisor(RevisionDeSeccion r, bool estricto, string ruta, DateOnly hoy)
    {
        public void Error(string campo, string codigo, string mensaje)
        {
            var ruta1 = campo.Length == 0 ? ruta : $"{ruta}.{campo}";
            if (estricto)
                r.Formato(ruta1, codigo, mensaje);
            else
                r.Falta(ruta1, mensaje);
        }

        private void Falta(string campo, string etiqueta) => Error(campo, "valor-invalido", $"Declaración PEP: falta «{etiqueta}».");

        private void Invalido(string campo, string mensaje) => Error(campo, "valor-invalido", $"Declaración PEP: {mensaje}");

        private void Requerido(string? valor, string campo, string etiqueta, int maximo)
        {
            if (valor is null)
                Falta(campo, etiqueta);
            else
                Largo(valor, campo, etiqueta, maximo);
        }

        private void Largo(string? valor, string campo, string etiqueta, int maximo)
        {
            if (valor is not null && valor.Length > maximo)
                Invalido(campo, $"«{etiqueta}» admite hasta {maximo} caracteres.");
        }

        private void Pais(string? valor, string campo, string etiqueta)
        {
            if (valor is null)
                Falta(campo, etiqueta);
            else if (!RegexPais().IsMatch(valor))
                Invalido(campo, $"«{etiqueta}»: «{valor}» no es un código de país ISO 3166-1 alfa-2 (p. ej. CO).");
        }

        private void TipoDocumento(string? valor, string campo, string etiqueta)
        {
            if (valor is null)
                Falta(campo, etiqueta);
            else if (CatalogoIdentificacion.Buscar("CO", valor) is null)
                Invalido(campo, $"«{etiqueta}»: «{valor}» no es un tipo de identificación válido (CC, CE, NIT, PA, PPT, TI, PASAPORTE, ID_FISCAL).");
        }

        public void Basicos(DeclaracionPep d)
        {
            if (d.RelacionEmpresa is null)
                Invalido("relacion_empresa", "falta el rol de la persona para saber su relación con la empresa.");
            Requerido(d.Nombre, "nombre", "Nombre completo o razón social", 300);
            TipoDocumento(d.TipoIdentificacion, "tipo_identificacion", "Tipo de documento");
            Requerido(d.Numero, "numero", "Número de documento", 50);
            Pais(d.Nacionalidad, "nacionalidad", "Nacionalidad");
            if (d.Direccion is null)
                Invalido("direccion", "diligencia primero la dirección para firmar la declaración.");
            else
                Largo(d.Direccion, "direccion", "Dirección", 500);
        }

        public void Condicion(DeclaracionPep d)
        {
            Requerido(d.Cargo, "cargo", "Cargo desempeñado", 200);
            Requerido(d.Institucion, "institucion", "Institución", 200);
            Pais(d.Pais, "pais", "País de la institución");
            Periodo(string.Empty, "del cargo", d.FechaVinculacion, d.FechaDesvinculacion, d.Vigente);

            if (d.CuentasExterior is null)
            {
                Falta("cuentas_exterior", "¿Tiene poder de firma u otro derecho sobre cuentas financieras en el exterior?");
            }
            else if (d.CuentasExterior == true)
            {
                var paises = d.PaisesCuentasExterior ?? [];
                if (paises.Count == 0)
                    Falta("paises_cuentas_exterior", "Países de las cuentas");
                if (paises.Count > MaximoFilas)
                    Invalido("paises_cuentas_exterior", $"puedes indicar hasta {MaximoFilas} países.");
                for (var i = 0; i < paises.Count; i++)
                    Pais(paises[i], $"paises_cuentas_exterior[{i}]", "País de las cuentas");
            }

            Conyuge(d.Conyuge);
            Familiares(d.Familiares ?? []);
            Sociedades(d.Sociedades ?? []);
        }

        private void Periodo(string prefijo, string de, DateOnly? vinculacion, DateOnly? desvinculacion, bool? vigente)
        {
            var campo = prefijo.Length == 0 ? "fecha_vinculacion" : $"{prefijo}.fecha_vinculacion";
            var campoFin = prefijo.Length == 0 ? "fecha_desvinculacion" : $"{prefijo}.fecha_desvinculacion";
            if (vinculacion is null)
                Falta(campo, $"Fecha de vinculación {de}");
            else if (vinculacion > hoy)
                Invalido(campo, $"la fecha de vinculación {de} no puede ser posterior a hoy.");

            if (vigente is null)
            {
                Falta(prefijo.Length == 0 ? "vigente" : $"{prefijo}.vigente", $"¿Sigue vigente? {de}");
                return;
            }

            if (vigente == true)
                return;
            if (desvinculacion is null)
                Falta(campoFin, $"Fecha de desvinculación {de}");
            else if (desvinculacion > hoy)
                Invalido(campoFin, $"la fecha de desvinculación {de} no puede ser posterior a hoy.");
            else if (vinculacion is { } inicio && desvinculacion < inicio)
                Invalido(campoFin, $"la fecha de desvinculación {de} no puede ser anterior a la de vinculación.");
        }

        private void Conyuge(ConyugePep? c)
        {
            if (c is null)
                return;
            Requerido(c.Nombre, "conyuge.nombre", "Nombre del cónyuge", 300);
            TipoDocumento(c.TipoIdentificacion, "conyuge.tipo_identificacion", "Tipo de documento del cónyuge");
            Requerido(c.Numero, "conyuge.numero", "Número de documento del cónyuge", 50);
        }

        private void Familiares(IReadOnlyList<FamiliarPep> filas)
        {
            if (filas.Count > MaximoFilas)
                Invalido("familiares", $"puedes registrar hasta {MaximoFilas} familiares.");
            for (var i = 0; i < filas.Count; i++)
            {
                var f = filas[i];
                var campo = $"familiares[{i}]";
                Requerido(f.Nombre, $"{campo}.nombre", $"Nombre del familiar {i + 1}", 300);
                Requerido(f.Parentesco, $"{campo}.parentesco", $"Parentesco del familiar {i + 1}", 100);
                TipoDocumento(f.TipoIdentificacion, $"{campo}.tipo_identificacion", $"Tipo de documento del familiar {i + 1}");
                Requerido(f.Numero, $"{campo}.numero", $"Número de documento del familiar {i + 1}", 50);
            }
        }

        private void Sociedades(IReadOnlyList<SociedadPep> filas)
        {
            if (filas.Count > MaximoFilas)
                Invalido("sociedades", $"puedes registrar hasta {MaximoFilas} sociedades.");
            for (var i = 0; i < filas.Count; i++)
            {
                var s = filas[i];
                var campo = $"sociedades[{i}]";
                Requerido(s.Nombre, $"{campo}.nombre", $"Nombre o razón social de la sociedad {i + 1}", 300);
                Requerido(s.Identificacion, $"{campo}.identificacion", $"Identificación de la sociedad {i + 1}", 50);
                if (s.Calidad is null)
                    Falta($"{campo}.calidad", $"En calidad de de la sociedad {i + 1}");
                else if (s.Calidad == CalidadEnSociedad.Otra)
                    Requerido(s.CalidadOtra, $"{campo}.calidad_otra", $"Cuál calidad en la sociedad {i + 1}", 100);
            }
        }

        public void Vinculo(VinculoConPep? v)
        {
            if (v?.Tiene is null)
            {
                Falta("vinculo_con_pep.tiene", "¿Tiene algún vínculo familiar o societario con una PEP?");
                return;
            }

            if (v.Tiene == false)
                return;
            Requerido(v.Nombre, "vinculo_con_pep.nombre", "Nombre de la PEP", 300);
            Requerido(v.Parentesco, "vinculo_con_pep.parentesco", "Vínculo o grado de parentesco", 100);
            Requerido(v.CargoInstitucion, "vinculo_con_pep.cargo_institucion", "Cargo e institución", 300);
            Periodo("vinculo_con_pep", "del vínculo", v.FechaVinculacion, v.FechaDesvinculacion, v.Vigente);
        }

        public void Juramento(DeclaracionPep d)
        {
            if (d.AceptaJuramento != true)
                Invalido("acepta_juramento", "debes aceptar la declaración bajo juramento.");
            if (d.FirmaEnCalidad is null)
                Falta("firma_en_calidad", "Firma en calidad de");
            else if (d.FirmaEnCalidad == FirmaEnCalidad.RepresentanteLegal)
            {
                Requerido(d.FirmanteNombre, "firmante_nombre", "Nombre del firmante", 300);
                TipoDocumento(d.FirmanteTipoIdentificacion, "firmante_tipo_identificacion", "Tipo de documento del firmante");
                Requerido(d.FirmanteNumero, "firmante_numero", "Número de documento del firmante", 50);
            }

            if (d.FirmaPng is null)
            {
                // Una declaración importada puede traer solo la huella de la firma que guardó el sistema de origen.
                if (estricto || d.FirmaHuella is null)
                    Falta("firma_png", "Firma");
                return;
            }

            var bytes = FirmaPng.Bytes(d.FirmaPng);
            var error = bytes is null ? "la firma no es una imagen en base64." : FirmaPng.Error(bytes);
            if (error is not null)
                Invalido("firma_png", error);
        }
    }
}
