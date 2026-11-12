import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  fijarDominioDeCorreo,
  getObtenerDominioDeCorreoQueryOptions,
  quitarDominioDeCorreo,
  verificarDominioDeCorreo,
  type ActualizarDominioDeCorreoDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../../shared/consultas';

/** El dominio de correo propio de la empresa (OD-18): registros DNS, estado de la verificación y remitente en uso. */
export function useDominioDeCorreo(empresaId: string, habilitado: boolean) {
  const opciones = getObtenerDominioDeCorreoQueryOptions(empresaId, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

function useCambiarDominio<T, V>(empresaId: string, funcion: (valor: V) => Promise<T>) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: funcion,
    onSuccess: () => invalidarRuta(cliente, `/api/v1/empresas/${empresaId}/dominio-correo`),
  });
}

export function useFijarDominio(empresaId: string) {
  return useCambiarDominio(empresaId, (cuerpo: ActualizarDominioDeCorreoDto) => fijarDominioDeCorreo(empresaId, cuerpo, { empresa: empresaId }));
}

export function useVerificarDominio(empresaId: string) {
  return useCambiarDominio(empresaId, () => verificarDominioDeCorreo(empresaId, { empresa: empresaId }));
}

export function useQuitarDominio(empresaId: string) {
  return useCambiarDominio(empresaId, () => quitarDominioDeCorreo(empresaId, { empresa: empresaId }));
}
