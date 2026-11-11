import { useQuery } from '@tanstack/react-query';
import { getObtenerFichaAceptadaDelVinculoQueryOptions } from '@validasoft/api-client';
import { conEmpresa } from '../../../shared/consultas';

/**
 * La ficha aceptada por la empresa en un vínculo (FD-3): la clave lleva la empresa porque lo que se ve depende de ella (ADR-0008).
 * Es lo que la empresa aceptó, no lo que el tercero tiene hoy; sin `terceros:leer` y `vinculos:leer` no se pide.
 */
export function useFichaAceptada(empresaId: string, vinculoId: string, habilitado: boolean) {
  const opciones = getObtenerFichaAceptadaDelVinculoQueryOptions(vinculoId, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado && Boolean(vinculoId) });
}
