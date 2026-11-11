/**
 * Avisos de «cambió X» que llevan los streams SSE (`event: cambio`, `CambioEnVivoDto` del backend, NT-12): qué tipo de recurso cambió y
 * su id, nunca datos. El cliente solo vuelve a pedir lo que ya mostraba, por la API de siempre y con sus permisos.
 */
export const RECURSOS_DE_CAMBIO = ['expediente', 'vinculo', 'cuenta', 'documento', 'ficha', 'certificado'] as const;

export type RecursoDeCambio = (typeof RECURSOS_DE_CAMBIO)[number];

export interface CambioEnVivo {
  recurso: RecursoDeCambio;
  id: string;
  vinculoId: string | null;
}

function esRecurso(valor: unknown): valor is RecursoDeCambio {
  return typeof valor === 'string' && (RECURSOS_DE_CAMBIO as readonly string[]).includes(valor);
}

/** El cuerpo de un `event: cambio`; `null` si no tiene la forma esperada (no se confía en el JSON del stream). */
export function interpretarCambio(datos: string): CambioEnVivo | null {
  try {
    const crudo: unknown = JSON.parse(datos);
    if (typeof crudo !== 'object' || crudo === null) return null;
    const v = crudo as Record<string, unknown>;
    if (!esRecurso(v.recurso) || typeof v.id !== 'string') return null;
    return { recurso: v.recurso, id: v.id, vinculoId: typeof v.vinculoId === 'string' ? v.vinculoId : null };
  } catch {
    return null;
  }
}
