/**
 * Texto solo para lectores de pantalla. Medidas en px a propósito: en `sx` de MUI, `width: 1` significa 100 %.
 */
export const visualmenteOculto = {
  position: 'absolute',
  width: '1px',
  height: '1px',
  margin: '-1px',
  padding: 0,
  border: 0,
  overflow: 'hidden',
  clip: 'rect(0 0 0 0)',
  whiteSpace: 'nowrap',
} as const;

/**
 * `ref` estable para enfocar un campo al montarlo (reemplaza `autoFocus`, que la regla de accesibilidad prohíbe): como la función
 * no cambia entre renderizados, React la llama una sola vez, al insertar el nodo.
 */
export function enfocarNodo(nodo: HTMLElement | null): void {
  nodo?.focus();
}
