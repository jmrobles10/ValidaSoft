using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha;

/// <summary>
/// De dónde viene una versión: el tercero en su portal (F1), el importador de un sistema de origen (F4) o ese sistema por el
/// sincronizador de coexistencia (FM-11). El sistema concreto va en el autor de la versión (<c>importador:{sistema}:…</c>,
/// <c>sincronizador:{sistema}</c>). Los eventos guardados antes de generalizarlo dicen <c>Portal</c>: se leen como
/// <see cref="SistemaExterno"/> (<see cref="ConvertidorDeOrigenDeSeccion"/>).
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(ConvertidorDeOrigenDeSeccion))]
public enum OrigenDeSeccion
{
    Tercero,
    Importada,
    SistemaExterno
}

/// <summary>Lee y escribe <see cref="OrigenDeSeccion"/> como texto; el valor antiguo <c>Portal</c> se lee como <c>SistemaExterno</c> (upcasting de lo ya guardado).</summary>
public sealed class ConvertidorDeOrigenDeSeccion : System.Text.Json.Serialization.JsonConverter<OrigenDeSeccion>
{
    public override OrigenDeSeccion Read(ref System.Text.Json.Utf8JsonReader reader, Type tipo, System.Text.Json.JsonSerializerOptions opciones)
    {
        if (reader.TokenType == System.Text.Json.JsonTokenType.Number && reader.TryGetInt32(out var numero) && Enum.IsDefined(typeof(OrigenDeSeccion), numero))
            return (OrigenDeSeccion)numero;
        var texto = reader.GetString();
        return texto switch
        {
            "Tercero" => OrigenDeSeccion.Tercero,
            "Importada" => OrigenDeSeccion.Importada,
            "SistemaExterno" or "Portal" => OrigenDeSeccion.SistemaExterno,
            _ => throw new System.Text.Json.JsonException($"Origen de sección desconocido: {texto}.")
        };
    }

    public override void Write(System.Text.Json.Utf8JsonWriter escritor, OrigenDeSeccion valor, System.Text.Json.JsonSerializerOptions opciones) =>
        escritor.WriteStringValue(valor.ToString());
}

/// <summary>Versión de un documento de la bóveda que respalda la versión de la sección (FM-5), con su tipo canónico.</summary>
public sealed record SoporteDeSeccion(Guid DocumentoId, int Version, string TipoDocumento);

// ---------- Eventos (stream SeccionDeFicha-{id}, tenant técnico «boveda») ----------

/// <summary>Primer evento del stream: una sección de un tercero (y, en la financiera, un periodo).</summary>
public sealed record SeccionDeFichaIniciada(Guid SeccionId, Guid TerceroId, string Seccion, int? Periodo, DateTimeOffset Fecha);

/// <summary>
/// Versión inmutable de la sección: datos normalizados (JSON canónico), huella de los datos y de la versión (datos +
/// soportes), soportes, completitud con su versión de reglas, versión del esquema y la huella de los datos de la última
/// versión completa antes de esta (referencia del soporte del cambio; así el recálculo no depende del orden de lectura).
/// </summary>
public sealed record VersionDeSeccionRegistrada(
    int Numero,
    JsonElement Datos,
    string HuellaDatos,
    string Huella,
    IReadOnlyList<SoporteDeSeccion> Soportes,
    CompletitudDeSeccion Completitud,
    int EsquemaVersion,
    string? HuellaDatosAnterior,
    OrigenDeSeccion Origen,
    string Autor,
    DateTimeOffset Fecha);

/// <summary>Plataforma recalculó la completitud de la versión vigente (cambió la versión de las reglas, el catálogo o el tipo de persona).</summary>
public sealed record CompletitudDeSeccionRecalculada(int Numero, CompletitudDeSeccion Completitud, DateTimeOffset Fecha);

/// <summary>Versión de la sección como la proyecta el agregado.</summary>
public sealed class VersionDeSeccion
{
    public int Numero { get; set; }

    public JsonElement Datos { get; set; }

