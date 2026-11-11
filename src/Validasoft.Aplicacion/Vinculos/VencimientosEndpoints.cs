using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>Última vez que la empresa le recordó al tercero un documento de un vínculo (id <c>{vinculo:N}|{documento:N}</c>).</summary>
public sealed class RecordatorioDeVencimiento
{
    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public Guid VinculoId { get; set; }

    public Guid DocumentoId { get; set; }

    public DateTimeOffset Fecha { get; set; }

    public string Por { get; set; } = string.Empty;

    public static string Clave(Guid vinculoId, Guid documentoId) => $"{vinculoId:N}|{documentoId:N}";
}

/// <summary>
/// Vencimientos de la empresa activa (VE-1..VE-3, docs/06 <c>/vencimientos</c>): por vínculo y documento compartido, la
/// vigencia que la bóveda calculó para esta empresa (TD-6), con su estado, su base y si se puede ratificar; el informe en
/// Excel y el recordatorio al tercero. Se calcula al consultar sobre los documentos de los terceros de la empresa (aún no
/// es la proyección <c>VencimientoProgramado</c>).
/// </summary>
public static class VencimientosEndpoints
{
    public const int MaximoPorRecordatorio = 200;

    /// <summary>
    /// <paramref name="dias"/>: los que vencen en los próximos N días más los que ya están en gracia o vencidos (30 por
    /// defecto; 0 = todos los que tienen fecha). <paramref name="estado"/>: Vigente, PorVencer, EnGracia o Vencida.
    /// </summary>
    [WolverineGet("/api/v1/vencimientos")]
    [Authorize(Policy = Permisos.DocumentosLeer)]
    public static Task<IReadOnlyList<VencimientoDto>> Listar(SesionDeEmpresa empresa, TimeProvider reloj, int? dias, string? estado, CancellationToken ct) =>
        CalcularAsync(empresa, reloj, dias, estado, ct);

