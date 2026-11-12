using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marten;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Aplicacion.Verificaciones;

/// <summary>Persona que se consulta en listas: el tercero o una persona relacionada de su ficha.</summary>
public sealed record PersonaAConsultar(string Clave, string Rol, string Nombre, string Pais, string TipoIdentificacion, string Numero)
{
    public PersonaConsultada AConsultada() => new(Clave, Rol, Nombre, $"{TipoIdentificacion} {Numero}");
}

/// <summary>Credenciales de la empresa ya descifradas (solo viven en memoria durante la consulta).</summary>
public sealed record CredencialesEnClaro(string Usuario, string Clave, string? UrlBase, ModoBusquedaListas ModoBusqueda);

/// <summary>Lo que se le pide a un proveedor: todas las personas de una vez (cada una es una búsqueda facturable).</summary>
public sealed record SolicitudDeListas(Guid EmpresaId, IReadOnlyList<PersonaAConsultar> Personas, CredencialesEnClaro? Credenciales);

/// <summary>
/// Respuesta de un proveedor: coincidencias por persona, el criterio usado (para el PDF), las listas cubiertas, el PDF del
/// proveedor si lo da y su respuesta cruda (JSON) para guardarla con su hash (VE-10).
/// </summary>
public sealed record RespuestaDeListas(string ReferenciaExterna, IReadOnlyList<CoincidenciaHallada> Coincidencias, string Criterio,
    IReadOnlyList<string> ListasConsultadas, byte[]? PdfDelProveedor, string? RespuestaCruda);

/// <summary>
/// Adaptador de un proveedor de listas restrictivas (VE-7). La implementación simulada es determinista; las reales
/// (Infolaft —la principal—, Stradata, Compliance) viven en infraestructura y solo se usan si la instalación las habilita
/// y la empresa configuró sus credenciales. Un proveedor caído lanza <see cref="ProveedorDeListasNoDisponibleException"/>.
/// </summary>
public interface IVerificadorDeListas
{
    string Proveedor { get; }

    Task<RespuestaDeListas> ConsultarAsync(SolicitudDeListas solicitud, CancellationToken ct);
}

public sealed class ProveedorDeListasNoDisponibleException(string mensaje, Exception? causa = null) : Exception(mensaje, causa);

/// <summary>Datos del PDF de evidencia que genera Validasoft (VE-10): qué, a quién, con qué criterio, cuándo y quién.</summary>
public sealed record DatosEvidenciaDeListas(
    string Empresa, string Tercero, string Contexto, string Proveedor, string ReferenciaExterna, DateTimeOffset Fecha, string ConsultadoPor,
    string Criterio, IReadOnlyList<string> ListasConsultadas, IReadOnlyList<PersonaConsultada> Personas, IReadOnlyList<CoincidenciaHallada> Coincidencias,
    string? Sha256Respuesta);

/// <summary>PDF de la evidencia de una consulta de listas (implementación en infraestructura, sin dependencias).</summary>
public interface IGeneradorDePdfDeEvidencia
{
    byte[] Generar(DatosEvidenciaDeListas datos);
}

/// <summary>
/// Configuración de las verificaciones (<c>Validasoft:Verificadores</c>, docs/11 §2.7.9): proveedor de la instalación,
/// si el simulado está permitido (solo desarrollo y pruebas), qué proveedores reales habilitó la instalación con su URL,
/// tiempo de espera, concurrencia y cortacircuito (VE-8), el monitoreo periódico de vínculos activos (VE-12), la vigencia del
/// estudio (30 días) y cada cuánto corre el procesador.
/// </summary>
public sealed class OpcionesDeVerificaciones(IConfiguration configuracion)
{
    private const string Raiz = "Validasoft:Verificadores";

    public bool PermitirSimulado { get; } = configuracion.GetValue($"{Raiz}:ListasRestrictivas:PermitirSimulado", false);

    public string? ProveedorPorDefecto { get; } = ProveedoresDeListas.Normalizar(configuracion[$"{Raiz}:ListasRestrictivas:ProveedorPorDefecto"]);

    public int DiasMonitoreo { get; } = Math.Clamp(configuracion.GetValue($"{Raiz}:ListasRestrictivas:DiasMonitoreo", 30), 0, 365);

