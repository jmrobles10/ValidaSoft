/** Formatos es-CO. El backend entrega ISO-8601 UTC; se muestran en la zona del tenant (por defecto Bogotá). */
export const ZONA_HORARIA_POR_DEFECTO = 'America/Bogota';
export const LOCALE = 'es-CO';

function aFecha(valor: string | Date): Date {
  return valor instanceof Date ? valor : new Date(valor);
}

/** 28/09/2026 */
export function formatearFecha(valor: string | Date, zonaHoraria = ZONA_HORARIA_POR_DEFECTO): string {
  return new Intl.DateTimeFormat(LOCALE, { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: zonaHoraria }).format(
    aFecha(valor),
  );
}

/** 28/09/2026, 3:05 p. m. */
export function formatearFechaHora(valor: string | Date, zonaHoraria = ZONA_HORARIA_POR_DEFECTO): string {
  return new Intl.DateTimeFormat(LOCALE, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZone: zonaHoraria,
  }).format(aFecha(valor));
}

/** 3:05 p. m. */
export function formatearHora(valor: string | Date, zonaHoraria = ZONA_HORARIA_POR_DEFECTO): string {
  return new Intl.DateTimeFormat(LOCALE, { hour: 'numeric', minute: '2-digit', timeZone: zonaHoraria }).format(aFecha(valor));
}

/** Clave del día calendario en la zona dada: `2026-09-28`. */
export function claveDia(valor: string | Date, zonaHoraria = ZONA_HORARIA_POR_DEFECTO): string {
  const partes = new Intl.DateTimeFormat('en-CA', { year: 'numeric', month: '2-digit', day: '2-digit', timeZone: zonaHoraria }).format(
    aFecha(valor),
  );
  return partes;
}

/** "Jueves, 28 de septiembre de 2026" (solo la primera letra en mayúscula, como se escribe en español). */
export function formatearDiaLargo(valor: string | Date, zonaHoraria = ZONA_HORARIA_POR_DEFECTO): string {
  const texto = new Intl.DateTimeFormat(LOCALE, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric', timeZone: zonaHoraria }).format(
    aFecha(valor),
  );
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}
