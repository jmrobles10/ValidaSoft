import { expect, test, type Locator, type Page } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { llamar } from '../soporte/api';
import { capturar } from '../soporte/capturas';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja } from '../soporte/empresa';
import { diligenciarDeclaracion, pngDeFirma, TITULO_DECLARACION } from '../soporte/declaracion-pep';
import { abrirSeccion, guardarSeccionCompleta, llenarContactoYDomicilio, publicarFlujoDeFicha, REPRESENTANTE } from '../soporte/ficha';
import { escribirFecha } from '../soporte/fechas';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevaPersonaNatural, nuevoTercero, type Empresa, type Escenario, type Invitacion } from '../soporte/semilla';

/**
 * Lo que quedó fuera de la primera entrega de la declaración jurada PEP (DP-16, DP-18): el tercero baja el PDF de su propia declaración
 * desde «Mi información» (la general y cada persona de la legal, solo con «Sí» y la declaración ya guardada) y el PDF de la empresa lleva
 * su logo (PNG o SVG) o, si no tiene o no se puede dibujar, el de Validasoft.
 */

test.setTimeout(240_000);

const PREGUNTA_NATURAL = '¿Eres una persona expuesta políticamente (PEP)?';
const PREGUNTA_PERSONA = '¿Es persona expuesta políticamente (PEP)?';

async function escenarioDeFicha(seccion: 'FICHA_GENERAL' | 'FICHA_LEGAL'): Promise<Escenario> {
  const e = await crearEscenario();
  await publicarFlujoDeFicha(e, e.sur, `PEP_PDF_${seccion}`, e.sur.areas.revision!, 'CLIENTE', [{ seccion }]);
  return e;
}

async function aceptar(page: Page, invitacion: Invitacion): Promise<void> {
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
}

/** Configura el logo de la empresa por la API (lo que hace la pantalla «Datos y marca»): `logo` vacío lo quita. */
async function fijarLogo(e: Escenario, empresa: Empresa, logo: string): Promise<void> {
  await llamar('PUT', `/api/v1/empresas/${empresa.id}`, { llave: e.admin, empresa: empresa.id, cuerpo: { logoUrl: logo } });
}

/** Baja lo que descarga el botón y lo devuelve como texto Latin-1 (los bytes del PDF), con el nombre que propuso el servidor. */
async function descargar(page: Page, boton: Locator, nombre: string): Promise<{ nombre: string; pdf: string }> {
  const descarga = page.waitForEvent('download');
  await boton.click();
  const archivo = await descarga;
  // Con E2E_CAPTURAS la copia queda junto a las capturas (para verla como se descargó).
  if (process.env.E2E_CAPTURAS) await archivo.saveAs(`${process.env.E2E_CAPTURAS}/${nombre}.pdf`);
  return { nombre: archivo.suggestedFilename(), pdf: (await readFile(await archivo.path())).toString('latin1') };
}

