import { expect, test, type FrameLocator, type Page } from '@playwright/test';
import { abrirEmbebido } from '../soporte/embebido';
import { irASeccion } from '../soporte/empresa';
import { crearEscenario } from '../soporte/semilla';

/**
 * Barra superior de la web de empresa (NAV-1…NAV-6): pestañas con «Más» adaptativo. A cualquier ancho, ni la barra ni
 * la página se desplazan en horizontal; las pestañas que no caben pasan a «Más» y Configuración es un engranaje.
 */

const SECCIONES = ['Inicio', 'Bandeja', 'Excepciones', 'Revalidaciones', 'Vencimientos', 'Cumplimiento', 'Certificados', 'Vínculos', 'Informes', 'Auditoría'];
const ALTO = 800;

/** Pestañas a la vista y, si hay «Más», el botón (con su texto). */
async function estadoDeLaBarra(app: FrameLocator) {
  const barra = app.getByRole('navigation', { name: 'Secciones' });
  const enlaces = await barra.getByRole('link').allInnerTexts();
  const mas = barra.locator('button[aria-haspopup="menu"]');
  return { barra, enlaces, mas, hayMas: (await mas.count()) > 0 };
}

/** Todo lo visible de la barra cabe en ella y la página del iframe no tiene scroll horizontal. */
async function sinDesplazamientoHorizontal(app: FrameLocator) {
  const medidas = await app.getByRole('navigation', { name: 'Secciones' }).evaluate((barra) => {
    const limite = barra.getBoundingClientRect().right + 0.5;
    const desbordados = Array.from(barra.children)
      .filter((hijo) => hijo.getAttribute('aria-hidden') !== 'true' && hijo.getClientRects().length > 0)
      .filter((hijo) => hijo.getBoundingClientRect().right > limite)
      .map((hijo) => hijo.textContent);
    const raiz = barra.ownerDocument.documentElement;
    return { desbordados, paginaDesborda: raiz.scrollWidth > raiz.clientWidth };
  });
  expect(medidas.desbordados).toEqual([]);
  expect(medidas.paginaDesborda).toBe(false);
}

/** Cuántas secciones se alcanzan: las pestañas a la vista más las del menú «Más» (se abre y se cierra). */
async function seccionesAlcanzables(page: Page, app: FrameLocator): Promise<string[]> {
  const { enlaces, mas, hayMas } = await estadoDeLaBarra(app);
  if (!hayMas) return enlaces;
  await mas.click();
  const items = await app.getByRole('menu', { name: 'Más secciones' }).getByRole('menuitem').allInnerTexts();
  await page.keyboard.press('Escape');
  await expect(app.getByRole('menu')).toHaveCount(0);
  return [...enlaces, ...items];
}

