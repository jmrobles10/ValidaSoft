import { expect, test, type Browser, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { capturar } from '../soporte/capturas';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion, rechazar } from '../soporte/empresa';
import { aprobarUltimaArea, enRevision, esperarListas, pedirVerificaciones, personaEmbebida, revisarVerificaciones } from '../soporte/cumplimiento';
import { abrirEmpresa, completarInvitacion, esperarEstadoDelTercero } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../soporte/semilla';

/**
 * Cumplimiento a lo largo del tiempo (H7), con el proveedor de listas simulado (un nombre con «coincidencia» aparece en OFAC): una
 * coincidencia confirmada que termina en rechazo sin tocar a la otra empresa, un estudio de cumplimiento que vence con el expediente abierto
 * y el monitoreo periódico de un vínculo activo que encuentra una persona nueva. El tiempo se simula con el barrido inmediato de
 * Plataforma (`revisarVerificaciones`): solo en Development y solo para el grupo de la prueba.
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

/** Fecha de hoy más `dias` días en la zona de Bogotá, como la muestran las pantallas (`dd/mm/aaaa`). */
function enPantalla(dias: number): string {
  return new Intl.DateTimeFormat('es-CO', { timeZone: 'America/Bogota', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(Date.now() + dias * 86_400_000));
}

/** Fecha `YYYY-MM-DD` de hoy (zona de Bogotá), para el campo de expedición del certificado propio de Sur. */
function hoy(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
}

const desdeAhora = (dias: number) => new Date(Date.now() + dias * 86_400_000);

async function paginaDe(browser: Browser): Promise<Page> {
  return (await browser.newContext()).newPage();
}

test('una coincidencia confirmada no deja aprobar y termina en rechazo; la otra empresa del mismo tercero no se entera y lo aprueba', async ({ page, browser }) => {
  test.setTimeout(420_000);
  const e = await crearEscenario();
  await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
  // El mismo tercero se valida en dos empresas: Norte pide listas restrictivas; Sur no pide ninguna verificación.
  const tercero = nuevoTercero('Ferretería Coincidencia Confirmada S.A.S.');
  const enNorte = await invitar(e, e.norte, tercero);
  const enSur = await invitar(e, e.sur, tercero);
  await completarInvitacion(page, enNorte, [
    { nombre: RUT, archivo: pdf(`RUT ${tercero.nombre}`) },
    { nombre: CAMARA, archivo: pdf(`Camara ${tercero.nombre}`) },
  ]);
  await completarInvitacion(page, enSur, [{ nombre: SST.nombre, archivo: pdf(`SST ${tercero.nombre}`), campos: { 'Fecha de expedición': hoy() } }]);

  // Compras aprueba lo suyo; Cumplimiento ve la coincidencia en OFAC y la confirma con su justificación.
  const ruben = await personaEmbebida(browser, e, e.ruben);
  await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');
  const olga = await personaEmbebida(browser, e, e.olga);
  await esperarListas(olga, tercero.nombre, 'Cumplimiento', 'Coincidencia por analizar');
  const coincidencia = olga.getByRole('listitem', { name: `${tercero.nombre} · OFAC (SDN)` });
  await coincidencia.getByRole('button', { name: 'Confirmar' }).click();
  const confirmar = olga.getByRole('dialog', { name: 'Confirmar coincidencia' });
  await confirmar.getByRole('textbox', { name: /Justificación/ }).fill('Es la misma persona: el NIT y el representante coinciden con el listado.');
  await confirmar.getByRole('button', { name: 'Guardar decisión' }).click();
  await expect(confirmar).toBeHidden();
  await expect(olga.getByRole('region', { name: 'Listas restrictivas' })).toContainText('Fallida');
  await expect(coincidencia).toContainText('Confirmada');

  // Aunque la última área apruebe, el expediente no se aprueba: la verificación bloqueante falló y nada la reemplaza.
  await aprobarUltimaArea(olga, 'Cumplimiento', /Listas restrictivas \(fallida\)/);
  await page.goto('/empresas');
  await abrirEmpresa(page, e.norte.nombre);
  await esperarEstadoDelTercero(page, 'EnRevision');

  // Cumplimiento (un área que puede rechazar) lo rechaza con motivos y su justificación; queda dicho quién lo rechazó.
  await rechazar(olga, ['No corresponde al tercero'], 'Coincidencia confirmada con OFAC: no se puede vincular.');
  await expect(olga.getByRole('alert').filter({ hasText: 'Cumplimiento lo rechazó el' })).toContainText('El vínculo volvió a Invitado.');
  await esperarEstadoDelTercero(page, 'Rechazado');
  // El tercero sabe que Norte cerró la revisión, pero no se entera de la coincidencia ni de la justificación interna.
  await expect(page.getByRole('main')).toContainText('Constructora Norte S.A.S. cerró la revisión sin aprobarla.');
  await expect(page.getByRole('main')).not.toContainText(/OFAC|no se puede vincular|Es la misma persona/);
  await capturar(page, 'cumplimiento-rechazo-tercero');

  // El informe SAGRILAFT de Norte cuenta al tercero.
  await irASeccion(olga, 'Cumplimiento');
  await olga.getByRole('tab', { name: 'Informe SAGRILAFT' }).click();
  await expect(olga.getByRole('table', { name: 'Informe SAGRILAFT' })).toContainText(tercero.nombre);

  // En Sur nada cambió: sin verificaciones, sin coincidencia y sin rechazo. Sofía revisa y aprueba como siempre.
  const sofia = await abrirEmbebido(await paginaDe(browser), e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Proveedor');
  await expect(sofia.getByRole('region', { name: 'Listas restrictivas' })).toHaveCount(0);
  await expect(sofia.getByText(/OFAC|Listas restrictivas|Fallida|por analizar|Rechazado por/)).toHaveCount(0);
  await aprobarArea(sofia, 'Revisión');
  await page.goto('/empresas');
  await abrirEmpresa(page, e.sur.nombre);
  await esperarEstadoDelTercero(page, 'Aprobado');
});

test('el estudio de cumplimiento vence con el expediente abierto: avisa a Cumplimiento, bloquea el cierre y se aprueba al renovarlo', async ({ page, browser }) => {
  test.setTimeout(360_000);
  const e = await crearEscenario();
  // El estudio vale un día: así basta simular dos días para que venza con el expediente todavía abierto.
  await pedirVerificaciones(e, [
    { tipo: 'ListasRestrictivas', bloqueante: true },
    { tipo: 'EstudioDeCumplimiento', bloqueante: true, vigenciaDias: 1 },
  ]);
  const nombre = 'Cementos Estudio Vencido S.A.S.';
  await enRevision(page, e, nombre);

  // Olga registra su estudio favorable (riesgo bajo), pero Compras aún no ha decidido: el expediente sigue abierto.
  const olga = await personaEmbebida(browser, e, e.olga);
  await esperarListas(olga, nombre, 'Cumplimiento', 'Satisfecha');
  await olga.getByRole('button', { name: 'Registrar estudio' }).click();
  const dialogo = olga.getByRole('dialog', { name: /Estudio de cumplimiento/ });
  await dialogo.getByRole('button', { name: '1 · Bajo' }).click();
  await dialogo.getByRole('combobox', { name: 'Concepto' }).click();
  await olga.getByRole('option', { name: 'Favorable', exact: true }).click();
  await dialogo.getByRole('button', { name: 'Registrar estudio' }).click();
  await expect(dialogo).toBeHidden();
  const estudio = olga.getByRole('region', { name: 'Estudio de cumplimiento' });
  await expect(estudio).toContainText('Satisfecha');
  await expect(estudio).toContainText('1 · Bajo');

  // Pasan dos días: el estudio venció con el expediente abierto. El barrido lo expira y lo atiende como lo haría el procesador.
  const resultado = await revisarVerificaciones(e, desdeAhora(2));
  expect(resultado.expiraciones).toBeGreaterThanOrEqual(1);

  // Cumplimiento lo ve vencido en el expediente, con qué hacer, y le llegó el aviso a su campana.
  await abrirDesdeBandeja(olga, nombre, 'Cumplimiento');
  await expect(olga.getByRole('region', { name: 'Estudio de cumplimiento' })).toContainText('Vencida');
  await expect(olga.getByRole('region', { name: 'Estudio de cumplimiento' })).toContainText('El estudio venció: hay que volver a hacerlo antes de aprobar.');
  await expect(olga.getByRole('button', { name: 'Registrar un estudio nuevo' })).toBeVisible();
  await capturar(olga.owner().page(), 'cumplimiento-estudio-vencido');
  await olga.getByRole('button', { name: /^Notificaciones, \d+ sin leer/ }).click();
  await expect(olga.getByText('Verificación vencida').first()).toBeVisible();
  await expect(olga.getByText(`Venció el estudio de cumplimiento de ${nombre}: hay que volver a hacerlo antes de cerrar el expediente.`)).toBeVisible();
  await olga.owner().page().keyboard.press('Escape');

  // Su bandeja de cumplimiento vuelve a mostrarlo con el estudio vencido.
  await irASeccion(olga, 'Cumplimiento');
  const fila = olga.getByRole('row').filter({ hasText: nombre });
  await expect(fila).toContainText('Estudio de cumplimiento');
  await expect(fila).toContainText('Vencida');

  // Compras y Cumplimiento aprueban sus áreas, pero el expediente no se cierra con un estudio vencido.
  const ruben = await personaEmbebida(browser, e, e.ruben);
  await abrirDesdeBandeja(ruben, nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');
  await abrirDesdeBandeja(olga, nombre, 'Cumplimiento');
  await aprobarUltimaArea(olga, 'Cumplimiento', /Estudio de cumplimiento \(vencid[ao]\)/);
  await esperarEstadoDelTercero(page, 'EnRevision');

  // Con un estudio nuevo, vigente, sí se aprueba.
  await olga.getByRole('button', { name: 'Registrar un estudio nuevo' }).click();
  const nuevo = olga.getByRole('dialog', { name: /Estudio de cumplimiento/ });
  await nuevo.getByRole('button', { name: '1 · Bajo' }).click();
  await nuevo.getByRole('combobox', { name: 'Concepto' }).click();
  await olga.getByRole('option', { name: 'Favorable', exact: true }).click();
  await nuevo.getByRole('button', { name: 'Registrar estudio' }).click();
  await expect(nuevo).toBeHidden();
  await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
  await esperarEstadoDelTercero(page, 'Aprobado');
});

test('el monitoreo periódico de un vínculo activo reconsulta solo y, cuando el tercero suma una persona en listas, lleva la novedad al oficial', async ({ page, browser }) => {
  test.setTimeout(420_000);
  const e = await crearEscenario();
  await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
  const nombre = 'Cementos Vigilados S.A.S.';
  const invitacion = await enRevision(page, e, nombre);

  // El tercero se aprueba sin coincidencias: el vínculo queda activo y en monitoreo.
  const ruben = await personaEmbebida(browser, e, e.ruben);
  await abrirDesdeBandeja(ruben, nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');
  const olga = await personaEmbebida(browser, e, e.olga);
  await esperarListas(olga, nombre, 'Cumplimiento', 'Satisfecha');
  await aprobarArea(olga, 'Cumplimiento');
  await esperarEstadoDelTercero(page, 'Aprobado');

  // En el vínculo, Cumplimiento ve el monitoreo: satisfecho y con su próxima consulta a los 30 días.
  const paginaVinculo = await paginaDe(browser);
  const vinculo = await abrirEmbebido(paginaVinculo, e, e.olga, e.norte, `vinculos/${invitacion.vinculoId}`);
  const monitoreo = vinculo.getByRole('region', { name: 'Monitoreo de listas' });
  await expect(monitoreo).toBeVisible();
  await expect(monitoreo).toContainText('Satisfecha');
  await expect(monitoreo).toContainText('El vínculo activo se reconsulta cada 30 días');
  await expect(monitoreo).toContainText(enPantalla(30));
  await expect(monitoreo).toContainText('Ninguna persona aparece en las listas consultadas.');
  await capturar(paginaVinculo, 'cumplimiento-monitoreo-vinculo');

  // Pasan 31 días: el procesador reconsulta el vínculo. Sin personas nuevas no hay novedad: la próxima consulta se corre otros 30 días.
  const resultado = await revisarVerificaciones(e, desdeAhora(31));
  expect(resultado.monitoreos).toBe(1);
  await paginaVinculo.reload();
  await expect(monitoreo).toContainText(enPantalla(31));
  await expect(monitoreo).toContainText('Sistema');
  await expect(monitoreo).toContainText(enPantalla(61));
  await expect(monitoreo).toContainText('Ninguna persona aparece en las listas consultadas.');
  await irASeccion(olga, 'Cumplimiento');
  await expect(olga.getByText('No hay nada por atender', { exact: true })).toBeVisible();

  // El tercero suma un socio a su información legal; como su nombre está en OFAC, el monitoreo lo encuentra.
  await page.goto('/ficha/legal');
  await expect(page.getByRole('heading', { level: 1, name: 'Información legal' })).toBeVisible();
  await page.getByRole('button', { name: 'Agregar persona' }).click();
  const persona = page.getByRole('dialog', { name: 'Agregar persona' });
  await persona.getByRole('combobox', { name: 'Rol' }).click();
  await page.getByRole('option', { name: 'Socio o accionista' }).click();
  await persona.getByRole('combobox', { name: 'Tipo de identificación' }).click();
  await page.getByRole('option', { name: 'CC', exact: true }).click();
  await persona.getByRole('textbox', { name: 'Número de identificación' }).fill('1012345678');
  await persona.getByRole('textbox', { name: 'Nombres' }).fill('Roberto');
  await persona.getByRole('textbox', { name: 'Apellidos' }).fill('Coincidencia Prueba');
  await persona.getByRole('button', { name: 'Listo' }).click();
  await page.getByRole('group', { name: 'Guardar la sección' }).getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('listitem', { name: 'Roberto Coincidencia Prueba' })).toBeVisible();

  // La novedad llega a la bandeja del oficial como «Monitoreo del vínculo», con su campana.
  await expect(async () => {
    await irASeccion(olga, 'Vínculos');
    await irASeccion(olga, 'Cumplimiento');
    await expect(olga.getByRole('row').filter({ hasText: nombre })).toContainText('Monitoreo del vínculo', { timeout: 1500 });
  }).toPass({ timeout: 60_000 });
  const fila = olga.getByRole('row').filter({ hasText: nombre });
  await expect(fila).toContainText('Coincidencia por analizar');
  await expect(fila.getByRole('cell', { name: '1', exact: true })).toBeVisible();
  await olga.getByRole('button', { name: /^Notificaciones, \d+ sin leer/ }).click();
  await expect(olga.getByText('Coincidencia en el monitoreo de listas').first()).toBeVisible();
  await olga.owner().page().keyboard.press('Escape');

  // «Abrir» lleva al vínculo, donde Olga ve la coincidencia y la confirma: el vínculo sigue activo hasta que un administrador decida.
  await fila.getByRole('link', { name: 'Abrir' }).click();
  await expect(olga.getByRole('heading', { level: 1, name: nombre })).toBeVisible();
  const novedad = olga.getByRole('region', { name: 'Monitoreo de listas' });
  const coincidencia = novedad.getByRole('listitem', { name: 'Roberto Coincidencia Prueba · OFAC (SDN)' });
  await expect(coincidencia).toContainText('Socio · CC 1012345678');
  await expect(novedad).toContainText('Coincidencia por analizar');
  await capturar(olga.owner().page(), 'cumplimiento-monitoreo-novedad');
  await coincidencia.getByRole('button', { name: 'Confirmar' }).click();
  const confirmar = olga.getByRole('dialog', { name: 'Confirmar coincidencia' });
  await confirmar.getByRole('textbox', { name: /Justificación/ }).fill('Es la misma persona del listado: sin duda.');
  await confirmar.getByRole('button', { name: 'Guardar decisión' }).click();
  await expect(confirmar).toBeHidden();
  await expect(coincidencia).toContainText('Confirmada');
  await expect(novedad).toContainText('el vínculo sigue activo hasta que un administrador decida si lo bloquea o lo desvincula');
  await expect(olga.getByText(/^Activo$/).first()).toBeVisible();

  // El administrador decide: bloquea el vínculo con su motivo.
  const ana = await abrirEmbebido(await paginaDe(browser), e, e.ana, e.norte, `vinculos/${invitacion.vinculoId}`);
  await ana.getByRole('button', { name: 'Más acciones' }).click();
  await ana.getByRole('menuitem', { name: /^Bloquear/ }).click();
  const bloquear = ana.getByRole('dialog', { name: /^Bloquear vínculo/ });
  await bloquear.getByRole('textbox', { name: 'Motivo' }).fill('Coincidencia confirmada en el monitoreo de listas.');
  await bloquear.getByRole('button', { name: 'Bloquear vínculo' }).click();
  await expect(bloquear).toBeHidden();
  await expect(ana.getByText('Coincidencia confirmada en el monitoreo de listas.').first()).toBeVisible();
});
