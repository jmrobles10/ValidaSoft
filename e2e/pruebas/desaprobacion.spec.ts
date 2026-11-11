import { expect, test, type Browser, type FrameLocator } from '@playwright/test';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja } from '../soporte/empresa';
import { accionDeExpediente, emitirAprobacion, etapa, reabrirEtapa, tarjetaDeArea } from '../soporte/devolucion';
import {
  abrirExpedienteManual,
  aprobarPorApi,
  crearTipoDeVinculo,
  invitarVinculo,
  leerCertificado,
  leerExpediente,
  leerVinculo,
  publicarFlujoSinDocumentos,
} from '../soporte/flujos';
import { crearEscenario, nuevoTercero, type Escenario, type Invitacion, type Persona } from '../soporte/semilla';

/**
 * Desaprobar (DS-1, DS-2), lo que el SRM hace con DesaprobarTercero, DesaprobarAFlujo y DesaprobarAEstadoInicial: reabrir una
 * etapa ya aprobada, devolver todo a la fase inicial y desaprobar un expediente aprobado (el vínculo vuelve a validación y su
 * certificado se revoca). Flujo de Norte sin documentos: etapa 1 Compras (Rubén) y etapa 2 Cumplimiento (Olga).
 */

interface Caso {
  e: Escenario;
  invitacion: Invitacion;
  expedienteId: string;
}

async function enDosEtapas(): Promise<Caso> {
  const e = await crearEscenario();
  await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
  const compras = e.norte.areas.compras!;
  const cumplimiento = e.norte.areas.cumplimiento!;
  await publicarFlujoSinDocumentos(e, e.norte, 'ALIADOS', 'ALIADO', [[{ area: compras }], [{ area: cumplimiento, puedeRechazar: true }]]);
  const invitacion = await invitarVinculo(e, e.norte, nuevoTercero(), 'ALIADO');
  const expedienteId = await abrirExpedienteManual(e, invitacion);
  return { e, invitacion, expedienteId };
}

async function aprobarTodo(c: Caso): Promise<void> {
  await aprobarPorApi(c.e, c.invitacion, c.expedienteId, c.e.norte.areas.compras!, c.e.ruben);
  await aprobarPorApi(c.e, c.invitacion, c.expedienteId, c.e.norte.areas.cumplimiento!, c.e.olga);
  expect((await leerExpediente(c.e, c.e.norte, c.expedienteId)).estado).toBe('Aprobado');
}

/** Ventana aparte con Validasoft embebido como `persona`, directo en el expediente del caso. */
async function enElExpediente(browser: Browser, c: Caso, persona: Persona): Promise<FrameLocator> {
  const ventana = await (await browser.newContext()).newPage();
  const app = await abrirEmbebido(ventana, c.e, persona, c.e.norte, `expedientes/${c.expedienteId}`);
  await expect(app.getByRole('heading', { level: 1, name: c.invitacion.nombre })).toBeVisible();
  return app;
}

test('reabrir una etapa aprobada: vuelve a pendiente, las siguientes esperan y el flujo sigue desde ahí', async ({ browser }) => {
  const c = await enDosEtapas();
  await aprobarPorApi(c.e, c.invitacion, c.expedienteId, c.e.norte.areas.compras!, c.e.ruben);

  const ana = await enElExpediente(browser, c, c.e.ana);
  await expect(etapa(ana, 1)).toContainText('Aprobada');
  await expect(etapa(ana, 2)).toContainText('En curso');
  await reabrirEtapa(ana, 1, 'Compras aprobó sin ver el certificado de la Cámara.');

  // La etapa 1 vuelve a revisarse; la 2 espera; la aprobación anterior queda en el historial como revertida.
  await expect(etapa(ana, 1)).toContainText('En curso');
  await expect(etapa(ana, 2)).toContainText('En espera');
  await expect(tarjetaDeArea(ana, 'Compras')).toContainText('Revertido por un administrador');
  await expect(ana.getByText('Devuelto a revisión por un administrador')).toBeVisible();
  await expect(ana.getByText(/Ana Administradora reabrió la etapa 1 el .*: esa etapa y las siguientes vuelven a revisarse\./)).toBeVisible();
  const despues = await leerExpediente(c.e, c.e.norte, c.expedienteId);
  expect(despues.etapaActual).toBe(1);
  expect(despues.desaprobaciones).toBe(1);

  // Rubén decide de nuevo y luego Olga: el expediente se aprueba y el vínculo queda activo.
  const ventana = await (await browser.newContext()).newPage();
  const ruben = await abrirEmbebido(ventana, c.e, c.e.ruben);
  await abrirDesdeBandeja(ruben, c.invitacion.nombre, 'Compras');
  await emitirAprobacion(ruben, 'Compras');
  await expect(etapa(ruben, 2)).toContainText('En curso');

  const otra = await (await browser.newContext()).newPage();
  const olga = await abrirEmbebido(otra, c.e, c.e.olga);
  await abrirDesdeBandeja(olga, c.invitacion.nombre, 'Cumplimiento');
  await emitirAprobacion(olga, 'Cumplimiento');
  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Activo');
});