    [WolverineGet("/api/v1/vencimientos/excel")]
    [Authorize(Policy = Permisos.DocumentosLeer)]
    public static async Task<IResult> Excel(SesionDeEmpresa empresa, TimeProvider reloj, int? dias, string? estado, CancellationToken ct)
    {
        var filas = await CalcularAsync(empresa, reloj, dias, estado, ct);
        var nombreEmpresa = (await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct))?.Nombre ?? "Empresa";
        var bytes = HojaDeCalculo.Crear("Vencimientos",
            ["Tercero", "Identificación", "Tipo de vínculo", "Documento", "Versión", "Vigente hasta", "Días", "Estado", "Fin de la gracia", "Base de la vigencia",
                "Ratificable", "Último recordatorio"],
            filas.Select(f => (IReadOnlyList<object?>)
            [
                f.Tercero.Nombre, f.Tercero.Identificacion, f.TipoVinculo.Nombre, f.Documento, f.Version, f.VigenteHasta, f.DiasRestantes,
                TextoEstado(f.Estado), f.FinDeGracia, TextoBase(f.Base), f.PermiteRatificar ? "Sí" : "No",
                f.UltimoRecordatorio is { } r ? CalculadoraDeVigencia.Fecha(r) : null
            ]));
        var archivo = $"vencimientos-{Slug(nombreEmpresa)}-{CalculadoraDeVigencia.Fecha(reloj.GetUtcNow()):yyyy-MM-dd}.xlsx";
        return Results.File(bytes, HojaDeCalculo.TipoContenido, archivo);
    }

    /// <summary>
    /// Recuerda al tercero los documentos elegidos: un correo por destinatario con cada documento y su fecha para esta
    /// empresa (el mismo aviso consolidado de TD-7). Lo ya recordado hoy se omite, para no repetir correos.
    /// </summary>
    [WolverinePost("/api/v1/vencimientos/recordar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<RecordatorioEnviadoDto> Recordar(RecordarVencimientosDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        var pedidos = (dto.Vencimientos ?? []).Distinct().ToList();
        if (pedidos.Count == 0)
            throw ReglaDeNegocioException.Validacion("lote-vacio", "Elige al menos un documento.");
        if (pedidos.Count > MaximoPorRecordatorio)
            throw ReglaDeNegocioException.Validacion("lote-demasiado-grande", $"Se recuerdan hasta {MaximoPorRecordatorio} documentos a la vez.");

        empresa.UsarMensajeria(mensajes);
        var ahora = reloj.GetUtcNow();
        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        var todos = (await CalcularAsync(empresa, reloj, 0, null, ct)).ToDictionary(v => (v.VinculoId, v.DocumentoId));
        var vinculoIds = pedidos.Select(p => p.VinculoId).Distinct().ToArray();
        var vinculos = (await empresa.Vinculos().Where(v => v.Id.IsOneOf(vinculoIds)).ToListAsync(ct)).ToDictionary(v => v.Id);
        var nombreEmpresa = (await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct))?.Nombre ?? "La empresa";

        var enviar = new List<(string Correo, VencimientoDto Vencimiento)>();
        var omitidos = 0;
        foreach (var pedido in pedidos)
        {
            if (!todos.TryGetValue((pedido.VinculoId, pedido.DocumentoId), out var vencimiento) ||
                vinculos.GetValueOrDefault(pedido.VinculoId) is not { CorreoTercero: { Length: > 0 } correo } ||
                (vencimiento.UltimoRecordatorio is { } ultimo && CalculadoraDeVigencia.Fecha(ultimo) == hoy))
            {
                omitidos++;
                continue;
            }

            enviar.Add((correo.Trim().ToLowerInvariant(), vencimiento));
            empresa.Sesion.Store(new RecordatorioDeVencimiento
            {
                Id = RecordatorioDeVencimiento.Clave(pedido.VinculoId, pedido.DocumentoId),
                EmpresaId = empresa.EmpresaId,
                VinculoId = pedido.VinculoId,
                DocumentoId = pedido.DocumentoId,
                Fecha = ahora,
                Por = contexto.ActorId
            });
            // El tercero también lo ve en la campana de su portal (NT-1).
            empresa.Sesion.Store(PlanificadorDeNotificacionesDelTercero.DeRecordatorioDeVencimiento(contexto.TenantRequerido(), vencimiento.Tercero.Id,
                empresa.EmpresaId, pedido.VinculoId, nombreEmpresa, vencimiento.Documento, vencimiento.VigenteHasta, ahora));
        }

        foreach (var grupo in enviar.GroupBy(x => x.Correo))
            await mensajes.PublishAsync(new CorreoDeVencimientoPendiente(grupo.Key, hoy,
            [
                .. grupo.OrderBy(x => x.Vencimiento.VigenteHasta)
                    .Select(x => new DocumentoPorVencer(x.Vencimiento.Documento, [new VencimientoPorEmpresa(nombreEmpresa, x.Vencimiento.VigenteHasta)]))
            ],
            // Un solo tercero detrás del correo: puede apagar este tipo de correo (NT-1); con varios no se sabe de quién es la preferencia.
            grupo.Select(x => x.Vencimiento.Tercero.Id).Distinct().ToList() is [var terceroId] ? terceroId : null, contexto.TenantId));
        await empresa.Sesion.SaveChangesAsync(ct);
        return new RecordatorioEnviadoDto(enviar.Select(x => x.Correo).Distinct().Count(), enviar.Count, omitidos);
    }

    private static async Task<IReadOnlyList<VencimientoDto>> CalcularAsync(SesionDeEmpresa empresa, TimeProvider reloj, int? dias, string? estado,
        CancellationToken ct)
    {
        EstadoVigencia? filtro = null;
        if (!string.IsNullOrWhiteSpace(estado))
            filtro = Enum.TryParse<EstadoVigencia>(estado, true, out var e) && Enum.IsDefined(e) && e != EstadoVigencia.SinVencimiento
                ? e
                : throw ReglaDeNegocioException.Validacion("estado-invalido", "El estado es Vigente, PorVencer, EnGracia o Vencida.");
        var horizonte = Math.Max(0, dias ?? 30);
        var hoy = CalculadoraDeVigencia.Fecha(reloj.GetUtcNow());

        var vinculos = (await empresa.Vinculos().ToListAsync(ct)).ToDictionary(v => v.Id);
        if (vinculos.Count == 0)
            return [];
        var terceroIds = vinculos.Values.Select(v => v.TerceroId).Distinct().ToArray();
        var terceros = (await empresa.Sesion.LoadManyAsync<Tercero>(ct, terceroIds)).ToDictionary(t => t.Id);
        var tipos = (await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct)).ToDictionary(t => t.Id);
        var recordatorios = (await empresa.Sesion.Query<RecordatorioDeVencimiento>().Where(r => r.EmpresaId == empresa.EmpresaId).ToListAsync(ct))
            .ToDictionary(r => r.Id);

        await using var boveda = empresa.ConsultaBoveda();
        var documentos = await boveda.Query<Documento>().Where(d => d.TerceroId.IsOneOf(terceroIds)).ToListAsync(ct);

        var filas = new List<VencimientoDto>();
        foreach (var documento in documentos)
        {
            if (documento.VersionActual is not { } numero)
                continue;
            var version = documento.VersionRequerida(numero);
            var permite = documento.Ambito == AmbitoDocumento.Global && CatalogoCanonico.Buscar(documento.TipoCanonico)?.PermiteRatificacion == true;
            foreach (var vigencia in version.Vigencias)
            {
                if (vigencia.VigenteHasta is not { } hasta || !vinculos.TryGetValue(vigencia.VinculoId, out var vinculo))
                    continue;
                var estadoVigencia = CalculadoraDeVigencia.Estado(hasta, vigencia.DiasAviso, vigencia.DiasGracia, hoy, vigencia.VencidaEn is not null);
                if (filtro is { } f && estadoVigencia != f)
                    continue;
                if (horizonte > 0 && estadoVigencia == EstadoVigencia.Vigente && hasta.DayNumber - hoy.DayNumber > horizonte)
                    continue;

                var tercero = terceros.GetValueOrDefault(vinculo.TerceroId);
                var tipo = tipos.GetValueOrDefault(vinculo.TipoVinculoId);
                filas.Add(new VencimientoDto(vinculo.Id, documento.Id,
                    new TerceroResumenDto(vinculo.TerceroId, tercero?.Nombre ?? string.Empty, tercero?.Identificacion.Clave ?? string.Empty),
                    new TipoVinculoResumenDto(vinculo.TipoVinculoId, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty),
                    documento.NombreTipo, numero, hasta, hasta.DayNumber - hoy.DayNumber, estadoVigencia.ToString(), vigencia.Base.ToString(),
                    vigencia.FinDeGracia, permite,
                    recordatorios.GetValueOrDefault(RecordatorioDeVencimiento.Clave(vinculo.Id, documento.Id))?.Fecha));
            }
        }

        return [.. filas.OrderBy(f => f.VigenteHasta).ThenBy(f => f.Tercero.Nombre).ThenBy(f => f.Documento)];
    }

    private static string TextoEstado(string estado) => estado switch
    {
        nameof(EstadoVigencia.PorVencer) => "Por vencer",
        nameof(EstadoVigencia.EnGracia) => "En gracia",
        _ => estado
    };

    private static string TextoBase(string baseVigencia) => baseVigencia switch
    {
        nameof(BaseVigencia.FechaExpedicion) => "Fecha de expedición",
        nameof(BaseVigencia.FechaCarga) => "Fecha de carga",
        nameof(BaseVigencia.VencimientoPropio) => "Vencimiento del documento",
        nameof(BaseVigencia.Ratificacion) => "Ratificación del tercero",
        _ => baseVigencia
    };

    private static string Slug(string texto) =>
        new string([.. texto.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ')]).Trim().Replace(' ', '-');
}
