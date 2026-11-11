/** Cada cuántos días puede salir el recordatorio automático (`ConfiguracionDeRecordatorios.MinimoDias` y `MaximoDias`). */
export const MINIMO_DIAS = 1;
export const MAXIMO_DIAS = 60;

/** Un entero de 1 a 60 (sin decimales, signos ni notación científica). */
export function diasValidos(texto: string): boolean {
  if (!/^\d{1,2}$/.test(texto.trim())) return false;
  const dias = Number(texto);
  return dias >= MINIMO_DIAS && dias <= MAXIMO_DIAS;
}
