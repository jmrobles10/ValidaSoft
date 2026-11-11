import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarRecordatorioAutomatico,
  getObtenerRecordatorioAutomaticoQueryOptions,
  recordarObservaciones,
  recordarObservacionesEnLote,
  type ActualizarRecordatorioAutomaticoDto,
  type RecordarObservacionesDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';

/** Tras recordar cambian el expediente (último recordatorio) y la bandeja. */
function useRefrescarExpedientes() {
  const cliente = useQueryClient();
  return async () => {
    await invalidarRuta(cliente, '/api/v1/expedientes');
    await invalidarRuta(cliente, '/api/v1/bandeja');
  };
}

/** Recordar al tercero las observaciones de un expediente (`POST /expedientes/{id}/recordar-observaciones`). */
export function useRecordarObservaciones(empresaId: string, expedienteId: string) {
  const refrescar = useRefrescarExpedientes();
  return useMutation({
    mutationFn: () => recordarObservaciones(expedienteId, { empresa: empresaId }),
    onSettled: refrescar,
  });
}

/** Recordatorio en lote: los elegidos o «todos los observados» (con el área opcional). */
export function useRecordarEnLote(empresaId: string) {
  const refrescar = useRefrescarExpedientes();
  return useMutation({
    mutationFn: (dto: RecordarObservacionesDto) => recordarObservacionesEnLote(dto, { empresa: empresaId }),
    onSettled: refrescar,
  });
}

/** Recordatorio automático de la empresa activa (NT-3). */
export function useRecordatorioAutomatico(empresaId: string, habilitado = true) {
  const opciones = getObtenerRecordatorioAutomaticoQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

export function useGuardarRecordatorioAutomatico(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: ActualizarRecordatorioAutomaticoDto) => actualizarRecordatorioAutomatico(dto, { empresa: empresaId }),
    onSuccess: () => invalidarRuta(cliente, '/api/v1/configuracion/recordatorio-observaciones'),
  });
}
