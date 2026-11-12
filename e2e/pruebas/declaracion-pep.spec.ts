import { expect, test, type FrameLocator, type Page } from '@playwright/test';
import { API } from '../soporte/api';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja } from '../soporte/empresa';
import { capturarBloque, capturarDialogo, dialogoDeclaracion, diligenciarDeclaracion, guardarAutorizacion, TITULO_DECLARACION } from '../soporte/declaracion-pep';
import { abrirSeccion, guardarSeccionCompleta, llenarContactoYDomicilio, publicarFlujoDeFicha, REPRESENTANTE } from '../soporte/ficha';
import { escribirFecha } from '../soporte/fechas';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero } from '../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevaPersonaNatural, nuevoTercero, type Escenario, type Invitacion } from '../soporte/semilla';

/**
 * Declaración jurada PEP (HU 49093) de punta a punta: «Sí» a «¿Es PEP?» abre el diálogo, el tercero no puede guardar sin declarar (ni el
 * servidor lo deja), firma dibujando o subiendo una imagen, la declaración se guarda con la sección y la empresa la ve y descarga su PDF.
 * Una persona natural y una jurídica con un socio PEP que firma como representante legal.
 */

test.setTimeout(240_000);

const PREGUNTA_NATURAL = '¿Eres una persona expuesta políticamente (PEP)?';
const PREGUNTA_PERSONA = '¿Es persona expuesta políticamente (PEP)?';

async function escenarioDeFicha(seccion: 'FICHA_GENERAL' | 'FICHA_LEGAL'): Promise<Escenario> {
  const e = await crearEscenario();
  await publicarFlujoDeFicha(e, e.sur, `PEP_${seccion}`, e.sur.areas.revision!, 'CLIENTE', [{ seccion }]);
  return e;
}

async function aceptar(page: Page, invitacion: Invitacion): Promise<void> {
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
}

/** La empresa abre el expediente del tercero y descarga el PDF de la declaración de la persona; devuelve su contenido en Latin-1. */
async function descargarPdfComoEmpresa(page: Page, app: FrameLocator, nombrePersona: string): Promise<string> {
  const boton = app.getByRole('button', { name: `Descargar el PDF de la declaración PEP de ${nombrePersona}` });
  await expect(boton).toBeVisible();
  const descarga = page.waitForEvent('download');
  await boton.click();
  const archivo = await descarga;
  const ruta = await archivo.path();
  // Con E2E_CAPTURAS la copia queda junto a las capturas (para verla como la ve la empresa).
  if (process.env.E2E_CAPTURAS) await archivo.saveAs(`${process.env.E2E_CAPTURAS}/${archivo.suggestedFilename()}`);
  const { readFile } = await import('node:fs/promises');
  expect(archivo.suggestedFilename()).toMatch(/^declaracion-pep-.*\.pdf$/);
  return (await readFile(ruta)).toString('latin1');
}

