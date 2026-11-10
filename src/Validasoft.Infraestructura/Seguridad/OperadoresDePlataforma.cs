using System.Security.Cryptography;
using System.Text;
using JasperFx;
using Marten;
using Marten.Exceptions;
using Microsoft.IdentityModel.JsonWebTokens;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Operadores de Plataforma por identidad delegada (PT-4): el <c>iss</c> de la aserción elige el
/// <see cref="SistemaDePlataforma"/>; se verifica con su clave pública igual que una aserción de un sistema integrado
/// (firma, audiencia, ≤ 5 min, <c>jti</c> de un solo uso) y se registra al operador en su primer acceso (sin contraseña).
/// </summary>
public sealed class VerificadorDeOperadores(IDocumentStore store, RegistroDeAsercionesUsadas usadas, TimeProvider reloj) : IVerificadorDeOperadores
{
    public async Task<OperadorDePlataforma> VerificarAsync(string asercion, string correlationId, CancellationToken ct)
    {
        string? emisor;
        try
        {
            emisor = new JsonWebToken(asercion).Issuer;
        }
        catch (ArgumentException)
        {
            throw ReglaDeNegocioException.NoAutenticado("asercion-invalida", "X-Usuario-Delegado no es un JWT.");
        }

        SistemaDePlataforma? sistema;
        await using (var consulta = store.QuerySession())
            sistema = string.IsNullOrWhiteSpace(emisor) ? null : await consulta.LoadAsync<SistemaDePlataforma>(emisor.Trim().ToUpperInvariant(), ct);
        if (sistema is null || !sistema.Activo)
            throw ReglaDeNegocioException.NoAutenticado("sistema-inactivo", "El emisor de la aserción no es un sistema de Plataforma activo.");

        var ahora = reloj.GetUtcNow();
        var (datos, jti, expira) = await VerificadorDeIdentidadDelegada.ValidarAsync(asercion, sistema.Codigo, sistema.ClavePublicaPem, ahora);
        await usadas.RegistrarAsync(IdDeRegistro(sistema.Codigo), jti, ahora, expira + VerificadorDeIdentidadDelegada.Tolerancia, ct);

        try
        {
            return await ResolverAsync(sistema, datos.IdExterno, datos.Nombre, datos.Correo, correlationId, ct);
        }
        catch (Exception e) when (e is DocumentAlreadyExistsException or MartenCommandException)
        {
            // Primer acceso simultáneo de la misma persona: el otro ya la registró; se vuelve a leer.
            return await ResolverAsync(sistema, datos.IdExterno, datos.Nombre, datos.Correo, correlationId, ct);
        }
    }

    /// <summary>Clave del registro anti-repetición: un Guid fijo por sistema de plataforma (no choca con los de los tenants).</summary>
    public static Guid IdDeRegistro(string codigo) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes("validasoft:plataforma:" + codigo)).AsSpan(0, 16));

    private async Task<OperadorDePlataforma> ResolverAsync(SistemaDePlataforma sistema, string sub, string nombre, string correo, string correlationId,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = store.LightweightSession();
        sesion.CorrelationId = correlationId;
        var operador = await sesion.LoadAsync<OperadorDePlataforma>(OperadorDePlataforma.Clave(sistema.Codigo, sub.Trim()), ct);
        if (operador is null)
        {
            operador = OperadorDePlataforma.Registrar(sistema.Codigo, sub, nombre, correo, ahora);
            sesion.Insert(operador);
        }
        else
        {
            operador.Acceder(nombre, correo, ahora);
            sesion.Store(operador);
        }

        await sesion.SaveChangesAsync(ct);
        return operador;
    }
}
