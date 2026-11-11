import { expect, type Page } from '@playwright/test';
import { codigo, enlace, esperarCorreo } from './correo';
import { escribirFecha, esFechaIso } from './fechas';
import type { Invitacion } from './semilla';

interface Archivo {
  name: string;
  mimeType: string;
  buffer: Buffer;
}

/** Códigos ya usados por correo: cada entrada pide «el siguiente» código, no uno viejo. */
const codigosUsados = new Map<string, string[]>();

/**
 * El tercero abre el enlace de su invitación (el correo de esa empresa y tipo) y, si el tipo exige OTP, escribe el
 * código que le llega al mismo correo. Queda en la pantalla de aceptar la invitación.
 */
export async function entrarPorInvitacion(page: Page, invitacion: Invitacion): Promise<void> {
  // El asunto lleva el nombre del tipo en minúscula (Proveedor, Cliente, Aliado…): el código en mayúsculas lo da igual.
  const tipo = invitacion.tipo.toLowerCase();
  const correo = await esperarCorreo(invitacion.correo, new RegExp(`^${escapar(invitacion.empresa.nombre)} te invita a validar tu vínculo como ${tipo}$`));
  await page.goto(enlace(correo, '/e/'));
  const pideCodigo = page.getByRole('heading', { name: 'Escribe el código' });
  const invita = page.getByRole('heading', { level: 1, name: `${invitacion.empresa.nombre} te invita a validar tu información` });
  await expect(pideCodigo.or(invita)).toBeVisible();
  if (await pideCodigo.isVisible()) await escribirCodigo(page, invitacion.correo);
  // El enlace abre la invitación de esta empresa aunque el tercero tenga otras pendientes (no pasa por «Mis empresas»).
  await expect(invita).toBeVisible();
}

/** En «Mis empresas», abre la tarjeta de la empresa (y tipo de vínculo) con su acción: aceptar o ver la revisión. */
export async function abrirEmpresa(page: Page, empresa: string, tipo = 'PROVEEDOR'): Promise<void> {
  const nombreTipo = tipo === 'CLIENTE' ? 'Cliente' : 'Proveedor';
  const item = page
    .getByRole('listitem')
    .filter({ has: page.getByRole('heading', { level: 2, name: empresa, exact: true }) })
    .filter({ has: page.getByText(nombreTipo, { exact: true }) });
  // Si el clic cae mientras la lista se vuelve a pintar (se refresca sola), la página no cambia: se repite hasta salir.
  await expect(async () => {
    if (/\/empresas\/?$/.test(new URL(page.url()).pathname)) await item.getByRole('link').first().click({ timeout: 3000 });
    await expect(page).not.toHaveURL(/\/empresas\/?$/, { timeout: 3000 });
  }).toPass({ timeout: 20_000 });
}

/** Pantalla «Escribe el código»: lee el OTP nuevo de Mailpit y lo escribe dígito a dígito. */
export async function escribirCodigo(page: Page, correoDelTercero: string): Promise<void> {
  const usados = codigosUsados.get(correoDelTercero) ?? [];
  const mensaje = await esperarCorreo(correoDelTercero, /código de acceso/, { excluir: usados });
  codigosUsados.set(correoDelTercero, [...usados, mensaje.id]);
  await page.getByRole('textbox', { name: 'Dígito 1 de 6' }).click();
  await page.keyboard.type(codigo(mensaje));
  const continuar = page.getByRole('button', { name: 'Continuar' });
  if ((await continuar.isVisible()) && (await continuar.isEnabled())) await continuar.click().catch(() => undefined);
  await expect(page.getByRole('heading', { name: 'Escribe el código' })).toBeHidden();
}

