import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarListasRestrictivas,
  calcularMatrizDeRiesgo,
  confirmarAdjuntoDeCumplimiento,
  consultarVerificacion,
  descargarAdjuntoDeCumplimiento,
  descargarArchivo,
  descargarEvidenciaMonitoreo,
  descargarEvidenciaVerificacion,
  getListarBandejaCumplimientoQueryOptions,
  getListarPersonasDeConsultaDeListasQueryOptions,
  getObtenerExpedienteQueryKey,
  getObtenerInformeCumplimientoQueryOptions,
  getObtenerInformeSagrilaftQueryOptions,
  getObtenerListasRestrictivasQueryOptions,
  getObtenerMonitoreoListasQueryOptions,
  getObtenerPersonasDelTerceroSagrilaftQueryOptions,
  iniciarAdjuntoDeCumplimiento,
  rechazarPorCumplimiento,
  registrarConsultaManualDeListas,
  registrarEstudioCumplimiento,
  resolverCoincidencia,
  resolverCoincidenciaMonitoreo,
  type ActualizarListasRestrictivasDto,
  type ArchivoDescargado,
  type DecisionCoincidencia,
  type DescargaEvidenciaDto,
  type ExpedienteDto,
  type AdjuntoDeCumplimientoDto,
  type ObtenerInformeCumplimientoParams,
  type ObtenerInformeSagrilaftParams,
  type PropositoDeAdjunto,
  type RechazarPorCumplimientoDto,
  type RegistrarConsultaManualDto,
  type RegistrarEstudioCumplimientoDto,
  type RespuestaDeMatrizDto,
} from '@validasoft/api-client';
import { abrirDescarga } from '@validasoft/ui';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';
import { invalidarExpedientes } from '../expedientes/api';

/** Bandeja del oficial (`GET /cumplimiento/bandeja`): se vuelve a pedir al abrirla. */
export function useBandejaCumplimiento(empresaId: string, habilitado = true) {
  const opciones = getListarBandejaCumplimientoQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', enabled: habilitado });
}

export function useMonitoreoListas(empresaId: string, vinculoId: string, habilitado = true) {
  const opciones = getObtenerMonitoreoListasQueryOptions(vinculoId, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado && Boolean(vinculoId), retry: false });
}

/** Una acción de Cumplimiento sobre el expediente (cada una con lo que pide su endpoint). */
export type AccionVerificacion =
  | { tipo: 'consultar' }
  | { tipo: 'resolver'; coincidenciaId: string; decision: DecisionCoincidencia; justificacion: string }
  | { tipo: 'estudio'; estudio: RegistrarEstudioCumplimientoDto }
  | { tipo: 'consultaManual'; consulta: RegistrarConsultaManualDto }
  | { tipo: 'rechazar'; rechazo: RechazarPorCumplimientoDto };

export function ejecutarVerificacion(empresaId: string, expedienteId: string, accion: AccionVerificacion): Promise<ExpedienteDto> {
  const opciones = { empresa: empresaId };
  switch (accion.tipo) {
    case 'consultar':
      return consultarVerificacion(expedienteId, 'ListasRestrictivas', opciones);
    case 'resolver':
      return resolverCoincidencia(
        expedienteId,
        'ListasRestrictivas',
        accion.coincidenciaId,
        { decision: accion.decision, justificacion: accion.justificacion.trim() },
        opciones,
      );
    case 'estudio':
      return registrarEstudioCumplimiento(expedienteId, accion.estudio, opciones);
    case 'consultaManual':
      return registrarConsultaManualDeListas(expedienteId, accion.consulta, opciones);
    case 'rechazar':
      return rechazarPorCumplimiento(expedienteId, accion.rechazo, opciones);
  }
}

