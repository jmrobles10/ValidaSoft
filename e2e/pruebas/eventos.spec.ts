import { expect, test, type Browser, type FrameLocator, type Page } from '@playwright/test';
import { esperarCorreo } from '../soporte/correo';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../soporte/empresa';
import { completarInvitacion } from '../soporte/proveedor';
import { firmaValida, Receptor } from '../soporte/receptor';
import { crearEscenario, invitar, nuevoTercero, SST, type Escenario, type Invitacion } from '../soporte/semilla';

/** Lo que se entera el sistema integrado (webhooks firmados) y las personas (campana y correo). */

const RUT = 'Registro Único Tributario';

function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

/** La administradora registra el webhook en Configuración › Webhooks y copia el secreto (se muestra una sola vez). */
async function registrarWebhook(browser: Browser, e: Escenario, receptor: Receptor): Promise<{ ana: FrameLocator; secreto: string }> {
  const ana = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.sur, 'webhooks');
  await ana.getByRole('button', { name: 'Nuevo webhook' }).click();
  await ana.getByRole('textbox', { name: /Dirección \(URL\)/ }).fill(receptor.url);
  await ana.getByRole('textbox', { name: /Descripción/ }).fill('Sistema integrado E2E');
  await ana.getByRole('button', { name: 'Crear webhook' }).click();
  const dialogo = ana.getByRole('dialog', { name: 'Secreto de firma' });
  const secreto = (await dialogo.getByRole('code').innerText()).trim();
  expect(secreto).toMatch(/^whsec_/);
  await dialogo.getByRole('button', { name: 'Ya lo guardé' }).click();
  return { ana, secreto };
}

/** Proveedor de Sur que entrega todo y Sofía aprueba: vínculo activo con certificado. */
async function habilitarEnSur(page: Page, browser: Browser, e: Escenario): Promise<Invitacion> {
  const invitacion = await invitar(e, e.sur, nuevoTercero('Tuberías del Valle S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${invitacion.correo}`) },
    { nombre: SST.nombre, archivo: pdf(`SST ${invitacion.correo}`), campos: { 'Fecha de expedición': haceDias(1) } },
  ]);
  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
  await aprobarArea(sofia, 'Revisión');
  return invitacion;
}

test('el sistema integrado recibe por webhook todo el recorrido, en orden y con firma válida', async ({ page, browser }) => {
  const e = await crearEscenario();
  const receptor = await Receptor.iniciar();
  try {
    const { ana, secreto } = await registrarWebhook(browser, e, receptor);
    const invitacion = await habilitarEnSur(page, browser, e);

    await receptor.esperar((x) => x.tipo === 'certificado.emitido' && x.datos.vinculoId === invitacion.vinculoId);
    const delVinculo = receptor.entregas.filter((x) => x.datos.vinculoId === invitacion.vinculoId).map((x) => x.tipo);
    const esperados = ['vinculo.creado', 'vinculo.invitacion_enviada', 'vinculo.invitacion_aceptada', 'expediente.abierto', 'expediente.en_revision', 'expediente.aprobado', 'vinculo.activado'];
    expect(delVinculo.filter((t) => esperados.includes(t))).toEqual(esperados);
    expect(delVinculo).toContain('certificado.emitido');
    expect(delVinculo).toContain('documento.compartido');

    // Cada entrega está firmada con el secreto (HMAC-SHA256 de «t.cuerpo»); con otro secreto no valida.
    for (const entrega of receptor.entregas) expect(firmaValida(secreto, entrega.firma, entrega.cuerpo)).toBe(true);
    expect(firmaValida('whsec_otro', receptor.entregas[0]?.firma ?? '', receptor.entregas[0]?.cuerpo ?? '')).toBe(false);
    // Ningún dato personal del tercero viaja en los eventos.
    for (const entrega of receptor.entregas) expect(entrega.cuerpo).not.toContain(invitacion.correo);

    // La administradora ve las entregas en la pantalla del webhook.
    // La administradora ve las entregas en la misma pantalla, sin recargar.
    await expect(ana.getByRole('table', { name: 'Webhooks' }).getByRole('row').nth(1)).not.toContainText('—', { timeout: 30_000 });
    await ana.getByRole('table', { name: 'Webhooks' }).getByRole('row').nth(1).click();
    const entregas = ana.getByRole('table', { name: 'Entregas recientes' });
    await expect(entregas.getByRole('row', { name: /^Ver intentos de certificado\.emitido/ })).toContainText('Entregada');
    await expect(entregas.getByRole('row', { name: /^Ver intentos de vinculo\.activado/ })).toContainText('Entregada');
  } finally {
    await receptor.cerrar();
  }
});

test('las personas se enteran: la campana y el correo de «Nuevo expediente en tu área» llevan al expediente', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero('Pinturas del Norte S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${invitacion.correo}`) },
    { nombre: SST.nombre, archivo: pdf(`SST ${invitacion.correo}`), campos: { 'Fecha de expedición': haceDias(1) } },
  ]);

  const correo = await esperarCorreo(e.sofia.correo, /^Nuevo expediente en tu área: /);
  expect(correo.texto).toContain('Tu área: Revisión.');
  expect(correo.texto).not.toContain('S.A.S..');

  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await sofia.getByRole('button', { name: /^Notificaciones, \d+ sin leer/ }).click();
  const notificacion = sofia.getByText('Nuevo expediente en tu área').first();
  await expect(notificacion).toBeVisible();
  await notificacion.click();
  await expect(sofia.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
});

test('si el sistema integrado falla, la entrega queda fallida a la vista y se puede reenviar', async ({ browser }) => {
  const e = await crearEscenario();
  const receptor = await Receptor.iniciar();
  try {
    const { ana, secreto } = await registrarWebhook(browser, e, receptor);
    receptor.fallar = true;
    const invitacion = await invitar(e, e.sur, nuevoTercero('Vidrios Templados S.A.S.'));
    await receptor.esperar((x) => x.tipo === 'vinculo.creado' && x.datos.vinculoId === invitacion.vinculoId);

    await ana.getByRole('table', { name: 'Webhooks' }).getByRole('row').nth(1).click();
    const fila = ana.getByRole('table', { name: 'Entregas recientes' }).getByRole('row', { name: /^Ver intentos de vinculo\.creado/ });
    await expect(fila).toBeVisible({ timeout: 30_000 });
    // Respondió 500: queda «Reintentando», con el intento contado y la hora del próximo.
    await expect(fila).toContainText('Reintentando');
    await expect(fila).toContainText('Próximo intento:');

    // El sistema se recupera y la administradora reenvía a mano: llega otra vez, firmada.
    receptor.fallar = false;
    const antes = receptor.entregas.filter((x) => x.tipo === 'vinculo.creado').length;
    await fila.getByRole('button', { name: 'Reenviar' }).click();
    await expect.poll(() => receptor.entregas.filter((x) => x.tipo === 'vinculo.creado').length, { timeout: 30_000 }).toBeGreaterThan(antes);
    const ultima = receptor.entregas.filter((x) => x.tipo === 'vinculo.creado').at(-1);
    expect(firmaValida(secreto, ultima?.firma ?? '', ultima?.cuerpo ?? '')).toBe(true);
    await expect(fila).toContainText('Entregada', { timeout: 30_000 });
  } finally {
    await receptor.cerrar();
  }
});