    public int MinutosReintento { get; } = Math.Clamp(configuracion.GetValue($"{Raiz}:ListasRestrictivas:MinutosReintento", 5), 1, 1440);

    public int VigenciaEstudioDias { get; } = Math.Clamp(configuracion.GetValue($"{Raiz}:EstudioCumplimiento:VigenciaDias", 30), 1, 3650);

    public TimeSpan IntervaloProceso { get; } = TimeSpan.FromSeconds(Math.Clamp(configuracion.GetValue($"{Raiz}:SegundosProceso", 30), 1, 3600));

    public OpcionesDeProveedor Proveedor(string nombre) => new(
        nombre,
        nombre == ProveedoresDeListas.Simulado ? PermitirSimulado : configuracion.GetValue($"{Raiz}:ListasRestrictivas:Proveedores:{nombre}:Habilitado", false),
        configuracion[$"{Raiz}:ListasRestrictivas:Proveedores:{nombre}:UrlBase"] ?? UrlPorOmision(nombre),
        TimeSpan.FromSeconds(Math.Clamp(configuracion.GetValue($"{Raiz}:ListasRestrictivas:Proveedores:{nombre}:TimeoutSegundos", 30), 1, 300)),
        Math.Clamp(configuracion.GetValue($"{Raiz}:ListasRestrictivas:Proveedores:{nombre}:MaximoConcurrencia", 4), 1, 64),
        Math.Clamp(configuracion.GetValue($"{Raiz}:ListasRestrictivas:Proveedores:{nombre}:FallosParaAbrir", 5), 1, 100),
        TimeSpan.FromSeconds(Math.Clamp(configuracion.GetValue($"{Raiz}:ListasRestrictivas:Proveedores:{nombre}:SegundosAbierto", 60), 1, 3600)));

    /// <summary>URL pública conocida de cada proveedor (la empresa puede traer la de su contrato).</summary>
    public static string? UrlPorOmision(string nombre) => nombre switch
    {
        ProveedoresDeListas.Infolaft => "https://infolaftsearch.com/infolaft/",
        ProveedoresDeListas.Stradata => "https://sds.stradata.com.co/api/v3/",
        ProveedoresDeListas.Compliance => "https://app.compliance.com.co/validador/",
        _ => null
    };
}

public sealed record OpcionesDeProveedor(string Nombre, bool Habilitado, string? UrlBase, TimeSpan Timeout, int MaximoConcurrencia, int FallosParaAbrir,
    TimeSpan TiempoAbierto);

/// <summary>
/// Protege a los proveedores reales y a Validasoft (VE-8): a lo sumo N consultas a la vez por proveedor, tiempo de espera y
/// un cortacircuito que, tras varios fallos seguidos, deja de llamar al proveedor durante un rato (503 inmediato).
/// </summary>
public sealed class ControlDeProveedores(TimeProvider reloj)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaforos = new();
    private readonly ConcurrentDictionary<string, (int Fallos, DateTimeOffset? AbiertoHasta)> _estado = new();

    public async Task<T> EjecutarAsync<T>(OpcionesDeProveedor opciones, Func<CancellationToken, Task<T>> accion, CancellationToken ct)
    {
        var estado = _estado.GetValueOrDefault(opciones.Nombre);
        if (estado.AbiertoHasta is { } hasta && reloj.GetUtcNow() < hasta)
            throw new ProveedorDeListasNoDisponibleException($"{opciones.Nombre} falló varias veces seguidas; se reintentará después de {hasta:HH:mm} UTC.");

        var semaforo = _semaforos.GetOrAdd(opciones.Nombre, _ => new SemaphoreSlim(opciones.MaximoConcurrencia, opciones.MaximoConcurrencia));
        await semaforo.WaitAsync(ct);
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(opciones.Timeout);
            var resultado = await accion(limite.Token);
            _estado[opciones.Nombre] = (0, null);
            return resultado;
        }
        catch (Exception e) when (e is not ReglaDeNegocioException && !ct.IsCancellationRequested)
        {
            var fallos = _estado.GetValueOrDefault(opciones.Nombre).Fallos + 1;
            _estado[opciones.Nombre] = (fallos, fallos >= opciones.FallosParaAbrir ? reloj.GetUtcNow() + opciones.TiempoAbierto : null);
            throw e is ProveedorDeListasNoDisponibleException ? e : new ProveedorDeListasNoDisponibleException($"{opciones.Nombre} no respondió: {e.Message}", e);
        }
        finally
        {
            semaforo.Release();
        }
    }
}

