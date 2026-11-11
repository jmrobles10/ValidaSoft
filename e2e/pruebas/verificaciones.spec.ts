import { expect, test, type Browser, type FrameLocator } from '@playwright/test';
import { llamar } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion, regionDeArea } from '../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Escenario, type Invitacion, type Persona } from '../soporte/semilla';

/**
 * Verificaciones de cumplimiento (H7) con el proveedor de listas simulado de desarrollo: un nombre con «coincidencia»
 * aparece en OFAC. El flujo de proveedores de Norte pide además listas restrictivas (y, en la primera prueba, el estudio
 * del oficial): aunque Compras y Cumplimiento aprueben, el expediente no se aprueba hasta que la verificación se satisface.
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

/** Agrega las verificaciones al flujo de proveedores de Norte y lo vuelve a publicar. */
async function pedirVerificaciones(e: Escenario, verificaciones: { tipo: string; bloqueante: boolean }[]): Promise<void> {
  const flujos = await llamar<{ id: string; codigo: string }[]>('GET', '/api/v1/flujos?tipoVinculo=PROVEEDOR', { llave: e.admin, empresa: e.norte.id });
  const flujo = flujos.find((f) => f.codigo === 'HABILITACION');
  if (!flujo) throw new Error('Falta el flujo HABILITACION de Norte.');
  await llamar('PUT', `/api/v1/flujos/${flujo.id}`, { llave: e.admin, empresa: e.norte.id, cuerpo: { verificaciones } });
  await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { llave: e.admin, empresa: e.norte.id });
}

