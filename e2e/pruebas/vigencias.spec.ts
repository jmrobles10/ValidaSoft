import { expect, test, type Browser, type Page } from '@playwright/test';
import { llamar } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { esperarCorreo } from '../soporte/correo';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../soporte/empresa';
import { abrirEmpresa, aceptarInvitacion, completarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha, revisarVigencias, SST, type Escenario, type Invitacion } from '../soporte/semilla';

/**
 * Vigencias del vínculo (VV-1…VV-12): en el pre-vencimiento el tercero ratifica su información y el vínculo no vence (o vence con su renovación
 * abierta, que sigue su curso); un bloqueo con fecha se levanta
 * solo; y la renovación por un documento vencido se cierra ratificándolo. El tiempo pasa con el job de Plataforma (`fecha` simulada,
 * solo Development): avisos a las 13:00 UTC del día de aviso y vencimiento a las 05:00 UTC del día siguiente a la gracia (TD-6).
 */

const RUT = 'Registro Único Tributario';
const RAZON_SOCIAL = 'Aceros de la Vigencia S.A.S.';

/** `YYYY-MM-DD` más `dias` días (calendario, sin zona). */
function sumar(fecha: string, dias: number): string {
  const [a, m, d] = fecha.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

/** Fecha `YYYY-MM-DD` de hoy en Bogotá más `dias`. */
function hoyMas(dias: number): string {
  return sumar(new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date()), dias);
}

/** `YYYY-MM-DD` → `dd/mm/aaaa`, como lo muestran las pantallas. */
function enPantalla(fecha: string): string {
  const [a, m, d] = fecha.split('-');
  return `${d ?? ''}/${m ?? ''}/${a ?? ''}`;
}

const momentoDeAviso = (fecha: string) => new Date(`${fecha}T13:00:00Z`);
const momentoDeVencimiento = (finDeGracia: string) => new Date(`${sumar(finDeGracia, 1)}T05:00:00Z`);

interface VinculoDelSistema {
  estado: string;
  vigenteHasta: string | null;
  expedienteAbiertoId: string | null;
  bloqueo: { hasta: string | null } | null;
}

const vinculoDe = (e: Escenario, invitacion: Invitacion) =>
  llamar<VinculoDelSistema>('GET', `/api/v1/vinculos/${invitacion.vinculoId}`, { llave: e.admin, empresa: invitacion.empresa.id });

const certificadoDe = (e: Escenario, invitacion: Invitacion) =>
  llamar<{ actual: { estado: string; emision: number; vigenteHasta: string | null } | null }>('GET', `/api/v1/vinculos/${invitacion.vinculoId}/certificado`, {
    llave: e.admin,
    empresa: invitacion.empresa.id,
  });

