import { expect, test, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, regionDeArea } from '../soporte/empresa';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../soporte/semilla';

/**
 * Ficha maestra, F5 (docs/14 §5.2, §5.3, §6; registro FM-48…FM-55): el RUT exigido sirve de soporte de la ficha y se cruza con
 * ella; un RUT nuevo le propone al tercero los cambios (nunca se guardan solos) y, con la actualización ya abierta por ese RUT, la
 * sección que el tercero confirma entra a la misma actualización.
 */

const RUT = 'Registro Único Tributario';
const RAZON_SOCIAL = 'Aceros Ficha Cinco S.A.S.';

/** RUT que lee el analizador simulado de las pruebas (marcas `[[clave=valor]]` en el texto del PDF). */
function rut(nit: string, direccion: string, version: string) {
  return pdf(`RUT ${version} [[nit=${nit}]][[razonSocial=${RAZON_SOCIAL}]][[direccion=${direccion}]][[codigoMunicipio=11001]][[actividadEconomicaPrincipal=4663]]`);
}

/** Diligencia Información general en el wizard (catálogos de ciudad y CIIU incluidos) y la guarda. */
async function diligenciarGeneral(page: Page, direccion: string): Promise<void> {
  await expect(page.getByRole('heading', { level: 1, name: 'Información general' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Razón social' }).fill(RAZON_SOCIAL);
  await page.getByRole('textbox', { name: 'Correo electrónico' }).fill('compras@aceros-ficha.co');
  await page.getByRole('textbox', { name: 'Teléfono' }).fill('601 555 1234');
  await page.getByRole('combobox', { name: 'País' }).fill('Colombia');
  await page.getByRole('option', { name: 'Colombia' }).first().click();
  await page.getByRole('combobox', { name: 'Ciudad' }).fill('Bogot');
  await page.getByRole('option', { name: /^Bogotá/ }).first().click();
  await page.getByRole('textbox', { name: 'Dirección', exact: true }).fill(direccion);
  await page.getByRole('combobox', { name: 'Actividad económica principal (CIIU)' }).fill('4663');
  await page.getByRole('option', { name: /^4663/ }).first().click();
  const barra = page.getByRole('group', { name: 'Guardar la sección' });
  await expect(barra.getByText('Lista: puedes continuar')).toBeVisible();
  await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByText('Guardaste Información general: está completa.')).toBeVisible();
}

test('el tercero sube un RUT nuevo, acepta la propuesta en el wizard y la empresa ve una sola actualización con el RUT, la General y su cruce', async ({ page, browser }) => {
  const e = await crearEscenario();
  // Sur recibe el análisis de IA de los documentos y pide RUT y la información general de sus clientes.
  await activarIa(e, e.sur);
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_FICHA', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }]);
  const tercero = { ...nuevoTercero(RAZON_SOCIAL) };
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  const nit = tercero.identificacion.numero;

  // El tercero acepta, diligencia la general en el wizard y sube su RUT: el expediente pasa a revisión.
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await page.getByRole('link', { name: /^Completar/ }).first().click();
  await diligenciarGeneral(page, 'Calle 45 # 12-08');
  await page.goto('/empresas');
  await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
  await subirDocumento(page, RUT, rut(nit, 'Calle 45 # 12-08', 'v1'));
  await esperarEstadoDelTercero(page, 'EnRevision');

  // Revisión (Sofía): la general se cruza con el RUT campo por campo y todo coincide; Revisión aprueba y el vínculo queda activo.
  const paginaSofia = await (await browser.newContext()).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  // El análisis de la IA llega por una cola: se abre el expediente de nuevo hasta que el cruce aparece.
  const cruceInicial = sofia.getByRole('region', { name: 'Cruce con el documento' });
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(cruceInicial).toContainText('Confianza de la lectura', { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await expect(cruceInicial.getByRole('table', { name: /^Cruce con Registro Único Tributario · versión 1/ })).toBeVisible();
  await expect(sofia.getByRole('region', { name: 'Respaldo de la información' })).toContainText('Información general (versión 1)');
  await aprobarArea(sofia, 'Revisión');

  // El tercero sube un RUT nuevo (otra dirección): la plataforma le ofrece actualizar su información desde él.
  await page.goto('/boveda');
  await page.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(rut(nit, 'Cl 80 # 10-20', 'v2'));
  await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });

  await page.goto('/ficha');
  const aviso = page.getByRole('alert', { name: 'Tienes documentos más nuevos que tu información' });
  await expect(aviso).toContainText('Tu RUT · versión 2', { timeout: 45_000 });
  await expect(aviso.getByText('1 dato distinto')).toBeVisible();
  // Un solo aviso por documento, con la sección que cambia como chip y una acción pequeña.
  await expect(aviso.getByRole('link', { name: /^Actualizar desde mi RUT/ })).toHaveCount(1);
  await aviso.getByRole('link', { name: /^Actualizar desde mi RUT/ }).click();

  // La propuesta muestra, campo por campo, lo que tiene y lo que dice el RUT; nada se guarda hasta que el tercero guarde.
  const propuesta = page.getByRole('alert', { name: 'Tienes documentos más nuevos que esta sección' });
  await propuesta.getByRole('button', { name: 'Ver los cambios de mi RUT' }).click();
  const tabla = propuesta.getByRole('table', { name: 'Datos que cambian según tu RUT' });
  await expect(tabla).toContainText('Calle 45 # 12-08');
  await expect(tabla).toContainText('Cl 80 # 10-20');
  await page.screenshot({ path: 'test-results/ficha-f5-propuesta.png', fullPage: true });
  await expect(page.getByRole('textbox', { name: 'Dirección', exact: true })).toHaveValue('Calle 45 # 12-08');
  await propuesta.getByRole('button', { name: 'Usar los valores de mi RUT' }).click();
  await expect(page.getByRole('textbox', { name: 'Dirección', exact: true })).toHaveValue('Cl 80 # 10-20');
  await expect(propuesta.getByRole('status')).toContainText('todavía no se ha guardado nada');
  const barra = page.getByRole('group', { name: 'Guardar la sección' });
  await expect(barra.getByText('Lista: puedes continuar')).toBeVisible();
  await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByText('Guardaste Información general: está completa.')).toBeVisible();

  // La empresa ve UNA actualización (la que abrió el RUT) con el RUT y la general, y la general con su cruce contra el RUT nuevo.
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('heading', { level: 4, name: 'Información general' })).toBeVisible({ timeout: 1500 });
    await expect(sofia.getByRole('heading', { level: 4, name: RUT })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await expect(sofia.getByText('Actualización', { exact: true }).first()).toBeVisible();
  const cruce = sofia.getByRole('region', { name: 'Cruce con el documento' });
  await expect(cruce.getByRole('table', { name: /^Cruce con Registro Único Tributario · versión 2/ })).toBeVisible({ timeout: 30_000 });
  await expect(cruce.getByRole('row', { name: /^Dirección: Coincide/ })).toBeVisible();
  await expect(sofia.getByRole('region', { name: 'Respaldo de la información' })).toContainText('Información general (versión 2)');
  await paginaSofia.screenshot({ path: 'test-results/ficha-f5-actualizacion.png', fullPage: true });

  // Sofía decide las dos de una vez: la actualización se aprueba y el vínculo sigue activo.
  await regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área \(2 documentos\)/ }).click();
  await sofia.getByRole('dialog', { name: /^Aprobar Revisión/ }).getByRole('button', { name: /^Aprobar/ }).last().click();
  await expect(sofia.getByText(/Todas las áreas aprobaron el |Expediente .* aprobado/).first()).toBeVisible();
});

