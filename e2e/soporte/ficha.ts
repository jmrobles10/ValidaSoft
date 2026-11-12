import { expect, type FrameLocator, type Page } from '@playwright/test';
import { llamar } from './api';
import { irASeccion } from './empresa';
import { escribirFecha } from './fechas';
import type { Area, Empresa, Escenario } from './semilla';

/**
 * Wizard «Mi información» del tercero (apps/public) para las pruebas de punta a punta: cada función llena una sección con lo mínimo
 * que la completa, tal como lo haría la persona, y la guarda. Los textos son los de apps/public/src/i18n (es-CO).
 */

const FRASE_LISTA = 'Lista: puedes continuar';

/** Guarda la sección abierta (botón «Guardar») y espera el aviso de que quedó completa. */
export async function guardarSeccionCompleta(page: Page, aviso: string | RegExp): Promise<void> {
  const barra = page.getByRole('group', { name: 'Guardar la sección' });
  await expect(barra.getByText(FRASE_LISTA)).toBeVisible();
  await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByText(aviso)).toBeVisible();
}

/** Abre una sección del wizard por su ruta y espera su título. */
export async function abrirSeccion(page: Page, ruta: string, titulo: string): Promise<void> {
  await page.goto(`/ficha/${ruta}`);
  await expect(page.getByRole('heading', { level: 1, name: titulo })).toBeVisible();
}

async function elegirOpcion(page: Page, combo: string, escribir: string, opcion: RegExp | string): Promise<void> {
  await page.getByRole('combobox', { name: combo, exact: true }).fill(escribir);
  await page.getByRole('option', { name: opcion }).first().click();
}

/** Lo común de la información general: contacto, domicilio y actividad (en Colombia, Bogotá, CIIU 4663). */
async function llenarContactoYDomicilio(page: Page, telefono: string, direccion: string): Promise<void> {
  await page.getByRole('textbox', { name: 'Teléfono', exact: true }).fill(telefono);
  await elegirOpcion(page, 'País', 'Colombia', 'Colombia');
  await elegirOpcion(page, 'Ciudad', 'Bogot', /^Bogotá/);
  await page.getByRole('textbox', { name: 'Dirección', exact: true }).fill(direccion);
  await elegirOpcion(page, 'Actividad económica principal (CIIU)', '4663', /^4663/);
}

/** Información general de una persona jurídica: la razón social llega prellenada (FD-1). */
export async function llenarGeneralJuridica(page: Page, razonSocial: string): Promise<void> {
  await abrirSeccion(page, 'general', 'Información general');
  await expect(page.getByRole('textbox', { name: 'Razón social', exact: true })).toHaveValue(razonSocial);
  await llenarContactoYDomicilio(page, '601 555 1234', 'Calle 45 # 12-08');
  await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
}

/**
 * Información general de una persona natural: nombres y apellidos llegan prellenados (FD-1); suma lo propio de la natural (fecha y
 * lugar de expedición del documento y las dos preguntas de PEP).
 */
export async function llenarGeneralNatural(page: Page): Promise<void> {
  await abrirSeccion(page, 'general', 'Información general');
  await llenarContactoYDomicilio(page, '300 555 1234', 'Carrera 7 # 32-16');
  await escribirFecha(page, 'Fecha de expedición de tu documento', '2012-03-20');
  await page.getByRole('textbox', { name: 'Lugar de expedición', exact: true }).fill('Bogotá');
  await page.getByRole('radiogroup', { name: '¿Eres una persona expuesta políticamente (PEP)?' }).getByRole('radio', { name: 'No' }).check();
  await page.getByRole('radiogroup', { name: '¿Tienes vínculo con una persona expuesta políticamente?' }).getByRole('radio', { name: 'No' }).check();
  await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
}