test('persona natural: el botón «Descargar PDF» aparece solo con la declaración guardada, el PDF del tercero lleva el logo de Validasoft y el de la empresa, el suyo', async ({ page, browser }) => {
  const e = await escenarioDeFicha('FICHA_GENERAL');
  const tercero = nuevaPersonaNatural('Ana María Pérez Gómez');
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  await aceptar(page, invitacion);

  await abrirSeccion(page, 'general', 'Información general');
  await llenarContactoYDomicilio(page, '300 555 1234', 'Carrera 7 # 32-16');
  await escribirFecha(page, 'Fecha de expedición de tu documento', '2012-03-20');
  await page.getByRole('textbox', { name: 'Lugar de expedición', exact: true }).fill('Bogotá');
  await page.getByRole('radiogroup', { name: '¿Tienes vínculo con una persona expuesta políticamente?' }).getByRole('radio', { name: 'No' }).check();
  const botonDelTercero = page.getByRole('button', { name: 'Descargar el PDF de tu declaración PEP' });

  // Declarada pero sin guardar, el PDF diría otra cosa que la pantalla: no hay botón hasta que se guarde.
  await page.getByRole('radiogroup', { name: PREGUNTA_NATURAL }).getByRole('radio', { name: 'Sí' }).check();
  await diligenciarDeclaracion(page, { firma: 'dibujar', completa: true });
  await expect(page.getByText('Declaración lista: se guarda con la sección')).toBeVisible();
  await expect(botonDelTercero).toHaveCount(0);

  await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
  await expect(page.getByText(/^Declaración firmada el \d{2}\/\d{2}\/\d{4}$/)).toBeVisible();
  await expect(botonDelTercero).toBeVisible();
  await capturar(page, 'declaracion-pep-descarga-tercero');

  // Un cambio sin guardar la esconde otra vez (el PDF sale de lo guardado) y al guardar vuelve.
  const telefono = page.getByRole('textbox', { name: 'Teléfono', exact: true });
  await telefono.fill('300 555 9999');
  await expect(botonDelTercero).toHaveCount(0);
  await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
  await expect(botonDelTercero).toBeVisible();

  // El tercero descarga su propia declaración: a nombre de Validasoft y con su logo; la firma, el cargo y la dirección de lo guardado.
  const propio = await descargar(page, botonDelTercero, 'declaracion-pep-del-tercero');
  expect(propio.nombre).toMatch(/^declaracion-pep-ana-maria-perez-gomez-[a-z0-9]+-\d{8}\.pdf$/);
  expect(propio.pdf.startsWith('%PDF-1.7')).toBe(true);
  expect(propio.pdf).toContain('(Validasoft)');
  expect(propio.pdf).toContain('/ShadingType 2'); // el isotipo con su degradado
  expect(propio.pdf).toContain('/Subtype /Image'); // la firma
  expect(propio.pdf).toContain('(Alcalde municipal)');
  expect(propio.pdf).toContain('Carrera 7 # 32-16');
  expect(propio.pdf).toContain('Luis Ruiz');
  expect(propio.pdf).not.toContain('/Width 360 /Height 120');

  // La empresa, con un logo PNG configurado, recibe el suyo en el PDF (y ya no el de Validasoft).
  await fijarLogo(e, e.sur, `data:image/png;base64,${pngDeFirma(360, 120).buffer.toString('base64')}`);
  await esperarEstadoDelTercero(page, 'EnRevision').catch(() => undefined);
  const paginaSofia = await (await browser.newContext({ acceptDownloads: true })).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('region', { name: TITULO_DECLARACION })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 60_000 });
  const deLaEmpresa = await descargar(paginaSofia, sofia.getByRole('button', { name: `Descargar el PDF de la declaración PEP de ${tercero.nombre}` }), 'declaracion-pep-de-la-empresa-logo-png');
  expect(deLaEmpresa.nombre).toMatch(/^declaracion-pep-ana-maria-perez-gomez-[a-z0-9]+-\d{8}\.pdf$/);
  expect(deLaEmpresa.pdf).toContain('/Width 360 /Height 120'); // el logo PNG de la empresa
  expect(deLaEmpresa.pdf).not.toContain('/ShadingType');
  expect(deLaEmpresa.pdf).toContain('(Alcalde municipal)');
});

test('persona jurídica: cada persona PEP con declaración guardada tiene su botón en la tarjeta y el PDF de la empresa lleva su logo SVG', async ({ page, browser }) => {
  const e = await escenarioDeFicha('FICHA_LEGAL');
  const tercero = nuevoTercero('Ferretería Andina S.A.S.');
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  await aceptar(page, invitacion);
  await abrirSeccion(page, 'legal', 'Información legal');

  const agregar = async (rol: string, numero: string, nombres: string, apellidos: string, porcentaje?: string) => {
    await page.getByRole('button', { name: 'Agregar persona' }).click();
    const persona = page.getByRole('dialog', { name: 'Agregar persona' });
    await persona.getByRole('combobox', { name: 'Rol' }).click();
    await page.getByRole('option', { name: rol, exact: true }).click();
    await persona.getByRole('combobox', { name: 'Tipo de identificación' }).click();
    await page.getByRole('option', { name: 'CC', exact: true }).click();
    await persona.getByRole('textbox', { name: 'Número de identificación' }).fill(numero);
    await persona.getByRole('textbox', { name: 'Nombres', exact: true }).fill(nombres);
    await persona.getByRole('textbox', { name: 'Apellidos', exact: true }).fill(apellidos);
    if (porcentaje) await persona.getByRole('textbox', { name: 'Participación (%)' }).fill(porcentaje);
    await persona.getByRole('radiogroup', { name: '¿Tiene vínculo con una persona expuesta políticamente?' }).getByRole('radio', { name: 'No' }).check();
    return persona;
  };

  const representante = await agregar(REPRESENTANTE.rol, REPRESENTANTE.numero, REPRESENTANTE.nombres, REPRESENTANTE.apellidos);
  await representante.getByRole('radiogroup', { name: PREGUNTA_PERSONA }).getByRole('radio', { name: 'No' }).check();
  await representante.getByRole('button', { name: 'Listo' }).click();
  const socio = await agregar('Socio o accionista', '79555111', 'Jorge Iván', 'Castro Pinto', '100');
  await socio.getByRole('radiogroup', { name: PREGUNTA_PERSONA }).getByRole('radio', { name: 'Sí' }).check();
  await diligenciarDeclaracion(page, { firma: 'subir', direccion: 'Calle 80 # 12-34', firmante: { nombre: 'Marta Lucía Ríos Duarte', tipo: 'CC', numero: '1020304050' } });
  await socio.getByRole('button', { name: 'Listo' }).click();

  const lista = page.getByRole('list', { name: 'Personas relacionadas' });
  const tarjetaDelSocio = lista.getByRole('listitem', { name: 'Jorge Iván Castro Pinto' });
  const botonDelSocio = tarjetaDelSocio.getByRole('button', { name: 'Descargar el PDF de la declaración PEP de Jorge Iván Castro Pinto' });
  await expect(botonDelSocio).toHaveCount(0); // sin guardar, no

  await guardarSeccionCompleta(page, 'Guardaste Información legal: está completa.');
  await expect(botonDelSocio).toBeVisible();
  // La persona que respondió «No» no tiene declaración que bajar.
  await expect(lista.getByRole('listitem', { name: 'Marta Lucía Ríos Duarte' }).getByRole('button', { name: /^Descargar el PDF/ })).toHaveCount(0);
  await capturar(page, 'declaracion-pep-descarga-tarjeta');

  const propio = await descargar(page, botonDelSocio, 'declaracion-pep-persona-del-tercero');
  expect(propio.nombre).toMatch(/^declaracion-pep-jorge-ivan-castro-pinto-79555111-\d{8}\.pdf$/);
  expect(propio.pdf).toContain('(Validasoft)');
  expect(propio.pdf).toContain('Calle 80 # 12-34');
  expect(propio.pdf).toContain('(Representante legal)');
  expect(propio.pdf).toContain('Socio o accionista de Ferreter');

  // El PDF de la empresa con un logo SVG: vectores con el color del logo, sin imagen nueva ni el degradado de Validasoft.
  const svg = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 40"><rect width="120" height="40" rx="8" fill="#E07A1F"/><circle cx="20" cy="20" r="11" fill="#FFFFFF"/><path d="M42 12h22l-11 18z" fill="#FFFFFF"/></svg>';
  await fijarLogo(e, e.sur, `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`);
  const paginaSofia = await (await browser.newContext({ acceptDownloads: true })).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('region', { name: TITULO_DECLARACION })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 60_000 });
  const deLaEmpresa = await descargar(paginaSofia, sofia.getByRole('button', { name: 'Descargar el PDF de la declaración PEP de Jorge Iván Castro Pinto' }), 'declaracion-pep-de-la-empresa-logo-svg');
  expect(deLaEmpresa.pdf).toContain('0.878431 0.478431 0.121569 rg'); // #E07A1F
  expect(deLaEmpresa.pdf).not.toContain('/ShadingType');
  expect(deLaEmpresa.pdf).toContain('Calle 80 # 12-34');
});

