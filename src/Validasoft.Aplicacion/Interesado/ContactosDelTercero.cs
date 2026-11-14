using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>
/// Los contactos del tercero y su verificación (decisión AV del registro): quién los registró, con qué se verificaron y qué pruebas hay hoy (los correos del RUT y de la
/// Cámara de comercio, los grupos que invitaron el mismo correo). El estado vigente vive en <see cref="Tercero.Contactos"/>; cada cambio deja una línea en el historial
/// (<see cref="HistorialDeContactos"/>, un stream por tercero en la bóveda), escrita en la misma transacción.
/// </summary>
public static class ContactosDelTercero
{
    public const string OrigenEmpresa = "Empresa";
    public const string OrigenAutorregistro = "Autorregistro";
    public const string OrigenImportacion = "Importacion";
    public const string OrigenAprobacion = "Aprobacion";
    public const string OrigenPlataforma = "Plataforma";

    /// <summary>Anexa eventos al historial de contactos del tercero (stream de la bóveda); lo crea con el primero.</summary>
    public static void Historial(IDocumentSession sesion, Guid terceroId, params object[] eventos) =>
        sesion.ForTenant(TenantsTecnicos.Boveda).Events.Append(HistorialDeContactos.IdPara(terceroId), eventos);

    /// <summary>
    /// Agrega un contacto (verificado por <paramref name="medio"/> si se dice; si no, sin verificar) y deja el rastro en el historial; <c>true</c> si lo agregó.
    /// <paramref name="origen"/> es de dónde viene y <paramref name="detalle"/>, quién (la empresa, el operador…); <paramref name="por"/>, quién lo verificó.
    /// </summary>
    public static bool Agregar(IDocumentSession sesion, Tercero tercero, string correo, string? nombre, string rol, string origen, string? detalle,
        MedioDeVerificacion? medio, string? por, DateTimeOffset ahora)
    {
        if (!tercero.AgregarContacto(correo, nombre, rol, medio, ahora))
            return false;

        sesion.Store(tercero);
        var normalizado = SolicitudDeAcceso.Normalizar(correo);
        var eventos = new List<object> { new ContactoRegistrado(tercero.Id, normalizado, origen, detalle, ahora) };
        if (medio is { } verificado)
            eventos.Add(new ContactoVerificado(tercero.Id, normalizado, verificado, por, ahora));
        Historial(sesion, tercero.Id, [.. eventos]);
        return true;
    }

    /// <summary>
    /// Deja en el historial los contactos con los que nace un tercero que registra una empresa (todos sin verificar: los puso la empresa, nadie probó que sean del
    /// tercero). Sin contactos, no escribe nada.
    /// </summary>
    public static void RegistrarIniciales(IDocumentSession sesion, Tercero tercero, DateTimeOffset ahora)
    {
        if (tercero.Contactos.Count == 0)
            return;
        Historial(sesion, tercero.Id,
            [.. tercero.Contactos.Select(c => (object)new ContactoRegistrado(tercero.Id, SolicitudDeAcceso.Normalizar(c.Correo), OrigenEmpresa, null, ahora))]);
    }

    /// <summary>Verifica un contacto que estaba sin verificar y deja el rastro; <c>true</c> si cambió.</summary>
    public static bool Verificar(IDocumentSession sesion, Tercero tercero, string correo, MedioDeVerificacion medio, string? por, DateTimeOffset ahora)
    {
        if (!tercero.Verificar(correo, medio, ahora))
            return false;

        sesion.Store(tercero);
        Historial(sesion, tercero.Id, new ContactoVerificado(tercero.Id, SolicitudDeAcceso.Normalizar(correo), medio, por, ahora));
        return true;
    }

    /// <summary>
    /// Los correos electrónicos oficiales del tercero: el del RUT y el de la Cámara de comercio, tomados de la versión <b>actual</b> de cada documento global cuando se
    /// analizó con IA (legible y sin alertas de manipulación). Sin análisis (o sin documento) no hay correo oficial y el contacto solo se verifica por corroboración o por la
    /// plataforma.
    /// <para>
    /// Cada correo trae de dónde viene la versión (quién la subió y qué empresas la aceptaron): <b>el correo de un documento solo verifica si alguien más lo respalda</b>
    /// (<see cref="CorreoOficial.Respalda"/>). Quien sube un RUT con su propio correo no se verifica con él: la IA no distingue un documento bien fabricado y la prueba sería circular.
    /// </para>
    /// </summary>
    public static async Task<IReadOnlyList<CorreoOficial>> CorreosOficialesAsync(IQuerySession boveda, Guid terceroId, CancellationToken ct)
    {
        var documentos = await boveda.Query<Documento>()
            .Where(d => d.TerceroId == terceroId && d.Ambito == AmbitoDocumento.Global && (d.TipoCanonico == "RUT" || d.TipoCanonico == "CAMARA_COMERCIO"))
            .ToListAsync(ct);
        var versiones = new List<(Documento Documento, VersionDocumento Version, AnalisisDeIaDeVersion Ia)>();
        foreach (var documento in documentos)
            if (documento.VersionActual is { } actual && documento.VersionRequerida(actual) is { Ia: { } ia, Suprimida: false } version)
                versiones.Add((documento, version, ia));
        if (versiones.Count == 0)
            return [];

        var aceptadas = await GruposQueAceptaronAsync(boveda, terceroId, versiones.Select(v => (v.Documento.Id, v.Version.Numero)).ToList(), ct);
        var oficiales = new List<CorreoOficial>();
        foreach (var (documento, version, ia) in versiones)
            if (VerificacionDeContactos.DeDocumento(documento.TipoCanonico, version.Numero, ia.Legible, ia.Alertas, ia.Campos, version.CargadaPorCorreo,
                    aceptadas.GetValueOrDefault((documento.Id, version.Numero))) is { } oficial)
                oficiales.Add(oficial);

        return oficiales;
    }

