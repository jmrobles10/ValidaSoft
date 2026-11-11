import { expect, test, type Browser, type FrameLocator } from '@playwright/test';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, irASeccion } from '../soporte/empresa';
import { emitirAprobacion, etapa, tarjetaDeArea } from '../soporte/devolucion';
import {
  abrirExpedienteManual,
  aprobarPorApi,
  crearArea,
  crearTipoDeVinculo,
  invitarVinculo,
  leerExpediente,
  leerVinculo,
  publicarFlujoSinDocumentos,
} from '../soporte/flujos';
import { crearEscenario, nuevoTercero, type Escenario, type Invitacion, type Persona } from '../soporte/semilla';

/**
 * Cómo avanza la revisión: varias etapas en secuencia (cada área ve el expediente solo cuando le toca), un área con dos aprobadores
 * (dos personas distintas) y la separación de funciones (quien invitó no decide). Flujos de Norte sin documentos.
 */

async function nuevoCaso(
  configurar: (e: Escenario) => Promise<void>,
  opciones: { delegado?: (e: Escenario) => Persona } = {},
): Promise<{ e: Escenario; invitacion: Invitacion; expedienteId: string }> {
  const e = await crearEscenario();
  await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
  await configurar(e);
  const invitacion = await invitarVinculo(e, e.norte, nuevoTercero(), 'ALIADO', opciones.delegado?.(e));
  return { e, invitacion, expedienteId: await abrirExpedienteManual(e, invitacion) };
}

async function como(browser: Browser, e: Escenario, persona: Persona, ruta = 'bandeja'): Promise<FrameLocator> {
  const ventana = await (await browser.newContext()).newPage();
  return abrirEmbebido(ventana, e, persona, e.norte, ruta);
}

/** Filas de la bandeja de la persona para el tercero (se vuelve a pedir: pasa por «Vínculos» para que se monte de nuevo). */
async function filasDe(app: FrameLocator, tercero: string, area: string) {
  await irASeccion(app, 'Vínculos');
  await irASeccion(app, 'Bandeja');
  await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
  return app.getByRole('row', { name: new RegExp(`Abrir expediente .* de ${tercero.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} \\(${area}\\)`) });
}

test('varias etapas en secuencia: cada área ve el expediente en su bandeja solo cuando le toca', async ({ browser }) => {
  const { e, invitacion, expedienteId } = await nuevoCaso(async (e) => {
    const legal = await crearArea(e, e.norte, 'legal', 'Legal', [e.olga]);
    await publicarFlujoSinDocumentos(e, e.norte, 'SECUENCIA', 'ALIADO', [
      [{ area: e.norte.areas.compras! }],
      [{ area: legal }],
      [{ area: e.norte.areas.cumplimiento!, puedeRechazar: true }],
    ]);
  });

  // Olga (Legal y Cumplimiento) no ve nada mientras la etapa 1 no se apruebe.
  const olga = await como(browser, e, e.olga);
  await expect(await filasDe(olga, invitacion.nombre, 'Legal')).toHaveCount(0);
  await expect(await filasDe(olga, invitacion.nombre, 'Cumplimiento')).toHaveCount(0);

  // Compras (etapa 1) aprueba desde su bandeja; las etapas 2 y 3 siguen esperando.
  const ruben = await como(browser, e, e.ruben);
  await abrirDesdeBandeja(ruben, invitacion.nombre, 'Compras');
  await expect(etapa(ruben, 2)).toContainText('En espera');
  await expect(etapa(ruben, 3)).toContainText('En espera');
  await emitirAprobacion(ruben, 'Compras');
  await expect(etapa(ruben, 1)).toContainText('Aprobada');
  await expect(etapa(ruben, 2)).toContainText('En curso');
  await expect(etapa(ruben, 3)).toContainText('En espera');

  // Ahora Olga ve Legal (etapa 2) y todavía no Cumplimiento (etapa 3).
  await expect(await filasDe(olga, invitacion.nombre, 'Cumplimiento')).toHaveCount(0);
  await abrirDesdeBandeja(olga, invitacion.nombre, 'Legal');
  await emitirAprobacion(olga, 'Legal');
  await expect(etapa(olga, 3)).toContainText('En curso');
  expect((await leerExpediente(e, e.norte, expedienteId)).etapaActual).toBe(3);

  // Y cuando Legal aprueba, Cumplimiento (etapa 3) recibe el expediente; al aprobarlo, el vínculo queda activo.
  await abrirDesdeBandeja(olga, invitacion.nombre, 'Cumplimiento');
  await emitirAprobacion(olga, 'Cumplimiento');
  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
  expect((await leerVinculo(e, invitacion)).estado).toBe('Activo');
});

