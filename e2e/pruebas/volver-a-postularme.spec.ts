import { expect, test } from '@playwright/test';
import { llamar } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, rechazar } from '../soporte/empresa';
import { leerCertificado, leerExpediente, leerVinculo } from '../soporte/flujos';
import { completarInvitacion, esperarEstadoDelTercero, subirDocumento, tarjeta } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../soporte/semilla';

/**
 * «Volver a postularme» de punta a punta (DS-4, docs/16 §4.10): un proveedor sube sus documentos, Cumplimiento rechaza el expediente
 * con un motivo, el proveedor lee el motivo (nunca la justificación interna ni el área), vuelve a postularse, sube la versión corregida
 * del documento que falló y, esta vez, las dos áreas aprueban: el vínculo queda activo y con su certificado. Flujo de habilitación de
 * Norte: RUT y Cámara de comercio, con Compras (Rubén) y Cumplimiento (Olga, que puede rechazar) en paralelo.
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';
const JUSTIFICACION = 'El representante legal aparece en una lista restrictiva con otro nombre.';

test.setTimeout(180_000);

test('el proveedor rechazado lee el motivo, vuelve a postularse, corrige su RUT y las dos áreas lo aprueban: vínculo activo y certificado emitido', async ({ page, browser }) => {
  const e = await crearEscenario();
  // El flujo de habilitación de Norte permite volver a postularse al instante tras un rechazo.
  const flujos = await llamar<{ id: string; tipoVinculo: string }[]>('GET', '/api/v1/flujos?tipoVinculo=PROVEEDOR', { llave: e.admin, empresa: e.norte.id });
  const habilitacion = flujos[0];
  if (!habilitacion) throw new Error('No está el flujo de habilitación de Norte.');
  await llamar('PUT', `/api/v1/flujos/${habilitacion.id}`, { llave: e.admin, empresa: e.norte.id, cuerpo: { reintento: { permitido: true, diasEspera: 0 } } });
  await llamar('POST', `/api/v1/flujos/${habilitacion.id}/publicar`, { llave: e.admin, empresa: e.norte.id });

  const tercero = nuevoTercero('Cementos del Valle S.A.S.');
  const invitacion = await invitar(e, e.norte, tercero, 'PROVEEDOR');
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${tercero.identificacion.numero} primera`) },
    { nombre: CAMARA, archivo: pdf(`Camara ${tercero.identificacion.numero}`) },
  ]);

  // Cumplimiento (Olga) rechaza el expediente por un hallazgo; Compras no alcanzó a decidir.
  const olga = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.olga);
  await abrirDesdeBandeja(olga, tercero.nombre, 'Cumplimiento');
  await rechazar(olga, ['No corresponde al tercero'], JUSTIFICACION);
  expect((await leerVinculo(e, invitacion)).estado).toBe('Invitado');

  // El proveedor ve el rechazo con el texto del motivo; no ve la justificación ni quién la escribió.
  await page.goto('/empresas');
  const norte = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.norte.nombre, exact: true }) });
  await expect(norte.getByText(`${e.norte.nombre} cerró tu revisión sin aprobarla el`, { exact: false })).toBeVisible();
  await expect(norte.getByText('El documento no corresponde a la persona o empresa que se está validando.')).toBeVisible();
  await expect(norte).not.toContainText('lista restrictiva');
  await expect(norte).not.toContainText('Cumplimiento');
  await expect(norte).not.toContainText('Olga');

  // «Volver a postularme» abre una revisión nueva: sus documentos ya están en la bóveda y entran a ella, sin cargarlos otra vez.
  // La lista se pinta de nuevo sola: si el clic cae en ese momento la página no cambia, así que se repite hasta que abre la revisión.
  await expect(async () => {
    if (/\/empresas\/?$/.test(new URL(page.url()).pathname)) await norte.getByRole('button', { name: 'Volver a postularme' }).click({ timeout: 3000 });
    await expect(page).toHaveURL(/\/expediente\/[0-9a-f-]{36}$/, { timeout: 5000 });
  }).toPass({ timeout: 30_000 });
  await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.norte.nombre}` })).toBeVisible();
  await expect(tarjeta(page, RUT)).toContainText('Versión 1');
  await expect(tarjeta(page, CAMARA)).toContainText('Versión 1');
  const reabierto = await leerVinculo(e, invitacion);
  expect(reabierto.estado).toBe('EnValidacion');
  const nuevo = reabierto.expedienteAbiertoId ?? '';
  expect(nuevo).not.toBe('');

  // La revisión nueva ya está en manos de la empresa; el proveedor sube la versión corregida del RUT que falló (versión 2).
  await esperarEstadoDelTercero(page, 'EnRevision');
  await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero} corregido`), { version: 2 });
  await expect(tarjeta(page, RUT)).toContainText('Versión 2');

  // Las dos áreas revisan el expediente nuevo y lo aprueban.
  const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben);
  // La bóveda adjunta la versión nueva al expediente por una cola: se abre de nuevo hasta que el área la ve.
  await expect(async () => {
    await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
    await expect(ruben.getByRole('main')).toContainText(`${RUT} · v2`, { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await aprobarArea(ruben, 'Compras');
  // Olga ya tenía abierto el expediente rechazado: la bandeja puede mostrar todavía esa fila, así que se abre hasta llegar al nuevo.
  await expect(async () => {
    await abrirDesdeBandeja(olga, tercero.nombre, 'Cumplimiento');
    await expect(olga.getByRole('main')).toContainText(`${RUT} · v2`, { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await aprobarArea(olga, 'Cumplimiento');

  // Vínculo activo, expediente aprobado y certificado vigente; el proveedor lo ve aprobado.
  await esperarEstadoDelTercero(page, 'Aprobado');
  expect((await leerVinculo(e, invitacion)).estado).toBe('Activo');
  expect((await leerExpediente(e, e.norte, nuevo)).estado).toBe('Aprobado');
  const certificado = await leerCertificado(e, invitacion);
  expect(certificado.actual?.estado).toBe('Vigente');
  expect(certificado.actual?.expedienteId).toBe(nuevo);
});
