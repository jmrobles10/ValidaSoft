import { useQueryClient } from '@tanstack/react-query';
import { useTiempoReal, type BandejaNotificacionesDto, type EsperasConexion, type EstadoConexion } from '@validasoft/api-client';
import { useSesion } from '../../sesion/sesionStore';
import { claveBandeja } from './api';
import { agregarNotificacion, interpretarEvento } from './bandeja';
import { MAPA_WEB } from './cambios';

export const RUTA_STREAM = '/api/v1/notificaciones/stream';

/**
 * Tiempo real de la web de empresa (`GET /api/v1/notificaciones/stream`, SSE con el token Bearer). Cada notificación nueva entra arriba
 * en la bandeja en caché; si el total de no leídas que manda el servidor no cuadra con la caché (llegó algo antes de abrir el stream, o
 * se leyó en otra pestaña), se vuelve a pedir la bandeja. Además, cada aviso de «cambió X» de la empresa activa refresca las consultas
 * afectadas (expediente, bandeja, ficha del tercero, cuentas, documentos, certificados…), sin pisar lo que se está editando
 * (`useTiempoReal`, NT-12).
 *
 * Se abre por persona, grupo y empresa activa (el servidor filtra con `X-Empresa`) y se cierra al desmontar o al cambiar cualquiera de
 * ellos. El token se lee en cada intento, porque el contenedor lo renueva; si llega uno nuevo mientras se espera para reintentar, se
 * reintenta ya.
 */
export function useStreamNotificaciones(empresaId: string, esperas?: Partial<EsperasConexion>): EstadoConexion {
  const cliente = useQueryClient();
  const usuarioId = useSesion((s) => s.sesion?.usuarioId ?? null);
  const tenantId = useSesion((s) => s.sesion?.tenantId ?? null);

  return useTiempoReal({
    ruta: RUTA_STREAM,
    activo: Boolean(usuarioId && tenantId),
    clave: `${usuarioId ?? ''}|${tenantId ?? ''}|${empresaId}`,
    mapa: MAPA_WEB,
    ...(esperas ? { esperas } : {}),
    cabeceras: (cfg) => {
      const token = cfg.obtenerToken();
      return token ? { Authorization: `Bearer ${token}`, 'X-Empresa': empresaId, 'X-Correlation-Id': cfg.generarId() } : null;
    },
    alLlegarTokenNuevo: (reconectarYa) =>
      useSesion.subscribe((s, previo) => {
        if (s.sesion?.token && s.sesion.token !== previo.sesion?.token) reconectarYa();
      }),
    alEvento: (crudo) => {
      const evento = interpretarEvento(crudo);
      if (!evento) return;
      const clave = claveBandeja(empresaId);
      const actual = cliente.getQueryData<BandejaNotificacionesDto>(clave);
      if (evento.tipo === 'notificacion') {
        if (actual) cliente.setQueryData<BandejaNotificacionesDto>(clave, agregarNotificacion(actual, evento.notificacion));
        else void cliente.invalidateQueries({ queryKey: clave });
        return;
      }
      if (actual?.noLeidas !== evento.total) void cliente.invalidateQueries({ queryKey: clave });
    },
  });
}
