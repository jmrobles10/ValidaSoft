import { useMutation, useQuery } from '@tanstack/react-query';
import { descargarCertificadoVerificado, getVerificarCertificadoQueryOptions, type CertificadoPublicoDto } from '@validasoft/api-client';
import { abrirDescarga } from '../shared/api';
import { obtenerClaves } from './claves';
import { verificarCertificado } from './verificacion';

/** La huella solo viaja si el enlace la trae: sin ella la API busca la emisión por el folio. */
const parametros = (huella: string | null) => (huella ? { huella } : undefined);

/**
 * `GET /public/v1/verificar/{folio}?huella=` (anónimo, `no-store`). El folio viene de la URL: se codifica para que no
 * pueda salirse de la ruta.
 */
export function useCertificadoVerificado(folio: string, huella: string | null) {
  return useQuery({ ...getVerificarCertificadoQueryOptions(encodeURIComponent(folio), parametros(huella)), staleTime: 0 });
}

/** Claves públicas del sello (cambian solo al rotar: una hora de caché, como la API). Sin reintento automático. */
export function useClavesCertificados() {
  return useQuery({ queryKey: ['claves-certificados'], queryFn: ({ signal }) => obtenerClaves(signal), staleTime: 3_600_000, retry: false });
}

/**
 * Verificación criptográfica en el navegador: espera a que las claves respondan (o fallen) y corre los pasos. La clave
 * de la consulta incluye todo lo que entra al cálculo, así que cualquier cambio vuelve a verificar.
 */
export function useVerificacionCriptografica(certificado: CertificadoPublicoDto, huella: string | null) {
  const claves = useClavesCertificados();
  const datosClaves = claves.status === 'success' ? claves.data : null;
  const verificacion = useQuery({
    queryKey: ['verificacion-certificado', certificado, datosClaves, huella],
    queryFn: () => verificarCertificado(certificado, datosClaves, huella),
    enabled: claves.status !== 'pending',
    staleTime: Infinity,
    retry: false,
  });
  return {
    resultado: verificacion.data ?? null,
    clavesFallaron: claves.status === 'error',
    reintentarClaves: () => void claves.refetch(),
  };
}

/** `POST /public/v1/verificar/{folio}/descarga`: URL prefirmada (solo de una emisión vigente) que se abre en el momento. */
export function useDescargarCertificado(folio: string, huella: string | null) {
  return useMutation({
    mutationFn: async () => abrirDescarga(await descargarCertificadoVerificado(encodeURIComponent(folio), parametros(huella))),
  });
}
