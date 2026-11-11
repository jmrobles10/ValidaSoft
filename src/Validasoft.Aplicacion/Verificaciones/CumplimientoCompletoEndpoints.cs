using System.Security.Cryptography;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Configuracion;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Cumplimiento;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Verificaciones;

/// <summary>
/// Matriz de riesgo de la empresa (CU-17): factores con pesos y opciones puntuadas, límites Bajo / Medio / Alto y la debida
/// diligencia de cada nivel. La lee quien tiene <c>configuracion:leer</c> (el oficial incluido) y la edita quien tiene
/// <c>configuracion:escribir</c>; cada cambio queda en la auditoría. Es privada de la empresa.
/// </summary>
public static class MatrizEndpoints
{
    public const string Entidad = "MatrizDeRiesgoEmpresa";

    /// <summary>La matriz guardada o, si la empresa aún no tiene una, la plantilla por omisión (<c>esPorDefecto</c>, inactiva).</summary>
    [WolverineGet("/api/v1/configuracion/matriz-riesgo")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<MatrizDeRiesgoDto> Obtener(SesionDeEmpresa empresa, CancellationToken ct)
    {
        var matriz = await empresa.Sesion.LoadAsync<MatrizDeRiesgoEmpresa>(empresa.EmpresaId, ct);
        return matriz is null ? MatrizDeRiesgoEmpresa.PorDefecto(empresa.EmpresaId).ADto(esPorDefecto: true) : matriz.ADto(esPorDefecto: false);
    }

    /// <summary>
    /// Guarda la matriz completa. 422: <c>factores-invalidos</c>, <c>peso-invalido</c>, <c>opciones-invalidas</c>,
    /// <c>factor-automatico-invalido</c>, <c>limites-invalidos</c>, <c>niveles-invalidos</c> u <c>origen-invalido</c>.
    /// </summary>
    [WolverinePut("/api/v1/configuracion/matriz-riesgo")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<MatrizDeRiesgoDto> Actualizar(ActualizarMatrizDeRiesgoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var existente = await empresa.Sesion.LoadAsync<MatrizDeRiesgoEmpresa>(empresa.EmpresaId, ct);
        var antes = existente?.ADto(esPorDefecto: false);
        var matriz = existente ?? new MatrizDeRiesgoEmpresa { Id = empresa.EmpresaId };

        var factores = dto.Factores.Select(f => new FactorDeRiesgo(f.Codigo, f.Nombre,
                Enum.TryParse<OrigenDelFactor>(f.Origen, true, out var origen) && Enum.IsDefined(origen)
                    ? origen
                    : throw ReglaDeNegocioException.Validacion("origen-invalido", "El origen de un factor es Manual o Automatico."),
                f.Peso, [.. f.Opciones.Select(o => new OpcionDeFactor(o.Codigo, o.Nombre, o.Puntaje, o.NivelMinimo))], f.Ayuda))
            .ToList();
        var niveles = dto.Niveles.Select(n => new NivelDeRiesgo(n.Nivel, n.Nombre,
                Enum.TryParse<DebidaDiligencia>(n.DebidaDiligencia, true, out var debida) && Enum.IsDefined(debida)
                    ? debida
                    : throw ReglaDeNegocioException.Validacion("niveles-invalidos", "La debida diligencia es Simple, Ordinaria o Ampliada."),
                n.MesesRevision))
            .ToList();

        matriz.Actualizar(dto.Activa, factores, dto.LimiteBajo, dto.LimiteMedio, niveles, ahora);
        empresa.Sesion.Store(matriz);
        var despues = matriz.ADto(esPorDefecto: false);
        Auditoria.Registrar(empresa.Sesion, contexto, Entidad, empresa.EmpresaId.ToString(),
            existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes, despues, ahora, empresa.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return despues;
    }

    /// <summary>
    /// Vista previa del cálculo para el expediente con las respuestas a los factores manuales, sin registrar nada: los
    /// automáticos los evalúa el sistema con lo que ya sabe. 422 <c>matriz-no-activa</c>, <c>respuestas-incompletas</c>,
    /// <c>opcion-invalida</c>.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/cumplimiento/matriz/calcular")]
    [Authorize(Policy = Permisos.CumplimientoDecidir)]
    public static async Task<ResultadoDeMatrizDto> Calcular(Guid id, CalcularMatrizDto dto, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var matriz = await empresa.Sesion.LoadAsync<MatrizDeRiesgoEmpresa>(empresa.EmpresaId, ct);
        if (matriz is not { Activa: true })
            throw ReglaDeNegocioException.Validacion("matriz-no-activa", "La empresa no tiene una matriz de riesgo activa (Configuración › Matriz de riesgo).");
        var expediente = await empresa.CargarExpedienteAsync(id, ct);
        return (await CalcularAsync(empresa, matriz, expediente, dto.Respuestas, ct)).ADto();
    }

    /// <summary>Aplica la matriz a un expediente: los factores automáticos con lo que ya hay (listas, ficha legal, identificación) y los manuales con las respuestas.</summary>
    internal static async Task<ResultadoDeMatriz> CalcularAsync(SesionDeEmpresa empresa, MatrizDeRiesgoEmpresa matriz, Expediente expediente,
        IReadOnlyList<RespuestaDeMatrizDto>? respuestas, CancellationToken ct)
    {
        var personas = await ConsultaDeListas.PersonasAsync(empresa.Sesion, expediente.TerceroId, ct);
        var automaticos = FactoresAutomaticos.Evaluar(expediente.TerceroIdentificacion, expediente.BuscarVerificacion(TipoVerificacion.ListasRestrictivas),
            personas.Select(p => p.AConsultada()));
        return matriz.Calcular([.. (respuestas ?? []).Select(r => new RespuestaDeFactor(r.Factor, r.Opcion))], automaticos);
    }
}

/// <summary>
/// Archivos del oficial en el expediente y consulta de listas hecha por fuera (CU-16). El archivo se sube en dos pasos como
/// toda carga (URL prefirmada y confirmación, que lee el objeto, comprueba el tipo real por sus bytes y lo guarda content-addressed
/// en el bucket del tenant): no entra a la bóveda del tercero y no se comparte con nadie. La evidencia manual de listas entra
/// igual que una consulta automática (sin coincidencias queda satisfecha; con ellas, por analizar).
/// </summary>
public static class AdjuntosDeCumplimientoEndpoints
{
    /// <summary>Formatos que admite un archivo de cumplimiento: PDF o imagen (lo que da una consulta o un soporte escaneado).</summary>
    public static readonly IReadOnlyList<string> Formatos = InspectorDeArchivos.Permitidos;

    /// <summary>Paso 1: URL PUT del archivo. 422 <c>adjunto-invalido</c> (tamaño o formato); 409 <c>estado-invalido</c> con el expediente cerrado.</summary>
    [WolverinePut("/api/v1/expedientes/{id}/cumplimiento/adjuntos")]
    [Authorize(Policy = Permisos.VerificacionesEscribir)]
    public static async Task<CargaDeAdjuntoDto> Iniciar(Guid id, IniciarAdjuntoDeCumplimientoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, CancellationToken ct)
    {
        var expediente = await empresa.CargarExpedienteAsync(id, ct);
        if (!expediente.EstaAbierto)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"El expediente está {expediente.Estado}: ya no recibe verificaciones.");
        LeerProposito(dto.Proposito);
        if (dto.TamanoBytes is <= 0 or > Expediente.TamanoMaximoDeAdjunto || !Formatos.Contains(dto.TipoMime ?? string.Empty) || string.IsNullOrWhiteSpace(dto.NombreArchivo))
            throw ReglaDeNegocioException.Validacion("adjunto-invalido",
                "El archivo debe ser un PDF o una imagen (JPEG, PNG o WebP) de hasta 15 MB.");

        var bucket = ClavesDeAlmacen.BucketDeTenant(contexto.TenantRequerido());
        var cargaId = Guid.CreateVersion7();
        await almacen.AsegurarBucketAsync(bucket, ct);
        var url = await almacen.UrlDeSubidaAsync(bucket, ClavesDeAlmacen.Carga(cargaId), dto.TipoMime!, reglas.VigenciaSubida, ct);
        return new CargaDeAdjuntoDto(cargaId, new InstruccionCargaDto(url.Metodo, url.Url, url.Cabeceras, url.Expira));
    }

    /// <summary>
    /// Paso 2: lee el objeto, verifica tamaño y el tipo real por sus bytes (422 <c>adjunto-invalido</c>) y lo registra en el
    /// expediente. 404 <c>carga-no-encontrada</c> si no hay carga; 409 <c>archivo-no-cargado</c> si aún no llegó el archivo.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/cumplimiento/adjuntos/{cargaId}/confirmar")]
    [Authorize(Policy = Permisos.VerificacionesEscribir)]
    public static async Task<AdjuntoDeCumplimientoDto> Confirmar(Guid id, Guid cargaId, ConfirmarAdjuntoDeCumplimientoDto dto, SesionDeEmpresa empresa,
        IContextoSolicitud contexto, IMessageBus mensajes, IProtectorDeSecretos protector, IAlmacenDeArchivos almacen, TimeProvider reloj, CancellationToken ct)
    {
        var proposito = LeerProposito(dto.Proposito);
        var bucket = ClavesDeAlmacen.BucketDeTenant(contexto.TenantRequerido());
        var temporal = ClavesDeAlmacen.Carga(cargaId);
        var objeto = await almacen.ConsultarAsync(bucket, temporal, ct)
                     ?? throw ReglaDeNegocioException.Conflicto("archivo-no-cargado", "Aún no recibimos el archivo: súbelo con la URL de carga y vuelve a confirmar.");

        var limites = new ReglasDeArchivo("El archivo", Formatos, Expediente.TamanoMaximoDeAdjunto, int.MaxValue, false);
        var contenido = await CargaEndpoints.ConLimpiezaAsync(almacen, bucket, temporal, async () =>
        {
            limites.ValidarTamano(objeto.Tamano);
            return await CargaEndpoints.LeerAsync(almacen, bucket, temporal, limites, ct);
        }, ct);
        var mime = InspectorDeArchivos.DetectarMime(contenido);
        if (mime is null || !Formatos.Contains(mime))
        {
            await almacen.EliminarAsync(bucket, temporal, ct);
            throw ReglaDeNegocioException.Validacion("adjunto-invalido", "El archivo no es un PDF ni una imagen válida (el tipo declarado no basta).");
        }

        var sha = Convert.ToHexStringLower(SHA256.HashData(contenido));
        var clave = ClavesDeAlmacen.AdjuntoDeCumplimiento(empresa.EmpresaId, sha);
        await almacen.CopiarAsync(bucket, temporal, bucket, clave, mime, new Dictionary<string, string>
        {
            ["nombre-original"] = Uri.EscapeDataString(dto.NombreArchivo),
            ["expediente"] = id.ToString(),
            ["mime"] = mime
        }, ct);
        await almacen.EliminarAsync(bucket, temporal, ct);

        var adjunto = new AdjuntoDeCumplimiento(Guid.CreateVersion7(), LimpiarNombre(dto.NombreArchivo), mime, contenido.LongLength, sha, bucket, clave, proposito,
            contexto.ActorId, reloj.GetUtcNow());
        empresa.UsarMensajeria(mensajes);
        await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var stream = await empresa.ExpedienteParaEscribirAsync(id, null, ct);
            var eventos = stream.Aggregate!.RegistrarAdjuntoDeCumplimiento(adjunto);
            await ExpedientesEndpoints.AnexarYGuardarAsync(empresa, stream, eventos, mensajes, protector, reloj, ct);
            return true;
        });
        return adjunto.ADto();
    }

