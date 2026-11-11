import { cerrarSesionIngreso, esErrorApi, msRestantes, renovarSesionIngreso, type SesionDirectaDto } from '@validasoft/api-client';
import { useEffect, useRef, useState } from 'react';
import { useSesion, type EstadoSesion } from './sesionStore';

/**
 * Sesión del ingreso directo (ING-1): el token de acceso vive en memoria y dura minutos; lo que sobrevive a recargar es la cookie
 * `vs_usuario` (HttpOnly), que solo sirve para renovarlo. Este módulo restaura la sesión al abrir la app, la renueva mientras la persona
 * está activa, la cierra y marca por qué terminó.
 */

/** Defensa CSRF adicional a `SameSite=Strict`: sin esta cabecera la API no renueva ni cierra (403 `cliente-invalido`). */
export const CABECERAS_DE_CLIENTE = { 'X-Validasoft-Cliente': 'web' } as const;

const OPCIONES = { token: null, empresa: null, headers: CABECERAS_DE_CLIENTE } as const;

/** Se renueva este tiempo antes de que venza el token de acceso (o a la mitad de su vida si es más corta). */
export const AVISO_RENOVACION_MS = 2 * 60 * 1000;

/** Pone la sesión recién abierta (o renovada) como la de la app. */
export function aplicarSesionDirecta(dto: SesionDirectaDto): boolean {
  return useSesion.getState().establecer(dto.token, null, { sugerirPasskey: dto.sugerirPasskey });
}

export type ResultadoDeRenovacion = 'renovada' | 'sin-sesion' | 'terminada' | 'revocada';

/** Cómo termina una sesión según el `codigo` con el que la API se negó a renovarla. */
export function resultadoDeFallo(error: unknown): ResultadoDeRenovacion {
  if (!esErrorApi(error)) return 'sin-sesion';
  switch (error.codigo) {
    case 'sesion-revocada':
    case 'passkey-requerida':
    case 'cuenta-no-disponible':
      return 'revocada';
    case 'sesion-expirada':
    case 'sesion-inactiva':
      return 'terminada';
    default:
      // `sin-sesion` (no hay cookie), un 429 o cualquier otro fallo: se ve el ingreso sin más.
      return 'sin-sesion';
  }
}

/**
 * Renueva con la cookie. Sin cookie (o con cualquier fallo que no explique el cierre) es `sin-sesion`: se ve el ingreso sin más. Una cookie que
 * existía pero ya no sirve es `terminada` (se agotó la sesión o la inactividad) o `revocada` (la cerraron).
 */
let enCurso: Promise<ResultadoDeRenovacion> | null = null;

export function renovarSesionDirecta(): Promise<ResultadoDeRenovacion> {
  // Una sola renovación a la vez en esta pestaña (StrictMode, dos temporizadores) y, con Web Locks, una sola entre pestañas: cada renovación rota
  // la cookie y la segunda con la cookie vieja sería un reuso.
  enCurso ??= (async () => {
    try {
      if (typeof navigator !== 'undefined' && 'locks' in navigator) {
        return await navigator.locks.request('validasoft-renovar-sesion', () => renovar());
      }
      return await renovar();
    } finally {
      enCurso = null;
    }
  })();
  return enCurso;
}

async function renovar(): Promise<ResultadoDeRenovacion> {
  for (let intento = 0; ; intento += 1) {
    try {
      const dto = await renovarSesionIngreso(OPCIONES);
      return aplicarSesionDirecta(dto) ? 'renovada' : 'terminada';
    } catch (error) {
      // Otra pestaña acaba de renovar (409): con su cookie nueva el segundo intento sirve.
      if (intento === 0 && esErrorApi(error) && error.codigo === 'renovacion-concurrente') {
        await new Promise((resolver) => window.setTimeout(resolver, 400));
        continue;
      }
      return resultadoDeFallo(error);
    }
  }
}

