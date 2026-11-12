import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarListasDePlataforma,
  getObtenerListasDePlataformaQueryOptions,
  migrarCredencialesDeListas,
  type CredencialesListasDto,
} from '@validasoft/api-client';
import { invalidarRuta } from '../../shared/consultas';

/** La consola de Plataforma no tiene empresa activa ni tenant (PT-4). */
const SIN_EMPRESA = { empresa: null } as const;

/** La cuenta de la plataforma con Infolaft (G-2, PP-5): una sola para todos los clientes. */
export function useListasDePlataforma() {
  return useQuery(getObtenerListasDePlataformaQueryOptions({ request: SIN_EMPRESA }));
}

/** Crea, cambia o quita (`clave: ''`) la cuenta de Infolaft; la clave solo se escribe y nunca vuelve. */
export function useActualizarListasDePlataforma() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (credenciales: CredencialesListasDto) => actualizarListasDePlataforma({ credenciales }, SIN_EMPRESA),
    onSuccess: async () => {
      await invalidarRuta(cliente, '/api/v1/plataforma/listas-restrictivas');
      await invalidarRuta(cliente, '/api/v1/plataforma/secretos');
    },
  });
}

/** Recoge la primera cuenta válida que una empresa tenía de Infolaft y quita las propias de todas (idempotente). */
export function useMigrarCredencialesDeListas() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: () => migrarCredencialesDeListas(SIN_EMPRESA),
    onSuccess: async () => {
      await invalidarRuta(cliente, '/api/v1/plataforma/listas-restrictivas');
      await invalidarRuta(cliente, '/api/v1/plataforma/secretos');
    },
  });
}
