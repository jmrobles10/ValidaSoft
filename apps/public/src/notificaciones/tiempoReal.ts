import { useQueryClient } from '@tanstack/react-query';
import {
  aplicarEventoDeBandeja,
  useTiempoReal,
  type BandejaNotificacionesTerceroDto,
  type EsperasConexion,
  type EstadoConexion,
  type MapaDeInvalidacion,
} from '@validasoft/api-client';
import { useSesionTercero } from '../sesion/sesionTercero';
import { claveBandeja } from './api';
import { interpretarEvento } from './bandeja';

export const RUTA_STREAM = '/public/v1/notificaciones/stream';

/**
 * Prefijos de la ruta de las consultas del portal que se refrescan según lo que cambió (NT-12). El mismo texto es el alcance con el
 * que una pantalla en edición se protege de que el tiempo real le pise lo escrito (`useEdicionEnCurso`).
 */
export const ALCANCES_PUBLICO = {
  expedientes: '/public/v1/expedientes',
  vinculos: '/public/v1/vinculos',
  invitaciones: '/public/v1/invitaciones',
  boveda: '/public/v1/boveda',
  ficha: '/public/v1/ficha',
} as const;

export const MAPA_PUBLICO: MapaDeInvalidacion = {
  // Una observación de la empresa llega con el estado de la ficha (FD-2): un cambio del expediente también lo refresca.
  expediente: [ALCANCES_PUBLICO.expedientes, ALCANCES_PUBLICO.vinculos, ALCANCES_PUBLICO.ficha],
  vinculo: [ALCANCES_PUBLICO.vinculos, ALCANCES_PUBLICO.invitaciones, ALCANCES_PUBLICO.expedientes],
  cuenta: [ALCANCES_PUBLICO.boveda],
  documento: [ALCANCES_PUBLICO.boveda, ALCANCES_PUBLICO.expedientes, ALCANCES_PUBLICO.ficha],
  ficha: [ALCANCES_PUBLICO.ficha, ALCANCES_PUBLICO.expedientes],
  certificado: [ALCANCES_PUBLICO.vinculos, ALCANCES_PUBLICO.expedientes],
};

/**
 * Tiempo real del portal (`GET /public/v1/notificaciones/stream`, SSE con la sesión del tercero): cada notificación nueva entra arriba
 * en su campana y cada aviso de «cambió X» —lo de sus vínculos— refresca la pantalla que tiene abierta (expediente, bóveda, ficha,
 * cuentas) sin pisar lo que está escribiendo. El servidor cierra el stream al vencer el token de la sesión: se reconecta con el renovado.
 * Un solo stream por pestaña (se usa desde la campana de la cabecera).
 */
export function useStreamDelTercero(esperas?: Partial<EsperasConexion>): EstadoConexion {
  const cliente = useQueryClient();
  const terceroId = useSesionTercero((s) => s.sesion?.terceroId ?? null);
  const activa = useSesionTercero((s) => s.estado === 'activa');

  return useTiempoReal({
    ruta: RUTA_STREAM,
    activo: activa && Boolean(terceroId),
    clave: terceroId ?? '',
    mapa: MAPA_PUBLICO,
    ...(esperas ? { esperas } : {}),
    cabeceras: (cfg) => {
      const token = cfg.obtenerToken();
      return token ? { Authorization: `Bearer ${token}`, 'X-Correlation-Id': cfg.generarId() } : null;
    },
    alLlegarTokenNuevo: (reconectarYa) =>
      useSesionTercero.subscribe((s, previo) => {
        if (s.sesion?.accessToken && s.sesion.accessToken !== previo.sesion?.accessToken) reconectarYa();
      }),
    alEvento: (crudo) => {
      const evento = interpretarEvento(crudo);
      if (evento) aplicarEventoDeBandeja<BandejaNotificacionesTerceroDto>(cliente, claveBandeja, evento);
    },
  });
}
