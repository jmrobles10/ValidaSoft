import { expect, test, type Browser, type Page } from '@playwright/test';
import { llamar } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { esperarCorreo, literal } from '../soporte/correo';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, regionDeArea } from '../soporte/empresa';
import { aceptarInvitacion, completarInvitacion, entrarPorInvitacion, esperarTarjeta, tarjeta } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST, type Escenario, type Invitacion } from '../soporte/semilla';

/**
 * Vigencia del certificado propio de Sur ({@link SST}): vence 30 días después de la fecha de expedición que declara el
 * proveedor, avisa a 10 días y tiene 5 de gracia. Declarar una expedición vieja deja el documento por vencer, en gracia o
 * vencido sin mover el reloj: los avisos y vencimientos programados en el pasado se ejecutan enseguida (TD-7).
 */

const RUT = 'Registro Único Tributario';

/** Fecha `YYYY-MM-DD` de hoy en Bogotá más `dias` (negativo: en el pasado). */
function fecha(dias: number): string {
  const hoy = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
  const [a, m, d] = hoy.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

/** `YYYY-MM-DD` → `dd/mm/aaaa`, como lo muestran las pantallas. */
function enPantalla(iso: string): string {
  const [a, m, d] = iso.split('-');
  return `${d ?? ''}/${m ?? ''}/${a ?? ''}`;
}

/** Proveedor de Sur que entrega RUT y el certificado con la fecha de expedición indicada. */
async function proveedorDeSur(page: Page, expedicion: string): Promise<{ e: Escenario; invitacion: Invitacion }> {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero());
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${invitacion.correo}`) },
    { nombre: SST.nombre, archivo: pdf(`SST ${invitacion.correo}`), campos: { 'Fecha de expedición': expedicion } },
  ]);
  return { e, invitacion };
}

async function revisora(browser: Browser, e: Escenario, invitacion: Invitacion) {
  const app = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await abrirDesdeBandeja(app, invitacion.nombre, 'Revisión');
  return app;
}

test('expedido hace 3 días: vigente hasta 30 días después de la expedición, sin avisos', async ({ page, browser }) => {
  const expedicion = fecha(-3);
  const { e, invitacion } = await proveedorDeSur(page, expedicion);
  const vence = enPantalla(fecha(27));

  await expect(tarjeta(page, SST.nombre)).toContainText('Vigente');
  await expect(tarjeta(page, SST.nombre)).toContainText(`Vence el ${vence}`);

  const sofia = await revisora(browser, e, invitacion);
  await expect(regionDeArea(sofia, 'Revisión')).toContainText(`${SST.nombre} · v1`);
  await expect(regionDeArea(sofia, 'Revisión')).toContainText(`Vigente · ${vence}`);
});

test('expedido hace 25 días: por vencer en 5, marcado para el proveedor y para la revisora', async ({ page, browser }) => {
  const { e, invitacion } = await proveedorDeSur(page, fecha(-25));
  const vence = enPantalla(fecha(5));

  await esperarTarjeta(page, SST.nombre, 'Por vencer');
  await expect(tarjeta(page, SST.nombre)).toContainText(`Vence el ${vence}`);

  // El aviso de los 10 días ya pasó cuando lo subió: no se programa (NW-15); lo ve marcado en la pantalla.
  await expect(tarjeta(page, SST.nombre)).toContainText('Vence pronto');

  const sofia = await revisora(browser, e, invitacion);
  await expect(regionDeArea(sofia, 'Revisión')).toContainText(`Por vencer · ${vence}`);
});

test('expedido hace 32 días: venció hace 2 y está en periodo de gracia', async ({ page, browser }) => {
  const { e, invitacion } = await proveedorDeSur(page, fecha(-32));

  await esperarTarjeta(page, SST.nombre, 'En periodo de gracia');
  await expect(tarjeta(page, SST.nombre)).toContainText(`Venció el ${enPantalla(fecha(-2))}`);
  await expect(tarjeta(page, SST.nombre)).toContainText(`Ya venció. Tienes hasta el ${enPantalla(fecha(3))} para subir una versión nueva.`);

  const sofia = await revisora(browser, e, invitacion);
  await expect(regionDeArea(sofia, 'Revisión')).toContainText(enPantalla(fecha(-2)));
});

test('expedido hace 40 días: vencido después de la gracia', async ({ page, browser }) => {
  const { e, invitacion } = await proveedorDeSur(page, fecha(-40));

  await esperarTarjeta(page, SST.nombre, 'Vencido');
  await expect(tarjeta(page, SST.nombre)).toContainText(`Venció el ${enPantalla(fecha(-10))}`);
  await expect(tarjeta(page, SST.nombre)).toContainText('Está vencido: sube una versión nueva.');

  // Se le avisa por correo (NW-15).
  const aviso = await esperarCorreo(invitacion.correo, new RegExp(`^Tu ${literal(SST.nombre)} venció para ${literal(e.sur.nombre)}$`));
  expect(aviso.texto).toContain('ya pasó el plazo de gracia');

  const sofia = await revisora(browser, e, invitacion);
  await expect(regionDeArea(sofia, 'Revisión')).toContainText(enPantalla(fecha(-10)));

  // Y la revisora de Sur lo ve en su campana.
  await sofia.getByRole('button', { name: /^Notificaciones/ }).click();
  await expect(sofia.getByText('Documento vencido').first()).toBeVisible();
});

test('una fecha de expedición futura no se admite', async ({ page }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero());
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await tarjeta(page, SST.nombre).getByRole('link', { name: /^Subir/ }).click();
  await page.getByLabel(/Fecha de expedición/).fill(fecha(3));
  // Se valida antes de elegir el archivo: no se puede subir con una expedición futura.
  await expect(page.getByRole('status')).toHaveText('Para subir el archivo, corrige: Fecha de expedición.');
  await expect(page.getByRole('button', { name: 'Elegir archivo' })).toBeDisabled();
});

test('cambiar la regla del tipo recalcula la vigencia que ya tenía el documento', async ({ page }) => {
  const { e } = await proveedorDeSur(page, fecha(-25));
  await esperarTarjeta(page, SST.nombre, 'Por vencer');

  // La administración del grupo amplía la frescura a 90 días: el mismo documento pasa a vigente hasta expedición + 90.
  const tipos = await llamar<{ id: string; codigo: string }[]>('GET', '/api/v1/tipos-documento', { llave: e.admin });
  const sst = tipos.find((t) => t.codigo === SST.codigo);
  if (!sst) throw new Error('No está el tipo del certificado de Sur.');
  await llamar('PUT', `/api/v1/tipos-documento/${sst.id}`, { llave: e.admin, cuerpo: { reglaFrescura: { tipo: 'MaxDiasDesdeExpedicion', dias: 90 } } });

  await esperarTarjeta(page, SST.nombre, `Vence el ${enPantalla(fecha(65))}`);
  await expect(tarjeta(page, SST.nombre)).toContainText('Vigente');
});
