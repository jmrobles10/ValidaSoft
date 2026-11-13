import { expect, type CDPSession, type Page } from '@playwright/test';
import { randomUUID } from 'node:crypto';
import { CLAVE_PLATAFORMA, llamar, nitAleatorio } from './api';
import { autenticadorVirtual, type AutenticadorVirtual } from './autenticador';
import { codigo, esperarCorreo, mensajesDe } from './correo';

/** Ayudas del ingreso directo (correo, código, passkey) compartidas por las pruebas y los recorridos del manual. */

export const TEXTO_ASUNTO_CODIGO = 'Tu código para ingresar a Validasoft';
const ASUNTO_CODIGO = new RegExp(TEXTO_ASUNTO_CODIGO);

export interface GrupoDirecto {
  admin: string;
  empresaId: string;
  correoAdmin: string;
  tenantId: string;
  slug: string;
  nombre: string;
}

/** Un grupo con ingreso directo, su primera empresa y un administrador de todo el grupo (sin passkey aún). */
export async function crearGrupoDirecto(opciones: { passkeyParaTodos?: boolean; nombre?: string } = {}): Promise<GrupoDirecto> {
  const sufijo = `${Date.now().toString(36)}${randomUUID().slice(0, 4)}`;
  const correoAdmin = `ana.${sufijo}@e2e.validasoft.co`;
  const slug = `ing-${sufijo}`;
  const nombre = opciones.nombre ?? `Grupo Ingreso ${sufijo}`;
  const creado = await llamar<{ tenant: { id: string }; apiKeyAdministrador: { valor: string } }>('POST', '/api/v1/plataforma/tenants', {
    llave: CLAVE_PLATAFORMA,
    cuerpo: {
      slug,
      nombre,
      region: 'co',
      zonaHoraria: 'America/Bogota',
      ingreso: { modos: ['Directo'], sesionHoras: 8, inactividadMinutos: 30, passkeyParaTodos: opciones.passkeyParaTodos ?? false },
    },
  });
  const admin = creado.apiKeyAdministrador.valor;
  const empresa = await llamar<{ id: string }>('POST', '/api/v1/empresas', {
    llave: admin,
    cuerpo: { identificacion: nitAleatorio(), nombre: 'Constructora Norte S.A.S.', prefijoFolio: `I${sufijo.slice(-5).toUpperCase()}`, logoUrl: null, replyTo: null },
  });
  await llamar('POST', '/api/v1/usuarios', {
    llave: admin,
    cuerpo: { nombre: 'Ana Administradora', correo: correoAdmin, identificacion: null, roles: [{ rol: 'Administrador', empresaId: null }], identidades: null },
  });
  return { admin, empresaId: empresa.id, correoAdmin, tenantId: creado.tenant.id, slug, nombre };
}

/** Da de alta a una persona con un rol en la empresa del grupo (por la API del administrador). */
export async function crearUsuario(g: GrupoDirecto, nombre: string, rol: string, correo: string): Promise<void> {
  await llamar('POST', '/api/v1/usuarios', {
    llave: g.admin,
    cuerpo: { nombre, correo, identificacion: null, roles: [{ rol, empresaId: g.empresaId }], identidades: null },
  });
}

/** Passkeys que el autenticador virtual tiene guardadas. */
export async function credenciales(a: { cdp: CDPSession; id: string }): Promise<number> {
  const { credentials } = await a.cdp.send('WebAuthn.getCredentials', { authenticatorId: a.id });
  return credentials.length;
}

/** Pide el código con el correo y lo escribe en las casillas; el código llega al correo de prueba. */
export async function entrarConCodigo(page: Page, correo: string): Promise<void> {
  const antes = (await mensajesDe(correo)).filter((m) => m.Subject.includes(TEXTO_ASUNTO_CODIGO)).map((m) => m.ID);
  await page.getByLabel('Correo').fill(correo);
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
  const correoDelCodigo = await esperarCorreo(correo, ASUNTO_CODIGO, { excluir: antes });
  await page.keyboard.type(codigo(correoDelCodigo));
}

/** La primera entrada de una administradora: código y registro de su primera passkey con el autenticador virtual. */
export async function entrarYRegistrarPasskey(page: Page, g: GrupoDirecto): Promise<AutenticadorVirtual> {
  const virtual = await autenticadorVirtual(page);
  await page.goto('/app/');
  await entrarConCodigo(page, g.correoAdmin);
  await expect(page.getByRole('heading', { name: 'Registra tu passkey' })).toBeVisible();
  await page.getByRole('button', { name: 'Registrar passkey' }).click();
  await expect(page.getByRole('navigation', { name: 'Secciones' })).toBeVisible();
  return virtual;
}

/** Si la acción pidió confirmar con la passkey (step-up), la confirma con el autenticador virtual. */
export async function confirmarConPasskey(page: Page): Promise<void> {
  const dialogo = page.getByRole('dialog', { name: 'Confirma que eres tú' });
  await expect(dialogo).toBeVisible();
  await dialogo.getByRole('button', { name: 'Usar mi passkey' }).click();
  await expect(dialogo).toBeHidden();
}

/** Cierra la sesión desde el menú de la cuenta y deja la pantalla de ingreso. */
export async function cerrarSesion(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
  await page.getByRole('menuitem', { name: 'Cerrar sesión' }).click();
  await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
}
