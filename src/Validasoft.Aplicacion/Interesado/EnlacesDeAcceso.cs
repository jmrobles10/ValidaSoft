using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Consentimientos;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>Emisión de enlaces de acceso y apertura de la sesión del tercero (docs/07 §2.2).</summary>
public static class EnlacesDeAcceso
{
    public static readonly TimeSpan VigenciaAutorregistro = TimeSpan.FromHours(72);

    /// <summary>
    /// Por cada <see cref="InvitacionEnviada"/>: borra los enlaces no usados del vínculo (un reenvío deja sin efecto el
    /// anterior, que responde 404), crea uno nuevo con la vigencia de la invitación y publica el correo en el outbox de
    /// <paramref name="mensajes"/>, en la misma transacción que el evento.
    /// </summary>
    public static async Task InvitarAsync(IDocumentSession sesion, IMessageBus mensajes, IProtectorDeSecretos protector, Guid tenantId,
        Vinculo vinculo, TipoVinculo tipo, InvitacionEnviada invitacion, CancellationToken ct)
    {
        Guid? vinculoId = vinculo.Id;
        sesion.DeleteWhere<EnlaceDeAcceso>(e => e.VinculoId == vinculoId && !e.Usado);

        var (enlace, token) = EnlaceDeAcceso.Emitir(tenantId, vinculo.Id, vinculo.TerceroId, invitacion.CorreoDestino, invitacion.Expira,
            tipo.ExigeOtpEnEnlace, invitacion.Fecha);
        sesion.Store(enlace);

        var empresa = await sesion.LoadAsync<Empresa>(vinculo.EmpresaId, ct);
        await mensajes.PublishAsync(new CorreoDeAccesoPendiente(PlantillaCorreo.Invitacion, enlace.CorreoDestino, protector.Proteger(token),
            enlace.Expira, empresa?.Nombre ?? "Una empresa", tipo.Nombre));
    }

    /// <summary>Enlace sin vínculo tras el autorregistro (sin OTP: la posesión del correo es el factor).</summary>
    public static async Task AutorregistroAsync(IDocumentSession sesion, IMessageBus mensajes, IProtectorDeSecretos protector, Guid terceroId,
        string correo, DateTimeOffset ahora)
    {
        var (enlace, token) = EnlaceDeAcceso.Emitir(null, null, terceroId, correo, ahora + VigenciaAutorregistro, false, ahora);
        sesion.Store(enlace);
        await mensajes.PublishAsync(new CorreoDeAccesoPendiente(PlantillaCorreo.AccesoAutorregistro, enlace.CorreoDestino,
            protector.Proteger(token), enlace.Expira, null, null));
    }

    /// <summary>El enlace por el hash de su token; inexistente (o borrado por un reenvío) ⇒ 404.</summary>
    public static async Task<EnlaceDeAcceso> CargarAsync(IQuerySession sesion, string token, CancellationToken ct)
    {
        var enlace = string.IsNullOrWhiteSpace(token) || token.Length > 128
            ? null
            : await sesion.LoadAsync<EnlaceDeAcceso>(EnlaceDeAcceso.HashDeToken(token.Trim()), ct);
        return enlace ?? throw ReglaDeNegocioException.NoEncontrado("enlace-no-encontrado",
            "Ese enlace no existe o fue reemplazado por uno más reciente. Revisa el último correo que recibiste.");
    }

    /// <summary>
    /// Sesión del tercero: incluye los vínculos de ese tercero cuyo correo de invitación coincide con el del enlace, en
    /// todos los tenants (consulta entre tenants de Marten). Canjear solo abre sesión: no comparte nada (B-1).
    /// </summary>
    public static async Task<SesionTerceroDto> AbrirSesionAsync(IQuerySession consulta, IEmisorDeSesionesTercero emisor, EnlaceDeAcceso enlace,
        bool otpVerificado, DateTimeOffset ahora, CancellationToken ct)
    {
        var terceroId = enlace.TerceroId;
        var correo = enlace.CorreoDestino;
        var vinculos = await consulta.Query<Vinculo>()
            .Where(v => v.AnyTenant() && v.TerceroId == terceroId && v.CorreoTercero == correo)
            .OrderBy(v => v.CreadoEn)
            .ToListAsync(ct);

        var empresas = await CargarEmpresasAsync(consulta, vinculos, ct);
        var enSesion = vinculos
            .Where(v => empresas.ContainsKey(v.EmpresaId))
            .Select(v => (Vinculo: v, Empresa: empresas[v.EmpresaId]))
            .ToList();

        var (token, expira) = emisor.Emitir(terceroId, correo, otpVerificado,
            [.. enSesion.Select(x => new VinculoEnSesion(x.Vinculo.Id, x.Empresa.TenantId, x.Empresa.Id))], ahora);

        return new SesionTerceroDto(token, expira, terceroId, otpVerificado,
            [.. enSesion.Select(x => new VinculoDeSesionDto(x.Vinculo.Id, x.Empresa.APublica(x.Empresa.Id), x.Vinculo.Estado.ToString()))]);
    }