/** Acepta los textos legales de la invitación; queda en «Tu revisión con {empresa}». */
export async function aceptarInvitacion(page: Page, invitacion: Invitacion): Promise<void> {
  for (const casilla of await page.getByRole('checkbox', { name: 'Leí y acepto este texto' }).all()) await casilla.check();
  await page.getByRole('button', { name: 'Aceptar' }).click();
  await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${invitacion.empresa.nombre}` })).toBeVisible();
}

/** Tarjeta de un requisito en «Tu revisión con …». */
export function tarjeta(page: Page, nombre: string) {
  return page.getByRole('listitem').filter({ has: page.getByRole('heading', { name: nombre, exact: true }) });
}

export interface OpcionesSubida {
  /** Versión que debe quedar en la bóveda (1 para la primera). */
  version?: number;
  /** Campos del formulario del tipo (etiqueta → valor), p. ej. `{ 'Fecha de expedición': '2026-10-01' }`. */
  campos?: Record<string, string>;
}

/**
 * Desde «Tu revisión con …», sube el documento del requisito `nombre` (subir, otra versión o versión corregida),
 * llena los campos que pida y vuelve a la revisión. Comprueba la versión que quedó en la bóveda.
 */
export async function subirDocumento(page: Page, nombre: string, archivo: Archivo, { version = 1, campos = {} }: OpcionesSubida = {}): Promise<void> {
  await tarjeta(page, nombre).getByRole('link', { name: /^Subir/ }).click();
  await expect(page.getByRole('heading', { level: 1, name: `Subir ${nombre}` })).toBeVisible();
  for (const [etiqueta, valor] of Object.entries(campos)) {
    // Una fecha (`aaaa-mm-dd`) se escribe en el selector de fecha por secciones; lo demás es texto.
    if (esFechaIso(valor)) await escribirFecha(page, new RegExp(escapar(etiqueta)), valor);
    else await page.getByLabel(new RegExp(escapar(etiqueta))).fill(valor);
  }
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(archivo);
  await expect(page.getByText(`Es tu versión ${String(version)} de ${nombre}`)).toBeVisible({ timeout: 30_000 });
  await page.getByRole('link', { name: 'Volver a mi revisión' }).click();
}

export type EstadoDelTercero = 'Abierto' | 'EnRevision' | 'Observado' | 'Aprobado' | 'Rechazado';

/** Frase que explica el estado general en «Tu revisión con …» (los chips de cada documento repiten algunos nombres). */
const FRASES: Record<EstadoDelTercero, RegExp> = {
  Abierto: /^Te falta(n)? \d+ documentos?\. Súbel/,
  EnRevision: / está revisando tu información\./,
  Observado: / te pide corregir lo siguiente\./,
  Aprobado: / aprobó tu información\. Tu vínculo está activo\./,
  Rechazado: / cerró la revisión sin aprobarla\./,
};

/** Hay una frase de estado `estado` visible en la página (sin recargar). */
export function fraseDeEstado(page: Page, estado: EstadoDelTercero) {
  return page.getByRole('main').getByText(FRASES[estado]);
}

/**
 * Recarga «Tu revisión con …» hasta que el estado general sea `estado`: la bóveda adjunta las versiones al expediente
 * por una cola (RQ-2) y las decisiones de la empresa llegan sin que la página se entere.
 */
export async function esperarEstadoDelTercero(page: Page, estado: EstadoDelTercero): Promise<void> {
  await expect(async () => {
    await page.reload();
    await expect(page.getByRole('heading', { level: 1, name: /^Tu revisión con / })).toBeVisible();
    await expect(fraseDeEstado(page, estado)).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
}

/** Recarga hasta que la tarjeta del requisito `nombre` muestre `texto` (p. ej. «Versión 2»). */
export async function esperarTarjeta(page: Page, nombre: string, texto: string | RegExp): Promise<void> {
  await expect(async () => {
    await page.reload();
    await expect(tarjeta(page, nombre)).toContainText(texto, { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
}

/** «Enviar respuesta» a las observaciones, con una explicación opcional; la revisión vuelve a «En revisión». */
export async function responder(page: Page, explicacion?: string): Promise<void> {
  if (explicacion) await page.getByRole('textbox', { name: 'Tu respuesta (opcional)' }).fill(explicacion);
  await page.getByRole('button', { name: 'Enviar respuesta' }).click();
  const error = page.getByRole('alert').filter({ hasNotText: /Vence pronto|Aceptaste|Enviaste tu respuesta/ });
  await expect(fraseDeEstado(page, 'EnRevision').or(error).first()).toBeVisible();
  if (!(await fraseDeEstado(page, 'EnRevision').isVisible())) throw new Error(`No se pudo enviar la respuesta: ${await error.first().innerText()}`);
}

/**
 * El tercero completa su lado de una invitación: entra por el correo, acepta y sube cada documento pedido; espera a
 * que el expediente quede «En revisión».
 */
export async function completarInvitacion(page: Page, invitacion: Invitacion, documentos: { nombre: string; archivo: Archivo; campos?: Record<string, string> }[]): Promise<void> {
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  for (const d of documentos) await subirDocumento(page, d.nombre, d.archivo, { campos: d.campos ?? {} });
  await esperarEstadoDelTercero(page, 'EnRevision');
}

/**
 * Como {@link completarInvitacion}, pero sin exigir que el tercero vea «En revisión»: con la revisión automática en nivel
 * Automático el expediente puede aprobarse al segundo de llegar a revisión (el procesador corre cada segundo), antes de que la
 * página alcance a mostrar «En revisión». Espera a que se vea «En revisión» o ya «Aprobado».
 */
export async function completarInvitacionYEsperarRevisionOAprobacion(
  page: Page,
  invitacion: Invitacion,
  documentos: { nombre: string; archivo: Archivo; campos?: Record<string, string> }[],
): Promise<void> {
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
  for (const d of documentos) await subirDocumento(page, d.nombre, d.archivo, { campos: d.campos ?? {} });
  await expect(async () => {
    await page.reload();
    await expect(page.getByRole('heading', { level: 1, name: /^Tu revisión con / })).toBeVisible();
    await expect(fraseDeEstado(page, 'EnRevision').or(fraseDeEstado(page, 'Aprobado'))).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
}

function escapar(texto: string): string {
  return texto.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