/** Como las decisiones de área: la respuesta reemplaza el expediente en caché y el resto (bandejas, vínculo) se refresca. */
export function useAccionVerificacion(empresaId: string, expedienteId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (accion: AccionVerificacion) => ejecutarVerificacion(empresaId, expedienteId, accion),
    onSuccess: (expediente) => cliente.setQueryData(conEmpresa(getObtenerExpedienteQueryKey(expedienteId), empresaId), expediente),
    onSettled: () => {
      void invalidarExpedientes(cliente);
      void invalidarRuta(cliente, '/api/v1/cumplimiento');
    },
  });
}

/** La evidencia y los archivos del oficial llaman `nombreArchivo` a lo que las demás descargas llaman `nombre`. */
function abrirEvidencia(descarga: DescargaEvidenciaDto): DescargaEvidenciaDto {
  abrirDescarga({ url: descarga.url, nombre: descarga.nombreArchivo });
  return descarga;
}

/** Descarga auditada de la evidencia (URL prefirmada de 5 minutos); `referencia` = una consulta anterior. */
export function useDescargarEvidencia(empresaId: string) {
  return useMutation({
    mutationFn: async ({ expedienteId, referencia }: { expedienteId: string; referencia?: string }) =>
      abrirEvidencia(
        await descargarEvidenciaVerificacion(expedienteId, 'ListasRestrictivas', referencia ? { referencia } : undefined, {
          empresa: empresaId,
        }),
      ),
  });
}

export function useDescargarEvidenciaMonitoreo(empresaId: string) {
  return useMutation({
    mutationFn: async (vinculoId: string) => abrirEvidencia(await descargarEvidenciaMonitoreo(vinculoId, { empresa: empresaId })),
  });
}

export function useResolverCoincidenciaMonitoreo(empresaId: string, vinculoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ coincidenciaId, decision, justificacion }: { coincidenciaId: string; decision: DecisionCoincidencia; justificacion: string }) =>
      resolverCoincidenciaMonitoreo(vinculoId, coincidenciaId, { decision, justificacion: justificacion.trim() }, { empresa: empresaId }),
    onSettled: () => Promise.all([invalidarRuta(cliente, '/api/v1/vinculos'), invalidarRuta(cliente, '/api/v1/cumplimiento')]),
  });
}

export function useListasRestrictivas(empresaId: string, habilitado = true) {
  const opciones = getObtenerListasRestrictivasQueryOptions(undefined, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

export function useActualizarListasRestrictivas(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: ActualizarListasRestrictivasDto) => actualizarListasRestrictivas(dto, { empresa: empresaId }),
    onSuccess: (configuracion) =>
      cliente.setQueryData(conEmpresa(getObtenerListasRestrictivasQueryOptions(undefined).queryKey, empresaId), configuracion),
  });
}

export function useInformeCumplimiento(empresaId: string, params: ObtenerInformeCumplimientoParams, habilitado = true) {
  const opciones = getObtenerInformeCumplimientoQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), placeholderData: (previo) => previo, enabled: habilitado });
}

/** Informe SAGRILAFT en Excel (`GET /informes/cumplimiento/excel`) por la descarga binaria del cliente. */
export function useDescargarInformeCumplimiento(empresaId: string) {
  return useMutation({
    mutationFn: (params: ObtenerInformeCumplimientoParams): Promise<ArchivoDescargado> =>
      descargarArchivo({ url: '/api/v1/informes/cumplimiento/excel', params }, { empresa: empresaId }),
  });
}

// ── Matriz de riesgo en el estudio (CU-17) ─────────────────────────────────────────────────────────────────────────

/**
 * Cálculo en vivo de la matriz para el expediente con las respuestas del oficial (`POST …/cumplimiento/matriz/calcular`, no
 * registra nada). La misma elección es la misma consulta; al cambiar una respuesta se conserva el resultado anterior mientras llega.
 */
