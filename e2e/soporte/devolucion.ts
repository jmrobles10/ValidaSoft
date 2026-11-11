import { expect, type FrameLocator } from '@playwright/test';
import { conDiagnostico } from './empresa';

/**
 * Acciones de la web de la empresa sobre el expediente y el vínculo en las pruebas del ciclo de vida: emitir el veredicto del
 * área en un flujo sin documentos, anularlo, reabrir etapas, volver a la fase inicial, desaprobar, anular, desvincular y
 * reintegrar. Los textos son los de apps/web/src/i18n (es-CO).
 */

function escapar(texto: string): string {
  return texto.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/** «Emitir veredicto» y aprobar (flujo sin documentos: el área decide sobre el expediente completo). */
export async function emitirAprobacion(app: FrameLocator, area: string): Promise<void> {
  await conDiagnostico(app, async () => {
    await app.getByRole('button', { name: 'Emitir veredicto' }).first().click();
    const dialogo = app.getByRole('dialog', { name: new RegExp(`^Veredicto de ${escapar(area)}`) });
    await dialogo.getByRole('button', { name: 'Aprobar', exact: true }).click();
    await expect(dialogo).toBeHidden();
  });
}

/** «Emitir veredicto» y observar con el motivo (el expediente vuelve al tercero). */
export async function emitirObservacion(app: FrameLocator, area: string, motivo: string, nota: string): Promise<void> {
  await conDiagnostico(app, async () => {
    await app.getByRole('button', { name: 'Emitir veredicto' }).first().click();
    const dialogo = app.getByRole('dialog', { name: new RegExp(`^Veredicto de ${escapar(area)}`) });
    await dialogo.getByRole('radio', { name: /Observar/ }).click();
    await dialogo.getByRole('checkbox', { name: new RegExp(`^${escapar(motivo)} `) }).check();
    await dialogo.getByRole('textbox', { name: /Observación para el tercero/ }).fill(nota);
    await dialogo.getByRole('button', { name: 'Observar y devolver' }).click();
    await expect(dialogo).toBeHidden();
  });
}

/** Tarjeta de un área en «Etapas y áreas» del detalle del expediente. */
export function tarjetaDeArea(app: FrameLocator, area: string) {
  return app.getByRole('heading', { level: 4, name: area, exact: true }).locator('xpath=../../..');
}

/** Sección «Etapa n» de «Etapas y áreas». */
export function etapa(app: FrameLocator, orden: number) {
  return app.getByRole('region', { name: `Etapa ${String(orden)}`, exact: true });
}

/** Anula el veredicto vigente del área desde su menú de administrador (cierre manual de la novedad). */
export async function anularVeredicto(app: FrameLocator, area: string, justificacion: string): Promise<void> {
  await conDiagnostico(app, async () => {
    await app.getByRole('button', { name: `Acciones de administrador en ${area}` }).click();
    await app.getByRole('menuitem', { name: 'Anular veredicto' }).click();
    const dialogo = app.getByRole('dialog', { name: new RegExp(`^Anular el veredicto de ${escapar(area)}`) });
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill(justificacion);
    await dialogo.getByRole('button', { name: 'Anular veredicto' }).click();
    await expect(dialogo).toBeHidden();
  });
}

/** Reabre una etapa ya aprobada: «Reabrir» en su encabezado y la justificación. */
export async function reabrirEtapa(app: FrameLocator, orden: number, justificacion: string): Promise<void> {
  await conDiagnostico(app, async () => {
    await app.getByRole('button', { name: `Reabrir la etapa ${String(orden)}` }).click();
    const dialogo = app.getByRole('dialog', { name: new RegExp(`^Reabrir la etapa ${String(orden)}`) });
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill(justificacion);
    await dialogo.getByRole('button', { name: 'Reabrir etapa' }).click();
    await expect(dialogo).toBeHidden();
  });
}

export type AccionDeMenu = 'Volver a la fase inicial' | 'Desaprobar expediente' | 'Anular expediente';

/** Una acción del menú «Más acciones» del expediente con su texto (justificación o motivo). */
export async function accionDeExpediente(app: FrameLocator, accion: AccionDeMenu, texto: string, desdeEtapa?: string): Promise<void> {
  await conDiagnostico(app, async () => {
    await app.getByRole('button', { name: 'Más acciones' }).click();
    await app.getByRole('menuitem', { name: accion }).click();
    const dialogo = app.getByRole('dialog', { name: new RegExp(`^${escapar(accion)}`) });
    if (desdeEtapa) {
      await dialogo.getByRole('combobox', { name: /Se revisa de nuevo desde/ }).click();
      await app.getByRole('option', { name: new RegExp(escapar(desdeEtapa)) }).click();
    }
    await dialogo.getByRole('textbox', { name: /Justificación|Motivo/ }).fill(texto);
    await dialogo.getByRole('button', { name: accion }).click();
    await expect(dialogo).toBeHidden();
  });
}

export type AccionDeVinculo = 'Desvincular' | 'Reintegrar' | 'Anular';

/**
 * Una acción del vínculo (en su encabezado, o en «Más acciones» si es destructiva) con su texto. En «Anular» escribe la palabra
 * de confirmación; en «Reintegrar» `sinRevalidar` elige la opción de no revalidar.
 */
export async function accionDeVinculo(app: FrameLocator, accion: AccionDeVinculo, texto: string, { sinRevalidar = false } = {}): Promise<void> {
  await conDiagnostico(app, async () => {
    const titulo = { Desvincular: 'Desvincular tercero', Reintegrar: 'Reintegrar vínculo', Anular: 'Anular vínculo' }[accion];
    if (accion === 'Reintegrar') {
      await app.getByRole('button', { name: 'Reintegrar', exact: true }).click();
    } else {
      await app.getByRole('button', { name: 'Más acciones' }).click();
      await app.getByRole('menuitem', { name: new RegExp(`^${accion}`) }).click();
    }
    const dialogo = app.getByRole('dialog', { name: new RegExp(`^${escapar(titulo)}`) });
    if (accion === 'Reintegrar' && sinRevalidar) await dialogo.getByRole('radio', { name: /Reintegrar sin revalidar/ }).click();
    await dialogo.getByRole('textbox', { name: /Motivo|Justificación/ }).first().fill(texto);
    if (accion === 'Anular') await dialogo.getByRole('textbox', { name: /Escribe ANULAR/ }).fill('ANULAR');
    await dialogo.getByRole('button', { name: new RegExp(`^${escapar(titulo)}$|^${accion}$`) }).click();
    await expect(dialogo).toBeHidden();
  });
}