/** Deja la pantalla en el estado que corresponde a un resultado de renovación fallido. */
export function marcarFallo(resultado: Exclude<ResultadoDeRenovacion, 'renovada'>): void {
  if (resultado === 'sin-sesion') useSesion.getState().marcar('sin-sesion');
  else useSesion.getState().marcar(resultado);
}

/** Cierra la sesión directa en la API (revoca la familia y borra la cookie) y en la app. Un fallo de red no impide salir de la pantalla. */
export async function cerrarSesionDirecta(): Promise<void> {
  try {
    await cerrarSesionIngreso(OPCIONES);
  } catch {
    // Sin red o con la cookie ya inválida, la sesión de la app se cierra igual.
  }
  useSesion.getState().marcar('cerrada');
  // Quien entre después empieza por su inicio, no por la pantalla donde estaba la sesión anterior.
  if (typeof window !== 'undefined') window.location.hash = '#/';
}

/**
 * Al abrir la app fuera de un contenedor intenta restaurar la sesión con la cookie. Devuelve si todavía lo está intentando (para mostrar
 * un esqueleto en vez de parpadear el ingreso).
 */
export function useRestaurarSesionDirecta(habilitado: boolean): boolean {
  const [restaurando, setRestaurando] = useState(habilitado);
  useEffect(() => {
    if (!habilitado) return undefined;
    let vigente = true;
    void renovarSesionDirecta().then((resultado) => {
      if (!vigente) return;
      if (resultado !== 'renovada') marcarFallo(resultado);
      setRestaurando(false);
    });
    return () => {
      vigente = false;
    };
  }, [habilitado]);
  return restaurando;
}

/** Eventos que cuentan como «la persona está usando la app» para no cerrarle la sesión por inactividad. */
const EVENTOS_DE_ACTIVIDAD = ['pointerdown', 'keydown', 'wheel', 'touchstart'] as const;

/**
 * Mantiene viva la sesión directa mientras la persona la use: poco antes de que venza el token de acceso lo renueva **si hubo actividad** desde la
 * última renovación; si no, lo deja vencer y la app pasa a `inactiva` (el servidor cierra la sesión pasada la inactividad del grupo). Sin
 * sesión directa (identidad delegada) no hace nada: ahí renueva el contenedor.
 */
export function useRenovacionDeSesionDirecta(): void {
  const sesion = useSesion((s) => s.sesion);
  const sesionId = sesion?.sesionId ?? null;
  const ultimaActividad = useRef(Date.now());
  const renovadaEn = useRef(Date.now());

  useEffect(() => {
    if (!sesionId) return undefined;
    const marcar = () => {
      ultimaActividad.current = Date.now();
    };
    for (const evento of EVENTOS_DE_ACTIVIDAD) window.addEventListener(evento, marcar, { passive: true });
    return () => {
      for (const evento of EVENTOS_DE_ACTIVIDAD) window.removeEventListener(evento, marcar);
    };
  }, [sesionId]);

  useEffect(() => {
    if (!sesion || !sesionId) return undefined;
    renovadaEn.current = Date.now();
    const restante = msRestantes(sesion);
    const aviso = Math.min(AVISO_RENOVACION_MS, restante / 2);

    const renovar = window.setTimeout(() => {
      // Sin actividad desde la última renovación no se renueva: el token vence y la sesión queda en pausa.
      if (ultimaActividad.current <= renovadaEn.current) return;
      void renovarSesionDirecta().then((resultado) => {
        if (resultado !== 'renovada' && useSesion.getState().sesion === sesion) marcarFallo(resultado);
      });
    }, Math.max(0, restante - aviso));

    const vencimiento = window.setTimeout(() => {
      if (useSesion.getState().sesion === sesion) useSesion.getState().marcar('inactiva');
    }, Math.max(0, restante));

    return () => {
      window.clearTimeout(renovar);
      window.clearTimeout(vencimiento);
    };
  }, [sesion, sesionId]);
}

/** ¿La pantalla de sesión corresponde a un ingreso directo que terminó (para mostrar el motivo)? */
export const ESTADOS_DE_SALIDA: readonly EstadoSesion[] = ['cerrada', 'terminada', 'revocada'];
