import { expect, test, type Browser, type FrameLocator } from '@playwright/test';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja } from '../soporte/empresa';
import { accionDeExpediente, accionDeVinculo, anularVeredicto, emitirObservacion, tarjetaDeArea } from '../soporte/devolucion';
import {
  abrirExpedienteManual,
  aprobarPorApi,
  crearTipoDeVinculo,
  invitarVinculo,
  leerExpediente,
  leerVinculo,
  publicarFlujoSinDocumentos,
} from '../soporte/flujos';
import { crearEscenario, nuevoTercero, type Escenario, type Invitacion, type Persona } from '../soporte/semilla';

/**
 * Lo que hace la empresa para cerrar o corregir un expediente y su vínculo: anular el veredicto de un área (el cierre manual de una
 * observación), anular el expediente, anular el vínculo y volver a abrir. Flujo de Norte sin documentos con Compras (Rubén) y
 * Cumplimiento (Olga) en paralelo.
 */

interface Caso {
  e: Escenario;
  invitacion: Invitacion;
  expedienteId: string;
}

async function conExpedienteAbierto(): Promise<Caso> {
  const e = await crearEscenario();
  await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
  await publicarFlujoSinDocumentos(e, e.norte, 'ALIADOS', 'ALIADO', [[{ area: e.norte.areas.compras! }, { area: e.norte.areas.cumplimiento! }]]);
  const invitacion = await invitarVinculo(e, e.norte, nuevoTercero(), 'ALIADO');
  return { e, invitacion, expedienteId: await abrirExpedienteManual(e, invitacion) };
}

async function como(browser: Browser, c: Caso, persona: Persona, ruta: string): Promise<FrameLocator> {
  const ventana = await (await browser.newContext()).newPage();
  const app = await abrirEmbebido(ventana, c.e, persona, c.e.norte, ruta);
  await expect(app.getByRole('heading', { level: 1, name: c.invitacion.nombre })).toBeVisible();
  return app;
}

test('anular el veredicto de Cumplimiento levanta la devolución al tercero sin esperar su respuesta', async ({ browser }) => {
  const c = await conExpedienteAbierto();

  // Cumplimiento observa: el expediente se devuelve al tercero y nadie más decide.
  const ventana = await (await browser.newContext()).newPage();
  const olga = await abrirEmbebido(ventana, c.e, c.e.olga);
  await abrirDesdeBandeja(olga, c.invitacion.nombre, 'Cumplimiento');
  await emitirObservacion(olga, 'Cumplimiento', 'Incompleto', 'Falta el certificado de existencia.');
  await expect(olga.getByText('Devuelto al tercero', { exact: true }).first()).toBeVisible();
  expect((await leerExpediente(c.e, c.e.norte, c.expedienteId)).estado).toBe('Observado');

  // La administradora anula la observación (el tercero ya lo resolvió por otro canal): la revisión sigue sin esperar un reenvío.
  const ana = await como(browser, c, c.e.ana, `expedientes/${c.expedienteId}`);
  // El aviso de devolución (la línea de tiempo conserva el evento «Devuelto al tercero»: no cuenta).
  await expect(ana.getByRole('alert').getByText('Devuelto al tercero', { exact: true })).toBeVisible();
  await anularVeredicto(ana, 'Cumplimiento', 'El tercero ya entregó el certificado por correo.');

  await expect(ana.getByRole('alert').getByText('Devuelto al tercero', { exact: true })).toHaveCount(0);
  await expect(tarjetaDeArea(ana, 'Cumplimiento')).toContainText('Anulado');
  await expect(tarjetaDeArea(ana, 'Cumplimiento')).toContainText('Pendiente');
  expect((await leerExpediente(c.e, c.e.norte, c.expedienteId)).estado).toBe('EnRevision');

  // Con la devolución levantada, las dos áreas pueden decidir de nuevo.
  await aprobarPorApi(c.e, c.invitacion, c.expedienteId, c.e.norte.areas.compras!, c.e.ruben);
  await aprobarPorApi(c.e, c.invitacion, c.expedienteId, c.e.norte.areas.cumplimiento!, c.e.olga);
  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Activo');
});