    /// <summary>
    /// Los grupos (tenants) cuyas empresas aceptaron cada versión: tienen un requisito del documento sobre esa versión con un veredicto de aprobación vigente de una persona
    /// o del administrador (la aprobación automática del sistema no es una revisión) en un expediente que no se anuló, rechazó ni caducó. Una consulta entre tenants.
    /// </summary>
    private static async Task<IReadOnlyDictionary<(Guid DocumentoId, int Numero), IReadOnlyList<Guid>>> GruposQueAceptaronAsync(IQuerySession consulta, Guid terceroId,
        IReadOnlyCollection<(Guid DocumentoId, int Numero)> versiones, CancellationToken ct)
    {
        var expedientes = (await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.TerceroId == terceroId).ToListAsync(ct))
            .Where(e => e.Estado is not (EstadoExpediente.Anulado or EstadoExpediente.Rechazado or EstadoExpediente.Caducado))
            .ToList();
        var empresasPorVersion = new Dictionary<(Guid DocumentoId, int Numero), HashSet<Guid>>();
        foreach (var expediente in expedientes)
            foreach (var requisito in expediente.Requisitos.Where(r => !r.EsSeccion && r.VersionActual is not null))
            {
                var clave = (requisito.VersionActual!.DocumentoId, requisito.VersionActual.Numero);
                var aceptada = requisito.Veredictos.Any(v => v is { Vigente: true, Veredicto: Veredicto.Aprobado } && v.Origen != OrigenVeredicto.Sistema && v.SobreVersion == clave.Numero);
                if (aceptada && versiones.Contains(clave))
                {
                    if (!empresasPorVersion.TryGetValue(clave, out var empresas))
                        empresasPorVersion[clave] = empresas = [];
                    empresas.Add(expediente.EmpresaId);
                }
            }

        if (empresasPorVersion.Count == 0)
            return new Dictionary<(Guid, int), IReadOnlyList<Guid>>();

        var tenants = (await consulta.LoadManyAsync<Empresa>(ct, empresasPorVersion.Values.SelectMany(e => e).Distinct().ToArray())).ToDictionary(e => e.Id, e => e.TenantId);
        return empresasPorVersion.ToDictionary(p => p.Key, p => (IReadOnlyList<Guid>)[.. p.Value.Where(tenants.ContainsKey).Select(id => tenants[id]).Distinct()]);
    }

    /// <summary>
    /// Los grupos (tenants) con vínculos con este tercero cuyo correo de invitación es <paramref name="correo"/>: la prueba de corroboración. Una consulta entre tenants;
    /// <paramref name="exceptoVinculoId"/> deja fuera el vínculo que se está registrando (el llamador suma su grupo).
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> GruposQueInvitaronAsync(IQuerySession consulta, Guid terceroId, string correo, Guid? exceptoVinculoId, CancellationToken ct)
    {
        var normalizado = SolicitudDeAcceso.Normalizar(correo);
        var vinculos = await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.TerceroId == terceroId).ToListAsync(ct);
        var empresaIds = vinculos
            .Where(v => v.Id != exceptoVinculoId && string.Equals(SolicitudDeAcceso.Normalizar(v.CorreoTercero), normalizado, StringComparison.Ordinal))
            .Select(v => v.EmpresaId)
            .Distinct()
            .ToArray();
        if (empresaIds.Length == 0)
            return [];
        return [.. (await consulta.LoadManyAsync<Empresa>(ct, empresaIds)).Select(e => e.TenantId).Distinct()];
    }

    /// <summary>¿Hay algún vínculo del tercero (de cualquier grupo) que ya aceptó su invitación sin pasar por el acceso aislado? Alguien ya probó entrar con ese correo.</summary>
    public static async Task<bool> HayInvitacionAceptadaAsync(IQuerySession consulta, Guid terceroId, CancellationToken ct) =>
        (await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.TerceroId == terceroId && v.InvitacionAceptada).ToListAsync(ct))
        .Any(v => v.AccesoAislado is null or EstadoAccesoAislado.Aprobado);
}
