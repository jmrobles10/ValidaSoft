import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  aprobarSolicitudDeAccesoDePlataforma,
  getListarSolicitudesDeAccesoDePlataformaQueryOptions,
  rechazarSolicitudDeAccesoDePlataforma,
  verificarContactoDelTercero,
} from '@validasoft/api-client';
import { invalidarRuta } from '../../shared/consultas';

/** La consola de Plataforma no tiene empresa activa ni tenant (PT-4). */
const SIN_EMPRESA = { empresa: null } as const;

/** Qué solicitudes de acceso a terceros lista la consola: la cola (las que decide un operador), todas las pendientes, las resueltas o todas. */
export type FiltroDeSolicitudes = 'cola' | 'pendientes' | 'resueltas' | 'todas';

export const useSolicitudesDeAcceso = (estado: FiltroDeSolicitudes) =>
  useQuery(getListarSolicitudesDeAccesoDePlataformaQueryOptions({ estado }, { request: SIN_EMPRESA }));

/** Aprueba (sin motivo) o rechaza (con motivo opcional) una solicitud de acceso a un tercero sin contactos verificados. */
export function useDecidirSolicitudDeAcceso() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ id, aprobar, motivo }: { id: string; aprobar: boolean; motivo?: string }) =>
      aprobar ? aprobarSolicitudDeAccesoDePlataforma(id, SIN_EMPRESA) : rechazarSolicitudDeAccesoDePlataforma(id, { motivo: motivo?.trim() ? motivo.trim() : null }, SIN_EMPRESA),
    onSettled: () => invalidarRuta(cliente, '/api/v1/plataforma/solicitudes-de-acceso'),
  });
}

/** Un operador verifica el correo de un contacto del tercero (medio «Plataforma»). */
export function useVerificarContactoDelTercero() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ terceroId, correo }: { terceroId: string; correo: string }) => verificarContactoDelTercero(terceroId, { correo }, SIN_EMPRESA),
    onSettled: () => invalidarRuta(cliente, '/api/v1/plataforma/solicitudes-de-acceso'),
  });
}
