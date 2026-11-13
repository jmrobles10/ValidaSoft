import type { Browser, Page } from '@playwright/test';
import { expect } from '@playwright/test';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST, type Escenario, type Invitacion, type Tercero } from '../../soporte/semilla';

/** Nombres de los requisitos del escenario (los mismos que usan las especificaciones de `e2e/pruebas`). */
export const RUT = 'Registro Único Tributario';
export const CAMARA = 'Certificado de existencia y representación legal';

/** Fecha de hace `dias` días, en la zona de Bogotá, para la fecha de expedición del SST. */
export function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

/** Un tercero invitado por Norte con RUT y Cámara entregados (o solo el RUT); queda «En revisión» si entregó todo. */
export async function terceroEnRevision(page: Page, nombre: string, todo = true): Promise<{ e: Escenario; invitacion: Invitacion; tercero: Tercero }> {
  const e = await crearEscenario();
  const tercero = nuevoTercero(nombre);
  const invitacion = await invitar(e, e.norte, tercero);
  const documentos = [{ nombre: RUT, archivo: pdf(`RUT ${nombre}`) }];
  if (todo) documentos.push({ nombre: CAMARA, archivo: pdf(`Camara ${nombre}`) });
  await completarInvitacion(page, invitacion, documentos);
  return { e, invitacion, tercero };
}

/** Un tercero con dos empresas: Norte (RUT y Cámara entregados) y Sur (SST entregado). Deja al tercero con sesión abierta. */
export async function terceroConDosEmpresas(page: Page, nombre: string): Promise<{ e: Escenario; tercero: Tercero }> {
  const e = await crearEscenario();
  const tercero = nuevoTercero(nombre);
  const enNorte = await invitar(e, e.norte, tercero);
  const enSur = await invitar(e, e.sur, tercero);
  await completarInvitacion(page, enNorte, [
    { nombre: RUT, archivo: pdf(`RUT ${nombre}`) },
    { nombre: CAMARA, archivo: pdf(`Camara ${nombre}`) },
  ]);
  await completarInvitacion(page, enSur, [{ nombre: SST.nombre, archivo: pdf(`SST ${nombre}`), campos: { 'Fecha de expedición': haceDias(2) } }]);
  return { e, tercero };
}

/** Un tercero aprobado por Sur (una sola área) con certificado vigente; Sofía (Revisión) lo aprueba desde la app embebida. */
export async function terceroAprobadoPorSur(page: Page, browser: Browser, nombre: string): Promise<{ e: Escenario; invitacion: Invitacion }> {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero(nombre));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${nombre}`) },
    { nombre: SST.nombre, archivo: pdf(`SST ${nombre}`), campos: { 'Fecha de expedición': haceDias(2) } },
  ]);
  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, nombre, 'Revisión');
  await aprobarArea(sofia, 'Revisión');
  await esperarEstadoDelTercero(page, 'Aprobado');
  expect(invitacion.vinculoId).toBeTruthy();
  return { e, invitacion };
}
