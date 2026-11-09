import { useQuery } from '@tanstack/react-query';
import { getObtenerIndicadoresQueryOptions, getObtenerMisPendientesQueryOptions } from '@validasoft/api-client';
import { conEmpresa } from '../../shared/consultas';

/** Lo que le toca a quien consulta (`GET /mis-pendientes`); se vuelve a pedir cada vez que se abre el inicio. */
export function useMisPendientes(empresaId: string, habilitado = true) {
  const opciones = getObtenerMisPendientesQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', enabled: habilitado });
}

/** Indicadores de la empresa en la ventana elegida (`GET /indicadores?dias=`); al cambiarla se conservan los anteriores. */
export function useIndicadores(empresaId: string, dias: number, habilitado = true) {
  const opciones = getObtenerIndicadoresQueryOptions({ dias }, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    placeholderData: (previo) => previo,
    enabled: habilitado,
  });
}
