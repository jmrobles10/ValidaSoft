import { expect, test, type Browser, type FrameLocator, type Page } from '@playwright/test';
import { llamar } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, irASeccion, regionDeArea } from '../soporte/empresa';
import { completarInvitacionYEsperarRevisionOAprobacion, esperarEstadoDelTercero } from '../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha, type Escenario, type Tercero } from '../soporte/semilla';

/**
 * Revisión automática con IA por niveles (ADR-0012, H7): Ana (administradora del grupo) activa el nivel Automático en
 * Configuración › Revisión automática; el sistema aprueba solo lo que no tiene excepciones, manda a una persona lo dudoso y deja
 * en Excepciones el muestreo que Sofía (Revisión de Sur) confirma o revierte. El análisis lo hace el proveedor simulado, que
 * lee marcas `[[clave=valor]]` del PDF (`confianza` por omisión 0,97).
 */

const RUT = 'Registro Único Tributario';
const BANCARIA = 'Certificación bancaria';

/** RUT que lee el analizador simulado; `marcas` agrega, p. ej., `[[confianza=0.55]]`. */
function rut(tercero: Tercero, marcas = '') {
  return pdf(`RUT ${tercero.nombre} [[nit=${tercero.identificacion.numero}]][[razonSocial=${tercero.nombre}]]${marcas}`);
}

/** Captura la pantalla con el tamaño dado (el iframe ocupa el alto de la ventana) y vuelve al tamaño anterior. */
async function capturar(page: Page, nombre: string, ancho: number, alto: number): Promise<void> {
  const anterior = page.viewportSize();
  await page.setViewportSize({ width: ancho, height: alto });
  await page.waitForTimeout(500);
  await page.screenshot({ path: `${process.env.E2E_CAPTURAS ?? 'test-results'}/${nombre}.png`, fullPage: true });
  if (anterior) await page.setViewportSize(anterior);
}

/** Va a Excepciones pasando antes por otra sección, para que la lista se monte de nuevo y se pida otra vez. */
async function abrirExcepciones(app: FrameLocator): Promise<void> {
  await irASeccion(app, 'Vínculos');
  const barra = app.getByRole('navigation', { name: 'Secciones' });
  const pestana = barra.getByRole('link', { name: /^Excepciones/ });
  if ((await pestana.count()) > 0) await pestana.click();
  else {
    await barra.locator('button[aria-haspopup="menu"]').click();
    await app.getByRole('menuitem', { name: /^Excepciones/ }).click();
  }
  await expect(app.getByRole('heading', { level: 1, name: 'Excepciones' })).toBeVisible();
}

/** La tarjeta de una excepción en la lista, por el nombre del tercero. */
function fila(app: FrameLocator, tercero: Tercero) {
  return app.getByRole('listitem').filter({ has: app.getByRole('heading', { level: 3, name: tercero.nombre }) });
}

/** Excepciones se vuelve a pedir al abrirla: se entra de nuevo hasta que aparezca la fila esperada (la revisión corre en segundo plano). */
async function esperarFila(app: FrameLocator, tercero: Tercero): Promise<void> {
  await expect(async () => {
    await abrirExcepciones(app);
    await expect(fila(app, tercero)).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 60_000 });
}

/** Cliente de Sur que sube su RUT (y la certificación bancaria, si el flujo la pide) y queda «En revisión». */
async function terceroConDocumentos(e: Escenario, browser: Browser, nombre: string, marcas = '', conBancaria = false) {
  const tercero = nuevoTercero(nombre);
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  const pagina = await (await browser.newContext()).newPage();
  const documentos = [{ nombre: RUT, archivo: rut(tercero, marcas) }];
  if (conBancaria) documentos.push({ nombre: BANCARIA, archivo: pdf(`Certificacion bancaria ${tercero.nombre}`) });
  // En Automático el limpio se aprueba solo y puede hacerlo antes de que la página alcance a mostrar «En revisión».
  await completarInvitacionYEsperarRevisionOAprobacion(pagina, invitacion, documentos);
  return { tercero, pagina };
}