test('desaprobar un expediente aprobado: vuelve a revisión, el vínculo a validación y el certificado se revoca', async ({ browser }) => {
  const c = await enDosEtapas();
  await aprobarTodo(c);
  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Activo');
  const antes = await leerCertificado(c.e, c.invitacion);
  expect(antes.actual).toMatchObject({ emision: 1, estado: 'Vigente' });

  const ana = await enElExpediente(browser, c, c.e.ana);
  await expect(ana.getByRole('alert').getByText('Expediente aprobado')).toBeVisible();
  await accionDeExpediente(ana, 'Desaprobar expediente', 'Apareció una inhabilidad que no estaba en las listas.');

  await expect(ana.getByText('Devuelto a revisión por un administrador')).toBeVisible();
  await expect(ana.getByText(/desaprobó el expediente el .* y se revisa de nuevo desde la etapa 2\./)).toBeVisible();
  await expect(etapa(ana, 1)).toContainText('Aprobada'); // la primera etapa conserva su aprobación
  await expect(etapa(ana, 2)).toContainText('En curso');
  await expect(ana.getByRole('alert').getByText('Expediente aprobado')).toHaveCount(0);

  // El vínculo vuelve a validación con este mismo expediente y el certificado queda revocado: todo en la misma transacción.
  expect(await leerVinculo(c.e, c.invitacion)).toMatchObject({ estado: 'EnValidacion', expedienteAbiertoId: c.expedienteId });
  const revocado = await leerCertificado(c.e, c.invitacion);
  expect(revocado.actual).toMatchObject({ emision: 1, estado: 'Revocado', bloqueante: true });

  // Cumplimiento aprueba de nuevo: el vínculo vuelve a Activo y se emite un certificado nuevo (emisión 2).
  const ventana = await (await browser.newContext()).newPage();
  const olga = await abrirEmbebido(ventana, c.e, c.e.olga);
  await abrirDesdeBandeja(olga, c.invitacion.nombre, 'Cumplimiento');
  await emitirAprobacion(olga, 'Cumplimiento');
  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();

  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Activo');
  const despues = await leerCertificado(c.e, c.invitacion);
  expect(despues.actual).toMatchObject({ emision: 2, estado: 'Vigente' });
  expect(despues.emisiones.map((x) => x.estado)).toEqual(['Vigente', 'Revocado']);
});

test('desaprobar desde la primera etapa repite todo el flujo y el vínculo pasa a En validación', async ({ browser }) => {
  const c = await enDosEtapas();
  await aprobarTodo(c);

  const ana = await enElExpediente(browser, c, c.e.ana);
  await accionDeExpediente(ana, 'Desaprobar expediente', 'Cambió la política de contratación.', 'La primera etapa');

  await expect(etapa(ana, 1)).toContainText('En curso');
  await expect(etapa(ana, 2)).toContainText('En espera');
  await expect(ana.getByText(/desaprobó el expediente el .* y se revisa de nuevo desde la etapa 1\./)).toBeVisible();
  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('EnValidacion');
  expect((await leerCertificado(c.e, c.invitacion)).actual?.estado).toBe('Revocado');
});

test('volver a la fase inicial de un expediente en revisión descarta lo decidido y deja de ofrecerse al estar en el comienzo', async ({ browser }) => {
  const c = await enDosEtapas();
  await aprobarPorApi(c.e, c.invitacion, c.expedienteId, c.e.norte.areas.compras!, c.e.ruben);

  const ana = await enElExpediente(browser, c, c.e.ana);
  await accionDeExpediente(ana, 'Volver a la fase inicial', 'Cambió la política de contratación.');

  await expect(etapa(ana, 1)).toContainText('En curso');
  await expect(etapa(ana, 2)).toContainText('En espera');
  await expect(ana.getByText(/lo devolvió a su fase inicial el .*: el flujo se repite desde la primera etapa\./)).toBeVisible();
  await expect(tarjetaDeArea(ana, 'Compras')).toContainText('Revertido por un administrador');

  // En el comienzo no hay nada que repetir: «Más acciones» solo ofrece anular.
  await ana.getByRole('button', { name: 'Más acciones' }).click();
  await expect(ana.getByRole('menuitem', { name: 'Volver a la fase inicial' })).toHaveCount(0);
  await expect(ana.getByRole('menuitem', { name: 'Anular expediente' })).toBeVisible();
  expect((await leerExpediente(c.e, c.e.norte, c.expedienteId)).etapaActual).toBe(1);
});

test('solo el administrador ve las acciones de desaprobar; un revisor no', async ({ browser }) => {
  const c = await enDosEtapas();
  await aprobarPorApi(c.e, c.invitacion, c.expedienteId, c.e.norte.areas.compras!, c.e.ruben);

  const ventana = await (await browser.newContext()).newPage();
  const ruben = await abrirEmbebido(ventana, c.e, c.e.ruben, c.e.norte, `expedientes/${c.expedienteId}`);
  await expect(ruben.getByRole('heading', { level: 1, name: c.invitacion.nombre })).toBeVisible();
  await expect(etapa(ruben, 1)).toContainText('Aprobada');
  await expect(ruben.getByRole('button', { name: 'Reabrir la etapa 1' })).toHaveCount(0);
  await expect(ruben.getByRole('button', { name: 'Más acciones' })).toHaveCount(0);
});
