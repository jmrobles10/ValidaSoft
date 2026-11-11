using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Ficha.Importacion;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Aplicacion.Importacion;

/// <summary>Filas ya validables de un catálogo de plataforma que salen de un sistema de origen, con las que no se pudieron convertir (motivo por fila).</summary>
public sealed record CatalogoImportable(CatalogoDePlataforma Catalogo, IReadOnlyList<FilaDeCatalogo> Filas, IReadOnlyList<string> Rechazos);

/// <summary>
/// Traduce un tercero de origen a las secciones de la ficha. <paramref name="plataforma"/>: los catálogos de plataforma (los de la
/// base más los que la corrida importará); <paramref name="hoy"/>: la fecha de negocio; <paramref name="registrado"/>: el tipo de
/// persona con que el tercero ya existe en Validasoft, si existe. Debe ser una función pura (sin E/S).
/// </summary>
public delegate ResultadoDeTraduccion TraducirTercero(ICatalogosDeFicha plataforma, DateOnly hoy, TipoPersona? registrado);

/// <summary>
/// Un tercero leído del origen. <see cref="Clave"/>: su clave en el sistema de origen (el cursor de los lotes; el orden lo define
/// el adaptador). <see cref="UnidadesAceptadas"/>: las unidades de origen (en cada sistema, algo como «la cuenta de cada empresa»)
/// donde el tercero estaba aprobado, para dejar aceptadas sus secciones en la empresa que la corrida mapee a esa unidad.
/// </summary>
public sealed record TerceroDeOrigen(string Clave, IReadOnlyList<string> UnidadesAceptadas, TraducirTercero Traducir);

/// <summary>De dónde lee el importador un sistema de origen: los catálogos y, por lotes, sus terceros. Solo lectura.</summary>
public interface IOrigenDeTerceros
{
    Task<IReadOnlyList<CatalogoImportable>> CatalogosAsync(CancellationToken ct);

    /// <summary>Hasta <paramref name="tamano"/> terceros con clave posterior a <paramref name="despuesDe"/>, en el orden del adaptador (el cursor del lote).</summary>
    Task<IReadOnlyList<TerceroDeOrigen>> TercerosAsync(string? despuesDe, int tamano, CancellationToken ct);

    /// <summary>Un tercero por su clave, o nulo si el origen no lo tiene.</summary>
    Task<TerceroDeOrigen?> TerceroAsync(string clave, CancellationToken ct);
}

/// <summary>
/// Adaptador de un sistema de origen (un ERP, un portal, un CRM) para el importador y el sincronizador genéricos. El núcleo no
/// sabe nada del sistema: el adaptador abre su origen y traduce sus terceros al modelo de la ficha. Se registra por inyección de
/// dependencias; sin adaptadores, el importador responde 404 <c>origen-no-registrado</c> y todo lo demás funciona igual.
/// </summary>
public interface IAdaptadorDeOrigen
{
    /// <summary>Código del sistema de origen, en minúsculas (<c>portal</c>). Va en la ruta, en la clave del legado y en la referencia externa del tercero.</summary>
    string Codigo { get; }

    /// <summary>¿Hay un origen conectado (base de datos o API) configurado?</summary>
    bool ConectadoConfigurado { get; }

    /// <summary>Valida el JSON de una corrida de origen <c>Archivo</c> (con la forma propia del adaptador); 422 <c>archivo-invalido</c> si no la tiene.</summary>
    void ValidarArchivo(string json);

    /// <summary>El origen que lee ese JSON (pruebas, un dry-run sin red).</summary>
    IOrigenDeTerceros DesdeArchivo(string json);

    /// <summary>El origen conectado de la configuración del adaptador; sin configurar ⇒ 409 <c>origen-no-configurado</c>.</summary>
    IOrigenDeTerceros Conectado();

    /// <summary>El que usa el sincronizador: el archivo de la configuración (desarrollo) o, si no hay, el conectado.</summary>
    IOrigenDeTerceros Configurado();
}

/// <summary>Los adaptadores registrados, por código.</summary>
public sealed class RegistroDeAdaptadores(IEnumerable<IAdaptadorDeOrigen> adaptadores)
{
    private readonly Dictionary<string, IAdaptadorDeOrigen> _porCodigo =
        adaptadores.ToDictionary(a => a.Codigo.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public IReadOnlyCollection<string> Codigos => [.. _porCodigo.Keys.Order(StringComparer.Ordinal)];

    /// <summary>El adaptador del código; si no hay ninguno registrado ⇒ 404 <c>origen-no-registrado</c>.</summary>
    public IAdaptadorDeOrigen Obtener(string? codigo) =>
        codigo is not null && _porCodigo.TryGetValue(codigo.Trim().ToLowerInvariant(), out var adaptador)
            ? adaptador
            : throw ReglaDeNegocioException.NoEncontrado("origen-no-registrado",
                $"No hay un adaptador de origen «{codigo}». Registrados: {(_porCodigo.Count == 0 ? "ninguno" : string.Join(", ", Codigos))}.");
}
