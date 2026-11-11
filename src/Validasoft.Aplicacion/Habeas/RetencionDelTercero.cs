using Marten;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Habeas;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Validasoft.Aplicacion.Notificaciones;
using ContratoPlan = Validasoft.Contratos;

namespace Validasoft.Aplicacion.Habeas;

public enum AccionDeSupresion
{
    Suprimir,
    Retener,
    Anonimizar
}

/// <summary>
/// Un elemento del plan de supresión. <see cref="DocumentoId"/> y <see cref="Numero"/> identifican la versión de un documento (tipo
/// <c>documento-version</c>); <see cref="SeccionId"/>, la sección de la ficha; <see cref="CuentaId"/>, la cuenta bancaria. La
/// descripción nunca lleva datos personales: tipo de documento y número de versión, sección o últimos cuatro de la cuenta.
/// </summary>
public sealed record ElementoDePlan(
    string Tipo, string Referencia, string Descripcion, AccionDeSupresion Accion, string? BaseLegal = null, DateOnly? RetenidaHasta = null,
    Guid? DocumentoId = null, int? Numero = null, Guid? SeccionId = null, Guid? CuentaId = null);

/// <summary>Lo que haría una supresión sobre la bóveda del tercero: todo, decidido elemento por elemento.</summary>
public sealed record PlanDeSupresion(Guid TerceroId, AlcanceDeSupresion Alcance, IReadOnlyList<ElementoDePlan> Elementos)
{
    public int Suprimir => Elementos.Count(e => e.Accion == AccionDeSupresion.Suprimir);

    public int Retener => Elementos.Count(e => e.Accion == AccionDeSupresion.Retener);

    public int Anonimizar => Elementos.Count(e => e.Accion == AccionDeSupresion.Anonimizar);

    public ContratoPlan.PlanDeSupresionDto ADto(Guid solicitudId) => new(solicitudId, Alcance.ToString(), Suprimir, Retener, Anonimizar,
        [.. Elementos.Select(e => new ContratoPlan.ElementoDePlanDeSupresionDto(e.Tipo, e.Referencia, e.Descripcion, e.Accion.ToString(), e.BaseLegal, e.RetenidaHasta))]);
}

/// <summary>Lo que dejó ejecutar el plan, para la constancia.</summary>
public sealed record ResultadoDeSupresion(IReadOnlyList<ElementoSuprimido> Suprimidos, IReadOnlyList<ElementoRetenido> Retenidos, IReadOnlyList<string> Anonimizados)
{
    public bool HayRetenciones => Retenidos.Count > 0;
}

/// <summary>
/// Supresión de la bóveda de un tercero que ejerce su derecho (Ley 1581 de 2012, OD-5, OD-6, docs/07 §8). Decide elemento por
/// elemento qué se elimina y qué se conserva por obligación legal:
/// <list type="bullet">
/// <item>Una versión de documento <b>referenciada</b> por una aceptación (un requisito aprobado de un expediente), por un expediente que
/// aún se revisa o por un certificado de cualquier empresa se <b>conserva íntegra</b> (anonimizarla rompería el hash del certificado),
/// con su base legal y la retención más larga de las que la referencian, y no se vuelve a compartir.</item>
/// <item>Lo demás se elimina: el binario, la proyección y, si ya nada del documento se conserva, el stream de eventos completo. Las
/// secciones de la ficha que ninguna empresa aceptó, las cuentas bancarias que ninguna empresa designó y las sesiones, enlaces y
/// preferencias del tercero se eliminan; sus contactos y su nombre comercial se anonimizan.</item>
/// </list>
/// El plan se puede calcular sin ejecutar (<see cref="PlanearAsync"/>) para que quien confirma vea lo que pasará.
/// </summary>
public sealed class RetencionDelTercero(ISesiones sesiones, IDocumentStore store, IAlmacenDeArchivos almacen, IConfiguration configuracion, TimeProvider reloj)
{
    public const string BaseLegalPorOmision =
        "Obligación legal de conservar los soportes de la relación comercial (Código de Comercio, art. 28; Ley 962 de 2005, art. 28).";

    private string BaseLegal => configuracion["Validasoft:Habeas:BaseLegalRetencion"] is { Length: > 0 } texto ? texto : BaseLegalPorOmision;

    private int AniosDeRetencion => Math.Clamp(configuracion.GetValue("Validasoft:Habeas:AniosRetencion", 10), 1, 30);

    // ---------- Plan ----------

