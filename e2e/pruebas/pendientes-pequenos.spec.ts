import { expect, test, type Browser, type FrameLocator, type Locator } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { CLAVE_PLATAFORMA, llamar } from '../soporte/api';
import { capturar } from '../soporte/capturas';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, regionDeArea } from '../soporte/empresa';
import { publicarFlujoDeFicha } from '../soporte/ficha';
import { crearTipoDeVinculo, invitarVinculo } from '../soporte/flujos';
import { abrirConsolaDePlataforma } from '../soporte/plataforma';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Empresa, type Escenario, type Persona } from '../soporte/semilla';

/**
 * Pendientes pequeños (PP): el detalle del vínculo muestra el correo sin cortes feos y el historial con sus rótulos en mayúscula (PP-2, PP-3);
 * un vínculo bloqueado se revalida al desbloquearlo (PP-9: B-5, VR-10 (1)) y la cuenta de Infolaft es de la plataforma, no de cada empresa (PP-5…PP-8).
 * La salida del vínculo de la bandeja del oficial al confirmar una coincidencia del monitoreo (PP-1, HI-5) se prueba en `cumplimiento-vigilancia.spec.ts`.
 */

const RUT = 'Registro Único Tributario';

async function ventanaDe(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa, ruta = 'bandeja'): Promise<FrameLocator> {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, empresa, ruta);
}

/**
 * Dónde se parte un texto en líneas: las letras que quedan al final de una línea que sigue en otra. Un correo que cabe en una línea no se parte; si no
 * cabe, solo puede partirse justo después de la «@» (nunca a mitad del usuario o del dominio).
 */
async function letrasAntesDeUnSalto(elemento: Locator): Promise<string[]> {
  return elemento.evaluate((el) => {
    const salidas: string[] = [];
    const recorrido = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    let anterior: { letra: string; arriba: number } | null = null;
    for (let nodo = recorrido.nextNode(); nodo; nodo = recorrido.nextNode()) {
      const texto = nodo.textContent ?? '';
      for (let i = 0; i < texto.length; i++) {
        const rango = document.createRange();
        rango.setStart(nodo, i);
        rango.setEnd(nodo, i + 1);
        const caja = rango.getClientRects()[0];
        if (!caja) continue;
        if (anterior && Math.abs(caja.top - anterior.arriba) > 4) salidas.push(anterior.letra);
        anterior = { letra: texto[i] ?? '', arriba: caja.top };
      }
    }
    return salidas;
  });
}

test('El detalle del vínculo muestra el correo sin cortes feos y su historial dice «Consentimientos registrados» con mayúscula', async ({ page, browser }) => {
  test.setTimeout(180_000);
  const e = await crearEscenario();
  const tercero = nuevoTercero('Aceros del Pulido S.A.S.');
  const invitacion = await invitar(e, e.norte, tercero);

  // El tercero acepta los textos legales: el vínculo registra sus consentimientos.
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);

  const detalle = await ventanaDe(browser, e, e.ana, e.norte, `vinculos/${invitacion.vinculoId}`);
  await expect(detalle.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
  const ventana = detalle.owner().page();
  await expect(detalle.getByText('Invitación aceptada', { exact: true })).toBeVisible();
  await capturar(ventana, 'pendientes-vinculo-detalle');

  // El historial: «Consentimientos registrados» (antes salía en minúscula); ningún rótulo de la línea de tiempo empieza en minúscula.
  await expect(detalle.getByText('Consentimientos registrados', { exact: true })).toBeVisible();
  await expect(detalle.getByText('consentimientos registrados', { exact: true })).toHaveCount(0);
  const titulos = await detalle.locator('section[aria-label] > ol > li > div > p:first-of-type').allTextContents();
  expect(titulos.length).toBeGreaterThan(2);
  expect(titulos.filter((t) => t !== t.charAt(0).toLocaleUpperCase('es-CO') + t.slice(1))).toEqual([]);

  // «Datos del vínculo»: el correo de contacto (largo) no se parte a mitad de línea; si no cabe, se parte en la «@» y se ve entero al pasar el cursor.
  const correo = detalle.locator('dt', { hasText: /^Contacto$/ }).locator('xpath=following-sibling::dd[1]');
  await expect(correo).toContainText(tercero.correo);
  expect((await letrasAntesDeUnSalto(correo)).filter((letra) => letra !== '@')).toEqual([]);
  for (const ancho of [1280, 1024, 700]) {
    await ventana.setViewportSize({ width: ancho, height: 900 });
    await expect(correo).toContainText(tercero.correo);
    expect((await letrasAntesDeUnSalto(correo)).filter((letra) => letra !== '@')).toEqual([]);
  }
});

