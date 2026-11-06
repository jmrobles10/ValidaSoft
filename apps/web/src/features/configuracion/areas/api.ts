import { useQuery } from '@tanstack/react-query';
import { getListarAreasQueryOptions } from '@validasoft/api-client';
import { conEmpresa } from '../../../shared/consultas';

/** Áreas de la empresa activa con sus miembros (`configuracion:leer`). */
export function useAreas(empresaId: string, habilitado = true) {
  const opciones = getListarAreasQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}
