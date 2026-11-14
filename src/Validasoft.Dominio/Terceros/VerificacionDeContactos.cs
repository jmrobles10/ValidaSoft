using System.Text.Json;
using Validasoft.Dominio.Ficha;

namespace Validasoft.Dominio.Terceros;

/// <summary>Si el correo de un contacto del tercero está probado como del tercero (decisión AV del registro).</summary>
public enum EstadoDeVerificacion
{
    /// <summary>
    /// Alguien lo puso (la empresa que registró primero el NIT, el autorregistro) pero nada prueba que sea del tercero. Entra con acceso completo a lo de su tercero
    /// mientras nadie más esté involucrado, pero no aprueba ni rechaza accesos aislados y, si otro contacto se verifica, pasa a acceso aislado.
    /// </summary>
    SinVerificar,

    /// <summary>El correo coincide con un dato oficial del tercero, dos grupos distintos lo invitaron por su cuenta, lo verificó la plataforma o lo aprobó un contacto verificado.</summary>
    Verificado
}

/// <summary>Con qué se verificó un contacto.</summary>
public enum MedioDeVerificacion
{
    /// <summary>El correo es el correo electrónico del RUT del tercero (dato extraído de la versión actual del documento).</summary>
    CorreoDelRut,

    /// <summary>El correo es el del certificado de existencia y representación legal (Cámara de comercio) del tercero.</summary>
    CorreoDeLaCamara,

    /// <summary>Dos grupos (tenants) distintos invitaron de forma independiente el mismo correo.</summary>
    Corroborado,

    /// <summary>Un operador de la plataforma lo verificó (o aprobó su acceso desde la cola de la consola).</summary>
    Plataforma,

    /// <summary>Lo trajo la importación del sistema de origen (la corre la plataforma, no una empresa).</summary>
    Importacion,

    /// <summary>Un contacto ya verificado aprobó su acceso: responde por él.</summary>
    AprobadoPorContacto
}

/// <summary>Un correo electrónico oficial del tercero: el del RUT o el de la Cámara de comercio, con su documento de origen.</summary>
public sealed record CorreoOficial(string Correo, MedioDeVerificacion Medio, string Documento, int Version);

/// <summary>
/// Reglas puras de verificación de los contactos del tercero: qué correos oficiales trae un documento y cuándo un contacto se verifica por ellos o por corroboración.
/// La aplicación les pasa lo que leyó de la base.
/// </summary>
public static class VerificacionDeContactos
{
    /// <summary>Con cuántos grupos distintos que invitan por su cuenta el mismo correo se corrobora.</summary>
    public const int GruposParaCorroborar = 2;

    /// <summary>
    /// El correo que trae un documento oficial analizado con IA: el <c>correo</c> del RUT o el de la Cámara de comercio. Solo cuenta un análisis legible y sin alertas
    /// de manipulación (un documento alterado no verifica a nadie); un correo con forma inválida tampoco.
    /// </summary>
    public static CorreoOficial? DeDocumento(string? tipoCanonico, int version, bool legible, IReadOnlyCollection<string> alertas, IReadOnlyDictionary<string, JsonElement> campos)
    {
        var medio = tipoCanonico?.ToUpperInvariant() switch
        {
            "RUT" => MedioDeVerificacion.CorreoDelRut,
            "CAMARA_COMERCIO" => MedioDeVerificacion.CorreoDeLaCamara,
            _ => (MedioDeVerificacion?)null
        };
        if (medio is null || !legible || alertas.Count > 0)
            return null;

        var correo = SolicitudDeAcceso.Normalizar(LecturaDeHechos.Texto(campos, "correo"));
        return EsCorreo(correo) ? new CorreoOficial(correo, medio.Value, tipoCanonico!.ToUpperInvariant(), version) : null;
    }

    /// <summary>El medio por el que <paramref name="correo"/> coincide con un correo oficial del tercero; nulo si no coincide con ninguno.</summary>
    public static MedioDeVerificacion? PorDocumento(string? correo, IEnumerable<CorreoOficial> oficiales)
    {
        var normalizado = SolicitudDeAcceso.Normalizar(correo);
        if (normalizado.Length == 0)
            return null;
        // El RUT va primero: es el dato más fuerte cuando los dos traen el mismo correo.
        return oficiales.Where(o => string.Equals(o.Correo, normalizado, StringComparison.Ordinal)).OrderBy(o => o.Medio).Select(o => (MedioDeVerificacion?)o.Medio).FirstOrDefault();
    }

    /// <summary>
    /// ¿Lo corroboran los grupos? Dos o más grupos (tenants) distintos invitaron el mismo correo; dos vínculos del mismo grupo (o de dos empresas del mismo grupo)
    /// no son independientes.
    /// </summary>
    public static bool Corroborado(IEnumerable<Guid> tenantsQueInvitaron) => tenantsQueInvitaron.Distinct().Count() >= GruposParaCorroborar;

    /// <summary>
    /// El medio por el que un contacto que aún no lo está se verifica ahora: el correo oficial (RUT o Cámara) y, si el tercero no tiene ya un contacto verificado, la
    /// corroboración (con un contacto verificado presente manda él: dos grupos que se ponen de acuerdo no lo desplazan).
    /// </summary>
    public static MedioDeVerificacion? Medio(string? correo, IEnumerable<CorreoOficial> oficiales, IEnumerable<Guid> tenantsQueInvitaron, bool terceroTieneVerificado) =>
        PorDocumento(correo, oficiales) ?? (!terceroTieneVerificado && Corroborado(tenantsQueInvitaron) ? MedioDeVerificacion.Corroborado : null);

    private static bool EsCorreo(string correo)
    {
        var arroba = correo.IndexOf('@', StringComparison.Ordinal);
        return arroba > 0 && arroba < correo.Length - 1 && !correo.Contains(' ', StringComparison.Ordinal) && correo[(arroba + 1)..].Contains('.', StringComparison.Ordinal);
    }
}
