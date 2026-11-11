import { expect, test, type FrameLocator, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { capturar } from '../soporte/capturas';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion, regionDeArea } from '../soporte/empresa';
import { aceptarInvitacion, entrarPorInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../soporte/semilla';

/**
 * Pendientes de la ficha (registro FD-1…FD-8): la información general llega prellenada con lo que ya se sabe del tercero; la empresa
 * ve su ficha aceptada (versión y soporte) separada de lo que el tercero cambió después, y un cambio crítico sin un documento
 * nuevo se marca «sin soporte» y no se aprueba sin justificación.
 */

const RUT = 'Registro Único Tributario';

/** Desde Certificados llega a la ficha aceptada del vínculo: ficha del tercero → vínculo → «Ver la ficha aceptada». */
async function abrirFichaAceptada(sofia: FrameLocator, nombre: string): Promise<void> {
  await irASeccion(sofia, 'Certificados');
  await sofia.getByRole('link', { name: `Abrir la ficha de ${nombre}` }).click();
  await sofia.getByRole('link', { name: /Ver el vínculo/ }).first().click();
  await sofia.getByRole('link', { name: /^Ver la ficha aceptada/ }).click();
  await expect(sofia.getByRole('heading', { level: 1, name: nombre })).toBeVisible();
}

async function guardarGeneral(page: Page): Promise<void> {
  const barra = page.getByRole('group', { name: 'Guardar la sección' });
  await expect(barra.getByText('Lista: puedes continuar')).toBeVisible();
  await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByText('Guardaste Información general: está completa.')).toBeVisible();
}

