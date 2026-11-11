import { expect, type Locator, type Page } from '@playwright/test';

/** `aaaa-mm-dd` (la fecha de la API) → `dd/mm/aaaa`, como la muestran el selector de fecha y las pantallas. */
export function enPantalla(iso: string): string {
  const [a, m, d] = iso.split('-');
  return `${d ?? ''}/${m ?? ''}/${a ?? ''}`;
}

/** ¿Es una fecha ISO `aaaa-mm-dd`? Los helpers que llenan campos por etiqueta la usan para elegir cómo escribir. */
export const esFechaIso = (valor: string): boolean => /^\d{4}-\d{2}-\d{2}$/.test(valor);

/**
 * Escribe una fecha ISO en un `CampoFecha` (UI-7): el selector no es un `<input type="date">` sino un grupo con las secciones
 * `dd/mm/aaaa`, así que se hace clic en la del día y se teclean los ocho dígitos (cada sección salta a la siguiente sola).
 * `etiqueta` es la del campo (con o sin el asterisco de obligatorio).
 */
export async function escribirFecha(pagina: Page, etiqueta: string | RegExp, iso: string, dentroDe?: Locator): Promise<void> {
  const [a, m, d] = iso.split('-');
  const campo = (dentroDe ?? pagina).getByRole('group', { name: etiqueta });
  await campo.getByRole('spinbutton', { name: 'Día' }).click();
  await pagina.keyboard.type(`${d ?? ''}${m ?? ''}${a ?? ''}`);
  await expect(campo.getByRole('spinbutton', { name: 'Año' })).toHaveText(a ?? '');
}