test('un área con dos aprobadores pide dos personas distintas: la misma no cuenta dos veces', async ({ browser }) => {
  const { e, invitacion, expedienteId } = await nuevoCaso(async (e) => {
    const comite = await crearArea(e, e.norte, 'comite', 'Comité', [e.ruben, e.olga]);
    await publicarFlujoSinDocumentos(e, e.norte, 'COMITE', 'ALIADO', [[{ area: comite, aprobadores: 2 }]]);
  });

  const ruben = await como(browser, e, e.ruben, `expedientes/${expedienteId}`);
  await expect(ruben.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
  await expect(tarjetaDeArea(ruben, 'Comité')).toContainText('Faltan 2 aprobaciones');
  await emitirAprobacion(ruben, 'Comité');

  // Con la primera aprobación el área sigue pendiente y Rubén ya no puede votar otra vez.
  await expect(tarjetaDeArea(ruben, 'Comité')).toContainText('Falta 1 aprobación');
  await expect(tarjetaDeArea(ruben, 'Comité')).toContainText('Ya emitiste tu veredicto; falta otra persona del área.');
  await expect(ruben.getByRole('button', { name: 'Emitir veredicto' })).toHaveCount(0);
  const parcial = await leerExpediente(e, e.norte, expedienteId);
  expect(parcial.estado).toBe('EnRevision');
  expect(parcial.etapas[0]?.areas[0]).toMatchObject({ aprobaciones: 1, aprobadoresRequeridos: 2 });

  // Olga es la segunda persona: el área se aprueba y el expediente también.
  const olga = await como(browser, e, e.olga, `expedientes/${expedienteId}`);
  await expect(olga.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
  await emitirAprobacion(olga, 'Comité');
  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
  expect((await leerVinculo(e, invitacion)).estado).toBe('Activo');
});

test('separación de funciones: quien invitó al tercero no puede decidir sobre su expediente', async ({ browser }) => {
  const { e, invitacion, expedienteId } = await nuevoCaso(
    async (e) => {
      const comite = await crearArea(e, e.norte, 'comite', 'Comité', [e.ana, e.olga]);
      await publicarFlujoSinDocumentos(e, e.norte, 'SEPARACION', 'ALIADO', [[{ area: comite }]], { separacionDeFunciones: true });
    },
    // Ana crea el vínculo y envía la invitación: queda como quien invitó.
    { delegado: (e) => e.ana },
  );

  // Ana es del Comité y tiene permiso de revisar, pero invitó: la API la frena con un mensaje que lo explica.
  const ana = await como(browser, e, e.ana, `expedientes/${expedienteId}`);
  await expect(ana.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
  await ana.getByRole('button', { name: 'Emitir veredicto' }).click();
  const dialogo = ana.getByRole('dialog', { name: /^Veredicto de Comité/ });
  await dialogo.getByRole('button', { name: 'Aprobar', exact: true }).click();
  await expect(dialogo.getByRole('alert')).toContainText(/separación de funciones/);
  await dialogo.getByRole('button', { name: 'Cancelar' }).click();
  expect((await leerExpediente(e, e.norte, expedienteId)).estado).toBe('EnRevision');

  // Olga, que no invitó, sí decide.
  await aprobarPorApi(e, invitacion, expedienteId, e.norte.areas.comite!, e.olga);
  expect((await leerVinculo(e, invitacion)).estado).toBe('Activo');
});
