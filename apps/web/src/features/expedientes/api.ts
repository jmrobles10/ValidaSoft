import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  abrirExpediente,
  anularExpediente,
  anularVeredicto,
  aprobarComoAdministrador,
  emitirVeredicto,
  emitirVeredictoRequisito,
  eximirRequisito,
  getListarAperturasPendientesQueryOptions,
  getListarBandejaQueryOptions,
  getListarMotivosQueryOptions,
  getObtenerExpedienteQueryKey,
  getObtenerExpedienteQueryOptions,
  getObtenerHistorialExpedienteQueryOptions,
  rechazarExpediente,
  reenviarExpediente,
  type ExpedienteDto,
  type ListarBandejaParams,
  type Veredicto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';
import type { DecisionDeArea } from './decisionArea';

/**
 * Bandeja, expediente e historial se vuelven a pedir cada vez que se abren (aunque estén en caché): otras áreas y el
 * tercero los cambian mientras tanto, y con la caché de 30 s quien volvía al expediente veía otra versión de los
 * documentos u otras áreas «Pendiente» que ya habían decidido. Lo cacheado se muestra mientras llega lo nuevo.
 */
const SIEMPRE_AL_ABRIR = { refetchOnMount: 'always' } as const;

export function useBandeja(empresaId: string, params: ListarBandejaParams) {
  const opciones = getListarBandejaQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, ...SIEMPRE_AL_ABRIR, queryKey: conEmpresa(opciones.queryKey, empresaId), placeholderData: (previo) => previo });
}

export function useAperturasPendientes(empresaId: string, habilitado: boolean) {
  const opciones = getListarAperturasPendientesQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

export function useExpediente(empresaId: string, id: string) {
  const opciones = getObtenerExpedienteQueryOptions(id, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, ...SIEMPRE_AL_ABRIR, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: Boolean(id) });
}

export function useHistorialExpediente(empresaId: string, id: string) {
  const opciones = getObtenerHistorialExpedienteQueryOptions(id, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, ...SIEMPRE_AL_ABRIR, queryKey: conEmpresa(opciones.queryKey, empresaId) });
}

/** Catálogo de motivos (base + propios del tenant); cambia poco. */
export function useMotivos(empresaId: string, habilitado = true) {
  const opciones = getListarMotivosQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), staleTime: 5 * 60_000, enabled: habilitado });
}

/** Una decisión sobre el expediente; cada variante lleva lo que exige su endpoint. */
export type EntradaExpediente =
  | { tipo: 'veredicto'; areaId: string; veredicto: Veredicto; motivos: string[]; observacion: string }
  | { tipo: 'veredictoRequisito'; requisitoId: string; areaId: string; veredicto: Veredicto; motivos: string[]; observacion: string; sobreVersion: number }
  | { tipo: 'eximir'; requisitoId: string; justificacion: string }
  | { tipo: 'veredictoArea'; areaId: string; decision: DecisionDeArea }
  | { tipo: 'rechazar'; areaId: string; motivos: string[]; justificacion: string }
  | { tipo: 'aprobarComoAdministrador'; areaId: string; justificacion: string }
  | { tipo: 'anularVeredicto'; areaId: string; justificacion: string; veredictoId?: string }
  | { tipo: 'reenviar'; respuesta: string }
  | { tipo: 'anular'; motivo: string };

/**
 * Sin `If-Match`: las áreas deciden en paralelo y el servidor serializa las escrituras (FL-12); si el estado ya no
 * admite la decisión responde 409 con el código del caso (`expediente-observado`, `area-ya-se-pronuncio`…).
 */
export function ejecutarEnExpediente(empresaId: string, expedienteId: string, entrada: EntradaExpediente): Promise<ExpedienteDto> {
  const opciones = { empresa: empresaId };
  const texto = (valor: string) => valor.trim() || null;
  switch (entrada.tipo) {
    case 'veredicto':
      return emitirVeredicto(
        expedienteId,
        entrada.areaId,
        { veredicto: entrada.veredicto, motivos: entrada.veredicto === 'Observado' ? entrada.motivos : [], observacion: texto(entrada.observacion) },
        opciones,
      );
    case 'veredictoRequisito':
      // `sobreVersion` es obligatorio: si llegó otra versión mientras se revisaba, la API responde 409 `version-superada`.
      return emitirVeredictoRequisito(
        expedienteId,
        entrada.requisitoId,
        entrada.areaId,
        {
          veredicto: entrada.veredicto,
          motivos: entrada.veredicto === 'Observado' ? entrada.motivos : [],
          observacion: texto(entrada.observacion),
          sobreVersion: entrada.sobreVersion,
        },
        opciones,
      );
    case 'veredictoArea':
      // Decisión del área completa: «Aprobar área» con `versiones` u «Observar» con `requisitos[]` (con requisitos,
      // la API ignora `motivos` y `observacion` de primer nivel).
      return emitirVeredicto(expedienteId, entrada.areaId, entrada.decision, opciones);
    case 'eximir':
      return eximirRequisito(expedienteId, entrada.requisitoId, { justificacion: entrada.justificacion.trim() }, opciones);
    case 'rechazar':
      return rechazarExpediente(expedienteId, { areaId: entrada.areaId, motivos: entrada.motivos, justificacion: entrada.justificacion.trim() }, opciones);
    case 'aprobarComoAdministrador':
      return aprobarComoAdministrador(expedienteId, entrada.areaId, { justificacion: entrada.justificacion.trim() }, opciones);
    case 'anularVeredicto':
      return anularVeredicto(expedienteId, entrada.areaId, { justificacion: entrada.justificacion.trim(), veredictoId: entrada.veredictoId ?? null }, opciones);
    case 'reenviar':
      return reenviarExpediente(expedienteId, { respuesta: texto(entrada.respuesta) }, opciones);
    case 'anular':
      return anularExpediente(expedienteId, { motivo: entrada.motivo.trim() }, opciones);
  }
}

/** Refresca expediente, bandeja y vínculos: un veredicto puede aprobar el expediente y activar el vínculo (FL-5). */
export function invalidarExpedientes(cliente: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    invalidarRuta(cliente, '/api/v1/expedientes'),
    invalidarRuta(cliente, '/api/v1/bandeja'),
    invalidarRuta(cliente, '/api/v1/vinculos'),
  ]);
}

/**
 * La respuesta (el expediente ya decidido) reemplaza al de la caché y el resto se refresca sin esperar: si la
 * mutación esperara la recarga, quien la llamó vería el expediente nuevo antes de saber que su decisión se guardó.
 */
export function useAccionExpediente(empresaId: string, expedienteId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (entrada: EntradaExpediente) => ejecutarEnExpediente(empresaId, expedienteId, entrada),
    onSuccess: (expediente) => {
      cliente.setQueryData(conEmpresa(getObtenerExpedienteQueryKey(expedienteId), empresaId), expediente);
    },
    // También ante 409/403: lo que se mostraba puede no ser lo vigente.
    onSettled: () => {
      void invalidarExpedientes(cliente);
    },
  });
}

export function useAbrirExpediente(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ vinculoId, flujoId }: { vinculoId: string; flujoId: string | null }) => abrirExpediente(vinculoId, { flujoId }, { empresa: empresaId }),
    onSettled: () => {
      void invalidarExpedientes(cliente);
    },
  });
}