    public string HuellaDatos { get; set; } = string.Empty;

    public string Huella { get; set; } = string.Empty;

    public List<SoporteDeSeccion> Soportes { get; set; } = [];

    public CompletitudDeSeccion Completitud { get; set; } = new(false, [], 0);

    public int EsquemaVersion { get; set; }

    public string? HuellaDatosAnterior { get; set; }

    public OrigenDeSeccion Origen { get; set; }

    public string Autor { get; set; } = string.Empty;

    public DateTimeOffset Fecha { get; set; }

    /// <summary>Cruces con las versiones de los documentos que la respaldan (F5, FM-8): hechos compartidos, uno por versión de documento.</summary>
    public List<CruceDeSeccion> Cruces { get; set; } = [];
}

/// <summary>
/// Sección de la ficha maestra de un tercero (FM-2): stream propio por (tercero, sección, periodo) en el tenant técnico
/// <c>boveda</c>, con id determinista. Versiones inmutables y una vigente (la última); una versión nueva solo nace si
/// cambian los datos o los soportes. Cada versión guarda su completitud, calculada por <see cref="ReglasDeFicha"/> (FM-16).
/// Los métodos de decisión devuelven eventos; los <c>Apply</c> solo evolucionan el estado.
/// </summary>
public sealed class SeccionDeFicha
{
    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid TerceroId { get; set; }

    public string Seccion { get; set; } = string.Empty;

    public int? Periodo { get; set; }

    public int? VersionVigente { get; set; }

