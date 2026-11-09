import { useQuery } from '@tanstack/react-query';
import { getObtenerFichaTerceroQueryOptions } from '@validasoft/api-client';
import { conEmpresa } from '../../shared/consultas';

/** Ficha 360 del tercero para la empresa activa: la clave lleva la empresa porque lo que se ve depende de ella. */
export function useFichaTercero(empresaId: string, terceroId: string, habilitado: boolean) {
  const opciones = getObtenerFichaTerceroQueryOptions(terceroId, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado && Boolean(terceroId) });
}