    /// <summary>Calcula qué se eliminaría y qué se conservaría con <paramref name="alcance"/>; no cambia nada.</summary>
    public async Task<PlanDeSupresion> PlanearAsync(Guid terceroId, AlcanceDeSupresion alcance, CancellationToken ct)
    {
        var hoy = CalculadoraDeVigencia.Fecha(reloj.GetUtcNow());
        var anios = AniosDeRetencion;
        var elementos = new List<ElementoDePlan>();

        await using var global = sesiones.ConsultaGlobal();
        await using var boveda = sesiones.ConsultaBoveda();

        // Lo que referencia al tercero en los tenants de las empresas: certificados, expedientes, aceptaciones de la ficha y vínculos.
        var certificados = await global.Query<Certificado>().Where(c => c.AnyTenant() && c.TerceroId == terceroId).ToListAsync(ct);
        var expedientes = await global.Query<Expediente>().Where(e => e.AnyTenant() && e.TerceroId == terceroId).ToListAsync(ct);
        var aceptadas = await global.Query<SeccionAceptada>().Where(a => a.AnyTenant() && a.TerceroId == terceroId).ToListAsync(ct);
        var vinculos = (await global.Query<Vinculo>().Where(v => v.AnyTenant() && v.TerceroId == terceroId).ToListAsync(ct)).ToDictionary(v => v.Id);

        DateOnly? FinDelVinculo(Guid vinculoId) =>
            vinculos.TryGetValue(vinculoId, out var v) && v.Estado is EstadoVinculo.Desvinculado or EstadoVinculo.Anulado
                ? CalculadoraDeVigencia.Fecha(v.ActualizadoEn)
                : null;

        // La retención corre desde el fin del vínculo (si sigue vigente, desde hoy) o desde el hecho que se retiene, lo que sea posterior.
        DateOnly HastaCuando(Guid vinculoId, DateOnly desde)
        {
            var fin = FinDelVinculo(vinculoId) ?? hoy;
            return (fin > desde ? fin : desde).AddYears(anios);
        }

        // Por huella de la versión: los certificados la nombran con su SHA-256.
        var porCertificado = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase);
        foreach (var certificado in certificados)
        {
            foreach (var emision in certificado.Emisiones)
            {
                var contenido = ContenidoDeCertificado.Leer(emision.ContenidoCanonico);
                var hasta = HastaCuando(certificado.VinculoId, CalculadoraDeVigencia.Fecha(emision.EmitidoEn));
                foreach (var requisito in contenido.Requisitos.Where(r => r.Sha256 is not null))
                    porCertificado[requisito.Sha256!] = porCertificado.TryGetValue(requisito.Sha256!, out var previa) && previa > hasta ? previa : hasta;
            }
        }

        // Por (documento, versión): los requisitos aprobados de un expediente son la aceptación; los de un expediente que aún se revisa, un trámite en curso.
        var aceptadasPorExpediente = new Dictionary<(Guid, int), DateOnly>();
        var enCurso = new HashSet<(Guid, int)>();
        foreach (var expediente in expedientes)
        {
            foreach (var requisito in expediente.Requisitos.Where(r => !r.EsSeccion && r.VersionActual is not null))
            {
                var clave = (requisito.VersionActual!.DocumentoId, requisito.VersionActual.Numero);
                if (expediente.EstaAbierto)
                    enCurso.Add(clave);
                else if (expediente.Estado == EstadoExpediente.Aprobado && requisito.Estado == EstadoRequisito.Aprobado)
                {
                    var hasta = HastaCuando(expediente.VinculoId, CalculadoraDeVigencia.Fecha(requisito.AceptadaEn ?? expediente.ActualizadoEn));
                    aceptadasPorExpediente[clave] = aceptadasPorExpediente.TryGetValue(clave, out var previa) && previa > hasta ? previa : hasta;
                }
            }
        }