/** Diligencia Información general en el wizard (catálogos de ciudad y CIIU incluidos) y la guarda. */
async function diligenciarGeneral(page: Page): Promise<void> {
  await expect(page.getByRole('heading', { level: 1, name: 'Información general' })).toBeVisible();
  await page.getByRole('textbox', { name: 'Razón social' }).fill(RAZON_SOCIAL);
  await page.getByRole('textbox', { name: 'Correo electrónico' }).fill('compras@aceros-vigencia.co');
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

/** Proveedor de Sur con RUT y certificado SST aprobados por Sofía (el vínculo queda activo y con certificado). */
async function proveedorAprobado(page: Page, browser: Browser, e: Escenario, nombre: string): Promise<Invitacion> {
  const invitacion = await invitar(e, e.sur, nuevoTercero(nombre));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${invitacion.correo}`) },
    { nombre: SST.nombre, archivo: pdf(`SST ${invitacion.correo}`), campos: { 'Fecha de expedición': hoyMas(-2) } },
  ]);
  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
  await aprobarArea(sofia, 'Revisión');
  await esperarEstadoDelTercero(page, 'Aprobado');
  return invitacion;
}

interface ClienteConVigencia {
  e: Escenario;
  flujoId: string;
  invitacion: Invitacion;
  /** Hasta cuándo vale la validación (aprobada hoy + 90 días). */
  hasta: string;
}

/**
 * Cliente de Sur con su flujo CLIENTE (RUT + información general; la validación vale 90 días, avisos a 30 y 5, 3 de gracia): acepta,
 * diligencia la general, sube su RUT y Sofía lo aprueba: el vínculo queda activo con vigencia y con su certificado.
 */
async function clienteConVigencia(page: Page, browser: Browser): Promise<ClienteConVigencia> {
  const e = await crearEscenario();
  const flujoId = await publicarFlujoConFicha(e, e.sur, 'CLIENTES_VIGENCIA', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }], {
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
  await subirDocumento(page, RUT, pdf('RUT vigencia v1'));
  await esperarEstadoDelTercero(page, 'EnRevision');
  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión', 'Cliente');
  await aprobarArea(sofia, 'Revisión');
  await esperarEstadoDelTercero(page, 'Aprobado');

  const activo = await vinculoDe(e, invitacion);
  expect(activo.estado).toBe('Activo');
  const hasta = activo.vigenteHasta ?? '';
  expect(hasta).toBe(hoyMas(90));
  expect((await certificadoDe(e, invitacion)).actual).toMatchObject({ estado: 'Vigente', emision: 1, vigenteHasta: hasta });
  return { e, flujoId, invitacion, hasta };
}

/** Texto del asunto del correo de pre-vencimiento de Sur. */
const asuntoDePreVencimiento = (e: Escenario) => new RegExp(`^Ratifica tu información con ${e.sur.nombre.replace(/[.]/g, '\\.')} antes del`);

test('en el pre-vencimiento el tercero ratifica su información antes de la fecha y el vínculo nunca vence', async ({ page, browser }) => {
  const { e, flujoId, invitacion, hasta } = await clienteConVigencia(page, browser);

  // El editor de flujos de Sur muestra la vigencia del vínculo que quedó configurada.
  const paginaEditor = await (await browser.newContext()).newPage();
  const editor = await abrirEmbebido(paginaEditor, e, e.ana, e.sur, `flujos/${flujoId}`);
  const vigencia = editor.getByRole('group', { name: 'Vigencia del vínculo' });
  await expect(vigencia).toBeVisible();
  await expect(vigencia.getByRole('textbox', { name: 'Días', exact: true })).toHaveValue('90');
  await expect(vigencia.getByRole('textbox', { name: 'Avisos (días antes de vencer)' })).toHaveValue('30, 5');
  await expect(vigencia.getByRole('textbox', { name: 'Gracia (días)' })).toHaveValue('3');
  await expect(editor.getByRole('textbox', { name: 'Vigencia (días)' })).toBeVisible(); // la sección general trae la suya
  await paginaEditor.screenshot({ path: 'test-results/vigencias-editor-de-flujos.png', fullPage: true });

  // La administración de Sur ve «Vigente hasta» en el vínculo.
  const paginaAna = await (await browser.newContext()).newPage();
  const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
  await expect(ana.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
  await expect(ana.getByText('Vigente hasta', { exact: true }).first()).toBeVisible();
  await expect(ana.getByText(enPantalla(hasta), { exact: true }).first()).toBeVisible();

  // Llega el primer aviso (30 días antes): pre-vencimiento. El vínculo pasa a «Por vencer», el certificado sigue vigente y ya está
  // abierta la renovación, con el correo que lleva al tercero a ratificar.
  const aviso = await revisarVigencias(e, momentoDeAviso(sumar(hasta, -30)));
  expect(aviso).toMatchObject({ revisados: 1, avisados: 1, vencidos: 0 });
  const preVencimiento = await vinculoDe(e, invitacion);
  expect(preVencimiento.estado).toBe('PorVencer');
  expect(preVencimiento.expedienteAbiertoId).not.toBeNull();
  expect((await certificadoDe(e, invitacion)).actual?.estado).toBe('Vigente');
  const correoAviso = await esperarCorreo(invitacion.correo, asuntoDePreVencimiento(e));
  expect(correoAviso.texto).toContain('pre-vencimiento');
  expect(correoAviso.texto).toContain('/e/');
  await paginaAna.reload();
  await expect(ana.getByText('La validación de este tercero está en pre-vencimiento')).toBeVisible();
  await expect(ana.getByText(`Vence el ${enPantalla(hasta)}. Su renovación está abierta`)).toBeVisible();
  await expect(ana.getByText('Por vencer', { exact: true }).first()).toBeVisible();
  await paginaAna.screenshot({ path: 'test-results/vigencias-vinculo-pre-vencimiento.png', fullPage: true });

  // El tercero lo ve en Mis empresas con la fecha y la acción de ratificar.
  await page.goto('/empresas');
  const tarjetaSur = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true }) });
  await expect(tarjetaSur).toContainText('En pre-vencimiento');
  await expect(tarjetaSur).toContainText(`En pre-vencimiento: ratifica tu información con ${e.sur.nombre} antes del ${enPantalla(hasta)}`);
  await expect(tarjetaSur.getByRole('link', { name: 'Ratificar mi información' })).toBeVisible();
  await page.screenshot({ path: 'test-results/vigencias-mis-empresas-pre-vencimiento.png', fullPage: true });

  // En su revisión la sección aparece para ratificar o actualizar, no para completar.
  await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
  const tarjetaGeneral = page.getByRole('listitem').filter({ has: page.getByRole('heading', { name: 'Información general', exact: true }) });
  await expect(tarjetaGeneral.getByRole('button', { name: 'Ratificar' })).toBeVisible({ timeout: 30_000 });
  await expect(tarjetaGeneral.getByRole('link', { name: 'Actualizar en Mi información' })).toBeVisible();
  await page.screenshot({ path: 'test-results/vigencias-renovacion-seccion.png', fullPage: true });

  // En Mi información el tercero ve qué empresa se lo pide y ratifica la sección sin editarla, antes de la fecha.
  await page.goto('/ficha');
  await expect(page.getByText('1 empresa te pide ratificar tu información')).toBeVisible({ timeout: 30_000 });
  await expect(page.getByText(`Confirma que sigue igual (sin cambiar nada) o actualízala. Te lo piden: ${e.sur.nombre}`)).toBeVisible();
  await page.screenshot({ path: 'test-results/vigencias-ratificar-mi-informacion.png', fullPage: true });
  await page.getByRole('button', { name: `Ratificar para ${e.sur.nombre}` }).click();
  const dialogo = page.getByRole('dialog', { name: '¿Tu información de Información general sigue igual?' });
  await expect(dialogo).toContainText(`${e.sur.nombre} la da por renovada sin volver a revisarla`);
  await dialogo.getByRole('button', { name: 'Sí, sigue igual' }).click();
  await expect(page.getByText(`Confirmaste tu información de Información general. ${e.sur.nombre} la dio por renovada.`)).toBeVisible();

  // Activo otra vez, con la vigencia nueva desde esa aprobación y el certificado reemitido, sin haber vencido nunca: la ficha del
  // tercero no se tocó y el historial de Sur no tiene ningún «Vínculo vencido».
  await expect.poll(async () => (await vinculoDe(e, invitacion)).estado, { timeout: 30_000 }).toBe('Activo');
  const renovado = await vinculoDe(e, invitacion);
  expect(renovado.vigenteHasta).toBe(hoyMas(90));
  expect(renovado.expedienteAbiertoId).toBeNull();
  await expect.poll(async () => (await certificadoDe(e, invitacion)).actual, { timeout: 30_000 }).toMatchObject({ estado: 'Vigente', emision: 2, vigenteHasta: hoyMas(90) });
  await expect(page.getByText('1 empresa te pide ratificar tu información')).toBeHidden({ timeout: 30_000 });
  await page.goto('/empresas');
  await expect(tarjetaSur).toContainText('Aprobado');
  await paginaAna.reload();
  await expect(ana.getByText('Activo', { exact: true }).first()).toBeVisible();
  await expect(ana.getByText('Vínculo en pre-vencimiento', { exact: true }).first()).toBeVisible(); // el aviso quedó en el historial
  await expect(ana.getByText('Vínculo vencido', { exact: true })).toBeHidden();
  await expect(ana.getByText('La validación de este tercero está en pre-vencimiento')).toBeHidden();
});

test('si el tercero no ratifica a tiempo el vínculo vence con su certificado y la misma renovación abierta sigue su curso', async ({ page, browser }) => {
  const { e, invitacion, hasta } = await clienteConVigencia(page, browser);

  // El primer aviso abre el pre-vencimiento con su renovación; el segundo (5 días antes) solo la recuerda.
  expect(await revisarVigencias(e, momentoDeAviso(sumar(hasta, -30)))).toMatchObject({ revisados: 1, avisados: 1 });
  const renovacionId = (await vinculoDe(e, invitacion)).expedienteAbiertoId ?? '';
  expect(renovacionId).not.toBe('');
  expect(await revisarVigencias(e, momentoDeAviso(sumar(hasta, -5)))).toMatchObject({ revisados: 1, avisados: 1 });
  const recordado = await vinculoDe(e, invitacion);
  expect(recordado).toMatchObject({ estado: 'PorVencer', expedienteAbiertoId: renovacionId });
  await esperarCorreo(invitacion.correo, asuntoDePreVencimiento(e));

  // Terminada la gracia (3 días después de la fecha) con la renovación sin ratificar: el vínculo vence, su certificado también y
  // la renovación que ya estaba abierta es la misma (no se abre otra).
  const vence = await revisarVigencias(e, momentoDeVencimiento(sumar(hasta, 3)));
  expect(vence).toMatchObject({ revisados: 1, vencidos: 1 });
  const vencido = await vinculoDe(e, invitacion);
  expect(vencido).toMatchObject({ estado: 'Vencido', expedienteAbiertoId: renovacionId });
  expect((await certificadoDe(e, invitacion)).actual?.estado).toBe('Vencido');
  await esperarCorreo(invitacion.correo, /venció: ratifica tu información/);

  // El tercero ve que venció y que le toca ratificar; lo hace en la misma renovación y todo vuelve a estar al día.
  await page.goto('/empresas');
  const tarjetaSur = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true }) });
  await expect(tarjetaSur).toContainText('Ratifica tu información');
  await expect(tarjetaSur).toContainText(`La validación con ${e.sur.nombre} venció`);
  await page.screenshot({ path: 'test-results/vigencias-mis-empresas-vencido.png', fullPage: true });
  await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
  const tarjetaGeneral = page.getByRole('listitem').filter({ has: page.getByRole('heading', { name: 'Información general', exact: true }) });
  await expect(tarjetaGeneral.getByRole('button', { name: 'Ratificar' })).toBeVisible({ timeout: 30_000 });
  await tarjetaGeneral.getByRole('button', { name: 'Ratificar' }).click();
  const dialogo = page.getByRole('dialog', { name: '¿Tu información de Información general sigue igual?' });
  await dialogo.getByRole('button', { name: 'Sí, sigue igual' }).click();
  await expect(page.getByText(`Confirmaste tu información de Información general. ${e.sur.nombre} la dio por renovada.`)).toBeVisible();

  await expect.poll(async () => (await vinculoDe(e, invitacion)).estado, { timeout: 30_000 }).toBe('Activo');
  const renovado = await vinculoDe(e, invitacion);
  expect(renovado).toMatchObject({ vigenteHasta: hoyMas(90), expedienteAbiertoId: null });
  await expect.poll(async () => (await certificadoDe(e, invitacion)).actual, { timeout: 30_000 }).toMatchObject({ estado: 'Vigente', emision: 2, vigenteHasta: hoyMas(90) });
});

test('un bloqueo con fecha se levanta solo: el job lo restituye al llegar el día y el vínculo vuelve a estar activo', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await proveedorAprobado(page, browser, e, 'Andamios de la Fecha S.A.S.');
  const paginaAna = await (await browser.newContext()).newPage();
  const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
  await expect(ana.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();

  // Ana bloquea hasta mañana (con fecha de fin).
  const manana = hoyMas(1);
  await ana.getByRole('button', { name: 'Más acciones' }).click();
  await ana.getByRole('menuitem', { name: /^Bloquear/ }).click();
  const bloquear = ana.getByRole('dialog', { name: /^Bloquear vínculo/ });
  await bloquear.getByRole('textbox', { name: 'Motivo' }).fill('Revisión temporal de sanciones.');
  await bloquear.getByLabel('Bloquear hasta (opcional)').fill(manana);
  await bloquear.getByRole('button', { name: 'Bloquear vínculo' }).click();
  await expect(bloquear).toBeHidden();
  await expect(ana.getByText('Vínculo bloqueado', { exact: true }).first()).toBeVisible();
  await expect(ana.getByText(`hasta el ${enPantalla(manana)}`).first()).toBeVisible();
  expect((await certificadoDe(e, invitacion)).actual?.estado).toBe('Suspendido');

  // Antes de la fecha el job no lo toca.
  expect(await revisarVigencias(e, new Date(Date.now() + 3_600_000))).toMatchObject({ desbloqueados: 0 });
  expect((await vinculoDe(e, invitacion)).estado).toBe('Bloqueado');

  // Pasada la fecha lo levanta: el vínculo y su certificado vuelven a como estaban, con el motivo en el historial.
  expect(await revisarVigencias(e, new Date(`${sumar(manana, 1)}T05:00:00Z`))).toMatchObject({ revisados: 1, desbloqueados: 1 });
  expect((await vinculoDe(e, invitacion)).estado).toBe('Activo');
  expect((await certificadoDe(e, invitacion)).actual?.estado).toBe('Vigente');
  await paginaAna.reload();
  await expect(ana.getByText('Vínculo desbloqueado', { exact: true }).first()).toBeVisible();
  await expect(ana.getByText(/El bloqueo temporal terminó el /).first()).toBeVisible();
  await expect(ana.getByText('Vínculo bloqueado', { exact: true }).first()).toBeVisible(); // el evento sigue en el historial
  await esperarCorreo(invitacion.correo, /suspendió tu certificado/);
});

test('el mensaje programado levanta un bloqueo que termina en segundos, sin esperar al job', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await proveedorAprobado(page, browser, e, 'Cerramientos Rápidos Ltda.');

  await llamar('POST', `/api/v1/vinculos/${invitacion.vinculoId}/bloquear`, {
    llave: e.admin,
    empresa: e.sur.id,
    cuerpo: { motivo: 'Revisión rápida', hasta: new Date(Date.now() + 5000).toISOString() },
  });
  expect((await vinculoDe(e, invitacion)).estado).toBe('Bloqueado');
  await expect.poll(async () => (await vinculoDe(e, invitacion)).estado, { timeout: 60_000, intervals: [1000] }).toBe('Activo');
  expect((await vinculoDe(e, invitacion)).bloqueo).toBeNull();
  await expect.poll(async () => (await certificadoDe(e, invitacion)).actual?.estado, { timeout: 30_000 }).toBe('Vigente');
});

test('un documento que vence abre la renovación del vínculo y el tercero la cierra ratificando que no cambió', async ({ page, browser }) => {
  // El RUT de este grupo vale 10 días desde su carga y, sin gracia, vence al terminar el décimo; el certificado SST de Sur sigue vigente.
  const e = await crearEscenario({ diasRut: 10 });
  const invitacion = await proveedorAprobado(page, browser, e, 'Cementos del Documento S.A.S.');
  expect((await vinculoDe(e, invitacion)).estado).toBe('Activo');

  // A los 20 días vence el RUT (no el SST, que da 30 + 5 de gracia): el vínculo vence y se abre su renovación solo con el RUT.
  const resultado = await revisarVigencias(e, new Date(Date.now() + 20 * 86_400_000), { incluirDocumentos: true });
  expect(resultado.documentosVencidos).toBe(1);
  await expect.poll(async () => (await vinculoDe(e, invitacion)).estado, { timeout: 30_000 }).toBe('Vencido');
  expect((await vinculoDe(e, invitacion)).expedienteAbiertoId).not.toBeNull();

  // El tercero ve en su revisión que le toca ratificar el RUT («No ha cambiado») y lo hace.
  await page.goto('/empresas');
  await abrirEmpresa(page, e.sur.nombre);
  const tarjetaRut = page.getByRole('listitem').filter({ has: page.getByRole('heading', { name: RUT, exact: true }) });
  await expect(tarjetaRut.getByRole('button', { name: 'No ha cambiado' })).toBeVisible({ timeout: 30_000 });
  await page.screenshot({ path: 'test-results/vigencias-renovacion-por-documento.png', fullPage: true });
  await tarjetaRut.getByRole('button', { name: 'No ha cambiado' }).click();
  const dialogo = page.getByRole('dialog', { name: `¿Tu ${RUT} sigue igual?` });
  await dialogo.getByRole('button', { name: 'Sí, sigue igual' }).click();
  await expect(page.getByText(`Confirmaste tu ${RUT}. ${e.sur.nombre} lo dio por renovado.`)).toBeVisible();

  // La renovación se aprueba sin pasar por las áreas: el vínculo vuelve a estar activo y el certificado se reemite.
  await expect.poll(async () => (await vinculoDe(e, invitacion)).estado, { timeout: 30_000 }).toBe('Activo');
  await expect.poll(async () => (await certificadoDe(e, invitacion)).actual, { timeout: 30_000 }).toMatchObject({ estado: 'Vigente', emision: 2 });
});
