import { expect, test, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { CLAVE_PLATAFORMA, llamar } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { abrirConsolaDePlataforma } from '../soporte/plataforma';
import { escribirFecha } from '../soporte/fechas';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento, tarjeta } from '../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha, SST } from '../soporte/semilla';

/**
 * Pulido visual de toda la aplicación (UI-1…UI-9, docs/06 §2.4): cargando = skeleton (nunca texto «Cargando…» ni un spinner de
 * página), botones compactos, el campo de autocompletar con su ícono dentro y en español, nada de contenedores cuadrados y la
 * consola sin layouts volcados a la izquierda. Además de comprobarlo, deja las capturas que se revisan a ojo en
 * `E2E_CAPTURAS` (por omisión `test-results/pulido`).
 */
const CAPTURAS = process.env.E2E_CAPTURAS ?? join('test-results', 'pulido');
mkdirSync(CAPTURAS, { recursive: true });
const captura = (nombre: string) => join(CAPTURAS, `${nombre}.png`);

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';
const RAZON_SOCIAL = 'Aceros Pulido S.A.S.';

const rut = (nit: string, direccion: string, version: string) =>
  pdf(`RUT ${version} [[nit=${nit}]][[razonSocial=${RAZON_SOCIAL}]][[direccion=${direccion}]][[codigoMunicipio=11001]][[actividadEconomicaPrincipal=4663]][[responsabilidades=05,48]]`);
const camara = (nit: string, razonSocial: string, version: string) => pdf(`Camara ${version} [[nit=${nit}]][[razonSocial=${razonSocial}]]`);