export function useCalculoDeMatriz(empresaId: string, expedienteId: string, respuestas: RespuestaDeMatrizDto[], habilitado: boolean) {
  return useQuery({
    queryKey: [`/api/v1/expedientes/${expedienteId}/cumplimiento/matriz/calcular`, respuestas, { empresa: empresaId }],
    queryFn: ({ signal }) => calcularMatrizDeRiesgo(expedienteId, { respuestas }, { empresa: empresaId }, signal),
    enabled: habilitado,
    retry: false,
    placeholderData: (previo) => previo,
  });
}

// ── Archivos del oficial ───────────────────────────────────────────────────────────────────────────────────────────

/** Paso 1 (`PUT …/adjuntos`), carga directa al almacenamiento con la URL prefirmada y paso 2 (`…/confirmar`). */
export async function subirAdjuntoDeCumplimiento(
  empresaId: string,
  expedienteId: string,
  archivo: File,
  proposito: PropositoDeAdjunto,
  fetchFn: typeof fetch = (...a) => globalThis.fetch(...a),
): Promise<AdjuntoDeCumplimientoDto> {
  const opciones = { empresa: empresaId };
  const inicio = await iniciarAdjuntoDeCumplimiento(expedienteId, { nombreArchivo: archivo.name, tamanoBytes: archivo.size, tipoMime: archivo.type, proposito }, opciones);
  const respuesta = await fetchFn(inicio.carga.url, { method: inicio.carga.metodo, headers: inicio.carga.cabeceras, body: archivo });
  if (!respuesta.ok) throw new Error(`carga-fallida:${String(respuesta.status)}`);
  return confirmarAdjuntoDeCumplimiento(expedienteId, inicio.cargaId, { nombreArchivo: archivo.name, proposito }, opciones);
}

/** Sube un archivo del oficial; el expediente (con su lista de archivos) se vuelve a pedir al terminar. */
export function useSubirAdjunto(empresaId: string, expedienteId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ archivo, proposito }: { archivo: File; proposito: PropositoDeAdjunto }) => subirAdjuntoDeCumplimiento(empresaId, expedienteId, archivo, proposito),
    onSettled: () => invalidarExpedientes(cliente),
  });
}

/** Descarga auditada (URL prefirmada de 5 minutos) de un archivo del oficial. */
export function useDescargarAdjunto(empresaId: string, expedienteId: string) {
  return useMutation({
    mutationFn: async (adjuntoId: string) => abrirEvidencia(await descargarAdjuntoDeCumplimiento(expedienteId, adjuntoId, { empresa: empresaId })),
  });
}

/** Personas que cubre la consulta de listas del expediente, con las claves que usa la consulta manual (mismo orden). */
export function usePersonasDeConsulta(empresaId: string, expedienteId: string, habilitado = true) {
  const opciones = getListarPersonasDeConsultaDeListasQueryOptions(expedienteId, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado, refetchOnMount: 'always' });
}

// ── Informe SAGRILAFT completo ─────────────────────────────────────────────────────────────────────────────────────

export function useInformeSagrilaft(empresaId: string, params: ObtenerInformeSagrilaftParams, habilitado = true) {
  const opciones = getObtenerInformeSagrilaftQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), placeholderData: (previo) => previo, enabled: habilitado });
}

/** Excel de tres hojas (`GET /informes/sagrilaft/excel`) por la descarga binaria del cliente. */
export function useDescargarInformeSagrilaft(empresaId: string) {
  return useMutation({
    mutationFn: (params: ObtenerInformeSagrilaftParams): Promise<ArchivoDescargado> =>
      descargarArchivo({ url: '/api/v1/informes/sagrilaft/excel', params }, { empresa: empresaId }),
  });
}

/** Personas relacionadas de un tercero con su PEP y beneficiario final (≥ 5 %), para el detalle del informe. */
export function usePersonasDelTerceroSagrilaft(empresaId: string, terceroId: string | null) {
  const opciones = getObtenerPersonasDelTerceroSagrilaftQueryOptions(terceroId ?? '', { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: terceroId !== null, retry: false });
}
