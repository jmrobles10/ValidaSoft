import { readFile } from 'node:fs/promises';
import { expect, test, type Browser, type FrameLocator, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { esperarCorreo } from '../soporte/correo';
import { abrirDesdeBandeja, aprobarArea, conDiagnostico } from '../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST, type Escenario, type Invitacion } from '../soporte/semilla';

/** Certificado verificable (H5): verificación pública, descarga, suspensión por bloqueo y revocación. */

const RUT = 'Registro Único Tributario';

function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

/** Proveedor aprobado por Sur (una sola área): queda activo y con certificado. */
async function proveedorAprobado(page: Page, browser: Browser): Promise<{ e: Escenario; invitacion: Invitacion }> {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero('Andamios Seguros S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${invitacion.correo}`) },
    { nombre: SST.nombre, archivo: pdf(`SST ${invitacion.correo}`), campos: { 'Fecha de expedición': haceDias(2) } },
  ]);
  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
  await aprobarArea(sofia, 'Revisión');
  await esperarEstadoDelTercero(page, 'Aprobado');
  return { e, invitacion };
}

/** Página de verificación del certificado del vínculo (el enlace que trae el QR), abierta como cualquier persona. */
async function verificacion(browser: Browser, ana: FrameLocator): Promise<{ publica: Page; ruta: string }> {
  const enlace = ana.getByRole('link', { name: 'Página de verificación' });
  await expect(enlace).toBeVisible();
  const url = new URL((await enlace.getAttribute('href')) ?? '');
  const publica = await (await browser.newContext()).newPage();
  const ruta = `${url.pathname}${url.search}`;
  await publica.goto(ruta);
  return { publica, ruta };
}

async function comoAdministradora(browser: Browser, e: Escenario, invitacion: Invitacion): Promise<FrameLocator> {
  const ana = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
  await expect(ana.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
  return ana;
}

test('cualquiera verifica el certificado: vigente, auténtico en su navegador, con el NIT enmascarado y el PDF descargable', async ({ page, browser }) => {
  const { e, invitacion } = await proveedorAprobado(page, browser);
  const ana = await comoAdministradora(browser, e, invitacion);
  const { publica } = await verificacion(browser, ana);

  await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' })).toBeVisible();
  await expect(publica.getByRole('main')).toContainText(`${e.sur.nombre} validó los documentos de ${invitacion.nombre}`);
  await expect(publica.getByRole('main')).toContainText(/NIT \d{2}\*+\d{2}/);
  await expect(publica.getByRole('main')).not.toContainText(invitacion.correo);

  // El navegador recalcula el contenido (JCS + SHA-256) y valida el sello Ed25519 con la clave publicada.
  await expect(publica.getByRole('status').filter({ hasText: 'Auténtico: el contenido está íntegro y lo selló Validasoft.' })).toBeVisible();
  for (const comprobacion of ['Contenido íntegro', 'Huella del enlace', 'Clave de la plataforma', 'Sello válido'])
    await expect(publica.getByRole('listitem').filter({ hasText: comprobacion })).toContainText('Correcto');

  const descarga = publica.waitForEvent('download');
  await publica.getByRole('button', { name: 'Descargar PDF' }).click();
  const archivo = await (await descarga).path();
  expect((await readFile(archivo)).subarray(0, 5).toString('latin1')).toBe('%PDF-');
});

test('una huella que no corresponde o un folio que no existe no se verifican', async ({ page, browser }) => {
  const { e, invitacion } = await proveedorAprobado(page, browser);
  const ana = await comoAdministradora(browser, e, invitacion);
  const { publica, ruta } = await verificacion(browser, ana);
  await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' })).toBeVisible();

  await publica.goto(ruta.replace(/huella=[0-9a-f]+/, 'huella=000000000000'));
  await expect(publica.getByRole('heading', { level: 1, name: 'No encontramos este certificado' })).toBeVisible();
  await publica.goto(`/verificar/${e.sur.prefijo}-C-2026-999999-1`);
  await expect(publica.getByRole('heading', { level: 1, name: 'No encontramos este certificado' })).toBeVisible();
});

test('bloquear el vínculo suspende el certificado y desbloquearlo lo restablece', async ({ page, browser }) => {
  const { e, invitacion } = await proveedorAprobado(page, browser);
  const ana = await comoAdministradora(browser, e, invitacion);
  const { publica, ruta } = await verificacion(browser, ana);

  await ana.getByRole('button', { name: 'Más acciones' }).click();
  await ana.getByRole('menuitem', { name: /^Bloquear/ }).click();
  const bloquear = ana.getByRole('dialog', { name: /^Bloquear vínculo/ });
  await bloquear.getByRole('textbox', { name: 'Motivo' }).fill('Coincidencia en lista restrictiva por revisar.');
  await bloquear.getByRole('button', { name: 'Bloquear vínculo' }).click();
  await expect(bloquear).toBeHidden();

  await publica.goto(ruta);
  await expect(publica.getByRole('main')).toContainText('Suspendido');
  await expect(publica.getByRole('main')).not.toContainText('Coincidencia en lista restrictiva'); // el motivo es interno
  await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está suspendido' })).toBeVisible();
  await expect(publica.getByText('El PDF solo se entrega mientras el certificado está vigente.')).toBeVisible();
  const suspendido = await esperarCorreo(invitacion.correo, /suspendió tu certificado/);
  expect(suspendido.texto).not.toContain('lista restrictiva');

  // Desbloquear lo devuelve a vigente.
  await ana.getByRole('button', { name: 'Desbloquear', exact: true }).click();
  await conDiagnostico(ana, async () => {
    const desbloquear = ana.getByRole('dialog');
    await desbloquear.getByRole('textbox').first().fill('Se descartó la coincidencia.');
    await desbloquear.getByRole('button', { name: /^Desbloquear/ }).click();
    await expect(desbloquear).toBeHidden();
  });
  await publica.goto(ruta);
  await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' })).toBeVisible();
});

test('revocar el certificado: deja de ser válido para quien lo verifique, sin exponer el motivo, y el proveedor se entera', async ({ page, browser }) => {
  const { e, invitacion } = await proveedorAprobado(page, browser);
  const ana = await comoAdministradora(browser, e, invitacion);
  const { publica, ruta } = await verificacion(browser, ana);

  await ana.getByRole('button', { name: 'Revocar certificado' }).click();
  const revocar = ana.getByRole('dialog', { name: /^Revocar el certificado/ });
  await revocar.getByRole('textbox', { name: 'Justificación' }).fill('Se aprobó con un documento adulterado.');
  await revocar.getByRole('button', { name: 'Revocar certificado' }).click();
  await expect(revocar).toBeHidden();

  await publica.goto(ruta);
  await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado fue revocado' })).toBeVisible();
  await expect(publica.getByRole('main')).toContainText('Dejó de valer el');
  await expect(publica.getByRole('main')).toContainText(`${e.sur.nombre} puede decirte el motivo.`);
  // El motivo es interno (CE-8) y sin vigencia no se entrega el PDF; el contenido sellado sigue siendo auténtico.
  await expect(publica.getByRole('main')).not.toContainText('Se aprobó con un documento adulterado.');
  await expect(publica.getByText('El PDF solo se entrega mientras el certificado está vigente.')).toBeVisible();
  await expect(publica.getByRole('status').filter({ hasText: 'Auténtico' })).toBeVisible();

  // El tercero se entera por correo, sin el motivo (NW-16).
  const aviso = await esperarCorreo(invitacion.correo, /revocó tu certificado/);
  expect(aviso.texto).not.toContain('adulterado');
});