test('persona natural: «Sí» a PEP abre la declaración, no se guarda sin ella, firma dibujando, queda sellada y la empresa descarga su PDF', async ({ page, browser }) => {
  const e = await escenarioDeFicha('FICHA_GENERAL');
  const tercero = nuevaPersonaNatural('Ana María Pérez Gómez');
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  const sesion = guardarAutorizacion(page);
  await aceptar(page, invitacion);

  await abrirSeccion(page, 'general', 'Información general');
  await llenarContactoYDomicilio(page, '300 555 1234', 'Carrera 7 # 32-16');
  await escribirFecha(page, 'Fecha de expedición de tu documento', '2012-03-20');
  await page.getByRole('textbox', { name: 'Lugar de expedición', exact: true }).fill('Bogotá');
  await page.getByRole('radiogroup', { name: '¿Tienes vínculo con una persona expuesta políticamente?' }).getByRole('radio', { name: 'No' }).check();
  const pregunta = page.getByRole('radiogroup', { name: PREGUNTA_NATURAL });

  // «Sí» abre la declaración, con los datos básicos de solo lectura (de lo que ya diligenció): nombre, documento, relación y dirección.
  await pregunta.getByRole('radio', { name: 'Sí' }).check();
  const dialogo = dialogoDeclaracion(page);
  await expect(dialogo).toBeVisible();
  await expect(dialogo.getByRole('textbox', { name: 'Nombre completo o razón social' })).toHaveValue('Ana María Pérez Gómez');
  await expect(dialogo.getByRole('textbox', { name: 'Relación con la empresa' })).toHaveValue('Proveedor');
  await expect(dialogo.getByRole('textbox', { name: 'Dirección', exact: true })).toHaveValue('Carrera 7 # 32-16');
  await capturarDialogo(page, dialogo, 'declaracion-pep-dialogo');

  // Cerrar sin declarar pregunta antes; «Seguir diligenciando» la deja abierta y «Salir sin declarar» devuelve la respuesta a «No».
  await dialogo.getByRole('button', { name: 'Cerrar' }).click();
  const salida = page.getByRole('dialog', { name: 'Declaración incompleta' });
  await expect(salida).toBeVisible();
  await salida.getByRole('button', { name: 'Seguir diligenciando' }).click();
  await expect(dialogo).toBeVisible();
  await dialogo.getByRole('button', { name: 'Cerrar' }).click();
  await page.getByRole('dialog', { name: 'Declaración incompleta' }).getByRole('button', { name: 'Salir sin declarar' }).click();
  await expect(dialogo).toBeHidden();
  await expect(pregunta.getByRole('radio', { name: 'No' })).toBeChecked();

  // El servidor tampoco deja guardar un «Sí» sin declaración (vista previa y guardado), aunque se salte la pantalla.
  const general = {
    nombres: 'Ana María',
    apellidos: 'Pérez Gómez',
    correo: tercero.correo,
    telefono: '300 555 1234',
    pais: 'CO',
    direccion: 'Carrera 7 # 32-16',
    pep: true,
    vinculo_pep: false,
  };
  const encabezados = { authorization: sesion.valor(), 'content-type': 'application/json' };
  for (const ruta of ['PUT', 'POST'] as const) {
    const respuesta = await page.request.fetch(`/public/v1/ficha/FICHA_GENERAL${ruta === 'POST' ? '/evaluacion' : ''}`, { method: ruta, headers: encabezados, data: { datos: general } });
    expect(respuesta.status()).toBe(422);
    expect(await respuesta.json()).toMatchObject({ codigo: 'declaracion-pep-requerida', campo: 'declaracion_pep' });
  }

  // «Sí» otra vez: el cónyuge es todo o nada y se avisa al confirmar.
  await pregunta.getByRole('radio', { name: 'Sí' }).check();
  await dialogo.getByRole('textbox', { name: 'Cargo desempeñado' }).fill('Alcalde municipal');
  await dialogo.getByRole('textbox', { name: 'Nombre del cónyuge' }).fill('Luis Ruiz');
  await dialogo.getByRole('button', { name: 'Confirmar declaración' }).click();
  await expect(dialogo.getByText('Completa los tres datos del cónyuge o déjalos vacíos').first()).toBeVisible();
  await expect(dialogo.getByText('Revisa los datos marcados antes de confirmar.')).toBeVisible();
  await capturarDialogo(page, dialogo, 'declaracion-pep-errores');
  await dialogo.getByRole('textbox', { name: 'Nombre del cónyuge' }).clear();

  // La declaración completa, con la firma dibujada con el mouse.
  await diligenciarDeclaracion(page, { firma: 'dibujar', completa: true });
  await expect(page.getByText('Declaración lista: se guarda con la sección')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Ver o editar declaración' })).toBeVisible();

  // Se guarda con la sección: queda firmada con la fecha de hoy y, al volver, el diálogo abre precargado.
  await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
  await expect(page.getByText(/^Declaración firmada el \d{2}\/\d{2}\/\d{4}$/)).toBeVisible();
  await page.reload();
  await expect(pregunta.getByRole('radio', { name: 'Sí' })).toBeChecked();
  await page.getByRole('button', { name: 'Ver o editar declaración' }).click();
  await expect(dialogo.getByRole('textbox', { name: 'Cargo desempeñado' })).toHaveValue('Alcalde municipal');
  await expect(dialogo.getByRole('radiogroup', { name: '¿Tiene poder de firma u otro derecho sobre cuentas financieras en el exterior?' }).getByRole('radio', { name: 'Sí' })).toBeChecked();
  await expect(dialogo.getByRole('img', { name: 'Vista previa de la firma' })).toBeVisible();
  await dialogo.getByRole('button', { name: 'Cerrar' }).click();
  await expect(dialogo).toBeHidden();

  // «No» conserva la declaración inactiva: al volver a «Sí» el diálogo abre precargado y no hay que escribirla otra vez.
  await pregunta.getByRole('radio', { name: 'No' }).check();
  await expect(page.getByText(/^Declaración firmada el/)).toHaveCount(0);
  await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
  await pregunta.getByRole('radio', { name: 'Sí' }).check();
  await expect(dialogo.getByRole('textbox', { name: 'Cargo desempeñado' })).toHaveValue('Alcalde municipal');
  await dialogo.getByRole('checkbox', { name: /^Declaro bajo juramento/ }).check();
  await dialogo.getByRole('button', { name: 'Confirmar declaración' }).click();
  await expect(dialogo).toBeHidden();
  await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');

  // La empresa: el expediente pasa solo a revisión; el área ve el bloque «Declaración jurada PEP» y descarga el PDF firmado.
  await esperarEstadoDelTercero(page, 'EnRevision').catch(() => undefined);
  const paginaSofia = await (await browser.newContext({ acceptDownloads: true })).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('region', { name: TITULO_DECLARACION })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 60_000 });
  const bloque = sofia.getByRole('region', { name: TITULO_DECLARACION });
  await expect(bloque).toContainText('Alcalde municipal');
  await expect(bloque).toContainText('Alcaldía de Sopó');
  await expect(bloque).toContainText('Luis Ruiz');
  await expect(bloque.getByText(/^Firmada el \d{2}\/\d{2}\/\d{4}$/)).toBeVisible();
  await capturarBloque(bloque, 'declaracion-pep-empresa');

  const pdf = await descargarPdfComoEmpresa(paginaSofia, sofia, tercero.nombre);
  expect(pdf.startsWith('%PDF-1.7')).toBe(true);
  expect(pdf).toContain('/Subtype /Image');
  expect(pdf).toContain('(Alcalde municipal)');
  expect(pdf).toContain('(Inversiones Sop');
  expect(pdf).toContain('Declaraci\\363n jurada de persona expuesta pol');
});