    /// <summary>URL prefirmada (5 min) de un archivo del oficial; la descarga queda en el historial del expediente. 404 <c>adjunto-no-encontrado</c>.</summary>
    [WolverinePost("/api/v1/expedientes/{id}/cumplimiento/adjuntos/{adjuntoId}/descarga")]
    [Authorize(Policy = Permisos.DocumentosDescargar)]
    public static async Task<DescargaEvidenciaDto> Descargar(Guid id, Guid adjuntoId, SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var stream = await empresa.ExpedienteParaEscribirAsync(id, null, ct);
        var adjunto = stream.Aggregate!.BuscarAdjuntoDeCumplimiento(adjuntoId)
                      ?? throw ReglaDeNegocioException.NoEncontrado("adjunto-no-encontrado", "Ese archivo no está en el expediente.");
        stream.AppendOne(new EvidenciaDeVerificacionDescargada(
            adjunto.Proposito == PropositoDeAdjunto.Estudio ? TipoVerificacion.EstudioDeCumplimiento : TipoVerificacion.ListasRestrictivas,
            adjunto.Id.ToString("N"), contexto.ActorTipo.ToString(), contexto.ActorId, BovedaEndpoints.Ip(http), BovedaEndpoints.UserAgent(http), reloj.GetUtcNow()));
        await empresa.Sesion.SaveChangesAsync(ct);
        var url = await almacen.UrlDeDescargaAsync(adjunto.Bucket, adjunto.Clave, adjunto.Nombre, adjunto.TipoContenido, reglas.VigenciaDescarga, ct);
        return new DescargaEvidenciaDto(url.Url, url.Expira, adjunto.Nombre);
    }

