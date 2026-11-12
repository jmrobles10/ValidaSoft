import { expect, test, type Browser, type FrameLocator, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { capturar } from '../soporte/capturas';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion, regionDeArea } from '../soporte/empresa';
import { abrirEmpresa, aceptarInvitacion, completarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, esperarTarjeta, subirDocumento, tarjeta } from '../soporte/proveedor';
import { leerCertificado } from '../soporte/flujos';
import { activarIa, crearEscenario, expedienteAbierto, invitar, nuevoTercero, publicarFlujoConFicha, type Escenario, type Invitacion, type Tercero } from '../soporte/semilla';

/**
 * Revalidaciones en lote y exención de un requisito, con sus efectos en el vínculo, en la línea de tiempo del expediente y en lo que
 * ve el tercero. El analizador de IA es el simulado: lee marcas `[[clave=valor]]` del PDF, así que una versión nueva con las mismas
 * marcas «no cambió» y una con otra razón social «tiene cambios».
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';
const BANCARIA = 'Certificación bancaria';

function marcasRut(tercero: Tercero, razonSocial = tercero.nombre): string {
  return `[[nit=${tercero.identificacion.numero}]][[razonSocial=${razonSocial}]]`;
}

function marcasBancaria(tercero: Tercero): string {
  return `[[banco=Bancolombia]][[tipoCuenta=Ahorros]][[numeroCuenta=${tercero.identificacion.numero}01]][[titular=${tercero.nombre}]]`;
}

async function paginaDe(browser: Browser): Promise<Page> {
  return (await browser.newContext()).newPage();
}

/** El tercero sube una versión nueva desde su bóveda (una sola vez para todas las empresas). */
async function actualizarDesdeLaBoveda(page: Page, documento: string, archivo: ReturnType<typeof pdf>, version: number): Promise<void> {
  await page.goto('/boveda');
  await page.getByRole('article', { name: documento }).getByRole('link', { name: 'Subir versión' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(archivo);
  await expect(page.getByText(`Es tu versión ${String(version)} de ${documento}`)).toBeVisible({ timeout: 30_000 });
}

/** Un cliente de Sur con RUT y certificación bancaria que Sofía aprueba: queda activo, con su certificado. */
async function clienteAprobado(browser: Browser, e: Escenario, sofia: FrameLocator, nombre: string, razonSocial?: string) {
  const tercero = nuevoTercero(nombre);
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  const pagina = await paginaDe(browser);
  await completarInvitacion(pagina, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${nombre} v1 ${marcasRut(tercero, razonSocial)}`) },
    { nombre: BANCARIA, archivo: pdf(`Bancaria ${nombre} v1 ${marcasBancaria(tercero)}`) },
  ]);
  await abrirDesdeBandeja(sofia, nombre, 'Revisión', 'Cliente');
  await aprobarArea(sofia, 'Revisión');
  await esperarEstadoDelTercero(pagina, 'Aprobado');
  return { tercero, invitacion, pagina };
}

test('Sofía aprueba en lote las revalidaciones sin cambios; la que cambió se revisa aparte, y el vínculo, el historial y el tercero lo reflejan', async ({ browser }) => {
  test.setTimeout(480_000);
  const e = await crearEscenario();
  await activarIa(e, e.sur);
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_RV', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { tipoDocumento: 'CERTIFICACION_BANCARIA' }]);

  const paginaSofia = await paginaDe(browser);
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  const a = await clienteAprobado(browser, e, sofia, 'Aceros Sin Cambios S.A.S.');
  const b = await clienteAprobado(browser, e, sofia, 'Aceros Con Cambios S.A.S.');

  // A actualiza el RUT y la certificación con los mismos datos; B cambia su razón social en el RUT.
  await actualizarDesdeLaBoveda(a.pagina, RUT, pdf(`RUT ${a.tercero.nombre} v2 ${marcasRut(a.tercero)}`), 2);
  await actualizarDesdeLaBoveda(a.pagina, BANCARIA, pdf(`Bancaria ${a.tercero.nombre} v2 ${marcasBancaria(a.tercero)}`), 2);
  await actualizarDesdeLaBoveda(b.pagina, RUT, pdf(`RUT ${b.tercero.nombre} v2 ${marcasRut(b.tercero, 'Otra Razón Social S.A.S.')}`), 2);

  // La bandeja de revalidaciones las lista (el análisis y la comparación llegan en segundo plano).
  const paginaRevalidaciones = await paginaDe(browser);
  const revalidaciones = await abrirEmbebido(paginaRevalidaciones, e, e.sofia, e.sur, 'revalidaciones');
  const fila = (tercero: Tercero, documento: string) => revalidaciones.getByRole('row').filter({ hasText: tercero.nombre }).filter({ hasText: documento });
  await expect(async () => {
    await paginaRevalidaciones.reload();
    await expect(fila(a.tercero, RUT)).toContainText('Sin cambios', { timeout: 1500 });
    await expect(fila(a.tercero, BANCARIA)).toContainText('Sin cambios', { timeout: 1500 });
    await expect(fila(b.tercero, RUT)).toContainText('Con cambios', { timeout: 1500 });
  }).toPass({ timeout: 90_000 });
  await expect(fila(a.tercero, RUT)).toContainText('v1 → v2');
  await expect(fila(b.tercero, RUT)).toContainText('v1 → v2');
  await capturar(paginaRevalidaciones, 'revalidaciones-lote');

  // «Seleccionar las sin cambios» toma las dos de A y deja la de B: lo que cambió no se aprueba en lote.
  await revalidaciones.getByRole('button', { name: 'Seleccionar las sin cambios' }).click();
  await expect(revalidaciones.getByRole('checkbox', { name: `Seleccionar ${RUT} de ${a.tercero.nombre}` })).toBeChecked();
  await expect(revalidaciones.getByRole('checkbox', { name: `Seleccionar ${BANCARIA} de ${a.tercero.nombre}` })).toBeChecked();
  await expect(revalidaciones.getByRole('checkbox', { name: `Seleccionar ${RUT} de ${b.tercero.nombre}` })).toBeDisabled();
  await revalidaciones.getByRole('button', { name: 'Aprobar 2 sin cambios' }).click();
  const dialogo = revalidaciones.getByRole('dialog', { name: '¿Aprobar 2 revalidaciones?' });
  await expect(dialogo).toContainText('cada una con su propio registro en el historial del expediente');
  await dialogo.getByRole('button', { name: 'Aprobar 2 sin cambios' }).click();
  await expect(revalidaciones.getByText('Aprobaste 2 revalidaciones.')).toBeVisible();
  await expect(fila(a.tercero, RUT)).toHaveCount(0);
  await expect(fila(a.tercero, BANCARIA)).toHaveCount(0);
  await expect(fila(b.tercero, RUT)).toContainText('Con cambios');

  // El vínculo de A sigue activo y su certificado se reemitió (emisión 2); el de B queda con pendientes hasta que se revise lo suyo.
  const vinculoA = await abrirEmbebido(await paginaDe(browser), e, e.sofia, e.sur, `vinculos/${a.invitacion.vinculoId}`);
  await expect(vinculoA.getByRole('heading', { level: 1, name: a.tercero.nombre })).toBeVisible();
  await expect(vinculoA.getByText(/^Activo$/).first()).toBeVisible();
  await expect(vinculoA.getByRole('main')).toContainText(new RegExp(`Vigente\\s*${e.sur.prefijo}-C-\\d{4}-\\d{6}-2`));
  // En su línea de tiempo, el vínculo de A cerró dos expedientes (el de ingreso y el de la actualización); el de B, uno, con el de la actualización abierto.
  await expect(vinculoA.getByText('Expediente cerrado', { exact: true })).toHaveCount(2);
  await expect(vinculoA.getByText('Expediente abierto', { exact: true })).toHaveCount(2);
  await capturar(vinculoA.owner().page(), 'revalidaciones-vinculo');
  // La actualización de A, ya cerrada, deja en su historial una aprobación por cada documento (cada una con su registro, aunque se aprobaron juntas).
  const actualizacionDeA = (await leerCertificado(e, a.invitacion)).actual?.expedienteId;
  expect(actualizacionDeA).toBeTruthy();
  const expedienteA = await abrirEmbebido(await paginaDe(browser), e, e.sofia, e.sur, `expedientes/${String(actualizacionDeA)}`);
  await expect(expedienteA.getByRole('heading', { level: 1, name: a.tercero.nombre })).toBeVisible();
  await expect(expedienteA.getByText(`Revisión aprobó ${RUT} (v2)`, { exact: true })).toHaveCount(1);
  await expect(expedienteA.getByText(`Revisión aprobó ${BANCARIA} (v2)`, { exact: true })).toHaveCount(1);
  const vinculoB = await abrirEmbebido(await paginaDe(browser), e, e.sofia, e.sur, `vinculos/${b.invitacion.vinculoId}`);
  await expect(vinculoB.getByRole('main')).toContainText(new RegExp(`En verificación\\s*${e.sur.prefijo}-C-\\d{4}-\\d{6}-1`));
  await expect(vinculoB.getByText('Expediente cerrado', { exact: true })).toHaveCount(1);
  await expect(vinculoB.getByText('Expediente abierto', { exact: true })).toHaveCount(2);

  // El tercero A ve su vínculo activo con las dos versiones nuevas aprobadas; el B, su actualización del RUT todavía en revisión: el encabezado y la
  // tarjeta del documento dicen lo mismo (VR-1), no «aprobó tu información» arriba y «En revisión» abajo.
  await a.pagina.goto('/empresas');
  await abrirEmpresa(a.pagina, e.sur.nombre, 'CLIENTE');
  await esperarEstadoDelTercero(a.pagina, 'Aprobado');
  await esperarTarjeta(a.pagina, RUT, /Versión 2[\s\S]*Aprobado/);
  await esperarTarjeta(a.pagina, BANCARIA, /Versión 2[\s\S]*Aprobado/);
  await capturar(a.pagina, 'revalidaciones-tercero');
  await b.pagina.goto('/empresas');
  await abrirEmpresa(b.pagina, e.sur.nombre, 'CLIENTE');
  await esperarEstadoDelTercero(b.pagina, 'EnRevision');
  await expect(b.pagina.getByRole('main')).not.toContainText('aprobó tu información');
  await expect(tarjeta(b.pagina, RUT)).toContainText('Versión 2');
  await expect(tarjeta(b.pagina, RUT)).toContainText('En revisión');

  // B se revisa a mano, en su expediente, y entonces sí se aprueba.
  await abrirDesdeBandeja(sofia, b.tercero.nombre, 'Revisión', 'Cliente');
  await aprobarArea(sofia, 'Revisión');
  await esperarTarjeta(b.pagina, RUT, /Versión 2[\s\S]*Aprobado/);
  await irASeccion(sofia, 'Revalidaciones');
  await expect(sofia.getByText('No tienes revalidaciones pendientes.')).toBeVisible();
});

test('Ana exime a un tercero de la Cámara de comercio con su motivo: el expediente sigue sin ese documento y el tercero lo ve eximido', async ({ page, browser }) => {
  test.setTimeout(300_000);
  const e = await crearEscenario();
  const nombre = 'Ferretería Eximida S.A.S.';
  const invitacion: Invitacion = await invitar(e, e.norte, nuevoTercero(nombre));

  // El tercero sube el RUT; la Cámara no la tiene: el expediente espera su documento.
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await subirDocumento(page, RUT, pdf(`RUT ${nombre}`));
  await expect(page.getByRole('main').getByText(/^Te falta 1 documento\. Súbelo/)).toBeVisible();

  // Ana (administradora, no de un área) entra al expediente desde su vínculo y exime la Cámara: pide el motivo y avisa qué pasará.
  const paginaAna = await paginaDe(browser);
  const ana = await abrirEmbebido(paginaAna, e, e.ana, e.norte, `expedientes/${await expedienteAbierto(e, invitacion)}`);
  await expect(ana.getByRole('heading', { level: 1, name: nombre })).toBeVisible();
  const documento = ana.getByRole('listitem').filter({ has: ana.getByRole('heading', { level: 4, name: CAMARA, exact: true }) }).first();
  await documento.getByRole('button', { name: 'Eximir', exact: true }).click();
  const dialogo = ana.getByRole('dialog', { name: `Eximir ${CAMARA}` });
  await expect(dialogo).toContainText('El documento deja de exigirse en este expediente y sus veredictos dejan de contar.');
  // Sin el motivo no deja confirmar.
  await expect(dialogo.getByRole('button', { name: 'Eximir documento' })).toBeDisabled();
  await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Es una persona jurídica sin Cámara de comercio: está inscrita en la Cámara de otro país.');
  await capturar(paginaAna, 'exencion-dialogo');
  await dialogo.getByRole('button', { name: 'Eximir documento' }).click();
  await expect(ana.getByText(`${CAMARA} eximido.`)).toBeVisible();
  await expect(documento).toContainText('Eximido por Ana Administradora');
  await expect(documento).toContainText('Es una persona jurídica sin Cámara de comercio: está inscrita en la Cámara de otro país.');
  await expect(documento.getByRole('button', { name: 'Eximir', exact: true })).toHaveCount(0);

  // En la línea de tiempo del expediente queda quién lo eximió y por qué; la fase de carga termina y el expediente pasa a revisión.
  const historial = ana.getByRole('heading', { name: 'Historial', level: 2 }).locator('xpath=following-sibling::*[1]');
  await expect(ana.getByText(`${CAMARA} eximido`, { exact: true })).toBeVisible();
  await expect(historial.or(ana.getByRole('main'))).toContainText('Es una persona jurídica sin Cámara de comercio: está inscrita en la Cámara de otro país.');
  await expect(ana.getByRole('main')).toContainText('En revisión');
  await capturar(paginaAna, 'exencion-expediente');

  // El tercero ve la Cámara eximida (ya no se la piden) y su expediente en revisión.
  await esperarEstadoDelTercero(page, 'EnRevision');
  await expect(tarjeta(page, CAMARA)).toContainText('Eximido');
  await expect(tarjeta(page, CAMARA).getByRole('link', { name: /^Subir/ })).toHaveCount(0);
  await capturar(page, 'exencion-tercero');

  // Las áreas revisan solo el RUT: la Cámara eximida no se les pide y el expediente se aprueba sin ella.
  const ruben = await abrirEmbebido(await paginaDe(browser), e, e.ruben, e.norte);
  await abrirDesdeBandeja(ruben, nombre, 'Compras');
  await expect(regionDeArea(ruben, 'Compras')).not.toContainText(CAMARA);
  await aprobarArea(ruben, 'Compras');
  const olga = await abrirEmbebido(await paginaDe(browser), e, e.olga, e.norte);
  await abrirDesdeBandeja(olga, nombre, 'Cumplimiento');
  await expect(regionDeArea(olga, 'Cumplimiento')).toContainText(CAMARA);
  await expect(regionDeArea(olga, 'Cumplimiento')).toContainText('Eximido');
  await aprobarArea(olga, 'Cumplimiento');
  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
  await expect(olga.getByText(`${CAMARA} eximido`, { exact: true })).toBeVisible();

  // El vínculo queda activo con su certificado, y el tercero ve su vínculo aprobado con la Cámara todavía eximida.
  await esperarEstadoDelTercero(page, 'Aprobado');
  await expect(tarjeta(page, CAMARA)).toContainText('Eximido');
  const vinculo = await abrirEmbebido(await paginaDe(browser), e, e.olga, e.norte, `vinculos/${invitacion.vinculoId}`);
  await expect(vinculo.getByRole('heading', { level: 1, name: nombre })).toBeVisible();
  await expect(vinculo.getByText(/^Activo$/).first()).toBeVisible();
  await expect(vinculo.getByRole('main')).toContainText(new RegExp(`Vigente\\s*${e.norte.prefijo}-C-\\d{4}-\\d{6}-1`));
  // La Cámara eximida no es un documento del vínculo: no figura entre los compartidos ni en ninguna parte de su ficha.
  await expect(vinculo.getByRole('heading', { name: 'Documentos compartidos' })).toBeVisible();
  await expect(vinculo.getByRole('main')).not.toContainText(CAMARA);
});