test('persona natural: firma por apoderado con dos PEP relacionadas y el PDF de la empresa las lleva con su tipo, relación y quién firmó', async ({ page, browser }) => {
  const e = await escenarioDeFicha('FICHA_GENERAL');
  const tercero = nuevaPersonaNatural('Ana María Pérez Gómez');
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  await aceptar(page, invitacion);

  await abrirSeccion(page, 'general', 'Información general');
  await llenarContactoYDomicilio(page, '300 555 1234', 'Carrera 7 # 32-16');
  await escribirFecha(page, 'Fecha de expedición de tu documento', '2012-03-20');
  await page.getByRole('textbox', { name: 'Lugar de expedición', exact: true }).fill('Bogotá');
  await page.getByRole('radiogroup', { name: '¿Tienes vínculo con una persona expuesta políticamente?' }).getByRole('radio', { name: 'No' }).check();
  await page.getByRole('radiogroup', { name: PREGUNTA_NATURAL }).getByRole('radio', { name: 'Sí' }).check();

  // La persona natural firma a nombre propio o por apoderado: no hay «Representante legal».
  const dialogo = page.getByRole('dialog', { name: TITULO_DECLARACION });
  await dialogo.getByRole('combobox', { name: 'Firma en calidad de' }).click();
  await expect(page.getByRole('option')).toHaveText(['Nombre propio', 'Apoderado']);
  await page.keyboard.press('Escape');
  await diligenciarDeclaracion(page, { firma: 'dibujar', pepRelacionadas: true, firmante: { nombre: 'Pedro Apoderado Gil', tipo: 'CC', numero: '80123456', calidad: 'Apoderado' } });
  await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');

  const propio = await descargar(page, page.getByRole('button', { name: 'Descargar el PDF de tu declaración PEP' }), 'declaracion-pep-relacionadas-del-tercero');
  expect(propio.pdf).toContain('(Carlos Ruiz)');
  expect(propio.pdf).toContain('(Ana Mora)');
  expect(propio.pdf).toContain('Asociado cercano: Socio');
  expect(propio.pdf).toContain('(Apoderado)');
  expect(propio.pdf).toContain('Pedro Apoderado Gil');

  // La empresa ve las dos PEP y quién firmó.
  await esperarEstadoDelTercero(page, 'EnRevision').catch(() => undefined);
  const paginaSofia = await (await browser.newContext({ acceptDownloads: true })).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('region', { name: TITULO_DECLARACION })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 60_000 });
  const bloque = sofia.getByRole('region', { name: TITULO_DECLARACION });
  await expect(bloque).toContainText('Familiar: Hermano(a) · 2.° consanguinidad · Carlos Ruiz');
  await expect(bloque).toContainText('Asociado cercano: Socio(a) o asociado(a) comercial · Ana Mora');
  await expect(bloque).toContainText('Por apoderado: Pedro Apoderado Gil (CC 80123456)');
});