/** Información tributaria: régimen ordinario y retenedor, sin ICA. */
export async function llenarTributaria(page: Page): Promise<void> {
  await abrirSeccion(page, 'tributaria', 'Información tributaria');
  const responsabilidades = page.getByRole('combobox', { name: 'Responsabilidades (casilla 53 del RUT)' });
  await responsabilidades.click();
  await page.getByRole('option', { name: /^05 · / }).click();
  await page.getByRole('option', { name: /^07 · / }).click();
  await page.keyboard.press('Escape');
  for (const pregunta of [
    '¿Eres contribuyente del impuesto de industria y comercio (ICA)?',
    '¿Eres autorretenedor de ICA?',
    '¿Estás exento de la retención del CREE?',
  ]) {
    await page.getByRole('radiogroup', { name: pregunta }).getByRole('radio', { name: 'No' }).check();
  }
  await guardarSeccionCompleta(page, 'Guardaste Información tributaria: está completa.');
}

export interface DatosDePersona {
  rol: string;
  numero: string;
  nombres: string;
  apellidos: string;
  porcentaje?: string;
}

/** Las dos personas que completan la información legal de una jurídica: un representante legal y un socio con el 100 %. */
export const REPRESENTANTE: DatosDePersona = { rol: 'Representante legal', numero: '1020304050', nombres: 'Marta Lucía', apellidos: 'Ríos Duarte' };
export const SOCIO: DatosDePersona = { rol: 'Socio o accionista', numero: '79555111', nombres: 'Jorge Iván', apellidos: 'Castro Pinto', porcentaje: '100' };

