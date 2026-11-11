import { obtenerSesionActual } from '@validasoft/api-client';
import { cargarOrigenesPermitidos, origenDelContenedor, origenesPermitidos, RECARGA_ORIGENES_MS, siguienteListaDeOrigenes, VERSION_PROTOCOLO } from '@validasoft/sdk';
import { useEffect, useRef } from 'react';
import { useSesionTercero } from '../sesion/sesionTercero';
import { ANTICIPO_PEDIDO_MS, evaluarSesion } from './embebido';
import { useEstadoPuente } from './estadoPuente';
import { MODO_EMBEBIDO } from './modo';

/**
 * Puente con el sistema integrador que aloja el iframe (IO-5; protocolo en `@validasoft/sdk`, `embebido.ts`):
 *   - anuncia `validasoft:ready` al contenedor (solo si su origen es uno de los permitidos de los sistemas integradores);
 *   - recibe `validasoft:sesion` **solo** de ese contenedor y solo si el token está dirigido a su origen, y lo pone como la sesión del
 *     tercero (sin cookie: el sistema renueva pidiendo otra sesión a su backend);
 *   - pide otra sesión (`validasoft:sesion-requerida`) cuando la actual está por vencer, venció o fue rechazada;
 *   - informa el alto del contenido (`validasoft:resize`).
 */
type MotivoPedido = 'inicial' | 'por-vencer' | 'vencida' | 'rechazada';

/** Le dice al contenedor cuánto mide el contenido (el del body, no el del documento: este nunca baja del alto del iframe). */
function avisarAlto(contenedor: string | null): void {
  if (contenedor) window.parent.postMessage({ tipo: 'validasoft:resize', height: Math.ceil(document.body.getBoundingClientRect().height) }, contenedor);
}

export function usePuenteEmbebido(): void {
  const setEstado = useEstadoPuente((s) => s.poner);
  const permitidosRef = useRef<readonly string[] | null>(null);
  const contenedorRef = useRef<string | null>(null);
  const estadoDeSesion = useSesionTercero((s) => s.estado);
  const expira = useSesionTercero((s) => s.sesion?.expira ?? null);
  const yaPidioVencida = useRef(false);

  const propio = typeof window === 'undefined' ? '' : window.location.origin;
  const configurados = import.meta.env.VITE_ORIGENES_CONTENEDOR as string | undefined;

  const pedir = (motivo: MotivoPedido) => {
    const contenedor = contenedorRef.current;
    if (contenedor) window.parent.postMessage({ tipo: 'validasoft:sesion-requerida', motivo }, contenedor);
  };

  // Orígenes permitidos: los de la API (se vuelven a pedir cada minuto) y, como respaldo, los de la configuración.
  useEffect(() => {
    if (!MODO_EMBEBIDO) return undefined;
    let activo = true;
    let remotos: readonly string[] | null = null;
    const base = import.meta.env.VITE_API_BASE_URL ?? '';
    const refrescar = async () => {
      remotos = siguienteListaDeOrigenes(remotos, await cargarOrigenesPermitidos(base));
      if (!activo) return;
      const lista = origenesPermitidos(configurados, propio, remotos, import.meta.env.DEV);
      const cambio = permitidosRef.current?.join('|') !== lista.join('|');
      permitidosRef.current = lista;
      const contenedor = origenDelContenedor(window, lista);
      contenedorRef.current = contenedor;
      if (!contenedor) {
        setEstado((e) => (e === 'lista' ? e : 'sin-contenedor'));
      } else if (cambio) {
        window.parent.postMessage({ tipo: 'validasoft:ready', version: VERSION_PROTOCOLO }, contenedor);
        avisarAlto(contenedor);
        setEstado((e) => (e === 'sin-contenedor' ? 'esperando' : e));
      }
    };
    void refrescar();
    const id = setInterval(() => void refrescar(), RECARGA_ORIGENES_MS);
    return () => {
      activo = false;
      clearInterval(id);
    };
  }, [configurados, propio, setEstado]);

  // Mensajes del contenedor.
  useEffect(() => {
    if (!MODO_EMBEBIDO) return undefined;
    const alRecibir = (evento: MessageEvent) => {
      const permitidos = permitidosRef.current;
      if (!permitidos || evento.source !== window.parent) return;
      const datos: unknown = evento.data;
      if (typeof datos !== 'object' || datos === null || (datos as { tipo?: unknown }).tipo !== 'validasoft:sesion') return;
      const token = (datos as { token?: unknown }).token;
      if (typeof token !== 'string') return;

      const veredicto = evaluarSesion(token, evento.origin, permitidos, contenedorRef.current, Date.now());
      if (!veredicto.aceptada) {
        setEstado('rechazada');
        pedir('rechazada');
        return;
      }

      // La sesión (con la empresa y el estado de cada vínculo) la da la API con ese mismo token, y solo entonces se abre: una sesión
      // provisional haría pedir datos que se cancelarían al cambiar de persona (sin terceroId) y dejarían la pantalla en el esqueleto.
      obtenerSesionActual({ token })
        .then((sesion) => {
          yaPidioVencida.current = false;
          useSesionTercero.getState().establecer(sesion);
          setEstado('lista');
        })
        .catch(() => {
          setEstado('rechazada');
          pedir('rechazada');
        });
    };
    window.addEventListener('message', alRecibir);
    return () => window.removeEventListener('message', alRecibir);
  }, [setEstado]);

  // Sesión por vencer o vencida: se le pide otra al sistema (el iframe no renueva solo, no hay cookie).
  useEffect(() => {
    if (!MODO_EMBEBIDO || !expira || estadoDeSesion !== 'activa') return undefined;
    const faltan = new Date(expira).getTime() - Date.now() - ANTICIPO_PEDIDO_MS;
    const id = setTimeout(() => pedir('por-vencer'), Math.max(0, faltan));
    return () => clearTimeout(id);
  }, [expira, estadoDeSesion]);

  useEffect(() => {
    if (!MODO_EMBEBIDO || estadoDeSesion !== 'vencida' || yaPidioVencida.current) return;
    yaPidioVencida.current = true;
    pedir('vencida');
  }, [estadoDeSesion]);

  // El alto del contenido, para que el anfitrión pueda ajustar el iframe.
  useEffect(() => {
    if (!MODO_EMBEBIDO) return undefined;
    const observador = new ResizeObserver(() => avisarAlto(contenedorRef.current));
    observador.observe(document.body);
    return () => observador.disconnect();
  }, []);

}
