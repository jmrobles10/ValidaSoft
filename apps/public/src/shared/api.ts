import { QueryClient, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { debeRefrescar } from '../expediente/requisitos';
import {
  aceptarInvitacion,
  descargarDocumentoBoveda,
  esErrorApi,
  getListarVinculosPublicosQueryOptions,
  getObtenerBovedaQueryOptions,
  getObtenerDocumentoBovedaQueryOptions,
  getObtenerExpedientePublicoQueryOptions,
  getObtenerInvitacionPublicaQueryOptions,
  reenviarExpedientePublico,
  retirarVersionDocumento,
  type ConsentimientoDto,
} from '@validasoft/api-client';

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

/** Todo lo del tercero vive bajo `/public/v1`: al cambiar algo, se refresca entero (son pocas consultas). */
function useRefrescarTodo() {
  const cliente = useQueryClient();
  return () => {
    void cliente.invalidateQueries();
  };
}

export const useVinculosPublicos = () => useQuery(getListarVinculosPublicosQueryOptions());

export const useInvitacion = (vinculoId: string) => useQuery(getObtenerInvitacionPublicaQueryOptions(vinculoId));

export function useAceptarInvitacion(vinculoId: string) {
  const refrescar = useRefrescarTodo();
  return useMutation({
    mutationFn: (consentimientos: ConsentimientoDto[]) => aceptarInvitacion(vinculoId, { consentimientos, otpCodigo: null }),
    onSettled: refrescar,
  });
}

export const useBoveda = () => useQuery(getObtenerBovedaQueryOptions());

export const useDocumentoBoveda = (documentoId: string, habilitado: boolean) =>
  useQuery({ ...getObtenerDocumentoBovedaQueryOptions(documentoId), enabled: habilitado });

export function useRetirarVersion(documentoId: string, numero: number) {
  const refrescar = useRefrescarTodo();
  return useMutation({ mutationFn: (motivo: string) => retirarVersionDocumento(documentoId, numero, { motivo }), onSettled: refrescar });
}

/** La URL prefirmada (5 min, `Content-Disposition: attachment`) se abre en el momento: el navegador descarga el archivo. */
export function useDescargarVersion() {
  return useMutation({
    mutationFn: async ({ documentoId, numero }: { documentoId: string; numero: number }) => {
      const descarga = await descargarDocumentoBoveda(documentoId, numero);
      const enlace = document.createElement('a');
      enlace.href = descarga.url;
      enlace.rel = 'noopener';
      enlace.download = descarga.nombre;
      document.body.append(enlace);
      enlace.click();
      enlace.remove();
      return descarga;
    },
  });
}

/**
 * Se refresca solo mientras falten documentos o esté devuelto: la versión subida llega por una cola (RQ-2).
 * `habilitado = false` no consulta (p. ej. «Mis empresas» con la invitación sin aceptar).
 */
export const useExpedientePublico = (id: string, habilitado = true) =>
  useQuery({ ...getObtenerExpedientePublicoQueryOptions(id), enabled: habilitado, refetchInterval: (consulta) => debeRefrescar(consulta.state.data) });

export function useReenviarExpediente(id: string) {
  const refrescar = useRefrescarTodo();
  return useMutation({ mutationFn: (respuesta: string) => reenviarExpedientePublico(id, { respuesta: respuesta.trim() || null }), onSettled: refrescar });
}

/** Tras subir una versión: bóveda, documento y vínculos cambian. */
export function useAlTerminarSubida() {
  return useRefrescarTodo();
}
