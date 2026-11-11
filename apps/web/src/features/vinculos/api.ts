import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  anularVinculo,
  bloquearVinculo,
  desbloquearVinculo,
  desvincularVinculo,
  getListarTiposVinculoQueryOptions,
  getListarVinculosQueryOptions,
  getObtenerHistorialVinculoQueryOptions,
  getObtenerTerceroQueryOptions,
  getObtenerViabilidadDeReintegroQueryOptions,
  getObtenerVinculoQueryOptions,
  invitarVinculo,
  obtenerVinculoPorReferencia,
  reintegrarVinculo,
  type AccionVinculo,
  type EnlaceInvitacionDto,
  type ListarVinculosParams,
  type VinculoDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';

/** Las claves llevan la empresa: el mismo id de vínculo en otra empresa es otro recurso (404). */
export function useVinculos(empresaId: string, params: ListarVinculosParams) {
  const opciones = getListarVinculosQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), placeholderData: (previo) => previo });
}

export function useVinculo(empresaId: string, id: string) {
  const opciones = getObtenerVinculoQueryOptions(id, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: Boolean(id) });
}

export function useHistorialVinculo(empresaId: string, id: string) {
  const opciones = getObtenerHistorialVinculoQueryOptions(id, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId) });
}

/** ¿Se puede reintegrar sin revalidar? y, si no, por qué (DS-3). Se pide al abrir el diálogo, siempre fresco. */
export function useViabilidadDeReintegro(empresaId: string, id: string, habilitado: boolean) {
  const opciones = getObtenerViabilidadDeReintegroQueryOptions(id, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado, staleTime: 0, gcTime: 0 });
}

/** Datos del tercero (globales; visibles si la empresa tiene un vínculo con él). */
export function useTercero(id: string, habilitado = true) {
  const opciones = getObtenerTerceroQueryOptions(id);
  return useQuery({ ...opciones, staleTime: 5 * 60_000, enabled: habilitado, retry: false });
}

export function useTiposVinculo(empresaId: string, habilitado: boolean) {
  const opciones = getListarTiposVinculoQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), staleTime: 5 * 60_000, enabled: habilitado });
}

export function buscarPorReferencia(empresaId: string, sistema: string, id: string): Promise<VinculoDto> {
  return obtenerVinculoPorReferencia({ sistema, id }, { empresa: empresaId });
}

export interface EntradaAccion {
  accion: AccionVinculo;
  vinculo: VinculoDto;
  /** Motivo, justificación o correo según la acción. */
  texto: string;
  /** Fin del bloqueo (ISO) o vigencia de la invitación en horas. */
  hasta?: string | null;
  vigenciaHoras?: number;
  /** Solo al reintegrar (DS-3): `true` (por omisión) vuelve a validar; `false` restituye el estado sin revalidar. */
  revalidar?: boolean;
}

export type ResultadoAccion = { tipo: 'vinculo'; vinculo: VinculoDto } | { tipo: 'invitacion'; invitacion: EnlaceInvitacionDto };

/** Ejecuta la acción con `If-Match` = versión leída: si alguien cambió el vínculo, la API responde 412. */
export function ejecutarAccion(empresaId: string, entrada: EntradaAccion): Promise<ResultadoAccion> {
  const { accion, vinculo, texto } = entrada;
  const opciones = { empresa: empresaId, ifMatch: vinculo.version };
  const comoVinculo = (promesa: Promise<VinculoDto>) => promesa.then((v): ResultadoAccion => ({ tipo: 'vinculo', vinculo: v }));

  switch (accion) {
    case 'invitar':
      return invitarVinculo(vinculo.id, { correoDestino: texto || null, vigenciaHoras: entrada.vigenciaHoras ?? 72 }, opciones).then(
        (invitacion): ResultadoAccion => ({ tipo: 'invitacion', invitacion }),
      );
    case 'bloquear':
      return comoVinculo(bloquearVinculo(vinculo.id, { motivo: texto, hasta: entrada.hasta ?? null }, opciones));
    case 'desbloquear':
      return comoVinculo(desbloquearVinculo(vinculo.id, { justificacion: texto }, opciones));
    case 'desvincular':
      return comoVinculo(desvincularVinculo(vinculo.id, { motivo: texto }, opciones));
    case 'reintegrar':
      return comoVinculo(reintegrarVinculo(vinculo.id, { justificacion: texto, revalidar: entrada.revalidar ?? true }, opciones));
    case 'anular':
      return comoVinculo(anularVinculo(vinculo.id, { motivo: texto }, opciones));
  }
}

export function useAccionVinculo(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (entrada: EntradaAccion) => ejecutarAccion(empresaId, entrada),
    // Éxito o 412: en ambos casos lo que se mostraba ya no es lo vigente. Sin esperar la recarga: el diálogo se
    // cierra con la respuesta, no con el vínculo recargado (que ya no admitiría la acción recién hecha).
    onSettled: () => {
      void invalidarRuta(cliente, '/api/v1/vinculos');
    },
  });
}