test('Ana activa Automático desde Configuración: el sistema aprueba solo lo limpio y manda a una persona lo dudoso, que cae en Excepciones', async ({ browser }) => {
  test.setTimeout(300_000);
  const e = await crearEscenario();
  await activarIa(e, e.sur);
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_RA', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }]);

  // Ana (administradora del grupo) configura Sur: sin configuración guardada es Manual.
  const paginaAna = await (await browser.newContext()).newPage();
  const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'revision-automatica');
  await expect(ana.getByRole('heading', { level: 1, name: 'Revisión automática' })).toBeVisible();
  await expect(ana.getByRole('radio', { name: /^Manual/ })).toBeChecked();
  await ana.getByRole('radio', { name: /^Automático/ }).check();
  await expect(ana.getByText('En Automático el sistema aprobará documentos sin que una persona los vea, salvo el muestreo.')).toBeVisible();

  // Sin tipos ni áreas no se puede guardar; elige solo el RUT y el área Revisión, y sin muestra (todo lo limpio se aprueba solo).
  await expect(ana.getByRole('button', { name: 'Guardar', exact: true })).toBeDisabled();
  await ana.getByRole('combobox', { name: 'Tipos de documento' }).click();
  await ana.getByRole('option', { name: RUT }).click();
  await paginaAna.keyboard.press('Escape');
  await ana.getByRole('combobox', { name: 'Áreas cuya revisión hace el sistema' }).click();
  await ana.getByRole('option', { name: 'Revisión' }).click();
  await paginaAna.keyboard.press('Escape');
  await ana.getByRole('textbox', { name: 'Muestra para confirmar (%)' }).fill('0');
  await capturar(paginaAna, 'revision-automatica-config', 1280, 1500);
  await capturar(paginaAna, 'revision-automatica-config-movil', 390, 2800);

  // Antes de guardar avisa qué pasará.
  await ana.getByRole('button', { name: 'Guardar', exact: true }).click();
  const aviso = ana.getByRole('dialog', { name: 'Activar la aprobación automática' });
  await expect(aviso).toContainText('1 tipo de documento en 1 área');
  await aviso.getByRole('button', { name: 'Guardar y activar' }).click();
  await expect(ana.getByText('Guardaste la revisión automática: nivel Automático.')).toBeVisible();
  const guardada = await llamar<{ nivel: string; tiposAutorizados: string[]; porcentajeMuestreo: number }>('GET', '/api/v1/configuracion/revision-automatica', {
    llave: e.admin,
    empresa: e.sur.id,
  });
  expect(guardada).toMatchObject({ nivel: 'Automatico', tiposAutorizados: ['RUT'], porcentajeMuestreo: 0 });

  // Dos clientes suben su RUT: uno con la lectura limpia (confianza 0,97) y otro con una lectura dudosa (0,55).
  const limpio = await terceroConDocumentos(e, browser, 'Aceros Limpios S.A.S.');
  const dudoso = await terceroConDocumentos(e, browser, 'Aceros Dudosos S.A.S.', '[[confianza=0.55]]');

  // El limpio se aprueba solo, sin que nadie lo vea: el tercero ve su vínculo activo.
  await esperarEstadoDelTercero(limpio.pagina, 'Aprobado');

  // Sofía (Revisión) ve solo el dudoso en Excepciones, con el motivo en español y sin Confirmar (una excepción se decide en el expediente).
  const paginaSofia = await (await browser.newContext()).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur, 'excepciones');
  await esperarFila(sofia, dudoso.tercero);
  const excepcion = fila(sofia, dudoso.tercero);
  await expect(excepcion).toContainText('Excepción');
  await expect(excepcion.getByRole('list', { name: 'Motivos' })).toContainText('Confianza baja');
  await expect(excepcion.getByRole('button', { name: /Confirmar/ })).toHaveCount(0);
  await expect(fila(sofia, limpio.tercero)).toHaveCount(0);
  await expect(sofia.getByRole('link', { name: 'Excepciones (1)' }).or(sofia.getByRole('button', { name: /Excepciones \(1\)/ }))).toBeVisible();
  await capturar(paginaSofia, 'revision-automatica-excepciones', 1280, 900);
  await capturar(paginaSofia, 'revision-automatica-excepciones-movil', 390, 1100);

  // «Abrir expediente» lleva al documento, que dice por qué requiere una persona; ella decide como siempre.
  await excepcion.getByRole('link', { name: /^Abrir el expediente/ }).click();
  await expect(sofia.getByRole('heading', { level: 1, name: dudoso.tercero.nombre })).toBeVisible();
  const decision = sofia.getByRole('region', { name: 'Decisión automática' });
  await expect(decision).toContainText('Requiere una persona:');
  await expect(decision).toContainText('Confianza baja');
  await expect(regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ })).toBeVisible();
});

