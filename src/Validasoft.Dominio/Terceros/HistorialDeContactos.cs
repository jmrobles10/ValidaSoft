using System.Security.Cryptography;
using System.Text;

namespace Validasoft.Dominio.Terceros;

// ---------- Eventos (stream HistorialDeContactos-{id}, tenant técnico «boveda») ----------

/// <summary>Un correo pasó a ser contacto del tercero. <see cref="Origen"/>: <c>Empresa</c>, <c>Autorregistro</c>, <c>Importacion</c>, <c>Aprobacion</c> o <c>Plataforma</c>.</summary>
public sealed record ContactoRegistrado(Guid TerceroId, string Correo, string Origen, string? Detalle, DateTimeOffset Fecha);

/// <summary>El correo del contacto quedó probado como del tercero por <see cref="Medio"/>; <see cref="Por"/> es quien lo decidió cuando fue una persona (un contacto o un operador).</summary>
public sealed record ContactoVerificado(Guid TerceroId, string Correo, MedioDeVerificacion Medio, string? Por, DateTimeOffset Fecha);

/// <summary>
/// El contacto estaba sin verificar y otro correo se verificó: el verificado prevalece y este pasa a acceso aislado (un contacto verificado o la plataforma lo aprueba o lo
/// rechaza). Sus versiones globales cargadas quedan marcadas para revisión.
/// </summary>
public sealed record ContactoPasoAAislado(Guid TerceroId, string Correo, string Motivo, DateTimeOffset Fecha);

/// <summary>Una línea del historial de los contactos de un tercero.</summary>
public sealed record EntradaDeHistorialDeContactos(DateTimeOffset Fecha, string Tipo, string Correo, string? Detalle);

/// <summary>
/// Historial de los contactos de un tercero: quién entró como contacto, cómo se verificó y quién pasó a acceso aislado. Un stream por tercero en la bóveda (snapshot inline);
/// el estado vigente de cada contacto vive en <see cref="Tercero.Contactos"/>. Lo lee el tercero (sus contactos) y la consola de Plataforma (la evidencia de una solicitud).
/// </summary>
public sealed class HistorialDeContactos
{
    public const string Registrado = "registrado";
    public const string Verificado = "verificado";
    public const string PasoAAislado = "paso_a_aislado";

    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid TerceroId { get; set; }

    public List<EntradaDeHistorialDeContactos> Entradas { get; set; } = [];

    /// <summary>Id determinista del historial de un tercero: escribirlo dos veces no crea dos.</summary>
    public static Guid IdPara(Guid terceroId) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"validasoft:historial-de-contactos:{terceroId:N}"))[..16]);

    public static HistorialDeContactos Create(ContactoRegistrado e) => Nuevo(e.TerceroId).Con(e);

    public static HistorialDeContactos Create(ContactoVerificado e) => Nuevo(e.TerceroId).Con(e);

    public static HistorialDeContactos Create(ContactoPasoAAislado e) => Nuevo(e.TerceroId).Con(e);

    public void Apply(ContactoRegistrado e) => Con(e);

    public void Apply(ContactoVerificado e) => Con(e);

    public void Apply(ContactoPasoAAislado e) => Con(e);

    private static HistorialDeContactos Nuevo(Guid terceroId) => new() { Id = IdPara(terceroId), TerceroId = terceroId };

    private HistorialDeContactos Con(ContactoRegistrado e)
    {
        Entradas.Add(new EntradaDeHistorialDeContactos(e.Fecha, Registrado, e.Correo, e.Detalle is { Length: > 0 } ? $"{e.Origen}: {e.Detalle}" : e.Origen));
        return this;
    }

    private HistorialDeContactos Con(ContactoVerificado e)
    {
        Entradas.Add(new EntradaDeHistorialDeContactos(e.Fecha, Verificado, e.Correo, e.Por is { Length: > 0 } ? $"{e.Medio} ({e.Por})" : e.Medio.ToString()));
        return this;
    }

    private HistorialDeContactos Con(ContactoPasoAAislado e)
    {
        Entradas.Add(new EntradaDeHistorialDeContactos(e.Fecha, PasoAAislado, e.Correo, e.Motivo));
        return this;
    }
}
