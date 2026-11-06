using JasperFx;
using Marten;
using Marten.Exceptions;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>Datos de la persona según la aserción ya verificada (<c>sub</c>, <c>name</c>, <c>email</c>).</summary>
public sealed record AsercionDelegada(string IdExterno, string Nombre, string Correo);

/// <summary>
/// Reconoce a la persona de una aserción delegada (ADR-0011): la busca por <c>(sistema, sub)</c>; si no existe la
/// enlaza al usuario con el mismo correo o la crea <b>sin roles</b> (JIT). Corre durante la autenticación, antes de que
/// exista el contexto de la petición, por eso abre su propia sesión con el sistema como actor.
/// </summary>
public sealed class RegistroDeUsuariosDelegados(IDocumentStore store, TimeProvider reloj)
{
    /// <summary>Evita escribir en cada petición: el último acceso se actualiza como mucho una vez por minuto.</summary>
    private static readonly TimeSpan IntervaloUltimoAcceso = TimeSpan.FromMinutes(1);

    public async Task<SistemaIntegrado?> CargarSistemaAsync(Guid tenantId, Guid sistemaId, CancellationToken ct)
    {
        await using var consulta = store.QuerySession(tenantId.ToString("N"));
        return await consulta.LoadAsync<SistemaIntegrado>(sistemaId, ct);
    }

    public async Task<Usuario> ResolverAsync(Guid tenantId, SistemaIntegrado sistema, string apiKeyId, AsercionDelegada asercion,
        string correlationId, CancellationToken ct)
    {
        var contexto = new ContextoDeIntegracion(tenantId, apiKeyId, sistema.Codigo, correlationId);
        try
        {
            return await ResolverUnaVezAsync(contexto, sistema, asercion, ct);
        }
        catch (Exception e) when (e is DocumentAlreadyExistsException or MartenCommandException)
        {
            // Dos primeros accesos simultáneos de la misma persona: el otro ya la creó o enlazó; se vuelve a leer.
            return await ResolverUnaVezAsync(contexto, sistema, asercion, ct);
        }
    }

    private async Task<Usuario> ResolverUnaVezAsync(ContextoDeIntegracion contexto, SistemaIntegrado sistema, AsercionDelegada asercion,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var correo = Usuario.NormalizarCorreo(asercion.Correo);
        var nombre = asercion.Nombre.Trim();

        await using var sesion = store.LightweightSession(contexto.TenantId!.Value.ToString("N"));
        sesion.CorrelationId = contexto.CorrelationId;
        sesion.SetHeader("actorTipo", contexto.ActorTipo.ToString());
        sesion.SetHeader("actorId", contexto.ActorId);
        sesion.SetHeader("sistema", sistema.Codigo);

        var clave = IndiceIdentidadExterna.Clave(sistema.Id, asercion.IdExterno);
        var indice = await sesion.LoadAsync<IndiceIdentidadExterna>(clave, ct);
        var usuario = indice is null ? null : await sesion.LoadAsync<Usuario>(indice.UsuarioId, ct);

        if (usuario is not null)
        {
            AsegurarActivo(usuario);
            var antes = usuario.ADto();
            var cambioDatos = await ActualizarDatosAsync(sesion, usuario, nombre, correo, ct);
            var registrarAcceso = usuario.UltimoAcceso is null || ahora - usuario.UltimoAcceso >= IntervaloUltimoAcceso;
            if (!cambioDatos && !registrarAcceso)
                return usuario;

            usuario.UltimoAcceso = ahora;
            sesion.Store(usuario);
            if (cambioDatos)
                Auditoria.Registrar(sesion, contexto, "Usuario", usuario.Id.ToString(), AccionConfiguracion.Modificado, antes, usuario.ADto(), ahora);
            await sesion.SaveChangesAsync(ct);
            return usuario;
        }

        var porCorreo = await sesion.Query<Usuario>().FirstOrDefaultAsync(u => u.Correo == correo, ct);
        if (porCorreo is not null)
        {
            // Alta anticipada por un administrador (o identidad de otro sistema): se enlaza por correo.
            AsegurarActivo(porCorreo);
            var antes = porCorreo.ADto();
            porCorreo.EnlazarIdentidad(sistema.Id, asercion.IdExterno);
            porCorreo.Nombre = string.IsNullOrWhiteSpace(nombre) ? porCorreo.Nombre : nombre;
            porCorreo.UltimoAcceso = ahora;
            usuario = porCorreo;
            Auditoria.Registrar(sesion, contexto, "Usuario", usuario.Id.ToString(), AccionConfiguracion.Modificado, antes, usuario.ADto(), ahora);
        }
        else
        {
            usuario = Usuario.CrearDesdeSistema(sistema.Id, asercion.IdExterno, nombre, correo, ahora);
            Auditoria.Registrar(sesion, contexto, "Usuario", usuario.Id.ToString(), AccionConfiguracion.Creado, null, usuario.ADto(), ahora);
        }

        sesion.Store(usuario);
        sesion.Insert(new IndiceIdentidadExterna { Id = clave, UsuarioId = usuario.Id });
        await sesion.SaveChangesAsync(ct);
        return usuario;
    }

    /// <summary>
    /// El sistema integrado es la fuente del nombre y el correo. Si el correo nuevo ya es de otro usuario del tenant se
    /// conserva el anterior (el administrador decide cómo unificarlos).
    /// </summary>
    private static async Task<bool> ActualizarDatosAsync(IDocumentSession sesion, Usuario usuario, string nombre, string correo,
        CancellationToken ct)
    {
        var cambio = false;
        if (!string.IsNullOrWhiteSpace(nombre) && nombre != usuario.Nombre)
        {
            usuario.Nombre = nombre;
            cambio = true;
        }

        if (correo != usuario.Correo)
        {
            var usuarioId = usuario.Id;
            if (!await sesion.Query<Usuario>().AnyAsync(u => u.Correo == correo && u.Id != usuarioId, ct))
            {
                usuario.Correo = correo;
                cambio = true;
            }
        }

        return cambio;
    }

    private static void AsegurarActivo(Usuario usuario)
    {
        if (!usuario.Activo)
            throw ReglaDeNegocioException.NoAutenticado("usuario-inactivo", "El usuario está desactivado en Validasoft.");
    }

    /// <summary>Actor de los cambios hechos durante la autenticación: la API key del sistema integrado.</summary>
    private sealed class ContextoDeIntegracion(Guid tenantId, string apiKeyId, string sistema, string correlationId) : IContextoSolicitud
    {
        public Guid? TenantId { get; } = tenantId;

        public Guid? EmpresaId => null;

        public ActorTipo ActorTipo => ActorTipo.Integracion;

        public string ActorId { get; } = apiKeyId;

        public string CorrelationId { get; } = correlationId;

        public string? Sistema { get; } = sistema;

        public IReadOnlySet<string> Scopes { get; } = new HashSet<string>();

        public IReadOnlySet<string> Empresas { get; } = new HashSet<string>();
    }
}