test('un RUT cuya razón social difiere de la general: la empresa no aprueba sin justificación y con ella queda escrita', async ({ page, browser }) => {
  const e = await crearEscenario();
  await activarIa(e, e.sur);
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_FICHA', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }]);
  const tercero = nuevoTercero(RAZON_SOCIAL);
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');

  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await page.getByRole('link', { name: /^Completar/ }).first().click();
  await diligenciarGeneral(page, 'Calle 45 # 12-08');
  await page.goto('/empresas');
  await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
  // El RUT dice otra razón social: es una diferencia en un campo crítico.
  await subirDocumento(
    page,
    RUT,
    pdf(`RUT otra [[nit=${tercero.identificacion.numero}]][[razonSocial=FERRETERIA OTRA LTDA]][[direccion=Calle 45 # 12-08]][[codigoMunicipio=11001]]`),
  );
  await esperarEstadoDelTercero(page, 'EnRevision');

  const paginaSofia = await (await browser.newContext()).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('region', { name: 'Cruce con el documento' }).getByRole('alert')).toContainText('Hay diferencias en campos críticos', { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await paginaSofia.screenshot({ path: 'test-results/ficha-f5-critica.png', fullPage: true });

  // «Aprobar área» pide la justificación: sin ella no se puede enviar; con ella el expediente se aprueba.
  await regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ }).click();
  const dialogo = sofia.getByRole('dialog', { name: /^Aprobar Revisión/ });
  await expect(dialogo.getByRole('alert')).toContainText('difiere en campos críticos');
  await expect(dialogo.getByRole('button', { name: /^Aprobar área/ })).toBeDisabled();
  await dialogo.getByRole('textbox', { name: /Justificación de la aprobación/ }).fill('La razón social cambió por fusión; verificado con la Cámara.');
  await dialogo.getByRole('button', { name: /^Aprobar área/ }).click();
  await expect(dialogo).toBeHidden();
  await expect(sofia.getByText(/Todas las áreas aprobaron el /)).toBeVisible();
  await expect(sofia.getByText('La razón social cambió por fusión; verificado con la Cámara.').first()).toBeVisible();
});