test('Un vínculo bloqueado no recibe la versión nueva y, al desbloquearlo, queda «En revisión» con el certificado «En verificación»', async ({ page, browser }) => {
  test.setTimeout(300_000);
  const e = await crearEscenario();
  await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
  await publicarFlujoDeFicha(e, e.norte, 'ALIADOS_NORTE', e.norte.areas.compras!, 'ALIADO', [{ tipoDocumento: 'RUT' }]);
  const tercero = nuevoTercero('Aceros Bloqueados S.A.S.');
  const invitacion = await invitarVinculo(e, e.norte, tercero, 'ALIADO');

  // El tercero se valida con su RUT y Compras lo aprueba: vínculo activo y certificado vigente.
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await subirDocumento(page, RUT, pdf('RUT bloqueo v1'));
  await esperarEstadoDelTercero(page, 'EnRevision');
  const ruben = await ventanaDe(browser, e, e.ruben, e.norte);
  await expect(async () => {
    await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
    await expect(regionDeArea(ruben, 'Compras').getByRole('button', { name: /^Aprobar área/ })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await aprobarArea(ruben, 'Compras');
  await esperarEstadoDelTercero(page, 'Aprobado');

  // Una administradora lo bloquea.
  const ana = await ventanaDe(browser, e, e.ana, e.norte, `vinculos/${invitacion.vinculoId}`);
  await expect(ana.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
  await ana.getByRole('button', { name: 'Más acciones' }).click();
  await ana.getByRole('menuitem', { name: /^Bloquear/ }).click();
  const bloquear = ana.getByRole('dialog', { name: /^Bloquear vínculo/ });
  await bloquear.getByRole('textbox', { name: 'Motivo' }).fill('Revisión de sanciones.');
  await bloquear.getByRole('button', { name: 'Bloquear vínculo' }).click();
  await expect(bloquear).toBeHidden();
  await expect(ana.getByText('Revisión de sanciones.').first()).toBeVisible();

  // Mientras está bloqueado, el tercero sube un RUT nuevo desde su bóveda: no le llega al vínculo bloqueado (B-5).
  await page.goto('/boveda');
  await page.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf('RUT bloqueo v2'));
  await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });
  await expect(ana.getByRole('main')).not.toContainText('En verificación');

  // Al desbloquearlo se pone al día: se abre la actualización, el vínculo queda «En revisión» y el certificado «En verificación» (VR-3, VR-11).
  await ana.getByRole('button', { name: /^Desbloquear/ }).click();
  const desbloquear = ana.getByRole('dialog', { name: /^Desbloquear vínculo/ });
  await desbloquear.getByRole('textbox', { name: 'Justificación' }).fill('Se levantó la sanción.');
  await desbloquear.getByRole('button', { name: 'Desbloquear vínculo' }).click();
  await expect(desbloquear).toBeHidden();
  await expect(async () => {
    await ana.owner().page().reload();
    await expect(ana.getByText(/^En revisión\. En revisión desde el /).first()).toBeVisible({ timeout: 4000 });
  }).toPass({ timeout: 60_000 });
  await expect(ana.getByRole('main')).toContainText(new RegExp(`En verificación\\s*${e.norte.prefijo}-C-\\d{4}-\\d{6}-1`));
  await capturar(ana.owner().page(), 'pendientes-desbloqueo-en-revision');
  // El tercero ve lo mismo: su revisión con la empresa vuelve a estar en revisión por el RUT nuevo.
  await page.goto('/empresas');
  await expect(page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.norte.nombre, exact: true }) }).getByText('En revisión', { exact: true }))
    .toBeVisible({ timeout: 45_000 });
});

test('La cuenta de Infolaft es de la plataforma: Plataforma la configura y la empresa solo ve que está disponible, sin credenciales', async ({ page, browser }) => {
  test.setTimeout(180_000);
  const e = await crearEscenario();
  const quitar = () =>
    llamar('PUT', '/api/v1/plataforma/listas-restrictivas', { llave: CLAVE_PLATAFORMA, cuerpo: { credenciales: { proveedor: 'Infolaft', clave: '' } } });
  await quitar();
  try {
    // La empresa, sin cuenta de la plataforma: Infolaft figura «No disponible» y no pide usuario ni clave.
    const sinCuenta = await ventanaDe(browser, e, e.ana, e.norte, 'listas-restrictivas');
    await expect(sinCuenta.getByRole('heading', { level: 1, name: 'Listas restrictivas' })).toBeVisible();
    const infolaftSin = sinCuenta.getByRole('region', { name: 'Infolaft', exact: true });
    await expect(infolaftSin.getByText('No disponible', { exact: true })).toBeVisible();
    await expect(sinCuenta.getByRole('textbox', { name: 'Usuario' })).toHaveCount(2); // solo los del legado (Stradata y Compliance)

    // Plataforma la configura desde su consola: la clave es de solo escritura y queda «Configurada · …9876».
    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('link', { name: 'Listas restrictivas' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Listas restrictivas' })).toBeVisible();
    await expect(consola.getByText('Sin configurar', { exact: true })).toBeVisible();
    await consola.getByRole('textbox', { name: 'Usuario de la cuenta' }).fill('sinco-plataforma');
    await consola.getByLabel('Clave', { exact: true }).fill('clave-e2e-9876');
    await consola.getByRole('textbox', { name: 'Plan contratado' }).fill('Empresarial');
    await consola.getByRole('textbox', { name: 'Cuota mensual de cada empresa' }).fill('800');
    await capturar(page, 'plataforma-listas-restrictivas-antes');
    await consola.getByRole('button', { name: 'Guardar cuenta' }).click();
    await expect(consola.getByText('Guardamos la cuenta de Infolaft.')).toBeVisible();
    await expect(consola.getByText('Configurada · …9876', { exact: true })).toBeVisible();
    await expect(consola.getByLabel('Clave', { exact: true })).toHaveValue('');
    await capturar(page, 'plataforma-listas-restrictivas');

    // La empresa, con la cuenta configurada: «Disponible» con su cuota, sin usuario ni clave de Infolaft.
    const conCuenta = await ventanaDe(browser, e, e.ana, e.norte, 'listas-restrictivas');
    const infolaft = conCuenta.getByRole('region', { name: 'Infolaft', exact: true });
    await expect(infolaft.getByText('Disponible', { exact: true })).toBeVisible();
    await expect(infolaft).toContainText('800 búsquedas');
    await expect(infolaft.getByRole('textbox')).toHaveCount(0);
    await expect(conCuenta.getByText('sinco-plataforma')).toHaveCount(0);
    await capturar(conCuenta.owner().page(), 'empresa-listas-restrictivas-cuenta-de-la-plataforma');
  } finally {
    await quitar();
  }
});
