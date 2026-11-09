import { useQuery } from '@tanstack/react-query';
import { getListarAuditoriaQueryOptions, type ListarAuditoriaParams } from '@validasoft/api-client';
import { conEmpresa } from '../../shared/consultas';

/** Auditoría de la empresa activa (`GET /auditoria`, lo más reciente primero); se vuelve a pedir al abrir la pantalla. */
export function useAuditoria(empresaId: string, params: ListarAuditoriaParams, habilitado: boolean) {
  const opciones = getListarAuditoriaQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    placeholderData: (previo) => previo,
    enabled: habilitado,
  });
}
