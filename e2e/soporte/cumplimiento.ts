import { expect, type Browser, type FrameLocator, type Page } from '@playwright/test';
import { CLAVE_PLATAFORMA, llamar } from './api';
import { pdf } from './archivos';
import { abrirEmbebido } from './embebido';
import { abrirDesdeBandeja, regionDeArea } from './empresa';
import { completarInvitacion } from './proveedor';
import { invitar, nuevoTercero, type Escenario, type Invitacion, type Persona } from './semilla';

/**
 * Apoyo de las pruebas de cumplimiento (H7): flujo con verificaciones, un tercero que llega a revisión, las personas de la
 * empresa en su propio navegador y las esperas de la consulta automática de listas. Los textos son los de apps/web/src/i18n.
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

/** Ruta de una captura: la carpeta de `E2E_CAPTURAS` si está definida y, si no, `test-results`. */
export function captura(nombre: string): string {
  return `${process.env.E2E_CAPTURAS ?? 'test-results'}/${nombre}.png`;
}

/** Agrega las verificaciones al flujo de proveedores de Norte y lo vuelve a publicar. */
export async function pedirVerificaciones(e: Escenario, verificaciones: { tipo: string; bloqueante: boolean; vigenciaDias?: number }[]): Promise<void> {
  const flujos = await llamar<{ id: string; codigo: string }[]>('GET', '/api/v1/flujos?tipoVinculo=PROVEEDOR', { llave: e.admin, empresa: e.norte.id });
  const flujo = flujos.find((f) => f.codigo === 'HABILITACION');
  if (!flujo) throw new Error('Falta el flujo HABILITACION de Norte.');
  await llamar('PUT', `/api/v1/flujos/${flujo.id}`, { llave: e.admin, empresa: e.norte.id, cuerpo: { verificaciones } });
  await llamar('POST', `/api/v1/flujos/${flujo.id}/publicar`, { llave: e.admin, empresa: e.norte.id });
}

/** El tercero acepta y sube RUT y Cámara (el expediente queda en revisión y las listas se consultan solas). */
export async function enRevision(page: Page, e: Escenario, nombre: string): Promise<Invitacion> {
  const invitacion = await invitar(e, e.norte, nuevoTercero(nombre));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${nombre}`) },
    { nombre: CAMARA, archivo: pdf(`Camara ${nombre}`) },
  ]);
  return invitacion;
}

/** Una persona de la empresa abre Validasoft embebido en su propio navegador. */
export async function personaEmbebida(browser: Browser, e: Escenario, quien: Persona, ruta = 'bandeja'): Promise<FrameLocator> {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, quien, e.norte, ruta);
}

/** Recarga el expediente (pasando por Vínculos) hasta que las listas muestren el estado esperado: la consulta es automática. */
export async function esperarListas(app: FrameLocator, tercero: string, area: string, estado: string): Promise<void> {
  await expect(async () => {
    await abrirDesdeBandeja(app, tercero, area);
    await expect(app.getByRole('region', { name: 'Listas restrictivas' })).toContainText(estado, { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
}

/** La última área aprueba, pero el expediente sigue en revisión: falta una verificación bloqueante. */
export async function aprobarUltimaArea(app: FrameLocator, area: string, falta: RegExp): Promise<void> {
  await regionDeArea(app, area).getByRole('button', { name: /^Aprobar área/ }).click();
  const dialogo = app.getByRole('dialog', { name: new RegExp(`^Aprobar ${area}`) });
  await dialogo.getByRole('button', { name: /^Aprobar/ }).last().click();
  await expect(dialogo).toBeHidden();
  const aviso = app.getByRole('alert').filter({ hasText: 'Falta cumplimiento para aprobar' });
  await expect(aviso).toContainText(falta);
}

export interface ResultadoDeVerificaciones {
  consultas: number;
  expiraciones: number;
  monitoreos: number;
}

/**
 * Corre ya lo que el procesador de verificaciones haría (consulta automática de listas, expiración de lo vencido con el expediente abierto y
 * monitoreo de los vínculos activos) para el grupo, como si fuera `fecha` (solo Development): así se prueba un estudio vencido o la reconsulta
 * periódica sin esperar los días de verdad.
 */
export async function revisarVerificaciones(e: Escenario, fecha: Date): Promise<ResultadoDeVerificaciones> {
  return llamar<ResultadoDeVerificaciones>('POST', '/api/v1/plataforma/verificaciones/revisar', {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { tenantId: e.tenantId, fecha: fecha.toISOString() },
  });
}