    /// <summary>Las personas que cubre hoy una consulta de listas (el tercero, sus representantes, socios y PEP), para reportar sobre ellas lo que dio la consulta hecha por fuera.</summary>
    [WolverineGet("/api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/personas")]
    [Authorize(Policy = Permisos.VerificacionesEscribir)]
    public static async Task<PersonasParaConsultaDto> Personas(Guid id, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var expediente = await empresa.CargarExpedienteAsync(id, ct);
        var personas = await ConsultaDeListas.PersonasAsync(empresa.Sesion, expediente.TerceroId, ct);
        return new PersonasParaConsultaDto([.. personas.Select(p => new PersonaConsultadaDto(p.Rol, p.Nombre, $"{p.TipoIdentificacion} {p.Numero}"))],
            [.. personas.Select(p => p.Clave)]);
    }

    /// <summary>
    /// Registra una consulta de listas hecha por fuera con su PDF ya subido (<c>propósito ConsultaManual</c>). Entra como una consulta
    /// más: sin coincidencias queda satisfecha; con ellas, por analizar. No cuenta contra la cuota del proveedor. 422
    /// <c>adjunto-invalido</c>, <c>fuente-invalida</c>, <c>consulta-fuera-de-plazo</c>, <c>persona-invalida</c>, <c>vigencia-invalida</c>; 403 <c>conflicto-de-intereses</c>.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/consulta-manual")]
    [Authorize(Policy = Permisos.VerificacionesEscribir)]
    public static async Task<ExpedienteDto> ConsultaManual(Guid id, RegistrarConsultaManualDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        HttpContext http, IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        var fuente = (dto.Fuente ?? string.Empty).Trim();
        if (fuente.Length is < 3 or > 100)
            throw ReglaDeNegocioException.Validacion("fuente-invalida", "Indica dónde hiciste la consulta (de 3 a 100 caracteres).");
        if (dto.MesesVigencia is < 1 or > 60)
            throw ReglaDeNegocioException.Validacion("vigencia-invalida", "La vigencia va de 1 a 60 meses.");
        empresa.UsarMensajeria(mensajes);
        return await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
            var expediente = stream.Aggregate!;
            var oficial = await VerificacionesEndpoints.OficialAsync(empresa, contexto, ct);
            var personas = await ConsultaDeListas.PersonasAsync(empresa.Sesion, expediente.TerceroId, ct);
            var adjunto = expediente.BuscarAdjuntoDeCumplimiento(dto.AdjuntoId)
                          ?? throw ReglaDeNegocioException.Validacion("adjunto-invalido", "Sube primero el PDF de la consulta.");
            var coincidencias = new List<CoincidenciaHallada>();
            foreach (var c in dto.Coincidencias ?? [])
            {
                if (personas.FirstOrDefault(p => string.Equals(p.Clave, c.ClavePersona, StringComparison.OrdinalIgnoreCase)) is not { } persona ||
                    string.IsNullOrWhiteSpace(c.Lista) || string.IsNullOrWhiteSpace(c.NombreEncontrado))
                    throw ReglaDeNegocioException.Validacion("persona-invalida", "Cada coincidencia lleva una persona de la consulta, la lista y el nombre encontrado.");
                coincidencias.Add(new CoincidenciaHallada(persona.Clave, c.Lista.Trim(), c.NombreEncontrado.Trim(), c.Detalle?.Trim(), c.Puntaje));
            }

            var meses = dto.MesesVigencia ?? expediente.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)?.FrecuenciaReconsultaMeses
                        ?? Expediente.MesesReconsultaPorOmision;
            var evidencia = new EvidenciaDeVerificacion(adjunto.Bucket, adjunto.Clave, adjunto.Sha256, adjunto.Tamano, adjunto.TipoContenido, adjunto.Nombre, true);
            var resultado = new ResultadoDeListas($"Manual · {fuente}", $"manual-{adjunto.Id:N}", [.. personas.Select(p => p.AConsultada())], coincidencias, evidencia,
                dto.ConsultadaEn.AddMonths(meses));
            var eventos = expediente.RegistrarConsultaManualDeListas(resultado, adjunto.Id, dto.ConsultadaEn, oficial, reloj.GetUtcNow());
            return await VerificacionesEndpoints.GuardarAsync(empresa, stream, eventos, mensajes, protector, http, reloj, ct);
        });
    }

    private static PropositoDeAdjunto LeerProposito(string? proposito) =>
        Enum.TryParse<PropositoDeAdjunto>(proposito, true, out var valor) && Enum.IsDefined(valor)
            ? valor
            : throw ReglaDeNegocioException.Validacion("proposito-invalido", "El propósito es Estudio o ConsultaManual.");

    private static string LimpiarNombre(string nombre)
    {
        var limpio = Path.GetFileName(nombre.Replace('\\', '/')).Trim();
        return limpio.Length == 0 ? "archivo" : limpio.Length > 150 ? limpio[..150] : limpio;
    }
}

