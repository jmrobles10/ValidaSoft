import { useQueryClient } from '@tanstack/react-query';
import { useEffect, useRef, useState } from 'react';
import { configuracionCliente, type ConfiguracionCliente } from '../configuracion';
import { construirUrl } from '../fetcher';
import { interpretarCambio } from './cambios';
import { abrirConexionSse, type EsperasConexion, type EstadoConexion } from './conexionSse';
import { fijarAplicadorDePendientes } from './edicion';
import { invalidarAlcance, invalidarPorCambio, invalidarTodoElMapa, type MapaDeInvalidacion } from './invalidar';
import type { EventoSse } from './sse';

/** Con el stream caído, mientras sigue reintentando, lo que muestra la pantalla se refresca cada tanto (proxys, el iframe del SRM). */
export const INTERVALO_DE_RESPALDO = 30_000;

export interface OpcionesTiempoReal {
  /** Ruta del stream (`/api/v1/notificaciones/stream` o `/public/v1/notificaciones/stream`). */
  ruta: string;
  /** Si es `false` no se abre (sin sesión todavía). */
  activo: boolean;
  /** Cuando cambia, se cierra y se vuelve a abrir (p. ej. otra empresa activa u otra persona). */
  clave: string;
  /** Cabeceras de cada intento (se leen al conectar: el token se renueva); `null` = aún no hay con qué conectar. */
  cabeceras: (cfg: Readonly<ConfiguracionCliente>) => Record<string, string> | null;
  /** Qué consultas refresca cada tipo de recurso que cambia. */
  mapa: MapaDeInvalidacion;
  /** Lo que no es un aviso de cambio (las notificaciones y el total de no leídas). */
  alEvento?: (evento: EventoSse) => void;
  /** Para reconectar ya cuando llega un token nuevo (devuelve cómo dejar de escuchar). */
  alLlegarTokenNuevo?: (reconectarYa: () => void) => () => void;
  esperas?: Partial<EsperasConexion>;
}

/**
 * Tiempo real de una pantalla (NT-12): un solo stream SSE por pestaña, con espera creciente al reconectar. Cada `event: cambio` vuelve
 * a pedir las consultas del recurso que cambió (respetando la guardia de edición: si hay cambios sin guardar, avisa en vez de pisar).
 * Al reconectar se refresca todo lo del mapa (no se sabe qué pasó mientras estuvo caído) y, si no logra conectar, se refresca cada 30 s
 * (sondeo de respaldo). Devuelve el estado de la conexión.
 */
export function useTiempoReal(opciones: OpcionesTiempoReal): EstadoConexion {
  const cliente = useQueryClient();
  const [estado, setEstado] = useState<EstadoConexion>('conectando');
  // Lo que cambia en cada render (funciones y mapas de quien llama) se lee desde aquí: reabrir el stream solo depende de `ruta`, `activo` y `clave`.
  const vigente = useRef(opciones);
  useEffect(() => {
    vigente.current = opciones;
  });
  const { ruta, activo, clave } = opciones;
  const base = opciones.esperas?.base;
  const maximo = opciones.esperas?.maximo;
  const fallosParaRespaldo = opciones.esperas?.fallosParaRespaldo;

  useEffect(() => {
    if (!activo) return undefined;
    const cfg = configuracionCliente();
    let anterior: EstadoConexion = 'conectando';
    const conexion = abrirConexionSse({
      url: construirUrl(cfg.baseUrl, ruta),
      fetch: cfg.fetch,
      esperas: {
        ...(base === undefined ? {} : { base }),
        ...(maximo === undefined ? {} : { maximo }),
        ...(fallosParaRespaldo === undefined ? {} : { fallosParaRespaldo }),
      },
      cabeceras: () => vigente.current.cabeceras(configuracionCliente()),
      alEstado: (nuevo) => {
        setEstado(nuevo);
        // Volvió después de caerse: pudo pasar de todo mientras tanto.
        if (nuevo === 'conectado' && (anterior === 'reconectando' || anterior === 'respaldo')) invalidarTodoElMapa(cliente, vigente.current.mapa);
        anterior = nuevo;
      },
      alEvento: (crudo) => {
        if (crudo.evento === 'cambio') {
          const cambio = interpretarCambio(crudo.datos);
          if (cambio) invalidarPorCambio(cliente, cambio, vigente.current.mapa);
          return;
        }
        vigente.current.alEvento?.(crudo);
      },
    });
    fijarAplicadorDePendientes((alcance) => void invalidarAlcance(cliente, alcance));
    const dejarDeEscuchar = vigente.current.alLlegarTokenNuevo?.(() => conexion.reconectarYa());
    return () => {
      dejarDeEscuchar?.();
      fijarAplicadorDePendientes(null);
      conexion.cerrar();
    };
  }, [cliente, ruta, activo, clave, base, maximo, fallosParaRespaldo]);

  useEffect(() => {
    if (!activo || estado !== 'respaldo') return undefined;
    const id = setInterval(() => invalidarTodoElMapa(cliente, vigente.current.mapa), INTERVALO_DE_RESPALDO);
    return () => clearInterval(id);
  }, [cliente, activo, estado]);

  return estado;
}