    public List<VersionDeSeccion> Versiones { get; set; } = [];

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset ActualizadaEn { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public VersionDeSeccion? Vigente => VersionVigente is { } n ? Versiones.FirstOrDefault(v => v.Numero == n) : null;

    [System.Text.Json.Serialization.JsonIgnore]
    public int SiguienteNumero => Versiones.Count == 0 ? 1 : Versiones.Max(v => v.Numero) + 1;

    /// <summary>Id determinista del stream: SHA-256 de <c>ficha|{tercero}|{sección}|{periodo}</c> (no hay dos streams por sección).</summary>
    public static Guid IdPara(Guid terceroId, string seccion, int? periodo)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"ficha|{terceroId:N}|{seccion}|{periodo?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"}"))[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x80); // versión 8 (personalizada)
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // variante RFC 4122
        return new Guid(bytes);
    }

    /// <summary>Huella de la versión: la de los datos más los soportes (ordenados).</summary>
    public static string HuellaDeVersion(string huellaDatos, IEnumerable<SoporteDeSeccion> soportes)
    {
        var texto = huellaDatos + "|" + string.Join(",", soportes.Select(s => $"{s.DocumentoId:N}:{s.Version}").Order(StringComparer.Ordinal));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }

    // ---------- Decisiones ----------

    public static SeccionDeFichaIniciada Iniciar(Guid terceroId, DefinicionDeSeccion seccion, int? periodo, DateTimeOffset ahora) =>
        new(IdPara(terceroId, seccion.Codigo, periodo), terceroId, seccion.Codigo, periodo, ahora);

    /// <summary>
    /// Registra una versión con los datos ya normalizados. Formato inválido ⇒ 422 (nada se guarda). Si la huella (datos +
    /// soportes) es la de la vigente ⇒ sin eventos (no hay versión nueva). Si los datos difieren de los de la última versión
    /// completa y la sección exige soporte en cambios, sin soportes la versión queda incompleta («Falta el soporte del
    /// cambio»). La primera versión, y las que completan un borrador que nunca estuvo completo, no lo exigen (FM-18).
    /// </summary>
    public IReadOnlyList<object> Registrar(DefinicionDeSeccion seccion, object datos, IReadOnlyList<SoporteDeSeccion> soportes, ContextoDeSeccion contexto,
        ICatalogosDeFicha catalogos, OrigenDeSeccion origen, string autor, DateTimeOffset ahora)
    {
        if (seccion.Codigo != Seccion)
            throw new InvalidOperationException("La definición no es la de esta sección.");

        var canonico = seccion.Esquema.Canonico(datos);
        var huellaDatos = JsonDeFicha.Huella(canonico);
        var anterior = Vigente;
        // Referencia del cambio: la última versión que quedó completa. Mientras la sección es un borrador que nunca estuvo
        // completo, diligenciarla no es «cambiarla»; y una versión incompleta por falta de soporte no se vuelve referencia.
        var referencia = Versiones.Where(v => v.Completitud.Completa).MaxBy(v => v.Numero)?.HuellaDatos;
        var cambio = ExigeSoporteDelCambio(origen) && referencia is not null && referencia != huellaDatos;
        var resultado = ReglasDeFicha.Evaluar(seccion, datos, contexto, catalogos, cambio, soportes.Count);
        ReglasDeFicha.AsegurarFormato(resultado);

        var huella = HuellaDeVersion(huellaDatos, soportes);
        if (anterior?.Huella == huella)
            return [];

        return
        [
            new VersionDeSeccionRegistrada(SiguienteNumero, canonico, huellaDatos, huella, [.. soportes], resultado.Completitud, seccion.VersionEsquema,
                referencia, origen, autor, ahora)
        ];
    }

    /// <summary>
    /// Completitud que tendría una versión con estos datos y soportes, sin registrarla (FM-46): la vista previa con la que el
    /// wizard habilita «Guardar y continuar». Usa la misma regla y la misma referencia del cambio que <see cref="Registrar"/>;
    /// un error de formato es el mismo 422. Sin sección guardada, <paramref name="actual"/> es nulo.
    /// </summary>
    public static CompletitudDeSeccion Anticipar(SeccionDeFicha? actual, DefinicionDeSeccion seccion, object datos, int soportes, ContextoDeSeccion contexto,
        ICatalogosDeFicha catalogos)
    {
        var huellaDatos = JsonDeFicha.Huella(seccion.Esquema.Canonico(datos));
        var referencia = actual?.Versiones.Where(v => v.Completitud.Completa).MaxBy(v => v.Numero)?.HuellaDatos;
        var resultado = ReglasDeFicha.Evaluar(seccion, datos, contexto, catalogos, referencia is not null && referencia != huellaDatos, soportes);
        ReglasDeFicha.AsegurarFormato(resultado);
        return resultado.Completitud;
    }

    /// <summary>
    /// Recalcula la completitud de la versión vigente con las reglas actuales (FM-16); sin cambios ⇒ nulo (idempotente).
    /// La regla del soporte usa la huella de referencia (última versión completa) guardada con la versión.
    /// </summary>
    public CompletitudDeSeccionRecalculada? Recalcular(DefinicionDeSeccion seccion, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, DateTimeOffset ahora)
    {
        if (Vigente is not { } vigente)
            return null;

        var datos = seccion.Esquema.Deserializar(vigente.Datos);
        var cambio = ExigeSoporteDelCambio(vigente.Origen) && vigente.HuellaDatosAnterior is not null && vigente.HuellaDatosAnterior != vigente.HuellaDatos;
        var completitud = ReglasDeFicha.ParaRecalculo(ReglasDeFicha.Evaluar(seccion, datos, contexto, catalogos, cambio, vigente.Soportes.Count));
        return completitud.Igual(vigente.Completitud) ? null : new CompletitudDeSeccionRecalculada(vigente.Numero, completitud, ahora);
    }

    /// <summary>
    /// Solo el cambio que hace el tercero exige soporte (FM-20). Las versiones que llegan de un sistema de origen (<c>Importada</c>, <c>SistemaExterno</c>)
    /// no: su respaldo es ese sistema, que no guarda el soporte de cada cambio, y exigirlo dejaría incompleta toda versión migrada
    /// (F4, FM-41). La empresa las revisa igual en la revalidación (F2).
    /// </summary>
    public static bool ExigeSoporteDelCambio(OrigenDeSeccion origen) => origen == OrigenDeSeccion.Tercero;

    /// <summary>
    /// Registra el cruce de la versión <paramref name="numero"/> con una versión de documento (F5, FM-8). Una sola vez por par
    /// (versión de la sección, versión del documento): si ya está, o la versión no existe, nulo (idempotente).
    /// </summary>
    public CruceDeSeccionRegistrado? RegistrarCruce(int numero, CruceDeSeccion cruce) =>
        Versiones.FirstOrDefault(v => v.Numero == numero) is { } version && !version.Cruces.Any(c => c.Es(cruce.DocumentoId, cruce.VersionDocumento))
            ? new CruceDeSeccionRegistrado(numero, cruce)
            : null;

    /// <summary>Datos tipados de la versión vigente (para las referencias a catálogos al recalcular).</summary>
    public object? DatosVigentes(DefinicionDeSeccion seccion) => Vigente is { } v ? seccion.Esquema.Deserializar(v.Datos) : null;

    // ---------- Evolución ----------

    public static SeccionDeFicha Create(SeccionDeFichaIniciada e) => new()
    {
        Id = e.SeccionId,
        TerceroId = e.TerceroId,
        Seccion = e.Seccion,
        Periodo = e.Periodo,
        CreadaEn = e.Fecha,
        ActualizadaEn = e.Fecha
    };

    public void Apply(VersionDeSeccionRegistrada e)
    {
        Versiones.Add(new VersionDeSeccion
        {
            Numero = e.Numero,
            Datos = e.Datos.Clone(),
            HuellaDatos = e.HuellaDatos,
            Huella = e.Huella,
            Soportes = [.. e.Soportes],
            Completitud = e.Completitud,
            EsquemaVersion = e.EsquemaVersion,
            HuellaDatosAnterior = e.HuellaDatosAnterior,
            Origen = e.Origen,
            Autor = e.Autor,
            Fecha = e.Fecha
        });
        VersionVigente = e.Numero;
        ActualizadaEn = e.Fecha;
    }

    public void Apply(CruceDeSeccionRegistrado e)
    {
        if (Versiones.FirstOrDefault(v => v.Numero == e.Numero) is { } version && !version.Cruces.Any(c => c.Es(e.Cruce.DocumentoId, e.Cruce.VersionDocumento)))
            version.Cruces.Add(e.Cruce);
    }

    public void Apply(CompletitudDeSeccionRecalculada e)
    {
        if (Versiones.FirstOrDefault(v => v.Numero == e.Numero) is { } version)
            version.Completitud = e.Completitud;
        ActualizadaEn = e.Fecha;
    }

    /// <summary>Copia con los eventos aplicados (para responder sin releer).</summary>
    public SeccionDeFicha Proyectar(IEnumerable<object> eventos)
    {
        var copia = JsonSerializer.Deserialize<SeccionDeFicha>(JsonSerializer.Serialize(this))!;
        foreach (var evento in eventos)
        {
            switch (evento)
            {
                case VersionDeSeccionRegistrada e: copia.Apply(e); break;
                case CompletitudDeSeccionRecalculada e: copia.Apply(e); break;
                case CruceDeSeccionRegistrado e: copia.Apply(e); break;
                default: throw new InvalidOperationException($"Evento no aplicable a SeccionDeFicha: {evento.GetType().Name}.");
            }
        }

        return copia;
    }
}

