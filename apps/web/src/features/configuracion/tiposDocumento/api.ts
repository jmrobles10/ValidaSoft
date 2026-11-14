import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarTipoDocumento,
  confirmarPlantillaTipoDocumento,
  crearTipoDocumento,
  descargarPlantillaTipoDocumento,
  getListarTiposDocumentoCanonicosQueryOptions,
  getObtenerPlantillaTipoDocumentoQueryOptions,
  iniciarPlantillaTipoDocumento,
  retirarPlantillaTipoDocumento,
  type TipoDocumentoTenantDto,
} from '@validasoft/api-client';
import { abrirDescarga, sha256Hex } from '@validasoft/ui';
import { invalidarRuta } from '../../../shared/consultas';
import { cuerpoActualizar, cuerpoCrear, type BorradorTipo } from './modelo';

/** Tipos de documento y su catálogo son del grupo (tenant): sin empresa activa. */
const SIN_EMPRESA = { empresa: null } as const;

/** Formatos que admite una plantilla (docs/05 §2.9.4, TD-5). */
export const MIMES_PLANTILLA = [
  'application/pdf',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
] as const;

export function useCanonicos(habilitado: boolean) {
  return useQuery({ ...getListarTiposDocumentoCanonicosQueryOptions({ request: SIN_EMPRESA }), enabled: habilitado, staleTime: 30 * 60_000 });
}

export function usePlantilla(tipoId: string) {
  return useQuery(getObtenerPlantillaTipoDocumentoQueryOptions(tipoId, { request: SIN_EMPRESA }));
}

/** Crear (con `Idempotency-Key` fija mientras el panel esté abierto) o actualizar un tipo. */
export function useGuardarTipo(tipo: TipoDocumentoTenantDto | null, claveIdempotencia: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (borrador: BorradorTipo) =>
      tipo
        ? actualizarTipoDocumento(tipo.id, cuerpoActualizar(borrador), SIN_EMPRESA)
        : crearTipoDocumento(cuerpoCrear(borrador), { ...SIN_EMPRESA, idempotencyKey: claveIdempotencia }),
    onSuccess: () => invalidarRuta(cliente, '/api/v1/tipos-documento'),
  });
}

/** Paso 1 (`PUT …/plantilla`), carga directa al almacenamiento con la URL prefirmada y paso 2 (`…/confirmar`). */
export async function subirPlantilla(tipoId: string, archivo: File, fetchFn: typeof fetch = (...a) => globalThis.fetch(...a)) {
  const sha256 = await sha256Hex(archivo);
  const inicio = await iniciarPlantillaTipoDocumento(tipoId, { nombreArchivo: archivo.name, tamanoBytes: archivo.size, tipoMime: archivo.type, sha256 }, SIN_EMPRESA);
  const respuesta = await fetchFn(inicio.carga.url, { method: inicio.carga.metodo, headers: inicio.carga.cabeceras, body: archivo });
  if (!respuesta.ok) throw new Error(`carga-fallida:${String(respuesta.status)}`);
  return confirmarPlantillaTipoDocumento(tipoId, inicio.version, SIN_EMPRESA);
}

export function useAccionesPlantilla(tipoId: string) {
  const cliente = useQueryClient();
  const refrescar = () => Promise.all([invalidarRuta(cliente, `/api/v1/tipos-documento/${tipoId}/plantilla`), invalidarRuta(cliente, '/api/v1/tipos-documento')]);
  const subir = useMutation({ mutationFn: (archivo: File) => subirPlantilla(tipoId, archivo), onSuccess: refrescar });
  const retirar = useMutation({ mutationFn: () => retirarPlantillaTipoDocumento(tipoId, SIN_EMPRESA), onSuccess: refrescar });
  const descargar = useMutation({
    mutationFn: async (version: number) => abrirDescarga(await descargarPlantillaTipoDocumento(tipoId, { version }, SIN_EMPRESA)),
  });
  return { subir, retirar, descargar };
}