    private static async Task<Dictionary<Guid, Empresa>> CargarEmpresasAsync(IQuerySession consulta, IReadOnlyList<Vinculo> vinculos,
        CancellationToken ct)
    {
        var ids = vinculos.Select(v => v.EmpresaId).Distinct().ToArray();
        return ids.Length == 0 ? [] : (await consulta.LoadManyAsync<Empresa>(ct, ids)).ToDictionary(e => e.Id);
    }
}

/// <summary>Vínculo con su empresa y su tipo, como lo ve el tercero.</summary>
public sealed record VinculoVisto(Vinculo Vinculo, Empresa? Empresa, TipoVinculo? Tipo)
{
    public VinculoPublicoDto ADto() => Vinculo.APublico(Empresa, Tipo);
}

/// <summary>Textos que el tercero debe aceptar en una invitación y los que la empresa aún no publica.</summary>
public sealed record TextosParaAceptar(Tercero? Tercero, TextoLegal? AvisoRequerido, IReadOnlyList<TextoLegal> Requeridos, IReadOnlyList<string> Faltantes);

/// <summary>Lecturas del tercero sobre vínculos de varios tenants: siempre limitadas a los vínculos de su sesión.</summary>
public static class VistaDelTercero
{
    public static async Task<IReadOnlyList<VinculoVisto>> CargarAsync(IQuerySession consulta, SesionDeTercero sesion, Guid[] vinculoIds,
        CancellationToken ct)
    {
        var permitidos = vinculoIds.Where(id => sesion.Vinculos.Any(v => v.VinculoId == id)).Distinct().ToArray();
        if (permitidos.Length == 0)
            return [];

        var terceroId = sesion.TerceroId;
        var vinculos = await consulta.Query<Vinculo>()
            .Where(v => v.AnyTenant() && v.Id.IsOneOf(permitidos) && v.TerceroId == terceroId)
            .OrderBy(v => v.CreadoEn)
            .ToListAsync(ct);

        var empresaIds = vinculos.Select(v => v.EmpresaId).Distinct().ToArray();
        var tipoIds = vinculos.Select(v => v.TipoVinculoId).Distinct().ToArray();
        var empresas = empresaIds.Length == 0 ? [] : await consulta.LoadManyAsync<Empresa>(ct, empresaIds);
        var tipos = tipoIds.Length == 0
            ? []
            : await consulta.Query<TipoVinculo>().Where(t => t.AnyTenant() && t.Id.IsOneOf(tipoIds)).ToListAsync(ct);

        return [.. vinculos.Select(v => new VinculoVisto(v, empresas.FirstOrDefault(e => e.Id == v.EmpresaId),
            tipos.FirstOrDefault(t => t.Id == v.TipoVinculoId)))];
    }

    public static async Task<VinculoVisto> CargarUnoAsync(IQuerySession consulta, SesionDeTercero sesion, Guid vinculoId, CancellationToken ct)
    {
        sesion.VinculoRequerido(vinculoId);
        return (await CargarAsync(consulta, sesion, [vinculoId], ct)).SingleOrDefault()
               ?? throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");
    }

    /// <summary>
    /// Autorización y términos vigentes de la empresa, más el aviso de la plataforma si el tercero no ha aceptado su
    /// versión vigente.
    /// </summary>
    public static async Task<TextosParaAceptar> TextosAsync(IQuerySession consulta, Guid terceroId, Guid empresaId, CancellationToken ct)
    {
        var tercero = await consulta.LoadAsync<Tercero>(terceroId, ct);
        var aviso = await TextosLegales.VigenteAsync(consulta, TipoTextoLegal.AvisoPlataforma, null, ct);
        var deEmpresa = await TextosLegales.VigentesDeEmpresaAsync(consulta, empresaId, ct);

        var avisoRequerido = aviso is not null && (tercero is null || tercero.RequiereAvisoPlataforma(aviso)) ? aviso : null;
        List<TextoLegal> requeridos = avisoRequerido is null ? [.. deEmpresa] : [avisoRequerido, .. deEmpresa];
        return new TextosParaAceptar(tercero, avisoRequerido, requeridos, TextosLegales.Faltantes(deEmpresa));
    }
}
