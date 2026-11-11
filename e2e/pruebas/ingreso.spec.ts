import { expect, test, type CDPSession, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { CLAVE_PLATAFORMA, llamar, nitAleatorio } from '../soporte/api';
import { codigo, esperarCorreo } from '../soporte/correo';

/**
 * Ingreso directo sin contraseñas (ING-1…ING-4): correo → código → passkey, ingreso solo con passkey (autenticador virtual del navegador por CDP),
 * Mi cuenta (passkeys y sesiones), invitación y gestión por el administrador, y operadores de Plataforma. Deja capturas de las pantallas de ingreso y
 * de Mi cuenta en `E2E_CAPTURAS` (por omisión `test-results/ingreso`).
 */

const CAPTURAS = process.env.E2E_CAPTURAS ?? join('test-results', 'ingreso');
mkdirSync(CAPTURAS, { recursive: true });
const captura = (nombre: string) => join(CAPTURAS, `${nombre}.png`);

const TEXTO_ASUNTO_CODIGO = 'Tu código para ingresar a Validasoft';
const ASUNTO_CODIGO = new RegExp(TEXTO_ASUNTO_CODIGO);

interface Grupo {
  admin: string;
  empresaId: string;
  correoAdmin: string;
}

/** Un grupo con ingreso directo, su primera empresa y un administrador de todo el grupo (sin passkey aún). */
async function crearGrupoDirecto(opciones: { passkeyParaTodos?: boolean } = {}): Promise<Grupo> {
  const sufijo = `${Date.now().toString(36)}${randomUUID().slice(0, 4)}`;
  const correoAdmin = `ana.${sufijo}@e2e.validasoft.co`;
  const creado = await llamar<{ tenant: { id: string }; apiKeyAdministrador: { valor: string } }>('POST', '/api/v1/plataforma/tenants', {
    llave: CLAVE_PLATAFORMA,
    cuerpo: {
      slug: `ing-${sufijo}`,
      nombre: `Grupo Ingreso ${sufijo}`,
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
  return { admin, empresaId: empresa.id, correoAdmin };
}

async function crearUsuario(g: Grupo, nombre: string, rol: string, correo: string): Promise<void> {
  await llamar('POST', '/api/v1/usuarios', {
    llave: g.admin,
    cuerpo: { nombre, correo, identificacion: null, roles: [{ rol, empresaId: g.empresaId }], identidades: null },
  });
}

/** Autenticador virtual con huella simulada (CDP `WebAuthn`): credenciales detectables y verificación de usuario. */
async function autenticadorVirtual(page: Page, sesionCdp?: CDPSession): Promise<{ cdp: CDPSession; id: string }> {
  const cdp = sesionCdp ?? (await page.context().newCDPSession(page));
  if (!sesionCdp) await cdp.send('WebAuthn.enable');
  const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', {
    options: { protocol: 'ctap2', transport: 'internal', hasResidentKey: true, hasUserVerification: true, isUserVerified: true, automaticPresenceSimulation: true },
  });
  return { cdp, id: authenticatorId };
}

async function credenciales(a: { cdp: CDPSession; id: string }): Promise<number> {
  const { credentials } = await a.cdp.send('WebAuthn.getCredentials', { authenticatorId: a.id });
  return credentials.length;
}

/** Pide el código con el correo y lo escribe en las seis casillas; devuelve cuántos correos de código había antes. */
async function entrarConCodigo(page: Page, correo: string, capturaDelCodigo?: string): Promise<void> {
  const antes = await fetch(`${process.env.E2E_MAILPIT ?? 'http://localhost:8025'}/api/v1/search?query=${encodeURIComponent(`to:"${correo}"`)}`)
    .then((r) => r.json() as Promise<{ messages: { ID: string; Subject: string }[] }>)
    .then((r) => r.messages.filter((m) => m.Subject.includes(TEXTO_ASUNTO_CODIGO)).map((m) => m.ID));
  await page.getByLabel('Correo').fill(correo);
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
  if (capturaDelCodigo) await page.screenshot({ path: captura(capturaDelCodigo), fullPage: true });
  const correoDelCodigo = await esperarCorreo(correo, ASUNTO_CODIGO, { excluir: antes });
  await page.keyboard.type(codigo(correoDelCodigo));
}

const barraDeSecciones = (page: Page) => page.getByRole('navigation', { name: 'Secciones' });

test('una persona entra con el código del correo, recarga sin volver a pedirlo y cierra la sesión', async ({ page }) => {
  const g = await crearGrupoDirecto();
  const correo = `rita.${Date.now().toString(36)}@e2e.validasoft.co`;
  await crearUsuario(g, 'Rita Revisora', 'Revisor', correo);

  await page.goto('/app/');
  await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Entrar con una passkey' })).toBeVisible();
  await page.screenshot({ path: captura('ingreso-correo'), fullPage: true });

  await entrarConCodigo(page, correo, 'ingreso-codigo');
  await expect(barraDeSecciones(page)).toBeVisible();
  // Entró solo con el código: la cuenta no exige passkey, pero se le sugiere registrar una.
  await expect(page.getByText('Protege tu cuenta con una passkey')).toBeVisible();

  // La cookie de refresco restaura la sesión al recargar, sin pedir otro código.
  await page.reload();
  await expect(barraDeSecciones(page)).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toHaveCount(0);

  await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
  await expect(page.getByText(correo)).toBeVisible();
  await page.getByRole('menuitem', { name: 'Cerrar sesión' }).click();
  await expect(page.getByText('Cerraste la sesión')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();

  // Cerrada la sesión, recargar tampoco la recupera.
  await page.reload();
  await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
  await expect(barraDeSecciones(page)).toHaveCount(0);
});

test('un código incorrecto dice cuántos intentos quedan y un correo sin cuenta no revela nada', async ({ page }) => {
  const g = await crearGrupoDirecto();
  const correo = `rita.${Date.now().toString(36)}@e2e.validasoft.co`;
  await crearUsuario(g, 'Rita Revisora', 'Revisor', correo);

  await page.goto('/app/');
  await page.getByLabel('Correo').fill(correo);
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
  const real = codigo(await esperarCorreo(correo, ASUNTO_CODIGO));
  const malo = real === '000000' ? '111111' : '000000';
  await page.keyboard.type(malo);
  await expect(page.getByText('El código no es correcto. Te quedan 4 intentos.')).toBeVisible();
  await page.screenshot({ path: captura('ingreso-codigo-incorrecto'), fullPage: true });

  // Un correo inventado llega a la misma pantalla de código y recibe el mismo mensaje.
  await page.getByRole('button', { name: 'Usar otro correo' }).click();
  await page.getByLabel('Correo').fill(`nadie.${Date.now().toString(36)}@e2e.validasoft.co`);
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
  await page.keyboard.type('123456');
  await expect(page.getByText('El código no es correcto. Te quedan 4 intentos.')).toBeVisible();
});

test('un administrador registra su passkey en el primer ingreso y después entra solo con ella', async ({ page }) => {
  const g = await crearGrupoDirecto();
  const virtual = await autenticadorVirtual(page);

  await page.goto('/app/');
  await entrarConCodigo(page, g.correoAdmin);

  // Su rol exige passkey: el código solo sirve para registrar la primera.
  await expect(page.getByRole('heading', { name: 'Registra tu passkey' })).toBeVisible();
  await page.screenshot({ path: captura('ingreso-registrar-passkey'), fullPage: true });
  await page.getByLabel('Nombre para reconocerla').fill('Mi teléfono');
  await page.getByRole('button', { name: 'Registrar passkey' }).click();
  await expect(barraDeSecciones(page)).toBeVisible();
  expect(await credenciales(virtual)).toBe(1);
  // Entró con código y passkey: no se le sugiere registrar otra.
  await expect(page.getByText('Protege tu cuenta con una passkey')).toHaveCount(0);

  // Cierra la sesión y vuelve a entrar solo con la passkey, sin correo ni código.
  await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
  await page.getByRole('menuitem', { name: 'Cerrar sesión' }).click();
  await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
  const codigosAntes = await fetch(`${process.env.E2E_MAILPIT ?? 'http://localhost:8025'}/api/v1/search?query=${encodeURIComponent(`to:"${g.correoAdmin}"`)}`)
    .then((r) => r.json() as Promise<{ messages: { Subject: string }[] }>)
    .then((r) => r.messages.filter((m) => m.Subject.includes(TEXTO_ASUNTO_CODIGO)).length);
  await page.getByRole('button', { name: 'Entrar con una passkey' }).click();
  await expect(barraDeSecciones(page)).toBeVisible();
  const codigosDespues = await fetch(`${process.env.E2E_MAILPIT ?? 'http://localhost:8025'}/api/v1/search?query=${encodeURIComponent(`to:"${g.correoAdmin}"`)}`)
    .then((r) => r.json() as Promise<{ messages: { Subject: string }[] }>)
    .then((r) => r.messages.filter((m) => m.Subject.includes(TEXTO_ASUNTO_CODIGO)).length);
  expect(codigosDespues).toBe(codigosAntes);
});

test('Mi cuenta: ver y cerrar sesiones, agregar, renombrar y eliminar passkeys', async ({ page }) => {
  const g = await crearGrupoDirecto();

  const virtual = await autenticadorVirtual(page);
  await page.goto('/app/');
  await entrarConCodigo(page, g.correoAdmin);
  await page.getByRole('button', { name: 'Registrar passkey' }).click();
  await expect(barraDeSecciones(page)).toBeVisible();

  await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
  await page.getByRole('menuitem', { name: 'Mi cuenta' }).click();
  await expect(page.getByRole('heading', { name: 'Ana Administradora' })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Passkeys' })).toBeVisible();
  await expect(page.getByText('Esta sesión', { exact: true })).toBeVisible();
  await expect(page.getByText(g.correoAdmin).first()).toBeVisible();
  await expect(page.getByText('Todo el grupo')).toBeVisible();
  await page.screenshot({ path: captura('mi-cuenta'), fullPage: true });

  // Agregar otra passkey desde otro dispositivo (el navegador no deja registrar dos en el mismo: excluye las de la cuenta), cambiarle el nombre
  // y comprobar en la lista que quedó registrada (el servidor ya verificó la respuesta del autenticador).
  await virtual.cdp.send('WebAuthn.removeVirtualAuthenticator', { authenticatorId: virtual.id });
  await autenticadorVirtual(page, virtual.cdp);
  await page.getByRole('button', { name: 'Agregar passkey' }).click();
  await page.getByLabel('Nombre para reconocerla').fill('Llave de la oficina');
  await page.getByRole('dialog').getByRole('button', { name: 'Registrar passkey' }).click();
  await expect(page.getByText('Llave de la oficina')).toBeVisible();
  await page.getByRole('button', { name: 'Cambiar nombre: Llave de la oficina' }).click();
  await page.getByLabel('Nombre para reconocerla').fill('Llave USB');
  await page.getByRole('dialog').getByRole('button', { name: 'Guardar' }).click();
  await expect(page.getByText('Llave USB')).toBeVisible();
  await page.screenshot({ path: captura('mi-cuenta-passkeys'), fullPage: true });

  // Con dos se puede eliminar una; la última no (el rol de Ana la exige).
  await page.getByRole('button', { name: 'Eliminar: Llave USB' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Eliminar' }).click();
  await expect(page.getByText('Llave USB')).toHaveCount(0);
  await expect(page.getByRole('button', { name: /^Eliminar: / })).toBeDisabled();

  // Cerrar esta sesión desde Mi cuenta.
  await page.getByRole('button', { name: 'Cerrar esta sesión' }).click();
  await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
});

test('un administrador invita por correo, ve el acceso de la persona y revoca su passkey', async ({ page, browser }) => {
  const g = await crearGrupoDirecto();
  const virtual = await autenticadorVirtual(page);
  await page.goto('/app/');
  await entrarConCodigo(page, g.correoAdmin);
  await page.getByRole('button', { name: 'Registrar passkey' }).click();
  await expect(barraDeSecciones(page)).toBeVisible();
  expect(await credenciales(virtual)).toBe(1);

  // Configuración › Usuarios: agrega a una auditora con la invitación por correo.
  const correoAuditora = `carla.${Date.now().toString(36)}@e2e.validasoft.co`;
  await page.goto(`/app/#/e/${g.empresaId}/usuarios`);
  await page.getByRole('button', { name: 'Agregar persona' }).click();
  const panel = page.locator('[aria-labelledby="titulo-roles"]');
  await panel.getByLabel('Nombre').first().fill('Carla Auditora');
  await panel.getByRole('textbox', { name: 'Correo', exact: true }).fill(correoAuditora);
  await panel.getByRole('button', { name: 'Agregar rol' }).click();
  await panel.getByRole('combobox', { name: 'Rol' }).click();
  await page.getByRole('option', { name: 'Auditor' }).click();
  await expect(panel.getByLabel('Enviar una invitación por correo')).toBeChecked();
  await panel.getByRole('button', { name: 'Agregar persona' }).click();
  const invitacion = await esperarCorreo(correoAuditora, /te invitó a Validasoft/);
  expect(invitacion.texto).toContain('Auditor');
  await expect(page.getByText('Carla Auditora')).toBeVisible();

  // Carla entra desde otro navegador: su rol exige passkey, la registra y queda con una sesión abierta.
  const contexto = await browser.newContext();
  const otra = await contexto.newPage();
  const virtualDeCarla = await autenticadorVirtual(otra);
  await otra.goto('/app/');
  await entrarConCodigo(otra, correoAuditora);
  await otra.getByRole('button', { name: 'Registrar passkey' }).click();
  await expect(barraDeSecciones(otra)).toBeVisible();
  expect(await credenciales(virtualDeCarla)).toBe(1);

  // El administrador ve su passkey y su sesión y revoca la passkey: la sesión de Carla se cierra.
  await page.reload();
  await page.getByRole('button', { name: 'Acciones de Carla Auditora' }).click();
  await page.getByRole('menuitem', { name: 'Passkeys y sesiones' }).click();
  const drawer = page.locator('[aria-labelledby="titulo-acceso"]');
  await expect(drawer.getByText('Mi passkey').or(drawer.getByText(/Chrome en/)).first()).toBeVisible();
  await page.screenshot({ path: captura('usuarios-acceso'), fullPage: true });
  await drawer.getByRole('button', { name: 'Revocar' }).click();
  await page.getByRole('dialog').getByLabel('Motivo (opcional)').fill('Perdió el teléfono');
  await page.getByRole('dialog').getByRole('button', { name: 'Revocar' }).click();
  // Revocar una passkey es una acción sensible (PI-13): pide confirmar con la passkey de la administradora y, al confirmar, sigue sola.
  const reautenticacion = page.getByRole('dialog', { name: 'Confirma que eres tú' });
  await expect(reautenticacion).toBeVisible();
  await reautenticacion.getByRole('button', { name: 'Usar mi passkey' }).click();
  await expect(page.getByText('Passkey revocada y sesiones cerradas.')).toBeVisible();

  // La sesión de Carla ya no sirve (su token cayó) y la auditoría de identidad lo registra.
  await otra.reload();
  await expect(otra.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
  await page.goto(`/app/#/e/${g.empresaId}/auditoria?tipo=Identidad`);
  await expect(page.getByText('Passkey revocada').first()).toBeVisible();
  await expect(page.getByText('Ingreso exitoso').first()).toBeVisible();
  await page.screenshot({ path: captura('auditoria-identidad'), fullPage: true });
  await contexto.close();
});

test('un operador de Plataforma entra con correo, código y passkey a la consola', async ({ page }) => {
  const correo = `paula.${Date.now().toString(36)}@e2e.validasoft.co`;
  await llamar('POST', '/api/v1/plataforma/operadores', { llave: CLAVE_PLATAFORMA, cuerpo: { nombre: 'Paula Plataforma', correo } });
  const virtual = await autenticadorVirtual(page);

  await page.goto('/app/');
  await entrarConCodigo(page, correo);
  await expect(page.getByRole('heading', { name: 'Registra tu passkey' })).toBeVisible();
  await page.getByRole('button', { name: 'Registrar passkey' }).click();

  // Sin tenant ni empresas: va directo a la consola de Plataforma.
  await expect(page.getByRole('heading', { name: 'Grupos y empresas' })).toBeVisible();
  expect(await credenciales(virtual)).toBe(1);
  await page.getByRole('link', { name: 'Operadores' }).click();
  await expect(page.getByRole('heading', { name: 'Operadores' })).toBeVisible();
  await expect(page.getByText(correo)).toBeVisible();
  await page.screenshot({ path: captura('plataforma-operadores'), fullPage: true });

  await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
  await page.getByRole('menuitem', { name: 'Mi cuenta' }).click();
  await expect(page.getByRole('heading', { name: 'Paula Plataforma' })).toBeVisible();
  await expect(page.getByText('Plataforma de Validasoft').first()).toBeVisible();
  await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
  await page.getByRole('menuitem', { name: 'Cerrar sesión' }).click();
  await page.getByRole('button', { name: 'Entrar con una passkey' }).click();
  await expect(page.getByRole('heading', { name: 'Grupos y empresas' })).toBeVisible();
});