        if (alcance is AlcanceDeSupresion.Todo or AlcanceDeSupresion.Documentos)
        {
            var documentos = await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).OrderBy(d => d.CreadoEn).ToListAsync(ct);
            foreach (var documento in documentos)
            {
                foreach (var version in documento.Versiones.Where(v => !v.Suprimida).OrderBy(v => v.Numero))
                {
                    var referencia = $"{documento.Id:N}:{version.Numero}";
                    var descripcion = $"{documento.NombreTipo} (versión {version.Numero})";
                    var clave = (documento.Id, version.Numero);
                    var motivos = new List<string>();
                    DateOnly? hasta = null;

                    if (porCertificado.TryGetValue(version.Sha256, out var porCert))
                    {
                        motivos.Add("respalda un certificado de validación emitido a una empresa");
                        hasta = Mayor(hasta, porCert);
                    }

                    if (aceptadasPorExpediente.TryGetValue(clave, out var porAceptacion))
                    {
                        motivos.Add("es el soporte de lo que una empresa aceptó al aprobar la validación");
                        hasta = Mayor(hasta, porAceptacion);
                    }

                    if (enCurso.Contains(clave))
                        motivos.Add("una empresa la está revisando en una validación en curso");
                    if (version.RetenidaPorLey && motivos.Count == 0)
                    {
                        motivos.Add("ya se marcó como retenida por ley");
                        hasta = version.RetenidaHasta;
                    }

                    elementos.Add(motivos.Count == 0
                        ? new ElementoDePlan("documento-version", referencia, descripcion, AccionDeSupresion.Suprimir, DocumentoId: documento.Id, Numero: version.Numero)
                        : new ElementoDePlan("documento-version", referencia, descripcion, AccionDeSupresion.Retener,
                            $"{BaseLegal} Esta versión {string.Join(" y ", motivos)}.", hasta, documento.Id, version.Numero));
                }
            }
        }

        if (alcance is AlcanceDeSupresion.Todo or AlcanceDeSupresion.Datos)
        {
            var secciones = await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).OrderBy(s => s.Seccion).ThenBy(s => s.Periodo).ToListAsync(ct);
            foreach (var seccion in secciones)
            {
                var descripcion = $"Sección {seccion.Seccion}" + (seccion.Periodo is { } p ? $" ({p})" : string.Empty);
                var aceptacion = aceptadas.Where(a => a.SeccionId == seccion.Id).ToList();
                var enRevision = expedientes.Any(e => e.EstaAbierto &&
                                                      e.Requisitos.Any(r => r.EsSeccion && r.Seccion == seccion.Seccion && r.Periodo == seccion.Periodo));
                if (aceptacion.Count == 0 && !enRevision)
                {
                    elementos.Add(new ElementoDePlan("seccion-ficha", seccion.Id.ToString("N"), descripcion, AccionDeSupresion.Suprimir, SeccionId: seccion.Id));
                    continue;
                }

                DateOnly? hasta = aceptacion.Count == 0
                    ? null
                    : aceptacion.Select(a => HastaCuando(a.VinculoId, CalculadoraDeVigencia.Fecha(a.AceptadaEn))).Max();
                elementos.Add(new ElementoDePlan("seccion-ficha", seccion.Id.ToString("N"), descripcion, AccionDeSupresion.Retener,
                    $"{BaseLegal} Una empresa {(aceptacion.Count > 0 ? "aceptó esta sección al aprobar la validación" : "la está revisando en una validación en curso")}.",
                    hasta, SeccionId: seccion.Id));
            }

            var designadas = vinculos.Values
                .SelectMany(v => new[] { v.CuentaDesignada, v.CuentaPropuesta }.OfType<CuentaDesignada>().Select(c => (Vinculo: v, Cuenta: c)))
                .Where(x => x.Cuenta.Estado is EstadoCuentaDesignada.Aprobada or EstadoCuentaDesignada.Propuesta)
                .ToList();
            foreach (var cuenta in await boveda.Query<CuentaBancaria>().Where(c => c.TerceroId == terceroId).OrderBy(c => c.RegistradaEn).ToListAsync(ct))
            {
                var descripcion = $"Cuenta bancaria {cuenta.NumeroEnmascarado}";
                var designaciones = designadas.Where(x => x.Cuenta.Cuenta.CuentaId == cuenta.Id).ToList();
                elementos.Add(designaciones.Count == 0
                    ? new ElementoDePlan("cuenta-bancaria", cuenta.Id.ToString("N"), descripcion, AccionDeSupresion.Suprimir, CuentaId: cuenta.Id)
                    : new ElementoDePlan("cuenta-bancaria", cuenta.Id.ToString("N"), descripcion, AccionDeSupresion.Retener,
                        $"{BaseLegal} Una empresa la tiene designada para pagarle al tercero.",
                        designaciones.Select(x => HastaCuando(x.Vinculo.Id, CalculadoraDeVigencia.Fecha(x.Cuenta.PropuestaEn))).Max(), CuentaId: cuenta.Id));
            }

            elementos.Add(new ElementoDePlan("contactos", terceroId.ToString("N"), "Contactos y nombre comercial del tercero", AccionDeSupresion.Anonimizar));
            if (!elementos.Any(e => e.Accion == AccionDeSupresion.Retener) && vinculos.Count == 0)
                elementos.Add(new ElementoDePlan("identidad", terceroId.ToString("N"), "Nombre e identificación del tercero", AccionDeSupresion.Anonimizar));
        }

        if (alcance == AlcanceDeSupresion.Todo)
            elementos.Add(new ElementoDePlan("accesos", terceroId.ToString("N"), "Sesiones, enlaces de acceso y preferencias del portal", AccionDeSupresion.Suprimir));

        return new PlanDeSupresion(terceroId, alcance, elementos);
    }

    private static DateOnly? Mayor(DateOnly? a, DateOnly b) => a is { } actual && actual > b ? actual : b;

    // ---------- Ejecución ----------

    /// <summary>
    /// Ejecuta el plan: elimina lo que se puede y marca lo que se conserva. Idempotente: lo ya suprimido no se repite. Todo el
    /// trabajo de un documento (su stream) va en su propia transacción; si algo falla, lo que ya se hizo queda hecho y la solicitud
    /// se puede resolver de nuevo con un plan nuevo.
    /// </summary>
    public async Task<ResultadoDeSupresion> EjecutarAsync(PlanDeSupresion plan, Guid solicitudId, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var suprimidos = new List<ElementoSuprimido>();
        var retenidos = new List<ElementoRetenido>();
        var anonimizados = new List<string>();
        var terceroId = plan.TerceroId;

        // Documentos: los objetos que otra versión (retenida o de otro documento) sigue usando no se borran del almacenamiento.
        var versiones = plan.Elementos.Where(e => e.Tipo == "documento-version").ToList();
        List<Documento> documentos;
        await using (var consulta = sesiones.ConsultaBoveda())
            documentos = [.. await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct)];
        var aSuprimir = versiones.Where(e => e.Accion == AccionDeSupresion.Suprimir).Select(e => (e.DocumentoId!.Value, e.Numero!.Value)).ToHashSet();
        var objetosQueSeConservan = documentos
            .SelectMany(d => d.Versiones.Where(v => v.TieneArchivo && !aSuprimir.Contains((d.Id, v.Numero))))
            .Select(v => (v.Bucket, v.ClaveObjeto)).ToHashSet();

        foreach (var grupo in versiones.GroupBy(e => e.DocumentoId!.Value))
        {
            var documento = documentos.FirstOrDefault(d => d.Id == grupo.Key);
            if (documento is null)
                continue;

            var todoSeElimina = documento.Versiones.All(v => v.Suprimida || aSuprimir.Contains((documento.Id, v.Numero))) &&
                                !grupo.Any(e => e.Accion == AccionDeSupresion.Retener);
            foreach (var elemento in grupo.Where(e => e.Accion == AccionDeSupresion.Suprimir))
            {
                var version = documento.Buscar(elemento.Numero!.Value)!;
                if (version.TieneArchivo && !objetosQueSeConservan.Contains((version.Bucket, version.ClaveObjeto)))
                    await almacen.EliminarAsync(version.Bucket, version.ClaveObjeto, ct);
                suprimidos.Add(new ElementoSuprimido(elemento.Tipo, elemento.Referencia, elemento.Descripcion));
            }

            if (todoSeElimina)
            {
                await EliminarDocumentoAsync(documento.Id, ct);
                continue;
            }

            await Peticiones.ConReintentosAsync(async () =>
            {
                await using var sesion = sesiones.Boveda();
                sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
                sesion.SetHeader("actorId", "habeas-data");
                var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documento.Id, ct);
                var actual = stream.Aggregate!;
                foreach (var elemento in grupo)
                {
                    if (elemento.Accion == AccionDeSupresion.Suprimir)
                    {
                        if (actual.Suprimir(elemento.Numero!.Value, "Supresión por solicitud de habeas data", ahora) is { } suprimida)
                            stream.AppendOne(suprimida);
                    }
                    else if (actual.MarcarRetencion(elemento.Numero!.Value, elemento.BaseLegal!, elemento.RetenidaHasta, solicitudId, ahora) is { } marcada)
                        stream.AppendOne(marcada);
                }

                await sesion.SaveChangesAsync(ct);
                return true;
            });
        }

        foreach (var elemento in plan.Elementos.Where(e => e.Accion == AccionDeSupresion.Retener))
            retenidos.Add(new ElementoRetenido(elemento.Tipo, elemento.Referencia, elemento.Descripcion, elemento.BaseLegal!, elemento.RetenidaHasta));

        // Ficha: las secciones sin aceptar desaparecen con su stream y su proyección.
        foreach (var seccion in plan.Elementos.Where(e => e.Tipo == "seccion-ficha" && e.Accion == AccionDeSupresion.Suprimir))
        {
            await store.Advanced.Clean.DeleteSingleEventStreamAsync(seccion.SeccionId!.Value, TenantsTecnicos.Boveda, ct);
            await using var sesion = sesiones.Boveda();
            sesion.Delete<SeccionDeFicha>(seccion.SeccionId.Value);
            await sesion.SaveChangesAsync(ct);
            suprimidos.Add(new ElementoSuprimido(seccion.Tipo, seccion.Referencia, seccion.Descripcion));
        }

        if (plan.Elementos.Any(e => e.Tipo == "seccion-ficha" && e.Accion == AccionDeSupresion.Suprimir))
            await ReflejarLaFichaAsync(terceroId, ct);

        // Cuentas bancarias de la bóveda sin designar.
        foreach (var cuenta in plan.Elementos.Where(e => e.Tipo == "cuenta-bancaria" && e.Accion == AccionDeSupresion.Suprimir))
        {
            await using var sesion = sesiones.Boveda();
            sesion.Delete<CuentaBancaria>(cuenta.CuentaId!.Value);
            await sesion.SaveChangesAsync(ct);
            suprimidos.Add(new ElementoSuprimido(cuenta.Tipo, cuenta.Referencia, cuenta.Descripcion));
        }

        // El propio tercero: contactos y nombre comercial; su nombre e identificación solo si no queda nada que ellos identifiquen.
        if (plan.Elementos.Any(e => e.Tipo == "contactos"))
        {
            await using var sesion = sesiones.Global();
            if (await sesion.LoadAsync<Tercero>(terceroId, ct) is { } tercero)
            {
                tercero.Contactos = [];
                tercero.NombreComercial = null;
                anonimizados.Add("Contactos y nombre comercial");
                if (plan.Elementos.Any(e => e.Tipo == "identidad"))
                {
                    tercero.Nombre = "Titular suprimido";
                    tercero.Identificacion = tercero.Identificacion with { Numero = $"SUPRIMIDO-{terceroId:N}"[..24], DigitoVerificacion = null };
                    tercero.ClaveIdentificacion = $"suprimido|{terceroId:N}";
                    anonimizados.Add("Nombre e identificación");
                }

                sesion.Store(tercero);
                await sesion.SaveChangesAsync(ct);
            }
        }

        // Accesos del portal: sesiones de refresco, enlaces de acceso y preferencias.
        if (plan.Elementos.Any(e => e.Tipo == "accesos"))
        {
            await using var sesion = sesiones.Global();
            sesion.DeleteWhere<SesionDeRefresco>(s => s.TerceroId == terceroId);
            sesion.DeleteWhere<EnlaceDeAcceso>(e => e.TerceroId == terceroId);
            sesion.Delete<PreferenciasDelTercero>(terceroId);
            await sesion.SaveChangesAsync(ct);
            suprimidos.Add(new ElementoSuprimido("accesos", terceroId.ToString("N"), "Sesiones, enlaces de acceso y preferencias del portal"));
        }

        return new ResultadoDeSupresion(suprimidos, retenidos, anonimizados);
    }

    /// <summary>Elimina el documento completo: su stream de eventos, su proyección y lo que se derivó de él (cargas pendientes, avisos).</summary>
    private async Task EliminarDocumentoAsync(Guid documentoId, CancellationToken ct)
    {
        await store.Advanced.Clean.DeleteSingleEventStreamAsync(documentoId, TenantsTecnicos.Boveda, ct);
        await using var sesion = sesiones.Boveda();
        sesion.Delete<Documento>(documentoId);
        sesion.DeleteWhere<CargaDeDocumento>(c => c.DocumentoId == documentoId);
        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>El estado de la ficha se reconstruye con las secciones que quedan (sin ninguna, se elimina).</summary>
    private async Task ReflejarLaFichaAsync(Guid terceroId, CancellationToken ct)
    {
        await using var sesion = sesiones.Boveda();
        var quedan = await sesion.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).ToListAsync(ct);
        if (quedan.Count == 0)
        {
            sesion.Delete<EstadoDeFicha>(terceroId);
        }
        else if (await sesion.LoadAsync<EstadoDeFicha>(terceroId, ct) is { } estado)
        {
            estado.Secciones.RemoveAll(s => quedan.All(q => q.Seccion != s.Seccion || q.Periodo != s.Periodo));
            sesion.Store(estado);
        }

        await sesion.SaveChangesAsync(ct);
    }
}
