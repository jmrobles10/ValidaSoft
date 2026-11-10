using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Informes;

/// <summary>
/// Expediente completo en ZIP (IF-5, docs/11 §H8 <c>ExpedienteExportado</c>): los archivos de los documentos aprobados
/// (la versión aceptada de cada requisito), <c>resumen.pdf</c> (datos, requisitos, áreas con tiempos, decisiones y línea de
/// tiempo, con el estilo de los PDF del proyecto), <c>linea-de-tiempo.json</c> (el historial completo) e <c>indice.json</c>
/// con el SHA-256 de cada archivo. Cada documento incluido queda como <c>DocumentoAccedido</c> (motivo «Exportación del
/// expediente …») y la descarga como <c>ExportacionRegistrada</c> con la huella del ZIP.
/// </summary>
public static partial class ExportacionDeExpedienteEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [WolverineGet("/api/v1/expedientes/{id}/exportar")]
    [Authorize(Policy = Permisos.ExpedientesExportar)]
    [Authorize(Policy = Permisos.DocumentosDescargar)]
    public static async Task<IResult> Exportar(Guid id, SesionDeEmpresa empresa, ISesiones sesiones, IContextoSolicitud contexto, HttpContext http,
        IAlmacenDeArchivos almacen, IGeneradorDePdfDeInformes generador, TimeProvider reloj, CancellationToken ct)
    {
        var expediente = await empresa.CargarExpedienteAsync(id, ct);
        var vinculo = await empresa.CargarVinculoAsync(expediente.VinculoId, ct);
        var ahora = reloj.GetUtcNow();
        var eventos = await empresa.Sesion.Events.FetchStreamAsync(id, token: ct);
        var tiempos = Expediente.MedirTiempos(eventos.Select(e => e.Data), ahora);

        // Los documentos aprobados: la versión vigente de cada requisito de documento aprobado (las secciones de la ficha
        // y los formularios no tienen archivo; sus datos están en el expediente y en la ficha).
        var aprobados = expediente.Requisitos
            .Where(r => !r.EsSeccion && r.Estado == EstadoRequisito.Aprobado && r.VersionActual is not null)
            .ToList();
        var destino = new DestinoDeComparticion(contexto.TenantRequerido(), vinculo.EmpresaId, vinculo.Id);
        var motivo = $"Exportación del expediente {expediente.Folio}";
        var incluidos = new List<(RequisitoDeExpediente Requisito, VersionDocumento Version)>();
        if (aprobados.Count > 0 && DocumentosDeVinculoEndpoints.Visible(vinculo))
        {
            incluidos = await Peticiones.ConReintentosAsync(async () =>
            {
                var lista = new List<(RequisitoDeExpediente, VersionDocumento)>();
                await using var boveda = sesiones.Boveda();
                foreach (var requisito in aprobados)
                {
                    var version = requisito.VersionActual!;
                    var stream = await boveda.Events.FetchForExclusiveWriting<Documento>(version.DocumentoId, ct);
                    if (stream.Aggregate is not { } documento || documento.TerceroId != expediente.TerceroId ||
                        documento.Versiones.FirstOrDefault(v => v.Numero == version.Numero) is not { TieneArchivo: true } archivo ||
                        !documento.EstaCompartida(version.Numero, vinculo.Id))
                        continue;
                    stream.AppendOne(documento.RegistrarAcceso(version.Numero, destino, contexto.ActorTipo.ToString(), contexto.ActorId,
                        BovedaEndpoints.Ip(http), BovedaEndpoints.UserAgent(http), motivo, ahora));
                    lista.Add((requisito, archivo));
                }

                await boveda.SaveChangesAsync(ct);
                return lista;
            });
        }

        var usuarios = await NombresAsync(empresa, expediente, eventos, ct);
        var empresaDatos = await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct);
        var indice = new List<object>();
        byte[] zip;
        using (var memoria = new MemoryStream())
        {
            using (var archivo = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
            {
                var nombres = new Dictionary<Guid, string>();
                foreach (var (requisito, version) in incluidos)
                {
                    var ruta = $"documentos/{Limpio(requisito.Codigo)}-v{version.Numero}-{Limpio(version.NombreOriginal)}";
                    await using var origen = await almacen.AbrirLecturaAsync(version.Bucket, version.ClaveObjeto, ct);
                    using var copia = new MemoryStream();
                    await origen.CopyToAsync(copia, ct);
                    var bytes = copia.ToArray();
                    var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
                    await EscribirAsync(archivo, ruta, bytes, ct);
                    nombres[requisito.Id] = ruta;
                    indice.Add(new
                    {
                        ruta, requisito = requisito.Codigo, nombre = requisito.Nombre, version = version.Numero, sha256 = sha,
                        coincideConElExpediente = sha == requisito.VersionActual!.Sha256
                    });
                }

                var historial = Peticiones.Historial(eventos);
                var lineaDeTiempo = JsonSerializer.SerializeToUtf8Bytes(historial, Json);
                await EscribirAsync(archivo, "linea-de-tiempo.json", lineaDeTiempo, ct);
                indice.Add(new { ruta = "linea-de-tiempo.json", sha256 = Convert.ToHexStringLower(SHA256.HashData(lineaDeTiempo)) });

                var pdf = generador.Generar(Resumen(expediente, tiempos, eventos, nombres, usuarios, empresaDatos, ahora));
                await EscribirAsync(archivo, "resumen.pdf", pdf, ct);
                indice.Add(new { ruta = "resumen.pdf", sha256 = Convert.ToHexStringLower(SHA256.HashData(pdf)) });

                var contenidoIndice = JsonSerializer.SerializeToUtf8Bytes(new
                {
                    expediente = new { expediente.Id, expediente.Folio, estado = expediente.Estado.ToString(), tipo = expediente.Tipo.ToString() },
                    empresa = new { id = empresa.EmpresaId, nombre = empresaDatos?.Nombre },
                    generadoEn = ahora,
                    generadoPor = new { tipo = contexto.ActorTipo.ToString(), id = contexto.ActorId },
                    archivos = indice
                }, Json);
                await EscribirAsync(archivo, "indice.json", contenidoIndice, ct);
            }

            zip = memoria.ToArray();
        }

        var nombreZip = $"expediente-{expediente.Folio}.zip";
        return await Exportaciones.EntregarAsync(empresa, contexto, http, reloj,
            new ArchivoExportado("Expediente", "Zip", zip, Exportaciones.TipoZip, nombreZip, incluidos.Count + 3, expediente.Id.ToString(), expediente.Folio,
                expediente.VinculoId), ct);
    }

    private static DocumentoPdf Resumen(Expediente e, TiemposDeExpediente t, IReadOnlyList<IEvent> eventos, IReadOnlyDictionary<Guid, string> enElZip,
        IReadOnlyDictionary<string, string> usuarios, Empresa? empresa, DateTimeOffset ahora)
    {
        string Persona(string? id) => id is null ? "Sistema" : usuarios.GetValueOrDefault(id) ?? id;
        string Dias(double horas) => Exportaciones.Dias(horas / 24);
        var areas = e.Etapas.SelectMany(x => x.Areas).ToDictionary(a => a.AreaId, a => a.Nombre);

        var decisiones = e.Etapas.SelectMany(x => x.Areas).SelectMany(a => a.Veredictos.Where(v => !v.Derivado).Select(v => (Area: a.Nombre, Requisito: "Expediente", V: v)))
            .Concat(e.Requisitos.SelectMany(r => r.Veredictos.Select(v => (Area: v.AreaId is { } x ? areas.GetValueOrDefault(x) ?? "—" : "—", Requisito: r.Nombre, V: v))))
            .OrderBy(x => x.V.Fecha)
            .Select(x => (IReadOnlyList<string>)
            [
                Exportaciones.Momento(x.V.Fecha), x.Area, x.Requisito, x.V.Veredicto == Veredicto.Aprobado ? "Aprobado" : "Observado",
                x.V.Origen switch { OrigenVeredicto.Sistema => "Sistema", OrigenVeredicto.Administrador => "Administrador", _ => "Humano" },
                Persona(x.V.RevisorId),
                string.Join(" ", new[] { string.Join(", ", x.V.Motivos.Select(m => m.TextoAlTercero)), x.V.Observacion, x.V.Justificacion }
                    .Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } texto ? texto : "—",
                x.V.Anulado ? "Anulado" : x.V.Vigente ? "Vigente" : "Superado"
            ])
            .ToList();

        var linea = eventos.Select(ev => (IReadOnlyList<string>)
        [
            Exportaciones.Momento(ev.Timestamp), Evento(ev.Data),
            Peticiones.Cabecera(ev, "actorTipo") is { } tipo ? $"{tipo} · {(Peticiones.Cabecera(ev, "actorId") is { } a ? Persona(a) : "—")}" : "—"
        ]).ToList();

        return new DocumentoPdf(empresa?.Nombre ?? "Empresa", InformeEstadoExpedientesEndpoints.IdentificacionDe(empresa), "Expediente",
            $"Expediente {e.Folio}", $"{e.TerceroNombre} ({InformeEstadoExpedientesEndpoints.Identificacion(e.TerceroIdentificacion)}) · {e.TipoVinculoNombre}",
            ahora, $"Exportado el {Exportaciones.Momento(ahora)} · Validasoft", Horizontal: true,
            [
                new DatosPdf("Datos del expediente",
                [
                    new("Folio", e.Folio),
                    new("Tipo", InformeEstadoExpedientesEndpoints.TextoTipo(e.Tipo.ToString())),
                    new("Estado", InformeEstadoExpedientesEndpoints.TextoEstado(e.Estado.ToString())),
                    new("Tercero", $"{e.TerceroNombre} ({InformeEstadoExpedientesEndpoints.Identificacion(e.TerceroIdentificacion)})"),
                    new("Tipo de vínculo", e.TipoVinculoNombre),
                    new("Flujo", e.Flujo is { } f ? $"{f.Nombre} ({f.Codigo}, versión {f.Version})" : "—"),
                    new("Abierto", Exportaciones.Momento(e.AbiertoEn)),
                    new("Cerrado", e.CerradoEn is { } c ? Exportaciones.Momento(c) : "—"),
                    new("Días totales / en carga / con el tercero / en revisión",
                        $"{Dias(t.HorasTotales)} / {Dias(t.HorasEnCarga)} / {Dias(t.HorasConTercero)} / {Dias(t.HorasEnRevision)}"),
                    new("Devoluciones al tercero", t.Devoluciones.ToString())
                ]),
                new TablaPdf("Requisitos",
                    [new("Requisito", 3), new("Estado", 1.2), new("Versión", 0.8), new("SHA-256", 4), new("En el ZIP", 3)],
                    [.. e.Requisitos.Where(r => r.Estado != EstadoRequisito.NoAplica).Select(r => (IReadOnlyList<string>)
                    [
                        $"{r.Nombre} ({r.Codigo})", r.Estado.ToString(), r.VersionActual is { } v ? $"v{v.Numero}" : "—",
                        r.Eximido ? $"Eximido: {r.JustificacionExencion}" : r.VersionActual?.Sha256 ?? "—", enElZip.GetValueOrDefault(r.Id) ?? "—"
                    ])],
                    "El flujo no pide documentos."),
                new TablaPdf("Áreas",
                    [new("Etapa", 0.7), new("Área", 3), new("Estado", 1.5), new("Días en revisión", 1.3), new("Rondas", 1), new("SLA (horas)", 1.1),
                        new("SLA incumplido", 1.2)],
                    [.. t.Areas.Select(a => (IReadOnlyList<string>)
                    [
                        a.Etapa.ToString(), a.Nombre, a.Estado.ToString(), Dias(a.Horas), a.Rondas.ToString(), a.SlaHoras?.ToString() ?? "—",
                        a.SlaIncumplido ? "Sí" : "No"
                    ])],
                    "Sin áreas."),
                new TablaPdf("Decisiones",
                    [new("Fecha", 1.6), new("Área", 1.8), new("Sobre", 2.2), new("Veredicto", 1.1), new("Origen", 1.2), new("Por", 1.8), new("Motivos y observación", 4),
                        new("Vigencia", 1)],
                    decisiones, "Aún no hay decisiones."),
                new TablaPdf("Línea de tiempo", [new("Fecha", 1.4), new("Evento", 5), new("Actor", 3)], linea, "Sin eventos."),
                new NotaPdf("Este resumen acompaña los archivos del ZIP; indice.json trae el SHA-256 de cada uno y linea-de-tiempo.json el historial " +
                            "completo del expediente. Cada documento incluido quedó registrado como acceso en la auditoría de la empresa.")
            ]);
    }

    /// <summary>Nombres de los usuarios que decidieron o actuaron en el expediente.</summary>
    private static async Task<IReadOnlyDictionary<string, string>> NombresAsync(SesionDeEmpresa empresa, Expediente e, IReadOnlyList<IEvent> eventos,
        CancellationToken ct)
    {
        var ids = e.Etapas.SelectMany(x => x.Areas).SelectMany(a => a.Veredictos).Concat(e.Requisitos.SelectMany(r => r.Veredictos))
            .Select(v => v.RevisorId)
            .Concat(eventos.Select(ev => Peticiones.Cabecera(ev, "actorId")))
            .Select(x => Guid.TryParse(x, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<string, string>();
        return (await empresa.Sesion.LoadManyAsync<Usuario>(ct, ids)).ToDictionary(u => u.Id.ToString(), u => u.Nombre);
    }

    private static string Evento(object datos) => datos switch
    {
        ExpedienteAbierto => "Expediente abierto",
        FlujoSeleccionado f => $"Flujo {f.Nombre} (versión {f.Version})",
        RequisitosCalculados r => $"Se piden {r.Requisitos.Count} requisitos",
        RequisitosAgregados r => $"Se agregan a la actualización: {string.Join(", ", r.Requisitos.Select(x => x.Nombre))}",
        VersionAdjuntadaARequisito v => $"Versión {v.Version.Numero} recibida",
        VeredictosInvalidadosPorNuevaVersion => "Versión nueva: se invalidan las decisiones previas",
        VeredictoDeRequisitoEmitido v => $"Requisito {(v.Veredicto == Veredicto.Aprobado ? "aprobado" : "observado")}",
        RequisitoEximido => "Requisito eximido",
        RequisitoRatificado => "Requisito ratificado por el tercero",
        CompletitudDeSeccionEvaluada => "Completitud de sección evaluada",
        SeccionAceptadaPorEmpresa s => $"Sección {s.Seccion} aceptada (versión {s.Version})",
        ExpedienteEnviadoARevision => "Enviado a revisión",
        EtapaActivada a => $"Etapa {a.Orden} activada",
        VeredictoDeAreaEmitido v => $"Área {(v.Veredicto == Veredicto.Aprobado ? "aprobó" : "observó")}{(v.DerivadoDeRequisitos ? " (por sus requisitos)" : string.Empty)}",
        ExpedienteObservado => "Devuelto al tercero por observaciones",
        ExpedienteReenviado => "El tercero reenvió",
        RevisionReanudada => "Revisión reanudada",
        EtapaAprobada a => $"Etapa {a.Orden} aprobada",
        ExpedienteAprobado => "Expediente aprobado",
        ExpedienteRechazado r => $"Expediente rechazado: {r.Justificacion}",
        ExpedienteAnulado a => $"Expediente anulado: {a.Motivo}",
        VeredictoAnuladoPorAdministrador a => $"Veredicto anulado: {a.Justificacion}",
        _ => Separar(datos.GetType().Name)
    };

    private static string Separar(string nombre) => RegexPalabras().Replace(nombre, " $1").Trim();

    /// <summary>Nombre de archivo seguro dentro del ZIP: sin rutas ni caracteres reservados.</summary>
    private static string Limpio(string nombre)
    {
        var texto = new string([.. Path.GetFileName(nombre).Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')]);
        return texto.Length == 0 ? "archivo" : texto.Length > 120 ? texto[^120..] : texto;
    }

    private static async Task EscribirAsync(ZipArchive zip, string ruta, byte[] contenido, CancellationToken ct)
    {
        await using var flujo = zip.CreateEntry(ruta, CompressionLevel.Optimal).Open();
        await flujo.WriteAsync(contenido, ct);
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex RegexPalabras();
}
