/**
 * Lo que va en el `PUT` de preferencias: solo los tipos cuyo valor editado difiere del guardado (el servidor deja los
 * demás como estaban). Un tipo que no viene en `guardadas` vale `true` (el valor por defecto).
 */
export function cambiosDePreferencias(guardadas: Readonly<Record<string, boolean>>, editados: Readonly<Record<string, boolean>>): Record<string, boolean> {
  const cambios: Record<string, boolean> = {};
  for (const [tipo, valor] of Object.entries(editados)) {
    if ((guardadas[tipo] ?? true) !== valor) cambios[tipo] = valor;
  }
  return cambios;
}
