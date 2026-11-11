import { expect, test, type Page } from '@playwright/test';
import { CLAVE_PLATAFORMA, llamar } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { capturar } from '../soporte/capturas';
import { esperarCorreo } from '../soporte/correo';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../soporte/empresa';
import { escribirFecha } from '../soporte/fechas';
import { abrirEmpresa, aceptarInvitacion, completarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha, type Escenario, type Invitacion } from '../soporte/semilla';

/**
 * Operación de la revisión (OP-1…OP-19): campañas de renovación (filtro → vista previa → aviso → proceso por tandas), delegación de revisión por
 * ausencias, plazos de revisión (SLA) con su indicador en la bandeja y su informe de cumplimiento, y la ayuda por documento de la empresa que lee el
 * tercero. El tiempo pasa con los jobs de Plataforma (`fecha` simulada, solo Development).
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';
const RAZON_SOCIAL = 'Aceros de la Campaña S.A.S.';

function sumar(fecha: string, dias: number): string {
  const [a, m, d] = fecha.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

const hoyMas = (dias: number) => sumar(new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date()), dias);

async function diligenciarGeneral(page: Page): Promise<void> {
  await expect(page.getByRole('heading', { level: 1, name: 'Información general' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Razón social' }).fill(RAZON_SOCIAL);
  await page.getByRole('textbox', { name: 'Correo electrónico' }).fill('compras@aceros-campania.co');
  await page.getByRole('textbox', { name: 'Teléfono' }).fill('601 555 1234');
  await page.getByRole('combobox', { name: 'País' }).fill('Colombia');
  await page.getByRole('option', { name: 'Colombia' }).first().click();
  await page.getByRole('combobox', { name: 'Ciudad' }).fill('Bogot');
  await page.getByRole('option', { name: /^Bogotá/ }).first().click();
  await page.getByRole('textbox', { name: 'Dirección', exact: true }).fill('Calle 45 # 12-08');
  await page.getByRole('combobox', { name: 'Actividad económica principal (CIIU)' }).fill('4663');
  await page.getByRole('option', { name: /^4663/ }).first().click();
  const barra = page.getByRole('group', { name: 'Guardar la sección' });
  await expect(barra.getByText('Lista: puedes continuar')).toBeVisible();
  await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByText('Guardaste Información general: está completa.')).toBeVisible();
}

/** Proveedor de Norte con RUT y Cámara subidos: su expediente queda en revisión de Compras y Cumplimiento. */
async function proveedorEnRevision(page: Page, e: Escenario, nombre: string, etiqueta: string): Promise<Invitacion> {
  const invitacion = await invitar(e, e.norte, nuevoTercero(nombre));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${etiqueta}`) },
    { nombre: CAMARA, archivo: pdf(`Cámara ${etiqueta}`) },
  ]);
  return invitacion;
}

test('la campaña de renovación muestra a quién afecta, avisa antes de confirmar y abre la renovación del tercero', async ({ page, browser }) => {
  const e = await crearEscenario();
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_CAMPANIA', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }], {
    dias: 90,
    diasAviso: [30, 5],
    diasGracia: 3,
  });
  const invitacion = await invitar(e, e.sur, nuevoTercero(RAZON_SOCIAL), 'CLIENTE');
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await page.getByRole('link', { name: /^Completar/ }).first().click();
  await diligenciarGeneral(page);
  await page.goto('/empresas');
  await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
  await subirDocumento(page, RUT, pdf('RUT campaña v1'));
  await esperarEstadoDelTercero(page, 'EnRevision');
  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión', 'Cliente');
  await aprobarArea(sofia, 'Revisión');
  await esperarEstadoDelTercero(page, 'Aprobado');

  // Vencimientos → Campañas de renovación → Nueva campaña.
  const paginaAna = await (await browser.newContext()).newPage();
  const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'vencimientos');
  await ana.getByRole('link', { name: 'Campañas de renovación' }).or(ana.getByRole('button', { name: 'Campañas de renovación' })).first().click();
  await expect(ana.getByText('Aún no hay campañas de renovación.')).toBeVisible();
  await capturar(paginaAna, 'campanias-vacio');
  await ana.getByRole('button', { name: 'Nueva campaña' }).click();
  await expect(ana.getByRole('heading', { level: 1, name: 'Nueva campaña de renovación' })).toBeVisible();

  // Con 30 días no entra (vence en 90); con 120 sí. La vista previa dice a quién afecta y qué pasará, sin enviar nada.
  await ana.getByRole('button', { name: 'Ver a quiénes afecta' }).click();
  await expect(ana.getByText('Ningún vínculo con este filtro necesita')).toBeVisible();
  await ana.getByRole('textbox', { name: 'Vencen en (días)' }).fill('120');
  await ana.getByRole('button', { name: 'Ver a quiénes afecta' }).click();
  await expect(ana.getByText('Se les abre la renovación')).toBeVisible();
  await expect(ana.getByText('Reciben correo')).toBeVisible();
  await expect(ana.getByRole('table').getByText(invitacion.nombre)).toBeVisible();
  await expect(ana.getByRole('table').getByText('Se abre la renovación')).toBeVisible();
  await capturar(paginaAna, 'campanias-vista-previa');

  // Antes de crear, el aviso dice qué pasará y a quién; cancelar no crea nada.
  await ana.getByLabel('Nombre de la campaña').fill('Renovación de clientes E2E');
  await ana.getByRole('button', { name: 'Abrir renovaciones' }).click();
  const aviso = ana.getByRole('dialog', { name: '¿Abrir las renovaciones?' });
  await expect(aviso.getByText('Se abrirá la renovación a 1 tercero.')).toBeVisible();
  await expect(aviso.getByText('A 1 tercero le llega un correo, y a todos un aviso en su campana.')).toBeVisible();
  await expect(aviso.getByText('Puedes pausarla o cancelarla cuando quieras.')).toBeVisible();
  await capturar(paginaAna, 'campanias-aviso');
  await aviso.getByRole('button', { name: 'Cancelar' }).click();
  await expect(ana.getByRole('dialog')).toHaveCount(0);
  const sinCrear = await llamar<{ datos: unknown[] }>('GET', '/api/v1/campanias', { llave: e.admin, empresa: e.sur.id });
  expect(sinCrear.datos).toHaveLength(0);

  // Confirmar: la campaña se procesa en segundo plano y queda completada con su renovación abierta.
  await ana.getByRole('button', { name: 'Abrir renovaciones' }).click();
  await ana.getByRole('dialog', { name: '¿Abrir las renovaciones?' }).getByRole('button', { name: 'Confirmar y abrir' }).click();
  await expect(ana.getByRole('heading', { level: 1, name: 'Renovación de clientes E2E' })).toBeVisible();
  await expect(ana.getByText('Completada', { exact: true })).toBeVisible({ timeout: 40_000 });
  await expect(ana.getByRole('table', { name: 'Vínculos de la campaña' }).getByText(/^Renovación abierta · /)).toBeVisible();
  await capturar(paginaAna, 'campanias-detalle');

  const vinculo = await llamar<{ estado: string; expedienteAbiertoId: string | null }>('GET', `/api/v1/vinculos/${invitacion.vinculoId}`, {
    llave: e.admin,
    empresa: e.sur.id,
  });
  expect(vinculo.estado).toBe('Activo');
  expect(vinculo.expedienteAbiertoId).not.toBeNull();

  // El tercero recibe el correo de la renovación y el aviso en su campana.
  const correo = await esperarCorreo(invitacion.correo, new RegExp(`^${e.sur.nombre.replace(/[.]/g, '\\.')} abrió la renovación de tu validación$`));
  expect(correo.texto).toContain('/e/');
  await page.goto('/');
  await page.getByRole('button', { name: /Notificaciones/ }).click();
  await expect(page.getByText('Tu empresa abrió tu renovación')).toBeVisible();

});

async function configurarPlazo(app: Awaited<ReturnType<typeof abrirEmbebido>>, horas: string): Promise<void> {
  await expect(app.getByRole('heading', { level: 1, name: 'Plazos de revisión' })).toBeVisible();
  await app.getByRole('textbox', { name: 'Plazo en horas de Compras' }).fill(horas);
  await app.getByRole('button', { name: 'Guardar cambios' }).click();
  await expect(app.getByText('Plazos de revisión guardados.')).toBeVisible();
}

test('el plazo de revisión avisa al acercarse y al vencer, marca la bandeja y el informe mide el cumplimiento', async ({ page, browser }) => {
  const e = await crearEscenario();
  const paginaAna = await (await browser.newContext()).newPage();

  // Configuración: Compras tiene 10 horas por defecto.
  const ana = await abrirEmbebido(paginaAna, e, e.ana, e.norte, 'plazos-de-revision');
  await configurarPlazo(ana, '10');
  await capturar(paginaAna, 'plazos-de-revision');

  const invitacion = await proveedorEnRevision(page, e, 'Aceros del Plazo S.A.S.', 'plazo');
  const expedientes = await llamar<{ datos: { id: string; etapas: { areas: { codigo: string; slaVence: string | null }[] }[] }[] }>('GET', '/api/v1/expedientes', {
    llave: e.admin,
    empresa: e.norte.id,
  });
  const compras = expedientes.datos[0]!.etapas.flatMap((x) => x.areas).find((a) => a.codigo === 'COMPRAS' || a.codigo === 'compras');
  expect(compras?.slaVence).toBeTruthy();
  const vence = new Date(compras!.slaVence!);
  const inicio = new Date(vence.getTime() - 10 * 3600 * 1000);

  // A las 9 horas: aviso previo; a las 11: vencido.
  const aviso = await llamar<{ avisos: number }>('POST', '/api/v1/plataforma/sla/revisar', {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { tenantId: e.tenantId, fecha: new Date(inicio.getTime() + 9 * 3600 * 1000).toISOString() },
  });
  expect(aviso.avisos).toBe(1);
  await esperarCorreo(e.ruben.correo, /^Plazo de revisión por vencer:/);
  const vencido = await llamar<{ incumplidos: number }>('POST', '/api/v1/plataforma/sla/revisar', {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { tenantId: e.tenantId, fecha: new Date(inicio.getTime() + 11 * 3600 * 1000).toISOString() },
  });
  expect(vencido.incumplidos).toBe(1);
  await esperarCorreo(e.ana.correo, /^Plazo de revisión vencido:/);

  // La bandeja de Rubén (Compras) muestra el plazo de la fila; el informe lo cuenta como vencido ahora... a la hora real aún corre.
  const paginaRuben = await (await browser.newContext()).newPage();
  const ruben = await abrirEmbebido(paginaRuben, e, e.ruben, e.norte, 'bandeja');
  await expect(ruben.getByRole('columnheader', { name: 'Plazo de revisión' })).toBeVisible();
  await expect(ruben.getByRole('row').filter({ hasText: invitacion.nombre }).getByText('A tiempo', { exact: true })).toBeVisible();
  await capturar(paginaRuben, 'bandeja-plazo');

  // Informe de cumplimiento del plazo de revisión.
  const paginaInforme = await (await browser.newContext()).newPage();
  const informe = await abrirEmbebido(paginaInforme, e, e.ana, e.norte, 'informes/sla');
  await expect(informe.getByRole('heading', { level: 1 })).toBeVisible();
  await expect(informe.getByText('Compras', { exact: true }).first()).toBeVisible();
  await capturar(paginaInforme, 'informe-sla');
});

test('la empresa escribe la ayuda de un documento y el tercero la lee al entregarlo', async ({ page, browser }) => {
  const e = await crearEscenario();
  const paginaAna = await (await browser.newContext()).newPage();
  const ana = await abrirEmbebido(paginaAna, e, e.ana, e.norte, 'ayuda-de-documentos');
  await expect(ana.getByRole('heading', { level: 1, name: 'Ayuda de documentos' })).toBeVisible();
  await capturar(paginaAna, 'ayuda-de-documentos');
  await ana.getByRole('button', { name: 'Editar la ayuda de Registro Único Tributario' }).click();
  await ana.getByRole('textbox', { name: /Ayuda de Constructora Norte/ }).fill('Trae el RUT con fecha de este año y las responsabilidades marcadas.');
  await ana.getByRole('button', { name: 'Guardar' }).click();
  await expect(ana.getByText('Ayuda guardada: el tercero la ve desde ahora.')).toBeVisible();
  await expect(ana.getByRole('row').filter({ hasText: 'Registro Único Tributario' }).getByText('Propia', { exact: true })).toBeVisible();

  // El tercero de Norte la lee en su expediente; Sur no la tiene (la del grupo).
  const invitacion = await invitar(e, e.norte, nuevoTercero('Aceros de la Ayuda S.A.S.'));
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  await expect(page.getByText('Trae el RUT con fecha de este año y las responsabilidades marcadas.').first()).toBeVisible();
});

test('la delegación avisa antes de crearse y quien la recibe revisa el área entre las fechas', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await proveedorEnRevision(page, e, 'Aceros de la Delegación S.A.S.', 'delegacion');

  // Rubén (Compras) delega su área en Olga mientras está de vacaciones.
  const paginaRuben = await (await browser.newContext()).newPage();
  const ruben = await abrirEmbebido(paginaRuben, e, e.ruben, e.norte, 'delegaciones');
  await expect(ruben.getByRole('heading', { level: 1, name: 'Delegación de revisión' })).toBeVisible();
  await expect(ruben.getByText('Nadie revisa en lugar de otra persona por ahora.')).toBeVisible();
  await capturar(paginaRuben, 'delegaciones-vacio');
  await ruben.getByRole('button', { name: 'Delegar mi revisión' }).first().click();
  const dialogo = ruben.getByRole('dialog', { name: 'Delegar mi revisión' });
  await dialogo.getByRole('combobox', { name: 'Quién revisa en su lugar' }).click();
  await ruben.getByRole('option', { name: /Olga Oficial/ }).click();
  await escribirFecha(paginaRuben, 'Desde', hoyMas(0), dialogo);
  await escribirFecha(paginaRuben, 'Hasta', hoyMas(5), dialogo);
  await dialogo.getByLabel('Motivo (opcional)').fill('Vacaciones');
  await expect(dialogo.getByText(/Olga Oficial revisará en tu lugar/)).toBeVisible();
  await expect(dialogo.getByText(/separación de funciones y de conflicto de intereses se le siguen aplicando/)).toBeVisible();
  await capturar(paginaRuben, 'delegaciones-aviso');
  await dialogo.getByRole('button', { name: 'Delegar', exact: true }).click();
  await expect(ruben.getByText(/Delegación creada: Olga Oficial revisa en tu lugar/)).toBeVisible();
  await expect(ruben.getByRole('row').filter({ hasText: 'Olga Oficial' }).getByText('Vigente', { exact: true })).toBeVisible();

  // Olga ve en su bandeja el expediente de Compras que no era suyo.
  const paginaOlga = await (await browser.newContext()).newPage();
  const olga = await abrirEmbebido(paginaOlga, e, e.olga, e.norte, 'bandeja');
  await expect(olga.getByRole('row').filter({ hasText: invitacion.nombre }).getByText('Compras', { exact: true })).toBeVisible();
  await capturar(paginaOlga, 'bandeja-delegada');

  // Rubén revoca: Olga deja de verla.
  await ruben.getByRole('button', { name: 'Revocar la delegación de Rubén Revisor a Olga Oficial' }).click();
  await ruben.getByRole('dialog', { name: 'Revocar delegación' }).getByRole('button', { name: 'Revocar delegación' }).click();
  await expect(ruben.getByText('Delegación revocada.')).toBeVisible();
  await paginaOlga.reload();
  await expect(olga.getByRole('row').filter({ hasText: invitacion.nombre }).getByText('Compras', { exact: true })).toHaveCount(0);
});

test('las plantillas de correo traen los avisos de pre-vencimiento y vigencia para personalizarlos', async ({ browser }) => {
  const e = await crearEscenario();
  const pagina = await (await browser.newContext()).newPage();
  const ana = await abrirEmbebido(pagina, e, e.ana, e.norte, 'plantillas');
  await expect(ana.getByText('Pre-vencimiento de la validación')).toBeVisible();
  await expect(ana.getByText('Validación vencida')).toBeVisible();
  await expect(ana.getByText('Renovación abierta por la empresa')).toBeVisible();
  await capturar(pagina, 'plantillas-vigencia');
});