for (const ancho of [1536, 1100, 700]) {
  test(`a ${String(ancho)} px la barra no desplaza, todas las secciones están a un clic y Configuración es un engranaje`, async ({ page }) => {
    const e = await crearEscenario();
    await page.setViewportSize({ width: ancho, height: ALTO });
    const ana = await abrirEmbebido(page, e, e.ana, e.norte, 'inicio');
    await expect(ana.getByRole('heading', { level: 1, name: 'Inicio' })).toBeVisible();

    await sinDesplazamientoHorizontal(ana);
    expect(await seccionesAlcanzables(page, ana)).toEqual(SECCIONES);
    if (ancho <= 1100) expect((await estadoDeLaBarra(ana)).hayMas).toBe(true);
    await expect(ana.getByRole('navigation', { name: 'Secciones' }).getByRole('link', { name: 'Configuración' })).toHaveCount(0);
    const engranaje = ana.getByRole('link', { name: 'Configuración', exact: true });
    await expect(engranaje).toBeVisible();
    await expect(engranaje).not.toHaveAttribute('aria-current', 'page');
    await page.screenshot({ path: `test-results/navegacion-${String(ancho)}-inicio.png` });

    // La última sección: si no cabe, el botón «Más» lleva su nombre y queda marcado como activo.
    await irASeccion(ana, 'Auditoría');
    await expect(ana.getByRole('heading', { level: 1, name: 'Auditoría' })).toBeVisible();
    const { barra, mas, hayMas } = await estadoDeLaBarra(ana);
    const activa = barra.locator('[aria-current="page"]');
    await expect(activa).toHaveCount(1);
    await expect(activa).toHaveText('Auditoría');
    if (hayMas) {
      await expect(mas).toHaveText('Auditoría');
      await expect(mas).toHaveAttribute('aria-current', 'page');
      await mas.click();
      await expect(ana.getByRole('menuitem', { name: 'Auditoría', exact: true })).toHaveAttribute('aria-current', 'page');
      await page.screenshot({ path: `test-results/navegacion-${String(ancho)}-mas-abierto.png`, animations: 'disabled' });
      await page.keyboard.press('Escape');
    }
    await sinDesplazamientoHorizontal(ana);
    await page.screenshot({ path: `test-results/navegacion-${String(ancho)}-activa-en-mas.png` });

    // Configuración: el engranaje queda activo y ninguna pestaña lo está; su barra lateral sigue igual.
    await engranaje.click();
    await expect(ana.getByRole('navigation', { name: 'Configuración' })).toBeVisible();
    await expect(engranaje).toHaveAttribute('aria-current', 'page');
    await expect(barra.locator('[aria-current="page"]')).toHaveCount(0);
    await sinDesplazamientoHorizontal(ana);
    await page.screenshot({ path: `test-results/navegacion-${String(ancho)}-configuracion.png` });
  });
}

test('al cambiar el ancho de la ventana la barra se recalcula sin recargar y sin bucles', async ({ page }) => {
  const e = await crearEscenario();
  await page.setViewportSize({ width: 1536, height: ALTO });
  const ana = await abrirEmbebido(page, e, e.ana, e.norte, 'bandeja');
  await expect(ana.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
  const aLaVista = async () => (await estadoDeLaBarra(ana)).enlaces.length;
  const ancha = await aLaVista();

  await page.setViewportSize({ width: 700, height: ALTO });
  await expect.poll(aLaVista).toBeLessThan(ancha);
  await expect((await estadoDeLaBarra(ana)).mas).toBeVisible();
  await sinDesplazamientoHorizontal(ana);
  expect(await seccionesAlcanzables(page, ana)).toEqual(SECCIONES);

  await page.setViewportSize({ width: 1536, height: ALTO });
  await expect.poll(aLaVista).toBe(ancha);
  await sinDesplazamientoHorizontal(ana);
});

test('en modo oscuro y con el menú «Más» abierto se ve bien, y el menú lleva a la sección elegida', async ({ page }) => {
  const e = await crearEscenario();
  await page.setViewportSize({ width: 1100, height: ALTO });
  const ana = await abrirEmbebido(page, e, e.ana, e.norte, 'bandeja');
  await expect(ana.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
  await ana.getByRole('button', { name: 'Usar tema oscuro' }).click();

  const { mas } = await estadoDeLaBarra(ana);
  await mas.click();
  // Con el menú abierto el resto de la página queda oculto a los lectores de pantalla (aria-hidden de MUI): se busca por CSS.
  await expect(ana.locator('nav[aria-label="Secciones"] button[aria-haspopup="menu"]')).toHaveAttribute('aria-expanded', 'true');
  await expect(ana.getByRole('menu', { name: 'Más secciones' })).toBeVisible();
  await page.screenshot({ path: 'test-results/navegacion-1100-oscuro-mas-abierto.png', animations: 'disabled' });

  // Un ítem del menú es un enlace de verdad: navega y el menú se cierra.
  await ana.getByRole('menuitem', { name: 'Auditoría', exact: true }).click();
  await expect(ana.getByRole('heading', { level: 1, name: 'Auditoría' })).toBeVisible();
  await expect(ana.getByRole('menu')).toHaveCount(0);
  await expect(mas).toHaveText('Auditoría');
  await page.screenshot({ path: 'test-results/navegacion-1100-oscuro-activa-en-mas.png' });
});