test('persona jurídica: un socio PEP declara en su fila de la información legal, firma como representante legal con una imagen y la empresa la ve y la lleva en el ZIP del expediente', async ({
  page,
  browser,
}) => {
  const e = await escenarioDeFicha('FICHA_LEGAL');
  const tercero = nuevoTercero('Ferretería Andina S.A.S.');
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  await aceptar(page, invitacion);
  await abrirSeccion(page, 'legal', 'Información legal');

  // El representante legal (PEP «No») y el socio con el 100 %, que responde «Sí».
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
  const dialogo = dialogoDeclaracion(page);
  await expect(dialogo).toBeVisible();
  // En la persona, la relación sale de su rol y la dirección la escribe quien declara.
  await expect(dialogo.getByRole('textbox', { name: 'Nombre completo o razón social' })).toHaveValue('Jorge Iván Castro Pinto');
  await expect(dialogo.getByRole('textbox', { name: 'Relación con la empresa' })).toHaveValue('Socio');
  await capturarDialogo(page, dialogo, 'declaracion-pep-persona');

  // Firma como representante legal: pide el nombre y el documento del firmante; la firma es una imagen subida.
  await diligenciarDeclaracion(page, {
    firma: 'subir',
    direccion: 'Calle 80 # 12-34',
    firmante: { nombre: 'Marta Lucía Ríos Duarte', tipo: 'CC', numero: '1020304050' },
  });
  await expect(socio.getByText('Declaración lista: se guarda con la sección')).toBeVisible();
  await socio.getByRole('button', { name: 'Listo' }).click();
  const lista = page.getByRole('list', { name: 'Personas relacionadas' });
  await expect(lista.getByRole('listitem', { name: 'Jorge Iván Castro Pinto' })).toBeVisible();
  await expect(lista.getByRole('listitem', { name: 'Jorge Iván Castro Pinto' }).getByText('Falta la declaración PEP')).toHaveCount(0);

  await guardarSeccionCompleta(page, 'Guardaste Información legal: está completa.');

  // La empresa ve la declaración del socio (y solo la de quien responde «Sí») y descarga su PDF.
  const paginaSofia = await (await browser.newContext({ acceptDownloads: true })).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('region', { name: TITULO_DECLARACION })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 60_000 });
  await expect(sofia.getByRole('region', { name: TITULO_DECLARACION })).toHaveCount(1);
  await expect(sofia.getByRole('region', { name: TITULO_DECLARACION })).toContainText('Marta Lucía Ríos Duarte');
  const pdf = await descargarPdfComoEmpresa(paginaSofia, sofia, 'Jorge Iván Castro Pinto');
  expect(pdf).toContain('/Subtype /Image');
  expect(pdf).toContain('Calle 80 # 12-34');
  expect(pdf).toContain('(Representante legal)');
  expect(pdf).toContain('Jorge Iv');
  await capturarBloque(sofia.getByRole('region', { name: TITULO_DECLARACION }), 'declaracion-pep-empresa-persona');

  // El ZIP del expediente lleva un PDF por persona PEP con declaración.
  const expedienteId = await expedienteAbierto(e, invitacion).catch(() => null);
  expect(expedienteId).toBeTruthy();
  const zip = await fetch(`${API}/api/v1/expedientes/${expedienteId}/exportar`, { headers: { 'X-Api-Key': e.admin, 'X-Empresa': e.sur.id } });
  expect(zip.status).toBe(200);
  expect(Buffer.from(await zip.arrayBuffer()).toString('latin1')).toContain('declaraciones-pep/declaracion-pep-jorge-ivan-castro-pinto-79555111-');
});
