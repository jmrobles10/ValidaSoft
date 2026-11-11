using Marten;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Consentimientos;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Aplicacion.Sandbox;

/// <summary>
/// El sandbox de un grupo (SB-1…SB-5): un <b>tenant hermano</b> del de producción, creado y sembrado cuando se pide. Hereda la
/// configuración del grupo (empresas, tipos de vínculo y de documento, áreas, flujos, motivos, plantillas y sistemas integrados)
/// y nada de sus datos: ni terceros, ni vínculos, ni documentos, ni usuarios, ni llaves. Reiniciarlo borra todo lo que se hizo
/// en él y lo vuelve a sembrar con la configuración de producción de ese momento; las empresas conservan su id y las API keys de
/// prueba (que viven en producción) siguen valiendo.
/// </summary>
public sealed class ServicioDeSandbox(ISesiones sesiones, IDocumentStore store, IProtectorDeSecretos protector, IAlmacenDeArchivos almacen,
    IConfiguration configuracion, CacheDeEmpresas cacheDeEmpresas, TimeProvider reloj)
{
    private const int TamanoDeBloque = 500;

    /// <summary>Texto de la autorización y los términos de las empresas del sandbox cuando la empresa real aún no los publicó.</summary>
    private const string TextoDePrueba = "Texto de prueba del sandbox de Validasoft: no tiene validez legal ni se usa en producción.";

    /// <summary>Validaciones de IA por mes de cada empresa del sandbox: las responde el proveedor simulado, sin costo.</summary>
    private const int ValidacionesDeIaDelSandbox = 100_000;

    /// <summary>El grupo de producción y su sandbox (nulo si aún no existe) a partir del tenant de cualquier credencial del grupo.</summary>
    public async Task<(Tenant Produccion, Tenant? Sandbox)> ResolverAsync(Guid tenantDeLaCredencial, CancellationToken ct)
    {
        await using var consulta = store.QuerySession();
        var tenant = await consulta.LoadAsync<Tenant>(tenantDeLaCredencial, ct)
                     ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "El tenant de la credencial no existe.");
        if (!tenant.EsSandbox)
            return (tenant, await BuscarAsync(consulta, tenant.Id, ct));

        var produccion = await consulta.LoadAsync<Tenant>(tenant.TenantDeProduccionId!.Value, ct)
                         ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe el tenant de producción de este sandbox.");
        return (produccion, tenant);
    }

    private static Task<Tenant?> BuscarAsync(IQuerySession consulta, Guid produccionId, CancellationToken ct) =>
        consulta.Query<Tenant>().Where(t => t.TenantDeProduccionId == produccionId).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Crea el sandbox si no existe y le agrega lo que le falte de la configuración de producción (empresas nuevas, tipos, flujos…)
    /// sin tocar lo que ya tiene. Idempotente. 404 <c>tenant-no-encontrado</c>; 409 <c>tenant-inactivo</c> si el grupo está desactivado.
    /// </summary>
    public async Task<(Tenant Sandbox, bool Creado)> AsegurarAsync(Guid produccionId, CancellationToken ct)
    {
        var (produccion, sandbox) = await ResolverAsync(produccionId, ct);
        if (!produccion.Activo)
            throw ReglaDeNegocioException.Conflicto("tenant-inactivo", "El grupo está desactivado: no tiene sandbox.");

        var creado = false;
        if (sandbox is null)
        {
            var nuevo = Tenant.CrearSandbox(produccion, reloj.GetUtcNow());
            try
            {
                await using var sesion = sesiones.ParaTenant(nuevo.Id);
                sesion.Store(nuevo);
                await sesion.SaveChangesAsync(ct);
                sandbox = nuevo;
                creado = true;
            }
            catch (Exception e) when (Peticiones.EsCarrera(e))
            {
                // Dos peticiones lo crearon a la vez (slug único): se queda con el que ganó.
                await using var consulta = store.QuerySession();
                sandbox = await BuscarAsync(consulta, produccion.Id, ct)
                          ?? throw new InvalidOperationException("No se pudo crear el sandbox del grupo.", e);
            }
        }

        await SembrarAsync(produccion, sandbox, ct);
        return (sandbox, creado);
    }

    /// <summary>
    /// Borra todo lo que se hizo en el sandbox —vínculos, expedientes, certificados, documentos y archivos de sus terceros, usuarios,
    /// suscripciones de webhook, eventos y correos de prueba— y lo siembra de nuevo con la configuración de producción de ahora.
    /// 404 <c>sandbox-no-existe</c> si aún no se creó.
    /// </summary>
    public async Task<Tenant> ReiniciarAsync(Guid produccionId, CancellationToken ct)
    {
        var (produccion, sandbox) = await ResolverAsync(produccionId, ct);
        if (sandbox is null)
            throw ReglaDeNegocioException.NoEncontrado("sandbox-no-existe", "El grupo aún no tiene sandbox: créalo con POST /api/v1/sandbox.");

        await LimpiarAsync(sandbox, ct);
        sandbox.ReiniciadoEn = reloj.GetUtcNow();
        await using (var sesion = sesiones.ParaTenant(sandbox.Id))
        {
            sesion.Store(sandbox);
            await sesion.SaveChangesAsync(ct);
        }

        await SembrarAsync(produccion, sandbox, ct);
        return sandbox;
    }

    // ---------- siembra ----------

    private async Task SembrarAsync(Tenant produccion, Tenant sandbox, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var copias = await SincronizarEmpresasAsync(produccion, sandbox, ahora, ct);
        var empresas = copias.ToDictionary(c => c.Key, c => c.Value.Id);

        await using (var origen = store.QuerySession(produccion.IdentificadorMarten))
        await using (var destino = sesiones.ParaTenant(sandbox.Id))
        {
            await CopiarAsync<TipoVinculo>(origen, destino, _ => true, t => t.Codigo, ct);
            await CopiarAsync<TipoDocumentoTenant>(origen, destino, t => { t.TenantId = sandbox.Id; return true; }, t => t.Codigo, ct);
            await CopiarAsync<Motivo>(origen, destino, _ => true, m => m.Codigo, ct);
            await CopiarAsync<Area>(origen, destino, a =>
            {
                if (!empresas.TryGetValue(a.EmpresaId, out var empresaId))
                    return false;
                a.EmpresaId = empresaId;
                a.Miembros = []; // las personas no se copian: en el sandbox se asignan de nuevo
                return true;
            }, a => $"{a.EmpresaId:N}:{a.Codigo}", ct);
            await CopiarAsync<Flujo>(origen, destino, f =>
            {
                if (!empresas.TryGetValue(f.EmpresaId, out var empresaId))
                    return false;
                f.EmpresaId = empresaId;
                return true;
            }, f => $"{f.EmpresaId:N}:{f.Codigo}", ct);
            await CopiarAsync<FlujoPorDefecto>(origen, destino, p =>
            {
                if (!empresas.TryGetValue(p.EmpresaId, out var empresaId))
                    return false;
                p.EmpresaId = empresaId;
                p.Id = FlujoPorDefecto.Clave(empresaId, p.TipoVinculoId);
                return true;
            }, p => p.Id, ct);
            await CopiarAsync<PlantillaNotificacion>(origen, destino, p =>
            {
                if (!empresas.TryGetValue(p.EmpresaId, out var empresaId))
                    return false;
                p.EmpresaId = empresaId;
                p.Id = PlantillaNotificacion.Clave(empresaId, p.Evento, p.Canal);
                return true;
            }, p => p.Id, ct);
            await SincronizarSistemasAsync(origen, destino, ct);

            foreach (var copia in copias.Values)
                await SembrarEmpresaAsync(destino, copia, ahora, ct);

            await destino.SaveChangesAsync(ct);
        }

        await SembrarTextosLegalesAsync(copias, sandbox, ahora, ct);
    }

    /// <summary>Una copia de pruebas por empresa de producción (nunca se borra una al reiniciar: conserva su id). Devuelve prod → copia.</summary>
    private async Task<Dictionary<Guid, Empresa>> SincronizarEmpresasAsync(Tenant produccion, Tenant sandbox, DateTimeOffset ahora, CancellationToken ct)
    {
        List<Empresa> reales, copias;
        await using (var consulta = store.QuerySession())
        {
            reales = [.. await consulta.Query<Empresa>().Where(e => e.TenantId == produccion.Id).ToListAsync(ct)];
            copias = [.. await consulta.Query<Empresa>().Where(e => e.TenantId == sandbox.Id).ToListAsync(ct)];
        }

        var porReal = copias.Where(c => c.EmpresaDeProduccionId is not null).ToDictionary(c => c.EmpresaDeProduccionId!.Value);
        var ocupados = copias.Select(c => c.PrefijoFolio).ToHashSet(StringComparer.Ordinal);

        await using var sesion = sesiones.Global();
        foreach (var real in reales)
        {
            if (porReal.TryGetValue(real.Id, out var copia))
            {
                copia.SincronizarCopiaDeSandbox(real);
            }
            else
            {
                var prefijo = AislamientoDeSandbox.PrefijoDeFolio(real.PrefijoFolio, ocupados);
                ocupados.Add(prefijo);
                copia = Empresa.CrearCopiaDeSandbox(real, sandbox.Id, prefijo, ahora);
                porReal[real.Id] = copia;
            }

            sesion.Store(copia);
        }

        await sesion.SaveChangesAsync(ct);
        foreach (var copia in porReal.Values)
            cacheDeEmpresas.Olvidar(copia.Id);
        return reales.ToDictionary(r => r.Id, r => porReal[r.Id]);
    }

    /// <summary>Agrega al sandbox lo que le falta de producción (por clave natural); lo que ya tiene —aunque se haya editado— no se toca.</summary>
    private static async Task CopiarAsync<T>(IQuerySession origen, IDocumentSession destino, Func<T, bool> adaptar, Func<T, string> clave,
        CancellationToken ct) where T : class
    {
        var presentes = (await destino.Query<T>().ToListAsync(ct)).Select(clave).ToHashSet(StringComparer.Ordinal);
        foreach (var documento in await origen.Query<T>().ToListAsync(ct))
        {
            // El documento se leyó de otra sesión: modificarlo aquí no cambia producción; Store lo escribe con el tenant del sandbox.
            if (adaptar(documento) && presentes.Add(clave(documento)))
                destino.Store(documento);
        }
    }

    /// <summary>
    /// Los sistemas integrados de producción existen también en el sandbox con el mismo id y la misma clave pública: así la aserción
    /// que firma el sistema vale en los dos, y una API key de prueba emitida para el sistema apunta a uno que existe. Se alinean en cada
    /// sincronización (clave, orígenes y si está activo); los que el integrador cree solo en el sandbox no se tocan.
    /// </summary>
    private static async Task SincronizarSistemasAsync(IQuerySession origen, IDocumentSession destino, CancellationToken ct)
    {
        var propios = (await destino.Query<SistemaIntegrado>().ToListAsync(ct)).ToDictionary(s => s.Id);
        var codigos = propios.Values.Select(s => s.Codigo).ToHashSet(StringComparer.Ordinal);
        foreach (var real in await origen.Query<SistemaIntegrado>().ToListAsync(ct))
        {
            if (propios.TryGetValue(real.Id, out var copia))
            {
                copia.Nombre = real.Nombre;
                copia.ClavePublicaPem = real.ClavePublicaPem;
                copia.OrigenesPermitidos = [.. real.OrigenesPermitidos];
                copia.Activo = real.Activo;
                destino.Store(copia);
            }
            else if (codigos.Add(real.Codigo))
            {
                destino.Store(real);
            }
        }
    }

    /// <summary>
    /// Lo que cada empresa del sandbox necesita para operar sin salir a ningún proveedor: un plan de IA activo con la llave de
    /// prueba (que el analizador por entorno manda al proveedor simulado) y las listas restrictivas con el proveedor simulado.
    /// </summary>
    private async Task SembrarEmpresaAsync(IDocumentSession destino, Empresa copia, DateTimeOffset ahora, CancellationToken ct)
    {
        if (await destino.LoadAsync<PlanDeIaEmpresa>(copia.Id, ct) is null)
        {
            var plan = PlanDeIaEmpresa.Nuevo(copia.Id, ahora);
            plan.Actualizar(new CambiosDePlanIa(ValidacionesDeIaDelSandbox, false, null, AislamientoDeSandbox.LlaveDeIa, null, null, null, true),
                protector.Proteger, ahora);
            destino.Store(plan);
        }

        if (await destino.LoadAsync<ListasRestrictivasEmpresa>(copia.Id, ct) is null)
        {
            var listas = ListasRestrictivasEmpresa.Nueva(copia.Id);
            listas.ElegirProveedor(ProveedoresDeListas.Simulado, ahora);
            destino.Store(listas);
        }
    }

    /// <summary>Autorización y términos de cada empresa del sandbox: los de su empresa real si ya los publicó; si no, un texto de prueba.</summary>
    private async Task SembrarTextosLegalesAsync(Dictionary<Guid, Empresa> copias, Tenant sandbox, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.Global();
        foreach (var (realId, copia) in copias)
        {
            foreach (var tipo in new[] { TipoTextoLegal.AutorizacionEmpresa, TipoTextoLegal.TerminosEmpresa })
            {
                if (await TextosLegales.VigenteAsync(sesion, tipo, copia.Id, ct) is not null)
                    continue;

                var real = await TextosLegales.VigenteAsync(sesion, tipo, realId, ct);
                var contenido = real is null ? TextoDePrueba : $"[Sandbox] {real.Contenido}";
                sesion.Insert(TextoLegal.Publicar(tipo, sandbox.Id, copia.Id, contenido, null, "sistema-sandbox", ahora));
            }
        }

        await sesion.SaveChangesAsync(ct);
    }

    // ---------- reinicio ----------

    private async Task LimpiarAsync(Tenant sandbox, CancellationToken ct)
    {
        List<Guid> terceros;
        await using (var consulta = store.QuerySession())
            terceros = [.. await consulta.Query<Tercero>().Where(t => t.SandboxTenantId == sandbox.Id).Select(t => t.Id).ToListAsync(ct)];

        foreach (var bloque in terceros.Chunk(TamanoDeBloque))
            await LimpiarTercerosAsync(bloque, ct);

        // El sandbox sigue vivo mientras se limpia: webhooks, correos y procesos en segundo plano agregan eventos a los mismos flujos que
        // aquí se borran. Si dos transacciones toman varias filas en distinto orden, PostgreSQL aborta una (40P01, interbloqueo): antes el
        // reinicio respondía 500 de forma intermitente cuando la víctima era la limpieza. La partición del tenant (la que más filas toca)
        // ya no espera bloqueos (BorrarParticionDelTenantAsync); los borrados por Marten se repiten si pierden el cruce.
        await Peticiones.ReintentarSiHayConflictoAsync(async () =>
        {
            await using var global = store.LightweightSession();
            global.DeleteWhere<EventoPublico>(e => e.TenantId == sandbox.Id);
            await global.SaveChangesAsync(ct);
        }, ct);

        await Peticiones.ReintentarSiHayConflictoAsync(() => BorrarParticionDelTenantAsync(sandbox.Id, ct), ct);
        await almacen.EliminarPrefijoAsync(ClavesDeAlmacen.BucketDeTenant(sandbox.Id), string.Empty, ct);
    }

    /// <summary>La bóveda es global: los terceros de pruebas y todo lo que cuelga de ellos se borran por su id.</summary>
    private async Task LimpiarTercerosAsync(Guid[] terceros, CancellationToken ct)
    {
        // Los flujos de eventos de lo que se borra se juntan entre intentos: si un intento se revierte y el siguiente ya no encuentra los
        // documentos, igual hay que borrar sus flujos.
        HashSet<Guid> flujos = [];
        await Peticiones.ReintentarSiHayConflictoAsync(async () =>
        {
            await using var boveda = store.LightweightSession(TenantsTecnicos.Boveda);
            var documentos = await boveda.Query<Documento>().Where(d => d.TerceroId.IsOneOf(terceros)).Select(d => d.Id).ToListAsync(ct);
            var secciones = await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId.IsOneOf(terceros)).Select(s => s.Id).ToListAsync(ct);
            flujos.UnionWith(documentos);
            flujos.UnionWith(secciones);

            boveda.DeleteWhere<Documento>(d => d.TerceroId.IsOneOf(terceros));
            boveda.DeleteWhere<SeccionDeFicha>(s => s.TerceroId.IsOneOf(terceros));
            boveda.DeleteWhere<EstadoDeFicha>(e => e.Id.IsOneOf(terceros));
            boveda.DeleteWhere<CuentaBancaria>(c => c.TerceroId.IsOneOf(terceros));
            boveda.DeleteWhere<AvisoDeVencimiento>(a => a.TerceroId.IsOneOf(terceros));
            boveda.DeleteWhere<CargaDeDocumento>(c => c.TerceroId.IsOneOf(terceros));
            await boveda.SaveChangesAsync(ct);
        }, ct);

        await Peticiones.ReintentarSiHayConflictoAsync(async () =>
        {
            await using var global = store.LightweightSession();
            global.DeleteWhere<EnlaceDeAcceso>(e => e.TerceroId.IsOneOf(terceros));
            global.DeleteWhere<SesionDeRefresco>(s => s.TerceroId.IsOneOf(terceros));
            global.DeleteWhere<Tercero>(t => t.Id.IsOneOf(terceros));
            await global.SaveChangesAsync(ct);
        }, ct);

        await Peticiones.ReintentarSiHayConflictoAsync(() => BorrarFlujosDeEventosAsync(TenantsTecnicos.Boveda, [.. flujos], ct), ct);
        foreach (var tercero in terceros)
            await almacen.EliminarPrefijoAsync(ClavesDeAlmacen.BucketBoveda, $"terceros/{tercero:N}/", ct);
    }

    private NpgsqlConnection Conexion() => new(configuracion.GetConnectionString("Validasoft")
                                               ?? throw new InvalidOperationException("Falta ConnectionStrings:Validasoft."));

    /// <summary>Pasadas máximas de un borrado que encuentra filas bloqueadas por quien sigue escribiendo, y su espera entre una y otra.</summary>
    private const int PasadasDeBorrado = 25;

    private static TimeSpan EsperaEntrePasadas(int pasada) => TimeSpan.FromMilliseconds(Math.Min(50 * pasada, 400));

    /// <summary>
    /// Borra los eventos y los flujos de un tenant (o solo los de <paramref name="soloEstos"/>) <b>sin esperar a nadie</b>: bloquea los
    /// flujos con <c>FOR UPDATE SKIP LOCKED</c>, borra los eventos de los que logró bloquear y luego los flujos. Los flujos que otro
    /// proceso tiene bloqueado en ese momento (una escritura en curso) se saltan y quedan para la siguiente pasada. Quien nunca espera
    /// un bloqueo no puede quedar en un interbloqueo, ni agregarle un evento a un flujo que ya se va a borrar: sus escrituras esperan a
    /// que termine esta transacción. Devuelve cuántos flujos quedaron pendientes.
    /// </summary>
    private static async Task<int> BorrarFlujosSinEsperarAsync(NpgsqlConnection conexion, NpgsqlTransaction transaccion, string tenant,
        Guid[]? soloEstos, CancellationToken ct)
    {
        var filtro = soloEstos is null ? string.Empty : " AND id = ANY(@ids)";

        var visibles = new List<Guid>();
        await using (var consulta = new NpgsqlCommand($"SELECT id FROM eventos.mt_streams WHERE tenant_id = @tenant{filtro}", conexion, transaccion))
        {
            consulta.Parameters.AddWithValue("tenant", tenant);
            if (soloEstos is not null)
                consulta.Parameters.AddWithValue("ids", soloEstos);
            await using var lector = await consulta.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
                visibles.Add(lector.GetGuid(0));
        }

        if (visibles.Count == 0)
            return 0;

        var bloqueados = new List<Guid>();
        await using (var bloquear = new NpgsqlCommand(
                         "SELECT id FROM eventos.mt_streams WHERE tenant_id = @tenant AND id = ANY(@ids) FOR UPDATE SKIP LOCKED", conexion, transaccion))
        {
            bloquear.Parameters.AddWithValue("tenant", tenant);
            bloquear.Parameters.AddWithValue("ids", visibles.ToArray());
            await using var lector = await bloquear.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
                bloqueados.Add(lector.GetGuid(0));
        }

        if (bloqueados.Count > 0)
        {
            // Los eventos antes que sus flujos (clave foránea).
            foreach (var tabla in new[] { ("mt_events", "stream_id"), ("mt_streams", "id") })
            {
                await using var borrar = new NpgsqlCommand($"DELETE FROM eventos.{tabla.Item1} WHERE tenant_id = @tenant AND {tabla.Item2} = ANY(@ids)", conexion, transaccion);
                borrar.Parameters.AddWithValue("tenant", tenant);
                borrar.Parameters.AddWithValue("ids", bloqueados.ToArray());
                await borrar.ExecuteNonQueryAsync(ct);
            }
        }

        return visibles.Count - bloqueados.Count;
    }

    /// <summary>Borra los flujos de eventos indicados (la bóveda es global: se borran por id), repitiendo con los que otro proceso tenía bloqueados.</summary>
    private async Task BorrarFlujosDeEventosAsync(string tenant, IReadOnlyList<Guid> streams, CancellationToken ct)
    {
        if (streams.Count == 0)
            return;

        var pendientes = streams.ToArray();
        for (var pasada = 1; ; pasada++)
        {
            await using var conexion = Conexion();
            await conexion.OpenAsync(ct);
            await using var transaccion = await conexion.BeginTransactionAsync(ct);
            var sinBorrar = await BorrarFlujosSinEsperarAsync(conexion, transaccion, tenant, pendientes, ct);
            await transaccion.CommitAsync(ct);
            if (sinBorrar == 0)
                return;

            if (pasada == PasadasDeBorrado)
                throw ReglaDeNegocioException.Conflicto("sandbox-ocupado", "Hay actividad en curso sobre lo que se va a borrar: intenta reiniciar de nuevo en unos segundos.");
            await Task.Delay(EsperaEntrePasadas(pasada), ct);
        }
    }

    /// <summary>
    /// Vacía la partición del tenant sandbox: todo documento <c>mt_doc_*</c> de <c>validacion</c> e <c>identidad</c> y sus eventos y
    /// flujos. Marten guarda el tenant en <c>tenant_id</c> de cada tabla, así que no toca nada de producción. El sandbox sigue vivo
    /// mientras tanto (webhooks, correos y procesos en segundo plano escriben en él), así que no se espera a quien tenga filas
    /// bloqueadas —<c>FOR UPDATE SKIP LOCKED</c>—: se borra lo que está libre y se repite con lo que quedó, en vez de quedar en un
    /// interbloqueo con una escritura que toma los mismos flujos y documentos en otro orden.
    /// </summary>
    private async Task BorrarParticionDelTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = tenantId.ToString("N");
        var tablas = new List<string>();
        await using (var conexion = Conexion())
        {
            await conexion.OpenAsync(ct);
            await using var consulta = new NpgsqlCommand(
                """
                SELECT c.table_schema || '.' || c.table_name
                FROM information_schema.columns c
                JOIN information_schema.tables t ON t.table_schema = c.table_schema AND t.table_name = c.table_name AND t.table_type = 'BASE TABLE'
                WHERE c.column_name = 'tenant_id' AND c.table_schema IN ('validacion', 'identidad')
                  AND c.table_name LIKE 'mt\_doc\_%'
                """, conexion);
            await using var lector = await consulta.ExecuteReaderAsync(ct);
            while (await lector.ReadAsync(ct))
                tablas.Add(lector.GetString(0));
        }

        for (var pasada = 1; ; pasada++)
        {
            var pendientes = await UnaPasadaDeBorradoAsync(tenant, tablas, ct);
            if (pendientes == 0)
                return;

            if (pasada == PasadasDeBorrado)
                throw ReglaDeNegocioException.Conflicto("sandbox-ocupado", "El sandbox tiene actividad en curso: intenta reiniciarlo de nuevo en unos segundos.");
            await Task.Delay(EsperaEntrePasadas(pasada), ct);
        }
    }

    /// <summary>Una pasada, en una transacción: flujos y eventos, luego los documentos. Devuelve cuántas filas quedaron por estar bloqueadas.</summary>
    private async Task<int> UnaPasadaDeBorradoAsync(string tenant, List<string> tablas, CancellationToken ct)
    {
        await using var conexion = Conexion();
        await conexion.OpenAsync(ct);
        await using var transaccion = await conexion.BeginTransactionAsync(ct);

        var pendientes = await BorrarFlujosSinEsperarAsync(conexion, transaccion, tenant, null, ct);
        foreach (var tabla in tablas)
        {
            var partes = tabla.Split('.');
            var nombre = $"\"{partes[0]}\".\"{partes[1]}\"";

            await using var contar = new NpgsqlCommand($"SELECT count(*) FROM {nombre} WHERE tenant_id = @tenant", conexion, transaccion);
            contar.Parameters.AddWithValue("tenant", tenant);
            var visibles = Convert.ToInt32(await contar.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
            if (visibles == 0)
                continue;

            await using var borrar = new NpgsqlCommand(
                $"DELETE FROM {nombre} WHERE ctid = ANY(ARRAY(SELECT ctid FROM {nombre} WHERE tenant_id = @tenant FOR UPDATE SKIP LOCKED))", conexion, transaccion);
            borrar.Parameters.AddWithValue("tenant", tenant);
            var borradas = await borrar.ExecuteNonQueryAsync(ct);
            pendientes += Math.Max(0, visibles - borradas);
        }

        await transaccion.CommitAsync(ct);
        return pendientes;
    }
}