test('anular el expediente devuelve el vínculo a Invitado y se puede abrir otro desde la web', async ({ browser }) => {
  const c = await conExpedienteAbierto();

  const ana = await como(browser, c, c.e.ana, `expedientes/${c.expedienteId}`);
  await accionDeExpediente(ana, 'Anular expediente', 'Se abrió con el tipo de vínculo equivocado.');
  await expect(ana.getByText('Expediente anulado', { exact: true }).first()).toBeVisible();
  expect(await leerVinculo(c.e, c.invitacion)).toMatchObject({ estado: 'Invitado', expedienteAbiertoId: null });

  // Desde el vínculo, abre un expediente nuevo (la empresa no depende del tercero para reabrir).
  await ana.getByRole('link', { name: 'Ver vínculo' }).click();
  await expect(ana.getByText('Sin expediente abierto.')).toBeVisible();
  await ana.getByRole('button', { name: 'Abrir expediente' }).click();
  const dialogo = ana.getByRole('dialog', { name: 'Abrir expediente' });
  await dialogo.getByRole('button', { name: 'Abrir expediente' }).click();
  await expect(ana.getByText(/^Expediente .* abierto\.$/)).toBeVisible();

  const vinculo = await leerVinculo(c.e, c.invitacion);
  expect(vinculo.estado).toBe('EnValidacion');
  expect(vinculo.expedienteAbiertoId).not.toBe(c.expedienteId);
});

test('anular el vínculo es definitivo: exige escribir ANULAR y anula su expediente abierto', async ({ browser }) => {
  const c = await conExpedienteAbierto();

  const ana = await como(browser, c, c.e.ana, `vinculos/${c.invitacion.vinculoId}`);
  await ana.getByRole('button', { name: 'Más acciones' }).click();
  await ana.getByRole('menuitem', { name: /^Anular/ }).click();
  const dialogo = ana.getByRole('dialog', { name: /^Anular vínculo/ });
  await dialogo.getByRole('textbox', { name: 'Motivo' }).fill('Se creó por error.');
  // Sin la palabra de confirmación no deja anular.
  await expect(dialogo.getByRole('button', { name: 'Anular vínculo' })).toBeDisabled();
  await dialogo.getByRole('textbox', { name: /Escribe ANULAR/ }).fill('ANULAR');
  await dialogo.getByRole('button', { name: 'Anular vínculo' }).click();
  await expect(dialogo).toBeHidden();
  await expect(ana.getByText('Vínculo anulado.')).toBeVisible();

  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Anulado');
  expect((await leerExpediente(c.e, c.e.norte, c.expedienteId)).estado).toBe('Anulado');
  // Terminal: ya no ofrece acciones.
  await expect(ana.getByRole('button', { name: 'Reintegrar', exact: true })).toHaveCount(0);
  await expect(ana.getByRole('button', { name: 'Más acciones' })).toHaveCount(0);
});

test('desvincular con el expediente abierto lo anula y el tercero se puede reintegrar', async ({ browser }) => {
  const c = await conExpedienteAbierto();

  const ana = await como(browser, c, c.e.ana, `vinculos/${c.invitacion.vinculoId}`);
  await accionDeVinculo(ana, 'Desvincular', 'Terminó la relación comercial.');
  await expect(ana.getByText('Tercero desvinculado.')).toBeVisible();

  expect(await leerVinculo(c.e, c.invitacion)).toMatchObject({ estado: 'Desvinculado', expedienteAbiertoId: null });
  expect((await leerExpediente(c.e, c.e.norte, c.expedienteId)).estado).toBe('Anulado');
  await expect(ana.getByRole('button', { name: 'Reintegrar', exact: true })).toBeVisible();
});
