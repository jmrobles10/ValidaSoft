import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actualizarTipoVinculo, crearTipoVinculo, getListarTiposVinculoQueryOptions, type TipoVinculoDto } from '@validasoft/api-client';
import { invalidarRuta } from '../../../shared/consultas';
import { cuerpoDeTipo, cuerpoTipoVinculo, type BorradorTipoVinculo } from './modelo';

/** Los tipos de vínculo son del grupo (tenant): sin empresa activa. */
const SIN_EMPRESA = { empresa: null } as const;

export function useTiposVinculoGrupo(habilitado: boolean) {
  return useQuery({ ...getListarTiposVinculoQueryOptions({ request: SIN_EMPRESA }), enabled: habilitado });
}

/**
 * Crear o editar (el `PUT` reemplaza nombre, atributos y exigencias; el código no cambia). Invalida también la lista
 * que usan los vínculos y el editor de flujos (misma ruta, clave con empresa).
 */
export function useGuardarTipoVinculo(tipo: TipoVinculoDto | null, claveIdempotencia: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (b: BorradorTipoVinculo) =>
      tipo ? actualizarTipoVinculo(tipo.id, cuerpoTipoVinculo(b), undefined, SIN_EMPRESA) : crearTipoVinculo(cuerpoTipoVinculo(b), { ...SIN_EMPRESA, idempotencyKey: claveIdempotencia }),
    onSuccess: () => invalidarRuta(cliente, '/api/v1/tipos-vinculo'),
  });
}

/** Activar o desactivar sin tocar lo demás (`PUT …?activo=`). Un tipo inactivo no admite flujos nuevos. */
export function useCambiarEstadoTipoVinculo() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ tipo, activo }: { tipo: TipoVinculoDto; activo: boolean }) => actualizarTipoVinculo(tipo.id, cuerpoDeTipo(tipo), { activo }, SIN_EMPRESA),
    onSettled: () => invalidarRuta(cliente, '/api/v1/tipos-vinculo'),
  });
}
