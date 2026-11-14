import { useEffect, useState } from 'react';

/** Lo que el contador de la campana muestra: nada sin no leídas, el número hasta 99 y luego `99+`. */
export function textoContador(noLeidas: number): string {
  if (noLeidas <= 0) return '';
  return noLeidas > 99 ? '99+' : String(noLeidas);
}

const TIEMPO = new Intl.RelativeTimeFormat('es-CO', { numeric: 'auto' });

const UNIDADES: readonly [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 3600],
  ['month', 30 * 24 * 3600],
  ['week', 7 * 24 * 3600],
  ['day', 24 * 3600],
  ['hour', 3600],
  ['minute', 60],
];

/** «hace 5 minutos», «ayer», «hace 3 semanas»; menos de un minuto (o una fecha futura por desfase de reloj) es «ahora». */
export function haceCuanto(fecha: string, ahora: number): string {
  const segundos = Math.round((ahora - new Date(fecha).getTime()) / 1000);
  for (const [unidad, tamano] of UNIDADES) {
    if (segundos >= tamano) return TIEMPO.format(-Math.floor(segundos / tamano), unidad);
  }
  return TIEMPO.format(0, 'second');
}

/** La hora actual, refrescada cada `intervalo` ms: para que «hace 2 minutos» avance con el panel abierto. */
export function useAhora(intervalo = 60_000): number {
  const [ahora, setAhora] = useState(() => Date.now());
  useEffect(() => {
    const id = setInterval(() => setAhora(Date.now()), intervalo);
    return () => clearInterval(id);
  }, [intervalo]);
  return ahora;
}
