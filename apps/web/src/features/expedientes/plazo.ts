const DIA_MS = 24 * 3600 * 1000;

/** Días que faltan para `hasta` (redondeado hacia arriba; 0 si ya pasó), como los cuenta el correo al tercero (LT-2). */
export function diasRestantes(hasta: string, ahora: number): number {
  return Math.max(0, Math.ceil((new Date(hasta).getTime() - ahora) / DIA_MS));
}
