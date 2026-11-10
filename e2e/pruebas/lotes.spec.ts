import { expect, test } from '@playwright/test';
import { CLAVE_PLATAFORMA, llamar } from '../soporte/api';
import { esperarCorreo, literal } from '../soporte/correo';
import { abrirEmbebido } from '../soporte/embebido';
import { aceptarInvitacion, entrarPorInvitacion } from '../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevaPersonaNatural, nuevoTercero, type Invitacion } from '../soporte/semilla';

/**
 * Invitación masiva por lote y caducidad por plazo de cargue (H8, LT-1…LT-10): la empresa sube la plantilla desde la pantalla,
 * revisa la validación por fila y ve a los terceros invitados; un expediente que el tercero no completa en el plazo caduca
 * (el job se corre con una fecha simulada, solo para el tenant de la prueba) y la empresa lo reabre.
 */

/** Fecha `dd/mm/aaaa` de hoy en Bogotá más `dias`, como la muestran las pantallas. */
function enPantalla(dias: number): string {
  const hoy = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
  const [a, m, d] = hoy.split('-').map(Number) as [number, number, number];
  const fecha = new Date(Date.UTC(a, m - 1, d + dias));
  return `${String(fecha.getUTCDate()).padStart(2, '0')}/${String(fecha.getUTCMonth() + 1).padStart(2, '0')}/${String(fecha.getUTCFullYear())}`;
}

test('la empresa carga un lote pequeño, revisa los errores por fila y los terceros válidos quedan invitados', async ({ page, browser }) => {
  const e = await crearEscenario();
  const aceros = nuevoTercero('Aceros del Lote S.A.S.');
  const ana = nuevaPersonaNatural('Ana Lote Pérez');
  const csv = [
    'Tipo de identificación;Número de identificación;DV;Razón social o nombres;Correo;Tipo de vínculo',
    `NIT;${aceros.identificacion.numero};${aceros.identificacion.digitoVerificacion};${aceros.nombre};${aceros.correo};PROVEEDOR`,
    `CC;${ana.identificacion.numero};;${ana.nombre};${ana.correo};PROVEEDOR`,
    `NIT;${nuevoTercero().identificacion.numero};;Sin correo válido;no-es-correo;PROVEEDOR`,
  ].join('\r\n');

  const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
  await app.getByRole('button', { name: 'Invitar por lote' }).click();
  await app.getByRole('button', { name: 'Nuevo lote' }).click();
  await app.locator('input[type="file"]').setInputFiles({ name: 'proveedores.csv', mimeType: 'text/csv', buffer: Buffer.from(csv, 'utf8') });

  // Vista previa: la fila con el correo malo se muestra con su error y no se invita.
  await expect(app.getByText('3 filas: 2 válidas, 1 con errores, 0 repetidas.')).toBeVisible();
  await expect(app.getByRole('table').getByText('«no-es-correo» no es un correo válido.')).toBeVisible();
  await app.getByLabel('Nombre del lote').fill('Proveedores E2E');
  await app.getByRole('button', { name: 'Invitar a 2 terceros' }).click();

  // Detalle del lote: avanza solo hasta completarse.
  await expect(app.getByRole('heading', { name: 'Proveedores E2E' })).toBeVisible();
  await expect(app.getByText('Completado', { exact: true })).toBeVisible({ timeout: 30_000 });
  await expect(app.getByText('2 de 2')).toBeVisible();
  const filas = app.getByRole('table', { name: 'Filas del lote' });
  await expect(filas.getByRole('link', { name: 'Invitado' })).toHaveCount(2);
  await expect(filas.getByText('Con errores')).toBeVisible();

  // El vínculo del tercero quedó invitado...
  await filas.getByRole('row').filter({ hasText: aceros.nombre }).getByRole('link', { name: 'Invitado' }).click();
  await expect(app.getByText(aceros.nombre).first()).toBeVisible();
  await expect(app.getByText('Invitado').first()).toBeVisible();

  // ...y el tercero entra con el enlace de su correo a la invitación de la empresa.
  const proveedor = await (await browser.newContext()).newPage();
  const invitacion = { correo: aceros.correo, nombre: aceros.nombre, empresa: e.norte, tipo: 'PROVEEDOR' } as Invitacion;
  await entrarPorInvitacion(proveedor, invitacion);
  await esperarCorreo(ana.correo, new RegExp(`^${literal(e.norte.nombre)} te invita a validar tu vínculo como proveedor$`));
});

test('el expediente que el tercero no completa en el plazo caduca y la empresa lo reabre con un plazo nuevo', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.norte, nuevoTercero('Plazo Vencido S.A.S.'));
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await expect(page.getByText(`Tienes hasta el ${enPantalla(30)} para completarla.`)).toBeVisible();
  const expedienteId = await expedienteAbierto(e, invitacion);

  // El job de plazos corre «dentro de 31 días» solo para este grupo (fecha simulada, Development).
  const fecha = new Date(Date.now() + 31 * 24 * 3600 * 1000).toISOString();
  const resultado = await llamar<{ caducados: number }>('POST', '/api/v1/plataforma/plazos-de-cargue/revisar', {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { tenantId: e.tenantId, fecha },
  });
  expect(resultado.caducados).toBe(1);
  await esperarCorreo(invitacion.correo, /caducó$/);

  const app = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.norte, `expedientes/${expedienteId}`);
  const aviso = app.getByRole('alert').filter({ hasText: 'Expediente caducado' });
  await expect(aviso).toBeVisible();
  await app.getByRole('button', { name: 'Reabrir expediente' }).click();
  await app.getByRole('dialog').getByLabel('Justificación (opcional)').fill('El proveedor pidió más tiempo.');
  await app.getByRole('dialog').getByRole('button', { name: 'Reabrir expediente' }).click();
  await expect(aviso).toBeHidden();
  await expect(app.getByText(/El tercero tiene hasta el .* para completar el expediente\./)).toBeVisible();

  // El tercero recibe el aviso de la reapertura con un enlace nuevo y vuelve a ver su plazo.
  await esperarCorreo(invitacion.correo, new RegExp(`^${literal(e.norte.nombre)} reabrió tu validación`));
  await page.reload();
  await expect(page.getByText(/Tienes hasta el .* para completarla\./)).toBeVisible();
});
