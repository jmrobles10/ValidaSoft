import { expect, test, type Browser, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { esperarCorreo, literal, textoSinEnlaces } from '../soporte/correo';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, aprobarEnNombreDe, observar, rechazar, regionDeArea } from '../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero, esperarTarjeta, responder, subirDocumento, tarjeta } from '../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevoTercero, type Escenario, type Invitacion, type Persona } from '../soporte/semilla';

/**
 * Decisiones de las áreas sobre un expediente de proveedor de Constructora Norte: Compras (Rubén) revisa el RUT;
 * Cumplimiento (Olga, puede rechazar) revisa el RUT y la Cámara de comercio. Política de devolución inmediata.
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

interface Caso {
  e: Escenario;
  invitacion: Invitacion;
  tercero: Page;
}

/** Grupo nuevo, proveedor invitado por Norte que ya aceptó y subió RUT y Cámara: el expediente está en revisión. */
async function expedienteEnRevision(page: Page): Promise<Caso> {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.norte, nuevoTercero());
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${invitacion.nombre} v1`) },
    { nombre: CAMARA, archivo: pdf(`Camara ${invitacion.nombre} v1`) },
  ]);
  return { e, invitacion, tercero: page };
}

/** Ventana aparte (otra persona, otro navegador) con Validasoft embebido en el expediente del caso. */
async function revisor(browser: Browser, c: Caso, persona: Persona, area: string) {
  const ventana = await (await browser.newContext()).newPage();
  const app = await abrirEmbebido(ventana, c.e, persona);
  await abrirDesdeBandeja(app, c.invitacion.nombre, area);
  return app;
}

test('las dos áreas aprueban: el vínculo queda activo y cada revisor solo decide por su área', async ({ page, browser }) => {
  const c = await expedienteEnRevision(page);

  const ruben = await revisor(browser, c, c.e.ruben, 'Compras');
  await expect(regionDeArea(ruben, 'Cumplimiento')).toContainText('No eres miembro de esta área.');
  await expect(regionDeArea(ruben, 'Cumplimiento').getByRole('button', { name: /^Aprobar área/ })).toHaveCount(0);
  await expect(regionDeArea(ruben, 'Compras').getByRole('button', { name: 'Aprobar área (1 documento)' })).toBeVisible();
  await aprobarArea(ruben, 'Compras');

  // Con Compras aprobada, el tercero sigue «En revisión»: falta Cumplimiento.
  await esperarEstadoDelTercero(page, 'EnRevision');

  const olga = await revisor(browser, c, c.e.olga, 'Cumplimiento');
  await expect(regionDeArea(olga, 'Cumplimiento').getByRole('button', { name: 'Aprobar área (2 documentos)' })).toBeVisible();
  await aprobarArea(olga, 'Cumplimiento');
  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();

  await esperarEstadoDelTercero(page, 'Aprobado');
  const aviso = await esperarCorreo(c.invitacion.correo, new RegExp(`^${literal(c.e.norte.nombre)} aprobó tu validación `));
  expect(aviso.texto).toContain('tu vínculo está activo y ya tienes tu certificado');
});

test('Compras observa el RUT, el tercero sube la versión corregida y la aprobación previa de Cumplimiento sobre ese documento se invalida', async ({ page, browser }) => {
  const c = await expedienteEnRevision(page);

  // Cumplimiento aprueba primero sus dos documentos (RUT v1 y Cámara v1).
  const olga = await revisor(browser, c, c.e.olga, 'Cumplimiento');
  await aprobarArea(olga, 'Cumplimiento');

  const ruben = await revisor(browser, c, c.e.ruben, 'Compras');
  await observar(ruben, 'Compras', [{ documento: `${RUT} · v1`, motivos: ['Ilegible', 'Incompleto'] }], 'La página 2 está cortada.');

  // Un solo correo con los motivos tal como se le comunican al tercero, sin nombres de revisores ni de áreas.
  const correo = await esperarCorreo(c.invitacion.correo, /tiene observaciones sobre tu validación/);
  expect(correo.texto).toContain('El documento no se puede leer con claridad.');
  expect(correo.texto).toContain('La información o el documento está incompleto.');
  expect(correo.texto).toContain('La página 2 está cortada.');
  for (const interno of ['Rubén', 'Compras', 'Olga', 'Cumplimiento']) expect(textoSinEnlaces(correo.texto)).not.toContain(interno);

  await esperarEstadoDelTercero(page, 'Observado');
  await expect(tarjeta(page, RUT)).toContainText('El documento no se puede leer con claridad.');
  await expect(tarjeta(page, RUT)).toContainText('“La página 2 está cortada.”');
  // La Cámara no se observó: no se le pide corregirla.
  await expect(tarjeta(page, CAMARA).getByRole('link', { name: 'Subir versión corregida' })).toHaveCount(0);

  await subirDocumento(page, RUT, pdf(`RUT ${c.invitacion.nombre} corregido`), { version: 2 });
  await esperarTarjeta(page, RUT, 'Versión 2');
  await responder(page, 'Adjunto el RUT completo.');
  await esperarEstadoDelTercero(page, 'EnRevision');

  // La versión 2 invalida lo decidido sobre el RUT en las dos áreas (RQ-8); la Cámara de Cumplimiento sigue aprobada.
  await abrirDesdeBandeja(olga, c.invitacion.nombre, 'Cumplimiento');
  await expect(regionDeArea(olga, 'Cumplimiento').getByRole('button', { name: 'Aprobar área (1 documento)' })).toBeVisible();
  await expect(regionDeArea(olga, 'Cumplimiento')).toContainText(`${RUT} · v2`);
  await aprobarArea(olga, 'Cumplimiento');

  await abrirDesdeBandeja(ruben, c.invitacion.nombre, 'Compras');
  await expect(regionDeArea(ruben, 'Compras')).toContainText(`${RUT} · v2`);
  await aprobarArea(ruben, 'Compras');

  await esperarEstadoDelTercero(page, 'Aprobado');
});

test('Cumplimiento rechaza: el expediente se cierra, el vínculo vuelve a Invitado y Compras no puede rechazar', async ({ page, browser }) => {
  const c = await expedienteEnRevision(page);

  const ruben = await revisor(browser, c, c.e.ruben, 'Compras');
  // Compras no tiene «Puede rechazar»: no ve el botón.
  await expect(ruben.getByRole('button', { name: 'Rechazar', exact: true })).toHaveCount(0);

  const olga = await revisor(browser, c, c.e.olga, 'Cumplimiento');
  await rechazar(olga, ['No corresponde al tercero'], 'La Cámara es de otra sociedad con el mismo nombre.');
  await expect(olga.getByText(/Cumplimiento lo rechazó el .*\. El vínculo volvió a Invitado\./)).toBeVisible();

  const correo = await esperarCorreo(c.invitacion.correo, /cerró tu validación .* sin aprobarla$/);
  expect(correo.texto).toContain('El documento no corresponde a la persona o empresa que se está validando.');
  expect(correo.texto).not.toContain('La Cámara es de otra sociedad'); // la justificación es interna

  await esperarEstadoDelTercero(page, 'Rechazado');
  await expect(page.getByText(`${c.e.norte.nombre} cerró la revisión sin aprobarla. Si tienes dudas, escríbele.`)).toBeVisible();
  // Ya no le piden nada: no hay respuesta que enviar.
  await expect(page.getByRole('button', { name: 'Enviar respuesta' })).toHaveCount(0);
});

test('Cumplimiento rechaza mientras el expediente está devuelto al tercero por una observación de Compras', async ({ page, browser }) => {
  const c = await expedienteEnRevision(page);

  const ruben = await revisor(browser, c, c.e.ruben, 'Compras');
  await observar(ruben, 'Compras', [{ documento: `${RUT} · v1`, motivos: ['Datos no coinciden'] }]);
  await esperarEstadoDelTercero(page, 'Observado');

  // Devuelto: nadie aprueba ni observa hasta que el tercero responda, pero un hallazgo grave sí se puede rechazar (FL-14).
  const olga = await revisor(browser, c, c.e.olga, 'Cumplimiento');
  await expect(regionDeArea(olga, 'Cumplimiento')).toContainText('Devuelto al tercero: espera su respuesta.');
  await expect(regionDeArea(olga, 'Cumplimiento').getByRole('button', { name: /^Aprobar área/ })).toHaveCount(0);
  await rechazar(olga, ['Datos no coinciden'], 'La razón social no coincide con la del RUT ni con la Cámara.');

  await esperarEstadoDelTercero(page, 'Rechazado');
  await expect(page.getByRole('button', { name: 'Enviar respuesta' })).toHaveCount(0);
});

test('Cumplimiento observa la Cámara y el tercero responde con una explicación, sin versión nueva', async ({ page, browser }) => {
  const c = await expedienteEnRevision(page);

  const olga = await revisor(browser, c, c.e.olga, 'Cumplimiento');
  await observar(olga, 'Cumplimiento', [{ documento: `${CAMARA} · v1`, motivos: ['Firma faltante'] }]);

  await esperarEstadoDelTercero(page, 'Observado');
  await responder(page, 'La Cámara se expide con firma digital; está en la última página.');
  await esperarEstadoDelTercero(page, 'EnRevision');

  // Cumplimiento ve la respuesta y vuelve a decidir sobre la misma versión 1; Compras nunca se frenó por la Cámara.
  await abrirDesdeBandeja(olga, c.invitacion.nombre, 'Cumplimiento');
  await expect(olga.getByText('La Cámara se expide con firma digital; está en la última página.').first()).toBeVisible();
  await expect(regionDeArea(olga, 'Cumplimiento')).toContainText(`${CAMARA} · v1`);
  await aprobarArea(olga, 'Cumplimiento');

  const ruben = await revisor(browser, c, c.e.ruben, 'Compras');
  await aprobarArea(ruben, 'Compras');
  await esperarEstadoDelTercero(page, 'Aprobado');
});

test('la administradora, que no es de ningún área, aprueba en nombre de Compras con justificación', async ({ page, browser }) => {
  const c = await expedienteEnRevision(page);

  // Ana no es de ningún área: no lo ve en su bandeja, entra directo al expediente.
  const ventana = await (await browser.newContext()).newPage();
  const ana = await abrirEmbebido(ventana, c.e, c.e.ana, c.e.norte, `expedientes/${await expedienteAbierto(c.e, c.invitacion)}`);
  await expect(ana.getByRole('heading', { level: 1, name: c.invitacion.nombre })).toBeVisible();
  await expect(regionDeArea(ana, 'Compras').getByRole('button', { name: /^Aprobar área/ })).toHaveCount(0);
  await aprobarEnNombreDe(ana, 'Compras', 'Rubén está de vacaciones; el RUT ya lo validó por correo.');
  await expect(regionDeArea(ana, 'Compras')).toContainText('Aprobada');

  const olga = await revisor(browser, c, c.e.olga, 'Cumplimiento');
  await aprobarArea(olga, 'Cumplimiento');
  await esperarEstadoDelTercero(page, 'Aprobado');
});
