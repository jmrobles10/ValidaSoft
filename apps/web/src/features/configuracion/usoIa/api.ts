import { useQuery } from '@tanstack/react-query';
import { getObtenerUsoIaQueryOptions } from '@validasoft/api-client';
import { conEmpresa } from '../../../shared/consultas';

/** Plan y uso de IA de la empresa activa en el mes (`GET /ia/uso?mes=aaaa-mm`, IA-8); al cambiar de mes se conserva el anterior. */
export function useUsoIa(empresaId: string, mes: string, habilitado: boolean) {
  const opciones = getObtenerUsoIaQueryOptions({ mes }, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    placeholderData: (previo) => previo,
    enabled: habilitado,
  });
}