/** «Agregar persona» de la información legal: el diálogo con lo mínimo y «Listo»; la persona queda en la lista (se guarda con la sección). */
export async function agregarPersona(page: Page, datos: DatosDePersona): Promise<void> {
  await page.getByRole('button', { name: 'Agregar persona' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Agregar persona' });
  await dialogo.getByRole('combobox', { name: 'Rol' }).click();
  await page.getByRole('option', { name: datos.rol, exact: true }).click();
  await dialogo.getByRole('combobox', { name: 'Tipo de identificación' }).click();
  await page.getByRole('option', { name: 'CC', exact: true }).click();
  await dialogo.getByRole('textbox', { name: 'Número de identificación' }).fill(datos.numero);
  await dialogo.getByRole('textbox', { name: 'Nombres', exact: true }).fill(datos.nombres);
  await dialogo.getByRole('textbox', { name: 'Apellidos', exact: true }).fill(datos.apellidos);
  if (datos.porcentaje) await dialogo.getByRole('textbox', { name: 'Participación (%)' }).fill(datos.porcentaje);
  await dialogo.getByRole('radiogroup', { name: '¿Es persona expuesta políticamente (PEP)?' }).getByRole('radio', { name: 'No' }).check();
  await dialogo.getByRole('radiogroup', { name: '¿Tiene vínculo con una persona expuesta políticamente?' }).getByRole('radio', { name: 'No' }).check();
  await dialogo.getByRole('button', { name: 'Listo' }).click();
  await expect(dialogo).toBeHidden();
  await expect(page.getByRole('list', { name: 'Personas relacionadas' }).getByRole('listitem', { name: `${datos.nombres} ${datos.apellidos}` })).toBeVisible();
}

/** Información financiera del último cierre (el año anterior): cifras que cuadran y sin moneda extranjera. Devuelve el año. */
export async function llenarFinanciera(page: Page): Promise<number> {
  const periodo = new Date().getFullYear() - 1;
  await abrirSeccion(page, 'financiera', 'Información financiera');
  await escribirFecha(page, 'Fecha de corte', `${String(periodo)}-12-31`);
  const cifras: [string, string][] = [
    ['Activos totales', '1.000.000.000'],
    ['Pasivos totales', '400.000.000'],
    ['Patrimonio', '600.000.000'],
    ['Ingresos brutos', '2.500.000.000'],
    ['Ingresos netos', '2.400.000.000'],
    ['Gastos operacionales', '1.200.000.000'],
    ['Gastos no operacionales', '90.000.000'],
    ['Otros ingresos', '30.000.000'],
  ];
  for (const [etiqueta, valor] of cifras) await page.getByRole('textbox', { name: etiqueta, exact: true }).fill(valor);
  await expect(page.getByRole('status', { name: 'Cuadre: activos = pasivos + patrimonio' })).toContainText('Cuadra');
  await page.getByRole('radiogroup', { name: '¿Realizas operaciones en moneda extranjera?' }).getByRole('radio', { name: 'No' }).check();
  await guardarSeccionCompleta(page, /^Guardaste Información financiera.*: está completa\.$/);
  return periodo;
}

/** Seguridad y salud en el trabajo: las ocho preguntas, todas con «Sí». */
export async function llenarSst(page: Page): Promise<void> {
  await abrirSeccion(page, 'sst', 'Seguridad y salud en el trabajo');
  for (const grupo of await page.getByRole('radiogroup').all()) await grupo.getByRole('radio', { name: 'Sí' }).check();
  await guardarSeccionCompleta(page, 'Guardaste Seguridad y salud en el trabajo: está completa.');
}

/** Especialidades: una del catálogo (concreto premezclado), buscada por su nombre. */
export async function llenarEspecialidades(page: Page): Promise<void> {
  await abrirSeccion(page, 'especialidades', 'Especialidades');
  await page.getByRole('searchbox', { name: 'Buscar especialidad' }).fill('premezclado');
  await page.getByRole('checkbox', { name: 'Concreto premezclado' }).check();
  await expect(page.getByRole('region', { name: 'Tus especialidades' })).toContainText('Concreto premezclado');
  await guardarSeccionCompleta(page, 'Guardaste Especialidades: está completa.');
}

export interface RequisitoDeFicha {
  tipoDocumento?: string;
  seccion?: string;
  /** «Se pide cuando» (RQ-12): p. ej. `tercero.tipoPersona = Natural`. */
  condicion?: { tipo: 'Predicado'; atributo: string; operador: string; valores: string[] };
}

/**
 * Flujo de la empresa para `tipoVinculo` con documentos y secciones de la ficha como requisitos, todos para el área indicada y cada uno
 * con su condición (p. ej. la cédula solo a la persona natural y la Cámara de comercio solo a la jurídica). Si es el primer flujo de ese
 * tipo, queda como el flujo por defecto al publicarlo.
 */
export async function publicarFlujoDeFicha(e: Escenario, empresa: Empresa, codigo: string, area: Area, tipoVinculo: string, requisitos: RequisitoDeFicha[]): Promise<string> {
  const flujo = await llamar<{ id: string }>('POST', '/api/v1/flujos', {
    llave: e.admin,
    empresa: empresa.id,
    cuerpo: {
      codigo,
      nombre: `Flujo ${codigo}`,
      tipoVinculo,
      etapas: [{ orden: 1, areas: [{ areaId: area.id, aprobadoresRequeridos: 1, puedeRechazarExpediente: true, automatica: false, slaHoras: null }] }],
      politicaDevolucion: 'Inmediata',
      separacionDeFunciones: false,
      requisitos: requisitos.map((r) => ({ ...r, obligatorio: true, areasQueRevisan: [area.id], aprobadoresRequeridos: null })),
    },
  });
  await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { llave: e.admin, empresa: empresa.id });
  return flujo.id;
}

/** `tercero.tipoPersona = valor`: la condición de un requisito que solo se pide a ese tipo de persona. */
export const soloPara = (tipoPersona: 'Natural' | 'Juridica'): NonNullable<RequisitoDeFicha['condicion']> => ({
  tipo: 'Predicado',
  atributo: 'tercero.tipoPersona',
  operador: 'Igual',
  valores: [tipoPersona],
});

/**
 * Empresa: desde Certificados llega a la ficha aceptada del vínculo (ficha del tercero → vínculo → «Ver la ficha aceptada», FD-3): lo que la
 * empresa aceptó de cada sección y, aparte, lo que el tercero cambió después.
 */
export async function abrirFichaAceptada(app: FrameLocator, tercero: string): Promise<void> {
  await irASeccion(app, 'Certificados');
  await app.getByRole('link', { name: `Abrir la ficha de ${tercero}` }).click();
  await app.getByRole('link', { name: /Ver el vínculo/ }).first().click();
  await app.getByRole('link', { name: /^Ver la ficha aceptada/ }).click();
  await expect(app.getByRole('heading', { level: 1, name: tercero })).toBeVisible();
}