test('la general llega prellenada y la ficha aceptada separa lo aceptado de lo que el tercero cambió después; un cambio crítico sin documento nuevo pide justificación', async ({
  page,
  browser,
}) => {
  const e = await crearEscenario();
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_FD', e.sur.areas.revision!, [{ seccion: 'FICHA_GENERAL' }]);
  const tercero = nuevoTercero('Aceros Pendientes S.A.S.');
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');

  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);

  // El tercero sube su RUT a la bóveda (será el soporte de su información) y abre la información general.
  await page.goto(`/boveda/subir?tipo=RUT&nombre=${encodeURIComponent(RUT)}`);
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf('RUT Pendientes'));
  await expect(page.getByText(`Es tu versión 1 de ${RUT}`)).toBeVisible({ timeout: 30_000 });
  await page.goto('/ficha/general');

  // FD-1: razón social, identificación (con DV) y el correo de la invitación llegan puestos, con un solo aviso.
  await expect(page.getByRole('heading', { level: 1, name: 'Información general' })).toBeVisible();
  await expect(page.getByRole('alert').filter({ hasText: 'Prellenamos tu información' })).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'Razón social' })).toHaveValue(tercero.nombre);
  await expect(page.getByRole('textbox', { name: 'Correo electrónico' })).toHaveValue(tercero.correo);
  await expect(page.getByRole('textbox', { name: 'Identificación' })).toHaveValue(new RegExp(`^NIT ${tercero.identificacion.numero}-\\d$`));
  await capturar(page, 'ficha-prellenada', { alto: 1500 });

  // Lo demás lo diligencia el tercero; el RUT queda como soporte y «Guardar» no pide tocar lo prellenado.
  await page.getByRole('textbox', { name: 'Teléfono' }).fill('601 555 1234');
  await page.getByRole('combobox', { name: 'País' }).fill('Colombia');
  await page.getByRole('option', { name: 'Colombia' }).first().click();
  await page.getByRole('combobox', { name: 'Ciudad' }).fill('Bogot');
  await page.getByRole('option', { name: /^Bogotá/ }).first().click();
  await page.getByRole('textbox', { name: 'Dirección', exact: true }).fill('Calle 45 # 12-08');
  await page.getByRole('combobox', { name: 'Actividad económica principal (CIIU)' }).fill('4663');
  await page.getByRole('option', { name: /^4663/ }).first().click();
  await page.getByRole('checkbox', { name: /^Registro Único Tributario · versión 1/ }).check();
  await guardarGeneral(page);
  await expect(page.getByText('Prellenamos tu información')).toBeHidden();

  // La empresa aprueba la información general.
  const paginaSofia = await (await browser.newContext()).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await aprobarArea(sofia, 'Revisión');

  // FD-3: la ficha aceptada dice lo que la empresa aceptó, su versión y su soporte; nada pendiente.
  await expect(async () => {
    await abrirFichaAceptada(sofia, tercero.nombre);
    await expect(sofia.getByRole('region', { name: 'Información general' })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  const general = sofia.getByRole('region', { name: 'Información general' });
  await expect(general).toContainText('Aceptada · versión 1');
  await expect(general.getByRole('region', { name: 'Soporte con que se aceptó' })).toContainText(`${RUT} · versión 1`);
  await expect(sofia.getByRole('region', { name: 'Resumen' })).toContainText('1 sección aceptada');
  await expect(sofia.getByRole('region', { name: 'Resumen' })).toContainText('Nada pendiente de revisión.');
  await capturar(paginaSofia, 'ficha-aceptada', { alto: 1500 });

  // El tercero cambia su razón social con el mismo RUT de antes: sin documento nuevo, y es un dato crítico.
  await page.goto('/ficha/general');
  await page.getByRole('textbox', { name: 'Razón social' }).fill('Aceros Pendientes y Cía. S.A.S.');
  await guardarGeneral(page);
  await expect(page.getByRole('alert').filter({ hasText: 'Tu último cambio no trae un documento nuevo' })).toContainText('Razón social');

  // La empresa sigue viendo lo aceptado y, aparte, la versión nueva del tercero con su marca «sin soporte» y su revalidación.
  await expect(async () => {
    await abrirFichaAceptada(sofia, tercero.nombre);
    await expect(sofia.getByText('1 sección tiene una versión nueva pendiente de revisión')).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await expect(general).toContainText('Aceptada · versión 1');
  const posterior = sofia.getByRole('region', { name: 'Versión nueva del tercero, pendiente de revisión' });
  await expect(posterior.getByRole('alert', { name: 'Cambio sin soporte' })).toContainText('datos críticos');
  const cambios = posterior.getByRole('table', { name: 'Qué cambió frente a la versión 1 aceptada' });
  await expect(cambios).toContainText('Aceros Pendientes S.A.S.');
  await expect(cambios).toContainText('Aceros Pendientes y Cía. S.A.S.');
  await capturar(paginaSofia, 'ficha-aceptada-pendiente', { alto: 1500 });

  // FD-4: en su revalidación, aprobar un cambio crítico sin documento nuevo pide una justificación.
  await posterior.getByRole('link', { name: /^Ver la revalidación/ }).click();
  await expect(sofia.getByRole('alert', { name: 'Cambio sin soporte' })).toBeVisible();
  await capturar(paginaSofia, 'ficha-sin-soporte-revision', { alto: 1500 });
  await regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ }).click();
  const dialogo = sofia.getByRole('dialog', { name: /^Aprobar Revisión/ });
  await expect(dialogo.getByRole('alert')).toContainText('no tienen soporte');
  await capturar(paginaSofia, 'ficha-sin-soporte-justificacion');
  await expect(dialogo.getByRole('button', { name: /^Aprobar área/ })).toBeDisabled();
  await dialogo.getByRole('textbox', { name: /Justificación de la aprobación/ }).fill('Cambio de razón social por fusión; verificado con la Cámara de Comercio.');
  await dialogo.getByRole('button', { name: /^Aprobar área/ }).click();
  await expect(dialogo).toBeHidden();
  await expect(sofia.getByText('Cambio de razón social por fusión; verificado con la Cámara de Comercio.').first()).toBeVisible();
});

test('los webhooks ofrecen seccion.aceptada bajo «Información del tercero» y los lotes bajo «Invitaciones»', async ({ browser }) => {
  const e = await crearEscenario();
  const pagina = await (await browser.newContext()).newPage();
  const ana = await abrirEmbebido(pagina, e, e.ana, e.sur, 'webhooks');
  await ana.getByRole('button', { name: 'Nuevo webhook' }).click();

  const evento = ana.getByRole('checkbox', { name: /seccion\.aceptada/ });
  await evento.scrollIntoViewIfNeeded();
  await expect(ana.getByText('Información del tercero', { exact: true })).toBeVisible();
  await expect(ana.getByText('Invitaciones', { exact: true })).toBeVisible();
  await expect(evento).toBeVisible();
  // Ningún grupo se titula con el prefijo crudo del tipo.
  await expect(ana.getByText('seccion', { exact: true })).toHaveCount(0);
  await capturar(pagina, 'webhooks-seccion-aceptada');
});
