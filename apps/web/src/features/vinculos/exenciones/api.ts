import { useMutation, useQueryClient } from '@tanstack/react-query';
import { eximirVinculo, revocarExencionVinculo, type EximirVinculoDto } from '@validasoft/api-client';
import { invalidarRuta } from '../../../shared/consultas';

/** Lo que cambia al eximir o revocar: el vínculo (y su historial), los vencimientos, los expedientes (lo abierto se exime en el acto) y los certificados. */
async function invalidarTodo(cliente: ReturnType<typeof useQueryClient>) {
  await Promise.all(
    ['/api/v1/vinculos', '/api/v1/vencimientos', '/api/v1/expedientes', '/api/v1/certificados', '/api/v1/mis-pendientes', '/api/v1/bandeja'].map((ruta) =>
      invalidarRuta(cliente, ruta),
    ),
  );
}

/** Eximir al vínculo (`vinculos:eximir`). */
export function useEximirVinculo(empresaId: string, vinculoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: EximirVinculoDto) => eximirVinculo(vinculoId, dto, { empresa: empresaId }),
    onSettled: () => {
      void invalidarTodo(cliente);
    },
  });
}

/** Revocar una exención vigente con justificación. */
export function useRevocarExencion(empresaId: string, vinculoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ exencionId, justificacion }: { exencionId: string; justificacion: string }) =>
      revocarExencionVinculo(vinculoId, exencionId, { justificacion }, { empresa: empresaId }),
    onSettled: () => {
      void invalidarTodo(cliente);
    },
  });
}
