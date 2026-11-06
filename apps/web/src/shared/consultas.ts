import { esErrorApi } from '@validasoft/api-client';
import { QueryClient, type QueryKey } from '@tanstack/react-query';

/**
 * Las claves que genera orval no incluyen la empresa (viaja en `X-Empresa`); se agrega aquí para que dos empresas
 * nunca compartan caché (ADR-0008).
 */
export function conEmpresa(clave: QueryKey, empresaId: string | null): QueryKey {
  return [...clave, { empresa: empresaId }];
}

/** Invalida todas las consultas cuya ruta empieza por el prefijo (p. ej. `/api/v1/vinculos`). */
export function invalidarRuta(cliente: QueryClient, prefijo: string): Promise<void> {
  return cliente.invalidateQueries({
    predicate: (consulta) => {
      const primera = consulta.queryKey[0];
      return typeof primera === 'string' && primera.startsWith(prefijo);
    },
  });
}

/** No se reintenta lo que el servidor ya rechazó (4xx): solo fallas de red o 5xx, hasta dos veces. */
export function debeReintentar(intentos: number, error: unknown): boolean {
  if (esErrorApi(error) && error.estado < 500) return false;
  return intentos < 2;
}

export function crearClienteConsultas(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: { staleTime: 30_000, retry: debeReintentar, refetchOnWindowFocus: false },
      mutations: { retry: false },
    },
  });
}