/** El tercero acepta y sube RUT y Cámara (el expediente queda en revisión y las listas se consultan solas). */
async function enRevision(page: import('@playwright/test').Page, e: Escenario, nombre: string): Promise<Invitacion> {
  const invitacion = await invitar(e, e.norte, nuevoTercero(nombre));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${nombre}`) },
    { nombre: CAMARA, archivo: pdf(`Camara ${nombre}`) },
  ]);
  return invitacion;
}

/** La última área aprueba, pero el expediente sigue en revisión: falta una verificación bloqueante. */
async function aprobarUltimaArea(app: FrameLocator, area: string, falta: RegExp): Promise<void> {
  await regionDeArea(app, area).getByRole('button', { name: /^Aprobar área/ }).click();
  const dialogo = app.getByRole('dialog', { name: new RegExp(`^Aprobar ${area}`) });
  await dialogo.getByRole('button', { name: /^Aprobar/ }).last().click();
  await expect(dialogo).toBeHidden();
  const aviso = app.getByRole('alert').filter({ hasText: 'Falta cumplimiento para aprobar' });
  await expect(aviso).toContainText(falta);
}

async function persona(browser: Browser, e: Escenario, quien: Persona, ruta = 'bandeja'): Promise<FrameLocator> {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, quien, e.norte, ruta);
}

/** Recarga el expediente (pasando por Vínculos) hasta que las listas muestren el estado esperado: la consulta es automática. */
async function esperarListas(app: FrameLocator, tercero: string, area: string, estado: string): Promise<void> {
  await expect(async () => {
    await abrirDesdeBandeja(app, tercero, area);
    await expect(app.getByRole('region', { name: 'Listas restrictivas' })).toContainText(estado, { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
}

test('sin coincidencias: las áreas aprueban y el expediente espera el estudio del oficial de cumplimiento', async ({ page, browser }) => {
  const e = await crearEscenario();
  await pedirVerificaciones(e, [
    { tipo: 'ListasRestrictivas', bloqueante: true },
    { tipo: 'EstudioDeCumplimiento', bloqueante: true },
  ]);
  const nombre = 'Aceros del Norte S.A.S.';
  await enRevision(page, e, nombre);

  const ruben = await persona(browser, e, e.ruben);
  await abrirDesdeBandeja(ruben, nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');

  const olga = await persona(browser, e, e.olga);
  await esperarListas(olga, nombre, 'Cumplimiento', 'Satisfecha');
  const listas = olga.getByRole('region', { name: 'Listas restrictivas' });
  await expect(listas).toContainText('Ninguna persona aparece en las listas consultadas.');
  await expect(listas.getByRole('button', { name: 'Evidencia (PDF)' })).toBeVisible();
  // Todas las áreas aprueban, pero el estudio bloquea: sigue en revisión y lo dice.
  await aprobarUltimaArea(olga, 'Cumplimiento', /Estudio de cumplimiento \(pendiente\)/);
  await esperarEstadoDelTercero(page, 'EnRevision');

  // El estudio con riesgo alto exige debida diligencia ampliada.
  await olga.getByRole('button', { name: 'Registrar estudio' }).click();
  const dialogo = olga.getByRole('dialog', { name: /Estudio de cumplimiento/ });
  await dialogo.getByRole('button', { name: '3 · Alto' }).click();
  await dialogo.getByRole('combobox', { name: 'Concepto' }).click();
  await olga.getByRole('option', { name: 'Favorable', exact: true }).click();
  await dialogo.getByRole('button', { name: 'Registrar estudio' }).click();
  await expect(dialogo.getByText('Con riesgo alto la debida diligencia debe ser ampliada.')).toBeVisible();
  await dialogo.getByRole('combobox', { name: 'Debida diligencia' }).click();
  await olga.getByRole('option', { name: 'Ampliada' }).click();
  await dialogo.getByRole('textbox', { name: 'Observaciones' }).fill('Sector de alto riesgo; sin hallazgos en listas ni prensa.');
  await dialogo.getByRole('button', { name: 'Registrar estudio' }).click();
  await expect(dialogo).toBeHidden();

  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
  await expect(olga.getByRole('region', { name: 'Estudio de cumplimiento' })).toContainText('3 · Alto');
  await esperarEstadoDelTercero(page, 'Aprobado');
});

test('con coincidencia: el oficial la ve en su bandeja, la descarta con justificación y el expediente se aprueba', async ({ page, browser }) => {
  const e = await crearEscenario();
  await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
  const nombre = 'Ferretería Coincidencia S.A.S.';
  await enRevision(page, e, nombre);

  const ruben = await persona(browser, e, e.ruben);
  await abrirDesdeBandeja(ruben, nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');
  // Rubén es revisor: ve las verificaciones pero no decide como Cumplimiento.
  await esperarListas(ruben, nombre, 'Compras', 'Coincidencia por analizar');
  await expect(ruben.getByRole('button', { name: 'Descartar' })).toHaveCount(0);

  const olga = await persona(browser, e, e.olga);
  await esperarListas(olga, nombre, 'Cumplimiento', 'Coincidencia por analizar');
  await aprobarUltimaArea(olga, 'Cumplimiento', /Listas restrictivas \(coincidencia por analizar\)/);
  await esperarEstadoDelTercero(page, 'EnRevision');

  // Su bandeja de cumplimiento lo muestra como lo único que falta.
  await irASeccion(olga, 'Cumplimiento');
  const fila = olga.getByRole('row').filter({ hasText: nombre });
  await expect(fila).toContainText('Solo falta esto para aprobar');
  await fila.getByRole('link', { name: 'Abrir' }).click();

  const coincidencia = olga.getByRole('listitem', { name: `${nombre} · OFAC (SDN)` });
  await expect(coincidencia).toContainText(nombre.toUpperCase());
  await coincidencia.getByRole('button', { name: 'Descartar' }).click();
  const dialogo = olga.getByRole('dialog', { name: 'Descartar coincidencia' });
  await dialogo.getByRole('button', { name: 'Guardar decisión' }).click();
  await expect(dialogo.getByText('Escribe la justificación.')).toBeVisible();
  await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Homónimo: el NIT del listado es otro.');
  await dialogo.getByRole('button', { name: 'Guardar decisión' }).click();
  await expect(dialogo).toBeHidden();

  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
  await expect(coincidencia).toContainText('Homónimo: el NIT del listado es otro.');
  await esperarEstadoDelTercero(page, 'Aprobado');

  // El informe SAGRILAFT lo cuenta y se exporta a Excel.
  await irASeccion(olga, 'Cumplimiento');
  await olga.getByRole('tab', { name: 'Informe SAGRILAFT' }).click();
  await expect(olga.getByRole('table', { name: 'Informe SAGRILAFT' })).toContainText(nombre);
  const pagina = olga.owner().page();
  const [descarga] = await Promise.all([pagina.waitForEvent('download'), olga.getByRole('button', { name: 'Exportar a Excel' }).click()]);
  expect(descarga.suggestedFilename()).toMatch(/\.xlsx$/);
});
