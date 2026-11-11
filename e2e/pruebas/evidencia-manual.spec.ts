import { expect, test } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirDesdeBandeja, aprobarArea } from '../soporte/empresa';
import { captura, enRevision, esperarListas, pedirVerificaciones, personaEmbebida } from '../soporte/cumplimiento';
import { esperarEstadoDelTercero } from '../soporte/proveedor';
import { crearEscenario } from '../soporte/semilla';

/**
 * Lo que el oficial hace por fuera del sistema (CU-16, CU-18), con el proveedor de listas simulado de desarrollo (un nombre con
 * «coincidencia» aparece en OFAC): registra una consulta hecha en otro lugar con su PDF y, si ya hay un hallazgo grave
 * confirmado, rechaza el expediente él mismo, sin pasar por un área.
 */

test('el oficial registra una consulta hecha por fuera con su PDF y una coincidencia: la analiza, la descarta y el expediente se aprueba', async ({ page, browser }) => {
  const e = await crearEscenario();
  await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
  const nombre = 'Ferretería Manual S.A.S.';
  await enRevision(page, e, nombre);

  const ruben = await personaEmbebida(browser, e, e.ruben);
  await abrirDesdeBandeja(ruben, nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');

  const olga = await personaEmbebida(browser, e, e.olga);
  await esperarListas(olga, nombre, 'Cumplimiento', 'Satisfecha');
  const listas = olga.getByRole('region', { name: 'Listas restrictivas' });
  await expect(listas).toContainText('Ninguna persona aparece en las listas consultadas.');

  // Ella consultó en otro lugar: sube el PDF, dice dónde y cuándo y reporta lo que encontró.
  await olga.getByRole('button', { name: 'Registrar consulta hecha por fuera' }).click();
  const dialogo = olga.getByRole('dialog', { name: /Consulta de listas hecha por fuera/ });
  await dialogo.locator('input[type="file"]').setInputFiles(pdf('Consulta manual Infolaft'));
  await expect(dialogo.getByText(/consulta-manual-infolaft\.pdf/)).toBeVisible();
  await dialogo.getByRole('textbox', { name: /Dónde la hiciste/ }).fill('Infolaft (sitio web)');
  await dialogo.getByRole('button', { name: 'Sí, hubo' }).click();
  const fila = dialogo.getByRole('listitem', { name: 'Coincidencia 1' });
  await fila.getByRole('combobox', { name: 'Persona' }).click();
  await olga.getByRole('option', { name: new RegExp(`^${nombre.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} · Tercero`) }).click();
  await fila.getByRole('textbox', { name: 'Lista' }).fill('OFAC (SDN)');
  await fila.getByRole('textbox', { name: 'Nombre encontrado' }).fill(nombre.toUpperCase());
  await fila.getByRole('textbox', { name: 'Detalle (opcional)' }).fill('Homónimo con otro NIT.');
  await expect(dialogo).toContainText('Con coincidencias, las listas quedan por analizar');
  await olga.owner().page().screenshot({ path: captura('evidencia-manual-dialogo'), fullPage: true });

  await dialogo.getByRole('button', { name: 'Registrar consulta' }).click();
  await expect(dialogo).toBeHidden();

  // Entra como una consulta más: «Manual · fuente», con el PDF subido por el oficial, y la coincidencia queda por analizar.
  await expect(listas).toContainText('Manual · Infolaft (sitio web)');
  await expect(listas).toContainText('PDF subido por Olga Oficial');
  await expect(listas).toContainText('Coincidencia por analizar');
  await expect(listas).toContainText('1 consulta anterior');
  const coincidencia = olga.getByRole('listitem', { name: `${nombre} · OFAC (SDN)` });
  await expect(coincidencia).toContainText('Homónimo con otro NIT.');
  await olga.owner().page().screenshot({ path: captura('evidencia-manual-lista'), fullPage: true });
  await olga.owner().page().setViewportSize({ width: 390, height: 900 });
  await expect(listas).toContainText('Manual · Infolaft (sitio web)');
  await olga.owner().page().screenshot({ path: captura('evidencia-manual-lista-movil'), fullPage: true });
  await olga.owner().page().setViewportSize({ width: 1280, height: 720 });

  // La descarta con justificación: las listas quedan satisfechas y, como no falta nada más, el expediente se aprueba.
  await coincidencia.getByRole('button', { name: 'Descartar' }).click();
  const decision = olga.getByRole('dialog', { name: 'Descartar coincidencia' });
  await decision.getByRole('textbox', { name: /Justificación/ }).fill('Es un homónimo: el NIT del listado es otro.');
  await decision.getByRole('button', { name: 'Guardar decisión' }).click();
  await expect(decision).toBeHidden();
  await expect(coincidencia).toContainText('Es un homónimo: el NIT del listado es otro.');
  await expect(olga.getByRole('region', { name: 'Listas restrictivas' })).toContainText('Satisfecha');
  await aprobarArea(olga, 'Cumplimiento');
  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
  await esperarEstadoDelTercero(page, 'Aprobado');
});

test('tras confirmar una coincidencia el oficial rechaza por cumplimiento: es definitivo y el tercero lo ve', async ({ page, browser }) => {
  const e = await crearEscenario();
  await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
  const nombre = 'Ferretería Coincidencia S.A.S.';
  await enRevision(page, e, nombre);

  const olga = await personaEmbebida(browser, e, e.olga);
  await esperarListas(olga, nombre, 'Cumplimiento', 'Coincidencia por analizar');
  // Sin un hallazgo grave confirmado no se ofrece rechazar.
  await expect(olga.getByRole('button', { name: 'Rechazar por cumplimiento' })).toHaveCount(0);

  const coincidencia = olga.getByRole('listitem', { name: `${nombre} · OFAC (SDN)` });
  await coincidencia.getByRole('button', { name: 'Confirmar' }).click();
  const confirmar = olga.getByRole('dialog', { name: 'Confirmar coincidencia' });
  await confirmar.getByRole('textbox', { name: /Justificación/ }).fill('Es la misma persona: el NIT y el representante coinciden.');
  await confirmar.getByRole('button', { name: 'Guardar decisión' }).click();
  await expect(confirmar).toBeHidden();
  await expect(olga.getByRole('region', { name: 'Listas restrictivas' })).toContainText('Fallida');

  // Ahora sí: avisa que es definitivo, pide motivos del catálogo de rechazo y una justificación.
  await olga.getByRole('button', { name: 'Rechazar por cumplimiento' }).click();
  const dialogo = olga.getByRole('dialog', { name: /Rechazar por cumplimiento/ });
  await expect(dialogo).toContainText('El rechazo es definitivo: el expediente se cierra, el vínculo vuelve a Invitado');
  await expect(dialogo).toContainText('Hallazgo que lo permite: una coincidencia confirmada en listas restrictivas');
  await expect(dialogo.getByRole('button', { name: 'Rechazar expediente' })).toBeDisabled();
  await dialogo.getByRole('checkbox', { name: /^No corresponde al tercero / }).check();
  await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Coincidencia confirmada con OFAC: no se puede vincular.');
  await olga.owner().page().screenshot({ path: captura('evidencia-manual-rechazo'), fullPage: true });
  await dialogo.getByRole('button', { name: 'Rechazar expediente' }).click();
  await expect(dialogo).toBeHidden();

  // El expediente dice quién lo rechazó y el historial también.
  const aviso = olga.getByRole('alert').filter({ hasText: 'Cumplimiento lo rechazó el' });
  await expect(aviso).toContainText('Cumplimiento lo rechazó el');
  await expect(aviso).toContainText('Coincidencia confirmada con OFAC: no se puede vincular.');
  await expect(olga.getByText('Rechazado por Cumplimiento').first()).toBeVisible();
  await expect(olga.getByRole('button', { name: 'Rechazar por cumplimiento' })).toHaveCount(0);
  await esperarEstadoDelTercero(page, 'Rechazado');
});
