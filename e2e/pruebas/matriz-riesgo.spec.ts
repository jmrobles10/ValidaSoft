import { expect, test } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../soporte/empresa';
import { aprobarUltimaArea, captura, enRevision, esperarListas, pedirVerificaciones, personaEmbebida } from '../soporte/cumplimiento';
import { esperarEstadoDelTercero } from '../soporte/proveedor';
import { crearEscenario } from '../soporte/semilla';

/**
 * Matriz de riesgo de la empresa (CU-17). Norte parte de la plantilla, cambia el peso del sector y la activa desde
 * Configuración; el oficial hace un estudio con ella: responde los factores manuales, ve el puntaje y el nivel, ajusta el nivel
 * con una justificación, sube un archivo y, como el estudio era lo último que faltaba, el expediente se aprueba.
 *
 * Con la plantilla del servidor (jurisdicción 15, coincidencias en listas 30, PEP 20, sector 20, operación 15) y el sector en 25:
 * sector alto (100) y operación grande (100), con los tres automáticos en 0, dan (25·100 + 15·100) / 105 = 38,1 → Medio.
 */

test('Norte configura su matriz y el estudio del oficial la usa: resultado, ajuste con justificación, archivo y aprobación', async ({ page, browser }) => {
  const e = await crearEscenario();
  await pedirVerificaciones(e, [
    { tipo: 'ListasRestrictivas', bloqueante: true },
    { tipo: 'EstudioDeCumplimiento', bloqueante: true },
  ]);

  // Ana configura la matriz desde Configuración: la plantilla, con el sector más pesado y activa.
  const ana = await abrirEmbebido(page, e, e.ana, e.norte, 'matriz-riesgo');
  await expect(ana.getByRole('heading', { level: 1, name: 'Matriz de riesgo' })).toBeVisible();
  await expect(ana.getByText('Aún no tienes una matriz propia')).toBeVisible();
  const sector = ana.getByRole('article', { name: 'Sector o actividad económica' });
  await expect(sector.getByText('Manual')).toBeVisible();
  await expect(ana.getByRole('article', { name: 'Jurisdicción del tercero' }).getByText('Automático')).toBeVisible();
  await expect(ana.getByRole('combobox', { name: /Debida diligencia · 3 · Alto/ })).toContainText('Intensificada');
  await sector.getByRole('textbox', { name: 'Peso' }).fill('25');
  await ana.getByRole('switch', { name: 'Calcular el riesgo con esta matriz' }).click();
  await expect(ana.getByText('Hay cambios sin guardar.')).toBeVisible();
  await page.screenshot({ path: captura('matriz-riesgo-web'), fullPage: true });

  await ana.getByRole('button', { name: 'Guardar matriz' }).click();
  const confirmacion = ana.getByRole('dialog', { name: 'Guardar la matriz' });
  await expect(confirmacion).toContainText('Los próximos estudios usarán esta matriz; los ya registrados conservan su cálculo.');
  await expect(confirmacion).toContainText('La matriz queda activa');
  await confirmacion.getByRole('button', { name: 'Guardar matriz' }).click();
  await expect(ana.getByText('Guardamos la matriz. Los próximos estudios la usan.')).toBeVisible();
  await expect(ana.getByText('Aún no tienes una matriz propia')).toBeHidden();
  await expect(ana.getByText(/^Versión 1 · guardada el /)).toBeVisible();

  // Y se ve bien en el celular.
  await page.setViewportSize({ width: 390, height: 900 });
  await expect(ana.getByRole('heading', { level: 1, name: 'Matriz de riesgo' })).toBeVisible();
  await page.screenshot({ path: captura('matriz-riesgo-movil'), fullPage: true });
  await page.setViewportSize({ width: 1280, height: 720 });

  // Un tercero llega a revisión: Compras aprueba y Cumplimiento aprueba su área, pero el estudio sigue pendiente.
  const nombre = 'Aceros del Norte S.A.S.';
  await enRevision(page, e, nombre);
  const ruben = await personaEmbebida(browser, e, e.ruben);
  await abrirDesdeBandeja(ruben, nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');
  const olga = await personaEmbebida(browser, e, e.olga);
  await esperarListas(olga, nombre, 'Cumplimiento', 'Satisfecha');
  await aprobarUltimaArea(olga, 'Cumplimiento', /Estudio de cumplimiento \(pendiente\)/);
  await esperarEstadoDelTercero(page, 'EnRevision');

  // El estudio con la matriz: solo se preguntan los factores manuales; los automáticos los evalúa el sistema.
  await olga.getByRole('button', { name: 'Registrar estudio' }).click();
  const dialogo = olga.getByRole('dialog', { name: /Estudio de cumplimiento/ });
  await expect(dialogo.getByText('Valoración de riesgo')).toBeVisible();
  await expect(dialogo.getByRole('combobox', { name: 'Jurisdicción del tercero' })).toHaveCount(0);
  await expect(dialogo.getByText('Responde todos los factores para ver el resultado.')).toBeVisible();
  await dialogo.getByRole('combobox', { name: 'Sector o actividad económica' }).click();
  await olga.getByRole('option', { name: /^Sector de alto riesgo/ }).click();
  await dialogo.getByRole('combobox', { name: 'Tipo y volumen de la operación' }).click();
  await olga.getByRole('option', { name: 'Operación grande o atípica' }).click();

  const resultado = dialogo.getByLabel('Resultado de la matriz');
  await expect(resultado).toContainText('38,1 de 100');
  await expect(resultado).toContainText('2 · Medio');
  await expect(resultado).toContainText('Ordinaria');
  await expect(resultado).toContainText('Se vuelve a revisar cada 12 meses.');
  await expect(dialogo).toContainText('Es lo último que falta: el expediente se aprueba');

  // El oficial discrepa: sube el nivel a Alto. Sin justificación no deja registrar; la debida diligencia sube a intensificada.
  await dialogo.getByRole('combobox', { name: 'Concepto' }).click();
  await olga.getByRole('option', { name: 'Favorable', exact: true }).click();
  await dialogo.getByRole('switch', { name: 'Ajustar el nivel' }).click();
  await dialogo.getByRole('button', { name: '3 · Alto' }).click();
  await expect(dialogo.getByRole('combobox', { name: 'Debida diligencia' })).toContainText('Intensificada');
  await dialogo.getByRole('button', { name: 'Registrar estudio' }).click();
  await expect(dialogo.getByText('Explica el ajuste del nivel (al menos 10 caracteres).')).toBeVisible();
  await dialogo.getByRole('textbox', { name: /Justificación del ajuste/ }).fill('Opera con efectivo en zona de frontera.');

  // El archivo que soporta el estudio.
  await dialogo.locator('input[type="file"]').setInputFiles(pdf('Soporte estudio'));
  await expect(dialogo.getByText('soporte-estudio.pdf')).toBeVisible();
  const paginaDeOlga = olga.owner().page();
  await paginaDeOlga.screenshot({ path: captura('matriz-estudio-dialogo-web'), fullPage: true });
  await paginaDeOlga.setViewportSize({ width: 390, height: 900 });
  await expect(dialogo.getByText('Valoración de riesgo')).toBeVisible();
  await paginaDeOlga.screenshot({ path: captura('matriz-estudio-dialogo-movil'), fullPage: true });
  await paginaDeOlga.setViewportSize({ width: 1280, height: 720 });

  await dialogo.getByRole('button', { name: 'Registrar estudio' }).click();
  await expect(dialogo).toBeHidden();
  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();

  const guardado = olga.getByRole('region', { name: 'Estudio de cumplimiento' });
  await expect(guardado).toContainText('3 · Alto');
  await expect(guardado).toContainText('Intensificada');
  await expect(guardado).toContainText('38,1 de 100');
  await expect(guardado).toContainText('Ajustado de 2 · Medio a 3 · Alto');
  await expect(guardado).toContainText('Opera con efectivo en zona de frontera.');
  await expect(guardado.getByText('soporte-estudio.pdf')).toBeVisible();
  // El archivo queda en el historial del expediente.
  await expect(olga.getByText('El oficial adjuntó un archivo: soporte-estudio.pdf')).toBeVisible();
  await esperarEstadoDelTercero(page, 'Aprobado');
});

test('quien no puede escribir la configuración ve la matriz pero no la cambia', async ({ page }) => {
  const e = await crearEscenario();
  const ruben = await abrirEmbebido(page, e, e.ruben, e.norte, 'matriz-riesgo');
  await expect(ruben.getByRole('heading', { level: 1, name: 'Matriz de riesgo' })).toBeVisible();
  await expect(ruben.getByText(/Puedes ver la matriz; para cambiarla necesitas el permiso de escritura/)).toBeVisible();
  await expect(ruben.getByRole('button', { name: 'Guardar matriz' })).toHaveCount(0);
  await expect(ruben.getByRole('button', { name: 'Usar la plantilla' })).toHaveCount(0);
  await expect(ruben.getByRole('article', { name: 'Sector o actividad económica' }).getByRole('textbox', { name: 'Peso' })).toBeDisabled();
});