/// <summary>
/// Rechazo directo del oficial de cumplimiento (CU-18). En el SRM el oficial aprueba o rechaza su estudio y, con la configuración
/// <c>RechazoOficial</c>, el rechazo cierra la solicitud; aquí también puede cerrarla, pero solo ante un hallazgo ya registrado
/// (coincidencia confirmada o estudio desfavorable), con motivos del catálogo y justificación, y nunca de forma automática (f-4).
/// </summary>
public static class RechazoDeCumplimientoEndpoints
{
    /// <summary>
    /// Rechaza el expediente. 409 <c>rechazo-sin-hallazgo</c> sin una verificación fallida; 409 <c>estado-invalido</c> fuera de revisión;
    /// 422 <c>motivo-requerido</c>, <c>justificacion-requerida</c>; 403 <c>conflicto-de-intereses</c>.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/cumplimiento/rechazar")]
    [Authorize(Policy = Permisos.CumplimientoDecidir)]
    public static async Task<ExpedienteDto> Rechazar(Guid id, RechazarPorCumplimientoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http,
        IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        return await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
            await ExpedientesEndpoints.AsegurarVinculoNoBloqueadoAsync(empresa, stream.Aggregate!, ct);
            var oficial = await VerificacionesEndpoints.OficialAsync(empresa, contexto, ct);
            var motivos = await CatalogoDeMotivos.ResolverAsync(empresa.Sesion, dto.Motivos, AplicaMotivo.Rechazo, ct);
            var eventos = stream.Aggregate!.RechazarPorCumplimiento(oficial, motivos, dto.Justificacion, reloj.GetUtcNow());
            return await VerificacionesEndpoints.GuardarAsync(empresa, stream, eventos, mensajes, protector, http, reloj, ct);
        });
    }
}