/// <summary>Estado de una sección (y periodo) en la ficha del tercero.</summary>
public sealed class EstadoDeSeccion
{
    public string Seccion { get; set; } = string.Empty;

    public int? Periodo { get; set; }

    public int Version { get; set; }

    public bool Completa { get; set; }

    public List<FaltanteDeSeccion> Faltantes { get; set; } = [];

    public int ReglaVersion { get; set; }

    public DateTimeOffset Actualizada { get; set; }
}

/// <summary>
/// Proyección <c>EstadoDeFicha</c> del tercero (FM-16): una fila por sección (y por periodo en la financiera) con la versión
/// vigente, si está completa, sus faltantes y la versión de reglas. Se escribe en la misma transacción que el evento que la
/// cambia (con concurrencia optimista) y la leen el wizard y las empresas: nadie recalcula al leer.
/// </summary>
public sealed class EstadoDeFicha
{
    public Guid Id { get; set; }

    public List<EstadoDeSeccion> Secciones { get; set; } = [];

    public DateTimeOffset ActualizadoEn { get; set; }

    public static EstadoDeFicha Nuevo(Guid terceroId) => new() { Id = terceroId };

    public EstadoDeSeccion? Para(string seccion, int? periodo) => Secciones.FirstOrDefault(s => s.Seccion == seccion && s.Periodo == periodo);