/** Diligencia Información general (catálogos incluidos) y la guarda. */
async function diligenciarGeneral(page: Page): Promise<void> {
  await expect(page.getByRole('heading', { level: 1, name: 'Información general' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Razón social' }).fill(RAZON_SOCIAL);
  await page.getByRole('textbox', { name: 'Correo electrónico' }).fill('compras@aceros-pulido.co');
  await page.getByRole('textbox', { name: 'Teléfono' }).fill('601 555 1234');
  await page.getByRole('combobox', { name: 'País' }).fill('Colombia');
  await page.getByRole('option', { name: 'Colombia' }).first().click();
  await page.getByRole('combobox', { name: 'Ciudad' }).fill('Bogot');
  await page.getByRole('option', { name: /^Bogotá/ }).first().click();
  await page.getByRole('textbox', { name: 'Dirección', exact: true }).fill('Calle 45 # 12-08');
  await page.getByRole('combobox', { name: 'Actividad económica principal (CIIU)' }).fill('4663');
  await page.getByRole('option', { name: /^4663/ }).first().click();
  const barra = page.getByRole('group', { name: 'Guardar la sección' });
  await expect(barra.getByText('Lista: puedes continuar')).toBeVisible();
  await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByText('Guardaste Información general: está completa.')).toBeVisible();
}

/** Sube una versión nueva desde la bóveda. */
async function subirVersionNueva(page: Page, nombre: string, archivo: ReturnType<typeof pdf>, version: number): Promise<void> {
  await page.goto('/boveda');
  await page.getByRole('article', { name: nombre }).getByRole('link', { name: 'Subir versión' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(archivo);
  await expect(page.getByText(`Es tu versión ${String(version)} de ${nombre}`)).toBeVisible({ timeout: 30_000 });
}

/** Los botones (`Button` de MUI) visibles de la página más altos que `maximo` px, como «nombre: alto». */
async function botonesMasAltosQue(page: Page, maximo: number): Promise<string[]> {
  return page.locator('main .MuiButton-root').evaluateAll(
    (botones, tope) =>
      botones
        .filter((b) => (b as HTMLElement).offsetParent !== null)
        .map((b) => ({ nombre: (b.getAttribute('aria-label') ?? b.textContent).trim(), alto: Math.round(b.getBoundingClientRect().height) }))
        .filter((b) => b.alto > tope)
        .map((b) => `${b.nombre}: ${String(b.alto)}`),
    maximo,
  );
}

test('el wizard, Mi información y los campos del tercero: avisos agrupados, botones compactos, autocompletar y acordeón con el mismo acabado', async ({ page }) => {
  test.setTimeout(240_000);
  const e = await crearEscenario();
  await activarIa(e, e.sur);
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_PULIDO', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { tipoDocumento: 'CAMARA_COMERCIO' }, { seccion: 'FICHA_GENERAL' }]);
  const tercero = nuevoTercero(RAZON_SOCIAL);
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  const nit = tercero.identificacion.numero;

  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await page.getByRole('link', { name: /^Completar/ }).first().click();
  await diligenciarGeneral(page);
  // Una sección más con datos (un borrador de la tributaria): el RUT nuevo también la toca y el aviso lo agrupa.
  await page.goto('/ficha/tributaria');
  await page.getByRole('combobox', { name: /^Responsabilidades/ }).click();
  await page.getByRole('option').first().click();
  await page.keyboard.press('Escape');
  await page.getByRole('group', { name: 'Guardar la sección' }).getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByText(/^Guardaste Información tributaria/)).toBeVisible();
  await page.goto('/empresas');
  await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
  await subirDocumento(page, RUT, rut(nit, 'Calle 45 # 12-08', 'v1'));
  await subirDocumento(page, CAMARA, camara(nit, RAZON_SOCIAL, 'v1'));
  await esperarEstadoDelTercero(page, 'EnRevision');

  // Versiones nuevas con otra dirección (RUT) y otra razón social (Cámara): dos documentos más nuevos que la ficha.
  await subirVersionNueva(page, RUT, rut(nit, 'Cl 80 # 10-20', 'v2'), 2);
  await subirVersionNueva(page, CAMARA, camara(nit, 'ACEROS PULIDO Y CIA S.A.S.', 'v2'), 2);

  // Mi información: un solo aviso compacto por documento con las secciones que afecta.
  await page.goto('/ficha');
  const aviso = page.getByRole('alert', { name: 'Tienes documentos más nuevos que tu información' });
  await expect(aviso).toContainText('Cámara de Comercio', { timeout: 45_000 });
  await expect(aviso).toContainText('RUT');
  await expect(page.getByText('Cargando')).toHaveCount(0);
  await page.screenshot({ path: captura('mi-informacion'), fullPage: true });
  const filasDeAviso = await aviso.getByRole('link', { name: /^Actualizar desde mi/ }).count();
  expect(filasDeAviso).toBe(2);
  // Cada documento es un bloque con su borde, separado del siguiente, y el subtítulo del aviso es una línea corta.
  const bloques = aviso.locator('li').filter({ has: page.getByRole('link', { name: /^Actualizar desde mi/ }) });
  await expect(bloques).toHaveCount(2);
  const primero = (await bloques.nth(0).boundingBox())!;
  const segundo = (await bloques.nth(1).boundingBox())!;
  expect(segundo.y).toBeGreaterThanOrEqual(primero.y + primero.height + 4);
  expect(await bloques.nth(0).evaluate((n) => getComputedStyle(n).borderTopWidth)).toBe('1px');
  await expect(aviso.getByText('Revisa los cambios y decide cuáles usar.')).toBeVisible();
  expect(await botonesMasAltosQue(page, 40)).toEqual([]);

  // Wizard, General: dos avisos casi iguales (RUT y Cámara) agrupados en uno, con una línea por documento.
  await aviso.getByRole('link', { name: /RUT/ }).first().click();
  await expect(page.getByRole('heading', { level: 1, name: 'Información general' })).toBeVisible();
  const propuestas = page.getByRole('alert', { name: 'Tienes documentos más nuevos que esta sección' });
  await expect(propuestas).toHaveCount(1);
  await expect(propuestas).toContainText('RUT');
  await expect(propuestas).toContainText('Cámara de Comercio');
  await page.screenshot({ path: captura('wizard-general'), fullPage: true });
  expect(await botonesMasAltosQue(page, 40)).toEqual([]);

  // Autocompletar (CIIU): el ícono queda dentro del campo, centrado, y su tooltip y textos están en español.
  const ciiu = page.getByRole('combobox', { name: 'Actividad económica principal (CIIU)' });
  await ciiu.scrollIntoViewIfNeeded();
  const campo = ciiu.locator('xpath=ancestor::div[contains(@class,"MuiAutocomplete-root")][1]');
  await page.screenshot({ path: captura('ciiu-cerrado'), clip: await recorte(campo, 24) });
  const indicador = campo.getByRole('button', { name: 'Abrir' });
  await expect(indicador).toBeVisible();
  const caja = (await campo.locator('.MuiOutlinedInput-root').boundingBox())!;
  const icono = (await indicador.boundingBox())!;
  expect(icono.x).toBeGreaterThanOrEqual(caja.x);
  expect(icono.x + icono.width).toBeLessThanOrEqual(caja.x + caja.width);
  expect(icono.y).toBeGreaterThanOrEqual(caja.y);
  expect(icono.y + icono.height).toBeLessThanOrEqual(caja.y + caja.height);
  expect(Math.abs(icono.y + icono.height / 2 - (caja.y + caja.height / 2))).toBeLessThanOrEqual(1);
  await indicador.click();
  await expect(page.getByRole('listbox')).toBeVisible();
  const zona = campo.locator('xpath=ancestor::form[1]');
  await page.screenshot({ path: captura('ciiu-abierto'), clip: await recorte(campo, 280, zona) });
  await page.keyboard.press('Escape');

  // Dirección por partes: mismo radio y borde que las tarjetas, sin la línea superior de MUI.
  const acordeon = page.locator('.MuiAccordion-root').first();
  await acordeon.scrollIntoViewIfNeeded();
  await page.screenshot({ path: captura('direccion-por-partes-cerrada'), clip: await recorte(acordeon, 16) });
  await acordeon.getByRole('button').first().click();
  await expect(acordeon.getByRole('combobox').or(acordeon.getByRole('textbox')).first()).toBeVisible();
  await page.screenshot({ path: captura('direccion-por-partes-abierta'), clip: await recorte(acordeon, 16) });
  const radio = await acordeon.evaluate((n) => getComputedStyle(n).borderTopLeftRadius);
  expect(radio).toBe('12px');

  // El celular: los botones no se inflan.
  await page.setViewportSize({ width: 390, height: 844 });
  await page.evaluate(() => window.scrollTo(0, 0));
  await page.screenshot({ path: captura('wizard-general-celular') });
  expect(await botonesMasAltosQue(page, 44)).toEqual([]);
});

/** Rectángulo de `localizador` con un margen, recortado a la página (para capturar un campo y su lista desplegada). */
async function recorte(localizador: ReturnType<Page['locator']>, margen: number, contenedor?: ReturnType<Page['locator']>) {
  const caja = (await localizador.boundingBox())!;
  const limite = contenedor ? await contenedor.boundingBox() : null;
  const alto = limite ? Math.min(caja.height + margen, Math.max(caja.height, limite.y + limite.height - caja.y)) : caja.height + margen * 2;
  return { x: Math.max(0, caja.x - 16), y: Math.max(0, caja.y - 12), width: caja.width + 32, height: alto + 12 };
}

test('la consola de Plataforma: skeleton mientras carga la lista y Plan de IA a pantalla completa en 1920×1080', async ({ page }) => {
  test.setTimeout(180_000);
  await page.setViewportSize({ width: 1920, height: 1080 });
  const e = await crearEscenario();
  const sufijo = randomUUID().replace(/-/g, '').slice(0, 6);
  await llamar('POST', '/api/v1/plataforma/tenants', { llave: CLAVE_PLATAFORMA, cuerpo: { slug: `pulido-${sufijo}`, nombre: `Grupo Pulido ${sufijo}`, region: 'co', zonaHoraria: 'America/Bogota' } });

  // La lista de grupos tarda en llegar: mientras tanto se ven filas de esqueleto, nunca «Cargando…».
  await page.route('**/api/v1/plataforma/grupos?**', async (ruta) => {
    await new Promise((resolver) => setTimeout(resolver, 3000));
    await ruta.continue();
  });
  const consola = await abrirConsolaDePlataforma(page);
  await expect(consola.getByRole('heading', { level: 1, name: 'Grupos y empresas' })).toBeVisible();
  await expect(consola.getByRole('status', { name: 'Cargando' }).first()).toBeVisible();
  await expect(consola.getByText('Cargando')).toHaveCount(0);
  await page.screenshot({ path: captura('consola-grupos-cargando') });
  await expect(consola.getByRole('list', { name: 'Grupos' })).toBeVisible({ timeout: 30_000 });
  await page.screenshot({ path: captura('consola-grupos') });

  // Plan de IA: dos columnas en pantallas anchas y todo a la vista, sin desplazarse.
  const empresaId = e.norte.id;
  await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(e.sufijo);
  await consola.getByRole('region', { name: `Grupo E2E ${e.sufijo}` }).getByRole('link', { name: `Plan de IA de ${e.norte.nombre}` }).click({ timeout: 30_000 });
  await expect(consola.getByRole('heading', { level: 1, name: /Plan de IA/ })).toBeVisible();
  expect(empresaId).not.toBe('');
  await page.screenshot({ path: captura('consola-plan-ia') });
  const hayScroll = await consola.locator('main').evaluate((n) => n.scrollHeight > n.clientHeight + 1);
  expect(hayScroll).toBe(false);
});

test('el alta de un grupo y los secretos de la consola también usan todo el ancho', async ({ page }) => {
  await page.setViewportSize({ width: 1920, height: 1080 });
  const consola = await abrirConsolaDePlataforma(page);
  await consola.getByRole('link', { name: 'Nuevo grupo' }).click();
  await expect(consola.getByRole('heading', { level: 1, name: 'Nuevo grupo' })).toBeVisible();
  await page.screenshot({ path: captura('consola-nuevo-grupo') });
  // Dos columnas: la del grupo y la empresa a la izquierda, la del administrador y el sistema a la derecha.
  const izquierda = (await consola.getByRole('region', { name: 'Grupo empresarial' }).boundingBox())!;
  const derecha = (await consola.getByRole('region', { name: 'Administrador del grupo' }).boundingBox())!;
  expect(derecha.x).toBeGreaterThan(izquierda.x + izquierda.width - 1);
  expect(izquierda.width).toBeGreaterThan(700);
  await consola.getByRole('link', { name: 'Secretos' }).click();
  await expect(consola.getByRole('heading', { level: 1, name: 'Secretos en reposo' })).toBeVisible();
  const tabla = (await consola.getByRole('table', { name: 'Secretos en reposo' }).boundingBox())!;
  expect(tabla.width).toBeGreaterThan(1500);
  await page.screenshot({ path: captura('consola-secretos') });
});

/** Controles nativos del navegador que no debe haber en ninguna pantalla (UI-7). */
const NATIVOS = 'input[type="date"], input[type="time"], input[type="month"], input[type="datetime-local"], input[type="number"], select';

test('Información financiera: sin controles nativos, año y fecha de corte en la misma fila y calendario en español con el estilo de la app', async ({ page }) => {
  test.setTimeout(240_000);
  const e = await crearEscenario();
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_FECHA', e.sur.areas.revision!, [{ seccion: 'FICHA_FINANCIERA' }]);
  const invitacion = await invitar(e, e.sur, nuevoTercero('Tornillos Fecha S.A.S.'), 'CLIENTE');
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await page.goto('/ficha/financiera');
  await expect(page.getByRole('heading', { level: 1, name: /Información financiera/ })).toBeVisible();
  const barra = page.getByRole('group', { name: 'Guardar la sección' });

  // Año y fecha de corte son campos cortos: comparten fila (cada uno de media columna), no ocupan todo el ancho.
  const anio = (await page.getByRole('combobox', { name: /^Año \(periodo\)/ }).boundingBox())!;
  const fecha = page.getByRole('group', { name: 'Fecha de corte' });
  const caja = (await fecha.boundingBox())!;
  expect(Math.abs(anio.y + anio.height / 2 - (caja.y + caja.height / 2))).toBeLessThanOrEqual(3);
  expect(caja.x).toBeGreaterThan(anio.x + anio.width - 1);
  const formulario = (await page.getByRole('form', { name: /Información financiera/ }).boundingBox())!;
  expect(caja.width).toBeLessThan(formulario.width * 0.6);
  expect(anio.width).toBeLessThan(formulario.width * 0.6);

  // Escribir con el teclado: dd/mm/aaaa en español, con el formato visible desde antes de enfocar.
  await expect(fecha.getByRole('spinbutton', { name: 'Día' })).toHaveText('dd');
  await escribirFecha(page, 'Fecha de corte', '2025-12-31');
  expect(await page.locator(NATIVOS).count()).toBe(0);

  // Calendario: tarjeta con el estilo de los menús, mes en español y lunes primero.
  await fecha.getByRole('button', { name: /^Elegir fecha/ }).click();
  const calendario = page.getByRole('dialog');
  await expect(calendario.getByText(/diciembre 2025/i)).toBeVisible();
  await expect(calendario.getByRole('button', { name: 'Hoy' })).toBeVisible();
  await expect(page.locator('.MuiPickerPopper-paper')).toHaveCSS('border-top-left-radius', '12px');
  // La captura se toma con la tarjeta ya completamente opaca (termina su animación de entrada).
  await expect(page.locator('.MuiPickerPopper-paper')).toHaveCSS('opacity', '1');
  const zona = (await page.getByRole('form', { name: /Información financiera/ }).boundingBox())!;
  await page.screenshot({ path: captura('financiera-calendario'), clip: { x: Math.max(0, zona.x - 16), y: Math.max(0, caja.y - 56), width: zona.width + 32, height: 520 } });

  // Elegir un día en el calendario pasa la fecha al campo; lo que se guarda viaja como aaaa-mm-dd.
  await calendario.getByRole('gridcell', { name: '15', exact: true }).click();
  await expect(fecha.getByRole('spinbutton', { name: 'Día' })).toHaveText('15');
  await expect(fecha.getByRole('spinbutton', { name: 'Mes' })).toHaveText('12');
  const guardado = page.waitForRequest((r) => r.method() !== 'GET' && (r.postData() ?? '').includes('fecha_corte'));
  await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
  expect((await guardado).postData()).toContain('2025-12-15');
  await expect(page.getByText(/^Guardaste Información financiera/)).toBeVisible();

  // Al volver, la fecha guardada se muestra como dd/mm/aaaa.
  await page.reload();
  await expect(page.getByRole('group', { name: 'Fecha de corte' }).getByRole('spinbutton', { name: 'Día' })).toHaveText('15');
  await page.screenshot({ path: captura('financiera-fila-anio-fecha'), fullPage: true });
});

test('el formulario de un documento con fecha usa el mismo selector (sin <input type="date">) y valida la expedición futura', async ({ page }) => {
  test.setTimeout(180_000);
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero('Pernos Fecha S.A.S.'));
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await tarjeta(page, SST.nombre).getByRole('link', { name: /^Subir/ }).click();
  await expect(page.getByRole('heading', { level: 1, name: `Subir ${SST.nombre}` })).toBeVisible();
  const fecha = page.getByRole('group', { name: /^Fecha de expedición/ });
  await expect(fecha).toBeVisible();
  expect(await page.locator(NATIVOS).count()).toBe(0);

  // Una fecha futura no se admite; el aviso va en el campo y el botón de elegir archivo sigue deshabilitado.
  const manana = new Date(Date.now() + 3 * 86_400_000);
  await escribirFecha(page, /^Fecha de expedición/, new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(manana));
  await expect(page.getByRole('button', { name: 'Elegir archivo' })).toBeDisabled();
  await fecha.getByRole('button', { name: /^Elegir fecha/ }).click();
  await expect(page.getByRole('dialog').getByRole('button', { name: 'Hoy' })).toBeVisible();
  await expect(page.locator('.MuiPickerPopper-paper')).toHaveCSS('opacity', '1');
  await page.screenshot({ path: captura('documento-con-fecha'), fullPage: true });
  await page.getByRole('dialog').getByRole('button', { name: 'Hoy' }).click();
  await expect(page.getByRole('button', { name: 'Elegir archivo' })).toBeEnabled();
});
