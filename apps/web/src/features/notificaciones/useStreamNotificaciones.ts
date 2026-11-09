import { useQueryClient } from '@tanstack/react-query';
import { configuracionCliente, construirUrl, type BandejaNotificacionesDto } from '@validasoft/api-client';
import { useEffect, useState } from 'react';
import { useSesion } from '../../sesion/sesionStore';
import { claveBandeja } from './api';
import { agregarNotificacion, interpretarEvento } from './bandeja';
import { abrirConexionSse, type EsperasConexion, type EstadoConexion } from './conexionSse';

export const RUTA_STREAM = '/api/v1/notificaciones/stream';

/**
 * Tiempo real de la campana (`GET /api/v1/notificaciones/stream`, SSE con el token Bearer). Cada notificación nueva
 * entra arriba en la bandeja en caché; si el total de no leídas que manda el servidor no cuadra con la caché (llegó
 * algo antes de abrir el stream, o se leyó en otra pestaña), se vuelve a pedir la bandeja.
 *
 * Se abre por persona, grupo y empresa activa (el servidor filtra con `X-Empresa`) y se cierra al desmontar o al
 * cambiar cualquiera de ellos. El token se lee en cada intento, porque el contenedor lo renueva; si llega uno nuevo
 * mientras se espera para reintentar, se reintenta ya.
 */
export function useStreamNotificaciones(empresaId: string, esperas?: Partial<EsperasConexion>): EstadoConexion {
  const cliente = useQueryClient();
  const usuarioId = useSesion((s) => s.sesion?.usuarioId ?? null);
  const tenantId = useSesion((s) => s.sesion?.tenantId ?? null);
  const [estado, setEstado] = useState<EstadoConexion>('conectando');
  const base = esperas?.base;
  const maximo = esperas?.maximo;
  const fallosParaRespaldo = esperas?.fallosParaRespaldo;

  useEffect(() => {
    if (!usuarioId || !tenantId) return;
    const cfg = configuracionCliente();
    const clave = claveBandeja(empresaId);
    const conexion = abrirConexionSse({
      url: construirUrl(cfg.baseUrl, RUTA_STREAM),
      fetch: cfg.fetch,
      esperas: {
        ...(base === undefined ? {} : { base }),
        ...(maximo === undefined ? {} : { maximo }),
        ...(fallosParaRespaldo === undefined ? {} : { fallosParaRespaldo }),
      },
      cabeceras: () => {
        const token = configuracionCliente().obtenerToken();
        if (!token) return null;
        return { Authorization: `Bearer ${token}`, 'X-Empresa': empresaId, 'X-Correlation-Id': cfg.generarId() };
      },
      alEstado: setEstado,
      alEvento: (crudo) => {
        const evento = interpretarEvento(crudo);
        if (!evento) return;
        const actual = cliente.getQueryData<BandejaNotificacionesDto>(clave);
        if (evento.tipo === 'notificacion') {
          if (actual) cliente.setQueryData<BandejaNotificacionesDto>(clave, agregarNotificacion(actual, evento.notificacion));
          else void cliente.invalidateQueries({ queryKey: clave });
          return;
        }
        if (actual?.noLeidas !== evento.total) void cliente.invalidateQueries({ queryKey: clave });
      },
    });
    const dejarDeEscuchar = useSesion.subscribe((s, previo) => {
      if (s.sesion?.token && s.sesion.token !== previo.sesion?.token) conexion.reconectarYa();
    });
    return () => {
      dejarDeEscuchar();
      conexion.cerrar();
    };
  }, [cliente, usuarioId, tenantId, empresaId, base, maximo, fallosParaRespaldo]);

  return estado;
}