    /// <summary>Refleja la versión vigente de la sección (sin vigente, quita la fila). Devuelve si cambió algo.</summary>
    public bool Reflejar(SeccionDeFicha seccion)
    {
        var previa = Para(seccion.Seccion, seccion.Periodo);
        if (seccion.Vigente is not { } vigente)
            return previa is not null && Secciones.Remove(previa);

        if (previa is not null && previa.Version == vigente.Numero && previa.Completa == vigente.Completitud.Completa &&
            previa.ReglaVersion == vigente.Completitud.ReglaVersion && previa.Faltantes.SequenceEqual(vigente.Completitud.Faltantes))
            return false;

        Secciones.Remove(previa!);
        Secciones.Add(new EstadoDeSeccion
        {
            Seccion = seccion.Seccion,
            Periodo = seccion.Periodo,
            Version = vigente.Numero,
            Completa = vigente.Completitud.Completa,
            Faltantes = [.. vigente.Completitud.Faltantes],
            ReglaVersion = vigente.Completitud.ReglaVersion,
            Actualizada = seccion.ActualizadaEn
        });
        var orden = CatalogoDeSecciones.Todas.Select(s => s.Codigo).ToList();
        Secciones = [.. Secciones.OrderBy(s => orden.IndexOf(s.Seccion)).ThenByDescending(s => s.Periodo)];
        ActualizadoEn = seccion.ActualizadaEn;
        return true;
    }

    /// <summary>
    /// Fila que cuenta para el wizard: la de la sección o, en la financiera, la del periodo más reciente diligenciado
    /// (qué periodos exige cada empresa llega con F2).
    /// </summary>
    public EstadoDeSeccion? DelWizard(DefinicionDeSeccion seccion) =>
        seccion.PorPeriodo
            ? Secciones.Where(s => s.Seccion == seccion.Codigo).MaxBy(s => s.Periodo)
            : Para(seccion.Codigo, null);

    public bool Completa(DefinicionDeSeccion seccion) => DelWizard(seccion)?.Completa == true;

    /// <summary>¿Todas las secciones del wizard del tipo de persona están completas?</summary>
    public bool FichaCompleta(TipoPersona tipoPersona) => CatalogoDeSecciones.OrdenDelWizard(tipoPersona).All(Completa);

    /// <summary>
    /// Siguiente sección del wizard (FM-17). Sin <paramref name="despuesDe"/>: la primera incompleta. Tras guardar una
    /// sección: ella misma si quedó incompleta (el wizard no avanza); si quedó completa, la siguiente incompleta en el
    /// orden y, si no hay, la primera incompleta anterior. Nula si todo está completo.
    /// </summary>
    public string? SiguienteSeccion(TipoPersona tipoPersona, string? despuesDe = null)
    {
        var orden = CatalogoDeSecciones.OrdenDelWizard(tipoPersona);
        var indice = despuesDe is null ? -1 : orden.ToList().FindIndex(s => s.Codigo == despuesDe);
        if (indice >= 0 && !Completa(orden[indice]))
            return orden[indice].Codigo;

        var siguientes = orden.Skip(indice + 1).Concat(orden.Take(Math.Max(indice, 0)));
        return siguientes.FirstOrDefault(s => !Completa(s))?.Codigo;
    }
}