/// <summary>
/// Lo que hay que saber para consultar: el proveedor que toca, sus opciones y las credenciales (las de la plataforma si el proveedor es suyo,
/// <see cref="CuentaDeLaPlataforma"/>, o las de la empresa).
/// </summary>
public sealed record ProveedorResuelto(string Nombre, OpcionesDeProveedor Opciones, CredencialesDeListas? Credenciales, IVerificadorDeListas Verificador,
    bool CuentaDeLaPlataforma = false);

/// <summary>
/// Consulta de listas restrictivas (VE-7…VE-11): resuelve el proveedor de la empresa, arma las personas (el tercero y sus
/// representantes, suplentes, socios y PEP de la ficha legal), revisa la cuota, consulta con control de concurrencia y
/// cortacircuito, guarda la evidencia (PDF del proveedor o uno propio, y la respuesta cruda con su hash) y cuenta el consumo
/// del mes por empresa y proveedor. No decide nada: el expediente o el monitoreo registran el resultado.
/// </summary>
public sealed class ConsultaDeListas(
    IEnumerable<IVerificadorDeListas> verificadores, OpcionesDeVerificaciones opciones, ControlDeProveedores control, IAlmacenDeArchivos almacen,
    IGeneradorDePdfDeEvidencia generadorPdf, IProtectorDeSecretos protector, IDocumentStore store, IEntornos entornos, TimeProvider reloj)
{
    public const string TipoPdf = "application/pdf";

    public OpcionesDeVerificaciones Opciones => opciones;

    /// <summary>La cuenta de la plataforma con los proveedores de listas (G-2): un solo documento global; nulo si Plataforma aún no la configuró.</summary>
    public async Task<ListasRestrictivasDePlataforma?> PlataformaAsync(CancellationToken ct)
    {
        await using var global = store.QuerySession();
        return await global.LoadAsync<ListasRestrictivasDePlataforma>(ListasRestrictivasDePlataforma.IdUnico, ct);
    }

    /// <summary>
    /// Proveedor que usará la empresa: el que eligió o el de la instalación. 503 <c>verificacion-no-disponible</c> si no hay
    /// ninguno, si la instalación no lo habilitó, si falta su adaptador o si es de la plataforma y Plataforma aún no configuró su cuenta;
    /// 422 <c>credenciales-requeridas</c> si es de la empresa y no cargó las suyas. Las credenciales de Infolaft son las de la plataforma
    /// (<paramref name="plataforma"/>), nunca las de la empresa (G-2).
    /// </summary>
    public ProveedorResuelto Resolver(ListasRestrictivasEmpresa? configuracion, ListasRestrictivasDePlataforma? plataforma, bool sandbox = false)
    {
        // En el sandbox (SB-4) siempre el proveedor simulado, aunque la instalación no lo permita: ninguna consulta de prueba sale a un
        // proveedor real ni gasta la cuota de la empresa.
        var nombre = sandbox ? ProveedoresDeListas.Simulado
            : configuracion?.Proveedor ?? opciones.ProveedorPorDefecto
              ?? throw ReglaDeNegocioException.NoDisponible("verificacion-no-disponible",
                  "La empresa no tiene un proveedor de listas restrictivas configurado (Configuración › Listas restrictivas).");
        var deProveedor = sandbox ? opciones.Proveedor(nombre) with { Habilitado = true } : opciones.Proveedor(nombre);
        if (!deProveedor.Habilitado)
            throw ReglaDeNegocioException.NoDisponible("verificacion-no-disponible", $"{nombre} no está habilitado en esta instalación.");
        var verificador = verificadores.FirstOrDefault(v => v.Proveedor == nombre)
                          ?? throw ReglaDeNegocioException.NoDisponible("verificacion-no-disponible", $"No hay adaptador para {nombre}.");
        var deLaPlataforma = ProveedoresDeListas.EsDeLaPlataforma(nombre);
        var credenciales = deLaPlataforma ? plataforma?.CredencialesDe(nombre) : configuracion?.CredencialesDe(nombre);
        if (!sandbox && ProveedoresDeListas.Reales.Contains(nombre) && credenciales is null)
            throw deLaPlataforma
                ? ReglaDeNegocioException.NoDisponible("verificacion-no-disponible", $"La plataforma aún no configuró la cuenta de {nombre}.")
                : ReglaDeNegocioException.Validacion("credenciales-requeridas", $"La empresa no configuró sus credenciales de {nombre}.");
        return new ProveedorResuelto(nombre, deProveedor, credenciales, verificador, deLaPlataforma);
    }

    /// <summary>El proveedor efectivo sin lanzar (para mostrarlo en la configuración); nulo si no hay ninguno utilizable.</summary>
    public string? Efectivo(ListasRestrictivasEmpresa? configuracion, ListasRestrictivasDePlataforma? plataforma)
    {
        try
        {
            return Resolver(configuracion, plataforma).Nombre;
        }
        catch (ReglaDeNegocioException)
        {
            return null;
        }
    }

    /// <summary>
    /// El tercero y, de su ficha legal vigente, representantes legales, suplentes, socios y quien se declaró PEP (sin repetir
    /// identificación). Las filas sin nombre o sin número no se consultan.
    /// </summary>
    public static async Task<IReadOnlyList<PersonaAConsultar>> PersonasAsync(IDocumentSession sesion, Guid terceroId, CancellationToken ct)
    {
        var tercero = await sesion.LoadAsync<Tercero>(terceroId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero del expediente no existe.");
        var personas = new List<PersonaAConsultar>
        {
            new(tercero.Identificacion.Clave, "Tercero", tercero.Nombre, tercero.Identificacion.Pais, tercero.Identificacion.Tipo, tercero.Identificacion.Numero)
        };

        var legal = await sesion.ForTenant(TenantsTecnicos.Boveda).LoadAsync<SeccionDeFicha>(SeccionDeFicha.IdPara(terceroId, CodigosDeSeccion.Legal, null), ct);
        if (legal?.Vigente is { } version)
        {
            var datos = JsonDeFicha.Deserializar<DatosLegales>(version.Datos);
            foreach (var persona in datos.Personas ?? [])
            {
                var rol = persona.Pep == true ? "Pep" : persona.Rol switch
                {
                    RolPersonaRelacionada.RepresentanteLegal => "RepresentanteLegal",
                    RolPersonaRelacionada.Suplente => "Suplente",
                    RolPersonaRelacionada.Socio => "Socio",
                    _ => null
                };
                var nombre = string.Join(' ', new[] { persona.Nombres, persona.Apellidos }.Where(n => !string.IsNullOrWhiteSpace(n))).Trim();
                if (rol is null || nombre.Length == 0 || string.IsNullOrWhiteSpace(persona.Numero) || string.IsNullOrWhiteSpace(persona.TipoIdentificacion))
                    continue;
                var pais = string.IsNullOrWhiteSpace(persona.Nacionalidad) ? tercero.Identificacion.Pais : persona.Nacionalidad.Trim().ToUpperInvariant();
                var clave = $"{pais}|{persona.TipoIdentificacion.Trim().ToUpperInvariant()}|{persona.Numero.Trim()}";
                if (personas.Any(p => string.Equals(p.Clave, clave, StringComparison.OrdinalIgnoreCase)))
                    continue;
                personas.Add(new PersonaAConsultar(clave, rol, nombre, pais, persona.TipoIdentificacion.Trim().ToUpperInvariant(), persona.Numero.Trim()));
            }
        }

        return personas;
    }

    /// <summary>
    /// Consulta a las personas del tercero y devuelve el resultado listo para registrar, con la evidencia ya guardada en el
    /// almacén del tenant. 503 <c>verificacion-no-disponible</c> si el proveedor falla; 422 <c>cuota-listas-agotada</c> si la
    /// consulta superaría la cuota mensual de la empresa. <paramref name="contexto"/>: folio del expediente (va en el PDF).
    /// </summary>
    public async Task<ResultadoDeListas> ConsultarAsync(IDocumentSession sesion, Guid tenantId, Guid empresaId, Guid terceroId, string contexto,
        string consultadoPor, int mesesVigencia, CancellationToken ct)
    {
        var configuracion = await sesion.LoadAsync<ListasRestrictivasEmpresa>(empresaId, ct);
        var proveedor = Resolver(configuracion, await PlataformaAsync(ct), await entornos.EsSandboxAsync(tenantId, ct));
        var personas = await PersonasAsync(sesion, terceroId, ct);
        var ahora = reloj.GetUtcNow();
        var mes = UsoMensualListas.MesDe(CalculadoraDeVigencia.Fecha(ahora));

        var uso = await sesion.LoadAsync<UsoMensualListas>(UsoMensualListas.Clave(empresaId, proveedor.Nombre, mes), ct);
        if (!(uso ?? UsoMensualListas.Nuevo(empresaId, proveedor.Nombre, mes)).Cabe(proveedor.Credenciales?.CuotaMensual, personas.Count))
            throw ReglaDeNegocioException.Validacion("cuota-listas-agotada",
                $"La empresa llegó a la cuota mensual de {proveedor.Nombre} ({proveedor.Credenciales!.CuotaMensual} búsquedas).");

        // La clave de la plataforma se descifra con la clave maestra; la de una empresa (Stradata, Compliance), con la de su grupo (CU-20).
        var credenciales = proveedor.Credenciales is { } c
            ? new CredencialesEnClaro(c.Usuario, proveedor.CuentaDeLaPlataforma ? protector.Desproteger(c.ClaveProtegida) : protector.DesprotegerDeGrupo(tenantId, c.ClaveProtegida),
                c.UrlBase ?? proveedor.Opciones.UrlBase, c.ModoBusqueda)
            : null;
        RespuestaDeListas respuesta;
        try
        {
            respuesta = await control.EjecutarAsync(proveedor.Opciones,
                t => proveedor.Verificador.ConsultarAsync(new SolicitudDeListas(empresaId, personas, credenciales), t), ct);
        }
        catch (ProveedorDeListasNoDisponibleException e)
        {
            throw ReglaDeNegocioException.NoDisponible("verificacion-no-disponible", e.Message);
        }

        var empresa = await sesion.LoadAsync<Empresa>(empresaId, ct);
        var tercero = personas[0];
        var bucket = ClavesDeAlmacen.BucketDeTenant(tenantId);
        await almacen.AsegurarBucketAsync(bucket, ct);

        string? claveRespuesta = null, shaRespuesta = null;
        if (respuesta.RespuestaCruda is { } cruda)
        {
            var bytes = Encoding.UTF8.GetBytes(cruda);
            shaRespuesta = Convert.ToHexStringLower(SHA256.HashData(bytes));
            claveRespuesta = ClavesDeAlmacen.EvidenciaDeVerificacion(empresaId, shaRespuesta, "json");
            await almacen.GuardarAsync(bucket, claveRespuesta, bytes, "application/json",
                new Dictionary<string, string> { ["proveedor"] = proveedor.Nombre, ["referencia"] = respuesta.ReferenciaExterna }, ct);
        }

        var consultadas = personas.Select(p => p.AConsultada()).ToList();
        var pdf = respuesta.PdfDelProveedor ?? generadorPdf.Generar(new DatosEvidenciaDeListas(empresa?.Nombre ?? "Empresa", tercero.Nombre, contexto,
            proveedor.Nombre, respuesta.ReferenciaExterna, ahora, consultadoPor, respuesta.Criterio, respuesta.ListasConsultadas, consultadas,
            respuesta.Coincidencias, shaRespuesta));
        var shaPdf = Convert.ToHexStringLower(SHA256.HashData(pdf));
        var clavePdf = ClavesDeAlmacen.EvidenciaDeVerificacion(empresaId, shaPdf, "pdf");
        await almacen.GuardarAsync(bucket, clavePdf, pdf, TipoPdf,
            new Dictionary<string, string> { ["proveedor"] = proveedor.Nombre, ["referencia"] = respuesta.ReferenciaExterna }, ct);

        await RegistrarUsoAsync(tenantId, empresaId, proveedor.Nombre, mes, respuesta.ReferenciaExterna, personas.Count, respuesta.Coincidencias.Count > 0, ct);

        var nombreArchivo = $"listas-{contexto}-{ahora.UtcDateTime.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.pdf";
        return new ResultadoDeListas(proveedor.Nombre, respuesta.ReferenciaExterna, consultadas, respuesta.Coincidencias,
            new EvidenciaDeVerificacion(bucket, clavePdf, shaPdf, pdf.LongLength, TipoPdf, nombreArchivo, respuesta.PdfDelProveedor is not null,
                claveRespuesta, shaRespuesta),
            ahora.AddMonths(mesesVigencia));
    }

    /// <summary>
    /// Suma la consulta al uso del mes en su propia transacción (concurrencia optimista con reintentos): una consulta ya
    /// pagada al proveedor siempre se cuenta, aunque el registro en el expediente pierda una carrera. Idempotente por referencia.
    /// </summary>
    private async Task RegistrarUsoAsync(Guid tenantId, Guid empresaId, string proveedor, string mes, string referencia, int busquedas,
        bool conCoincidencias, CancellationToken ct)
    {
        for (var intento = 0; ; intento++)
        {
            try
            {
                await using var sesion = store.LightweightSession(tenantId.ToString("N"));
                var uso = await sesion.LoadAsync<UsoMensualListas>(UsoMensualListas.Clave(empresaId, proveedor, mes), ct)
                          ?? UsoMensualListas.Nuevo(empresaId, proveedor, mes);
                if (!uso.Registrar(referencia, busquedas, conCoincidencias))
                    return;
                sesion.Store(uso);
                await sesion.SaveChangesAsync(ct);
                return;
            }
            catch (Exception e) when (intento < 5 && (Peticiones.EsCarrera(e) || e is Marten.Exceptions.MartenCommandException))
            {
                // Otra consulta de la misma empresa contó a la vez: se vuelve a leer y se suma.
            }
        }
    }
}

/// <summary>
/// Lee la respuesta de un proveedor de listas de forma tolerante (VE-8). El formato exacto de Infolaft (<c>Search/searchPaginated</c>) no está
/// documentado públicamente (supuesto), así que busca la lista de resultados en las claves habituales de una respuesta
/// paginada (<c>content</c>, <c>results</c>, <c>data</c>, <c>items</c>, <c>records</c>, <c>resultados</c>, o la raíz si es un
/// arreglo) y, en cada resultado, el nombre, la lista, el detalle y el puntaje con varios nombres posibles. Lo que no
/// entiende no lo inventa: un resultado sin nombre se toma con el nombre buscado.
/// </summary>
public static class LectorDeRespuestaDeListas
{
    private static readonly string[] ClavesLista = ["content", "results", "resultados", "data", "items", "records", "registros", "hits", "matches"];
    private static readonly string[] ClavesNombre = ["name", "nombre", "fullName", "nombreCompleto", "fullname", "matchedName", "razonSocial"];
    private static readonly string[] ClavesFuente = ["list", "lista", "listName", "nombreLista", "nombre_lista", "source", "fuente", "listType", "tipoLista", "category"];
    private static readonly string[] ClavesDetalle = ["description", "descripcion", "detalle", "observaciones", "remarks", "cargo", "detail"];
    private static readonly string[] ClavesPuntaje = ["score", "porcentaje", "accuracy", "match", "similarity", "similitud"];

    public static IReadOnlyList<CoincidenciaHallada> Leer(string json, PersonaAConsultar persona, string fuentePorOmision = "Infolaft")
    {
        using var documento = JsonDocument.Parse(json);
        var resultados = Resultados(documento.RootElement);
        var lista = new List<CoincidenciaHallada>();
        foreach (var r in resultados)
        {
            if (r.ValueKind != JsonValueKind.Object)
                continue;
            var nombre = Texto(r, ClavesNombre) ?? persona.Nombre;
            var fuente = Texto(r, ClavesFuente) ?? fuentePorOmision;
            lista.Add(new CoincidenciaHallada(persona.Clave, fuente, nombre, Texto(r, ClavesDetalle), Puntaje(r)));
        }

        return lista;
    }

    private static IEnumerable<JsonElement> Resultados(JsonElement raiz)
    {
        if (raiz.ValueKind == JsonValueKind.Array)
            return raiz.EnumerateArray().ToList();
        if (raiz.ValueKind != JsonValueKind.Object)
            return [];
        foreach (var clave in ClavesLista)
        {
            if (Propiedad(raiz, clave) is { } valor)
            {
                if (valor.ValueKind == JsonValueKind.Array)
                    return valor.EnumerateArray().ToList();
                if (valor.ValueKind == JsonValueKind.Object)
                    return Resultados(valor); // { "data": { "content": [...] } }
            }
        }

        return [];
    }

    private static JsonElement? Propiedad(JsonElement objeto, string nombre)
    {
        foreach (var p in objeto.EnumerateObject())
            if (string.Equals(p.Name, nombre, StringComparison.OrdinalIgnoreCase))
                return p.Value;
        return null;
    }

    private static string? Texto(JsonElement objeto, string[] claves)
    {
        foreach (var clave in claves)
        {
            if (Propiedad(objeto, clave) is not { } valor)
                continue;
            var texto = valor.ValueKind switch
            {
                JsonValueKind.String => valor.GetString(),
                JsonValueKind.Number => valor.GetRawText(),
                JsonValueKind.Array => string.Join(", ", valor.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString())),
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(texto))
                return texto.Trim();
        }

        return null;
    }

    private static readonly string[] ClavesPdfBase64 = ["pdf", "pdfBase64", "pdf_base64", "reportPdf", "reportBase64", "reporte", "report", "certificado", "certificate", "file"];
    private static readonly string[] ClavesPdfUrl = ["pdfUrl", "pdf_url", "urlPdf", "reportUrl", "report_url", "urlReporte", "certificateUrl", "downloadUrl"];

    /// <summary>
    /// El PDF que traiga la respuesta de un proveedor (CU-21): en base64 (lo decodifica y comprueba que sea PDF) o un enlace https para
    /// descargarlo. Busca en la raíz y un nivel dentro de <c>data</c>/<c>content</c>. Nada hallado ⇒ (nulo, nulo).
    /// </summary>
    public static (byte[]? Pdf, string? Enlace) PdfDe(string json)
    {
        using var documento = JsonDocument.Parse(json);
        foreach (var nivel in Niveles(documento.RootElement))
        {
            foreach (var clave in ClavesPdfBase64)
            {
                if (Propiedad(nivel, clave) is { ValueKind: JsonValueKind.String } valor)
                {
                    try
                    {
                        var bytes = Convert.FromBase64String(valor.GetString()!.Trim());
                        if (EsPdf(bytes))
                            return (bytes, null);
                    }
                    catch (FormatException)
                    {
                        // No era base64: se sigue buscando.
                    }
                }
            }

            foreach (var clave in ClavesPdfUrl)
            {
                if (Propiedad(nivel, clave) is { ValueKind: JsonValueKind.String } valor && !string.IsNullOrWhiteSpace(valor.GetString()))
                    return (null, valor.GetString()!.Trim());
            }
        }

        return (null, null);
    }

    public static bool EsPdf(byte[] bytes) => bytes.Length > 5 && bytes[0] == 0x25 && bytes[1] == 0x50 && bytes[2] == 0x44 && bytes[3] == 0x46 && bytes[4] == 0x2D;

    private static IEnumerable<JsonElement> Niveles(JsonElement raiz)
    {
        if (raiz.ValueKind != JsonValueKind.Object)
            yield break;
        yield return raiz;
        foreach (var clave in new[] { "data", "content", "result", "resultado" })
        {
            if (Propiedad(raiz, clave) is { ValueKind: JsonValueKind.Object } interior)
                yield return interior;
        }
    }

    private static decimal? Puntaje(JsonElement objeto)
    {
        foreach (var clave in ClavesPuntaje)
        {
            if (Propiedad(objeto, clave) is not { } valor)
                continue;
            if (valor.ValueKind == JsonValueKind.Number && valor.TryGetDecimal(out var numero))
                return numero;
            if (valor.ValueKind == JsonValueKind.String &&
                decimal.TryParse(valor.GetString()?.TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out var desdeTexto))
                return desdeTexto;
        }

        return null;
    }
}