test('con muestreo del 100 % la aprobación queda en Excepciones: una persona confirma una y revierte otra, que vuelve a revisión humana', async ({ browser }) => {
  test.setTimeout(300_000);
  const e = await crearEscenario();
  await activarIa(e, e.sur);
  // RUT (lo aprueba el sistema) y certificación bancaria (fuera del alcance: la decide Sofía, así el expediente sigue en revisión).
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_RA', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { tipoDocumento: 'CERTIFICACION_BANCARIA' }]);
  await llamar('PUT', '/api/v1/configuracion/revision-automatica', {
    llave: e.admin,
    empresa: e.sur.id,
    cuerpo: {
      nivel: 'Automatico',
      umbralConfianza: 0.95,
      porcentajeMuestreo: 100,
      topeMensualAprobaciones: null,
      tiposAutorizados: ['RUT'],
      areasAutorizadas: [e.sur.areas.revision!.id],
      esperaIaMinutos: 10,
    },
  });

  const confirmado = await terceroConDocumentos(e, browser, 'Aceros Confirmados S.A.S.', '', true);
  const revertido = await terceroConDocumentos(e, browser, 'Aceros Revertidos S.A.S.', '', true);

  const paginaSofia = await (await browser.newContext()).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur, 'excepciones');
  await esperarFila(sofia, confirmado.tercero);
  await esperarFila(sofia, revertido.tercero);
  await expect(fila(sofia, confirmado.tercero)).toContainText('Muestreo');
  await expect(fila(sofia, confirmado.tercero)).toContainText('Aprobado por el sistema');
  await expect(fila(sofia, confirmado.tercero)).toContainText(/Confianza 97\s?%/);

  // Confirmar es un clic: la fila sale de Pendientes.
  await fila(sofia, confirmado.tercero).getByRole('button', { name: new RegExp(`^Confirmar la aprobación de ${RUT} de ${confirmado.tercero.nombre}`) }).click();
  await expect(sofia.getByText(`Confirmaste la aprobación automática de ${RUT}.`)).toBeVisible();
  await expect(fila(sofia, confirmado.tercero)).toHaveCount(0);

  // Revertir avisa qué pasa y exige la justificación.
  await fila(sofia, revertido.tercero).getByRole('button', { name: new RegExp(`^Revertir la aprobación de ${RUT} de ${revertido.tercero.nombre}`) }).click();
  const dialogo = sofia.getByRole('dialog', { name: 'Revertir la aprobación automática' });
  await expect(dialogo).toContainText('el documento vuelve a revisión humana');
  await expect(dialogo.getByRole('button', { name: 'Revertir aprobación' })).toBeDisabled();
  await capturar(paginaSofia, 'revision-automatica-revertir', 1280, 900);
  await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('El RUT que subió no es el vigente.');
  await dialogo.getByRole('button', { name: 'Revertir aprobación' }).click();
  await expect(sofia.getByText(`Revertiste la aprobación: ${RUT} vuelve a revisión humana.`)).toBeVisible();
  await expect(fila(sofia, revertido.tercero)).toHaveCount(0);

  // En Resueltas queda quién hizo qué.
  await sofia.getByRole('tab', { name: 'Resueltas' }).click();
  await expect(fila(sofia, confirmado.tercero)).toContainText('Confirmada por Sofía Revisora');
  await expect(fila(sofia, revertido.tercero)).toContainText('Revertida por Sofía Revisora');
  await capturar(paginaSofia, 'revision-automatica-resueltas', 1280, 900);

  // El documento revertido vuelve a revisión humana: se ve en el expediente y en su historial, y el área puede decidirlo.
  await fila(sofia, revertido.tercero).getByRole('link', { name: /^Abrir el expediente/ }).click();
  await expect(sofia.getByRole('heading', { level: 1, name: revertido.tercero.nombre })).toBeVisible();
  const decision = sofia.getByRole('region', { name: 'Decisión automática' });
  await expect(decision).toContainText('Revertida por Sofía Revisora');
  await expect(decision).toContainText('Justificación: El RUT que subió no es el vigente.');
  await expect(decision.getByRole('button')).toHaveCount(0);
  await expect(sofia.getByText(`Una persona revirtió la aprobación automática de ${RUT} en el área Revisión`)).toBeVisible();
  await expect(sofia.getByText(new RegExp(`La revisión automática aprobó ${RUT} en el área Revisión \\(confianza 97\\s?%\\): queda para confirmar`))).toBeVisible();
  await expect(regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ })).toBeVisible();
  await abrirDesdeBandeja(sofia, confirmado.tercero.nombre, 'Revisión', 'Cliente');
  await expect(sofia.getByRole('region', { name: 'Decisión automática' })).toContainText('Aprobado por el sistema');
});
