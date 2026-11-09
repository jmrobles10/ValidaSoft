import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { aprobarCuentaDesignada, getObtenerCuentaDesignadaQueryOptions, rechazarCuentaDesignada, type CuentaDesignadaDelVinculoDto } from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../../shared/consultas';

function opcionesCuenta(empresaId: string, vinculoId: string) {
  const opciones = getObtenerCuentaDesignadaQueryOptions(vinculoId, { request: { empresa: empresaId } });
  return { ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId) };
}

/** Cuenta designada, propuesta y última rechazada del vínculo; el número completo solo llega con `cuentas:aprobar`. */
export function useCuentaDesignada(empresaId: string, vinculoId: string, habilitado: boolean) {
  return useQuery({ ...opcionesCuenta(empresaId, vinculoId), enabled: habilitado && Boolean(vinculoId) });
}

/**
 * Aprobar o rechazar devuelven el estado nuevo de la cuenta: se pinta de una vez y luego se recarga todo el vínculo
 * (historial incluido). Con error (p. ej. `sin-cuenta-propuesta`) también se recarga: lo mostrado ya no es lo vigente.
 */
function useResolverCuenta<T>(empresaId: string, vinculoId: string, accion: (entrada: T) => Promise<CuentaDesignadaDelVinculoDto>) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: accion,
    onSuccess: (datos) => {
      cliente.setQueryData(opcionesCuenta(empresaId, vinculoId).queryKey, datos);
    },
    onSettled: () => invalidarRuta(cliente, '/api/v1/vinculos'),
  });
}

export function useAprobarCuenta(empresaId: string, vinculoId: string) {
  return useResolverCuenta(empresaId, vinculoId, () => aprobarCuentaDesignada(vinculoId, { empresa: empresaId }));
}

export function useRechazarCuenta(empresaId: string, vinculoId: string) {
  return useResolverCuenta(empresaId, vinculoId, (motivo: string) => rechazarCuentaDesignada(vinculoId, { motivo }, { empresa: empresaId }));
}
