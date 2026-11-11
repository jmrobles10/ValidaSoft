import { expect, test, type Browser, type FrameLocator } from '@playwright/test';
import { abrirEmbebido } from '../soporte/embebido';
import { accionDeVinculo } from '../soporte/devolucion';
import {
  abrirExpedienteManual,
  aprobarPorApi,
  crearTipoDeVinculo,
  invitarVinculo,
  leerCertificado,
  leerVinculo,
  publicarFlujoSinDocumentos,
} from '../soporte/flujos';
import { crearEscenario, nuevoTercero, type Escenario, type Invitacion } from '../soporte/semilla';

/**
 * Desvincular y reintegrar (DS-3), lo que el SRM hace con reintegrarProveedor y su documental 0/1: revalidar (renovación con todos
 * los documentos del flujo) o reintegrar sin revalidar (restituye el estado y emite el certificado de nuevo con lo ya aprobado,
 * solo si sigue vigente). Flujo de Norte sin documentos con una etapa: Compras (Rubén).
 */

interface Caso {
  e: Escenario;
  invitacion: Invitacion;
  expedienteId: string;
}

async function conVinculo(): Promise<Caso> {
  const e = await crearEscenario();
  await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
  await publicarFlujoSinDocumentos(e, e.norte, 'ALIADOS', 'ALIADO', [[{ area: e.norte.areas.compras! }]]);
  const invitacion = await invitarVinculo(e, e.norte, nuevoTercero(), 'ALIADO');
  return { e, invitacion, expedienteId: await abrirExpedienteManual(e, invitacion) };
}

async function conVinculoActivo(): Promise<Caso> {
  const c = await conVinculo();
  await aprobarPorApi(c.e, c.invitacion, c.expedienteId, c.e.norte.areas.compras!, c.e.ruben);
  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Activo');
  return c;
}

/** Ana abre el vínculo en la web de Norte. */
async function enElVinculo(browser: Browser, c: Caso): Promise<FrameLocator> {
  const ventana = await (await browser.newContext()).newPage();
  const app = await abrirEmbebido(ventana, c.e, c.e.ana, c.e.norte, `vinculos/${c.invitacion.vinculoId}`);
  await expect(app.getByRole('heading', { level: 1, name: c.invitacion.nombre })).toBeVisible();
  return app;
}

test('desvincular y reintegrar sin revalidar: el vínculo vuelve a Activo y el certificado se emite de nuevo, sin abrir expediente', async ({ browser }) => {
  const c = await conVinculoActivo();
  expect((await leerCertificado(c.e, c.invitacion)).actual).toMatchObject({ emision: 1, estado: 'Vigente' });

  const ana = await enElVinculo(browser, c);
  await accionDeVinculo(ana, 'Desvincular', 'Terminó el contrato marco.');
  await expect(ana.getByText('Tercero desvinculado.')).toBeVisible();
  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Desvinculado');
  expect((await leerCertificado(c.e, c.invitacion)).actual?.estado).toBe('Revocado');

  // Lo aprobado sigue vigente: se puede reintegrar sin revalidar.
  await accionDeVinculo(ana, 'Reintegrar', 'Se renovó el contrato marco.', { sinRevalidar: true });
  await expect(ana.getByText('Vínculo reintegrado.')).toBeVisible();

  expect(await leerVinculo(c.e, c.invitacion)).toMatchObject({ estado: 'Activo', expedienteAbiertoId: null });
  const certificados = await leerCertificado(c.e, c.invitacion);
  expect(certificados.actual).toMatchObject({ emision: 2, estado: 'Vigente', expedienteId: c.expedienteId });
  expect(certificados.emisiones.map((x) => x.estado)).toEqual(['Vigente', 'Revocado']);
  // El historial del vínculo cuenta lo que pasó.
  await expect(ana.getByText('Vínculo reintegrado', { exact: true })).toBeVisible();
});

test('reintegrar revalidando abre una renovación y, al aprobarse, el vínculo vuelve a Activo con otro certificado', async ({ browser }) => {
  const c = await conVinculoActivo();

  const ana = await enElVinculo(browser, c);
  await accionDeVinculo(ana, 'Desvincular', 'Terminó el contrato marco.');
  await expect(ana.getByText('Tercero desvinculado.')).toBeVisible();

  await accionDeVinculo(ana, 'Reintegrar', 'Retoma la relación.');
  await expect(ana.getByText('Vínculo reintegrado.')).toBeVisible();

  const vinculo = await leerVinculo(c.e, c.invitacion);
  expect(vinculo.estado).toBe('EnValidacion');
  const renovacionId = vinculo.expedienteAbiertoId;
  expect(renovacionId).not.toBeNull();
  expect(renovacionId).not.toBe(c.expedienteId);
  // La tarjeta del vínculo muestra el expediente en curso: es la renovación abierta al reintegrar.
  await expect(ana.getByText('Expediente en curso')).toBeVisible();

  await aprobarPorApi(c.e, c.invitacion, renovacionId!, c.e.norte.areas.compras!, c.e.ruben);
  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Activo');
  const certificados = await leerCertificado(c.e, c.invitacion);
  expect(certificados.actual).toMatchObject({ emision: 2, estado: 'Vigente', expedienteId: renovacionId });
});

test('un vínculo que nunca fue aprobado no se reintegra sin revalidar: el diálogo dice por qué', async ({ browser }) => {
  const c = await conVinculo(); // con el expediente abierto, sin aprobar

  const ana = await enElVinculo(browser, c);
  await accionDeVinculo(ana, 'Desvincular', 'Se canceló la negociación.');
  await expect(ana.getByText('Tercero desvinculado.')).toBeVisible();
  // Desvincular anula el expediente abierto en la misma transacción.
  expect((await leerVinculo(c.e, c.invitacion)).expedienteAbiertoId).toBeNull();

  await ana.getByRole('button', { name: 'Reintegrar', exact: true }).click();
  const dialogo = ana.getByRole('dialog', { name: /^Reintegrar vínculo/ });
  await expect(dialogo.getByText('No se puede reintegrar sin revalidar:')).toBeVisible();
  await expect(dialogo.getByRole('listitem').filter({ hasText: /El vínculo nunca fue aprobado/ })).toBeVisible();
  await expect(dialogo.getByRole('radio', { name: /Reintegrar sin revalidar/ })).toBeDisabled();
  await expect(dialogo.getByRole('radio', { name: /^Revalidar/ })).toBeChecked();

  // Revalidar: sin aprobación previa vuelve a Invitado para empezar de nuevo.
  await dialogo.getByRole('textbox', { name: 'Justificación' }).fill('Nuevo contrato.');
  await dialogo.getByRole('button', { name: 'Reintegrar vínculo' }).click();
  await expect(dialogo).toBeHidden();
  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Invitado');
});
