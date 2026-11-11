import { expect, test, type CDPSession, type Page } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { CLAVE_PLATAFORMA, clavesDeSistema, llamar, nitAleatorio } from '../soporte/api';
import { codigo, esperarCorreo } from '../soporte/correo';
import { abrirConsolaDePlataforma } from '../soporte/plataforma';

/**
 * Plataforma e identidad (PI-1…PI-18): la consola de Plataforma (uso de IA con su tarifa, ingresos de los operadores, clave de arranque, empresas y sistemas de
 * un grupo) y, para quien entra directo, la reautenticación con passkey, los códigos de recuperación y la configuración del inicio de sesión único. Cada
 * pantalla nueva deja una captura de escritorio y otra de teléfono en `E2E_CAPTURAS` (por omisión `test-results/plataforma-identidad`).
 */

const CAPTURAS = process.env.E2E_CAPTURAS ?? join('test-results', 'plataforma-identidad');
mkdirSync(CAPTURAS, { recursive: true });

/** Captura de escritorio y, después, del mismo estado en un teléfono (375 × 812): lo que no cabe o queda volcado a un lado se ve ahí. */
async function capturar(page: Page, nombre: string): Promise<void> {
  const original = page.viewportSize();
  await page.screenshot({ path: join(CAPTURAS, `${nombre}.png`), fullPage: true });
  await page.setViewportSize({ width: 375, height: 812 });
  await page.waitForTimeout(250);
  await page.screenshot({ path: join(CAPTURAS, `${nombre}-movil.png`), fullPage: true });
  if (original) await page.setViewportSize(original);
}

const TEXTO_ASUNTO_CODIGO = 'Tu código para ingresar a Validasoft';
const ASUNTO_CODIGO = new RegExp(TEXTO_ASUNTO_CODIGO);
const barraDeSecciones = (page: Page) => page.getByRole('navigation', { name: 'Secciones' });

async function autenticadorVirtual(page: Page): Promise<{ cdp: CDPSession; id: string }> {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('WebAuthn.enable');
  const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', {
    options: { protocol: 'ctap2', transport: 'internal', hasResidentKey: true, hasUserVerification: true, isUserVerified: true, automaticPresenceSimulation: true },
  });
  return { cdp, id: authenticatorId };
}

async function entrarConCodigo(page: Page, correo: string): Promise<void> {
  const antes = await fetch(`${process.env.E2E_MAILPIT ?? 'http://localhost:8025'}/api/v1/search?query=${encodeURIComponent(`to:"${correo}"`)}`)
    .then((r) => r.json() as Promise<{ messages: { ID: string; Subject: string }[] }>)
    .then((r) => r.messages.filter((m) => m.Subject.includes(TEXTO_ASUNTO_CODIGO)).map((m) => m.ID));
  await page.getByLabel('Correo').fill(correo);
  await page.getByRole('button', { name: 'Continuar' }).click();
  await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
  const correoDelCodigo = await esperarCorreo(correo, ASUNTO_CODIGO, { excluir: antes });
  await page.keyboard.type(codigo(correoDelCodigo));
}

interface Grupo {
  admin: string;
  empresaId: string;
  correoAdmin: string;
  tenantId: string;
  slug: string;
  nombre: string;
}

/** Un grupo con ingreso directo, su primera empresa y un administrador de todo el grupo (sin passkey aún). */
async function crearGrupoDirecto(): Promise<Grupo> {
  const sufijo = `${Date.now().toString(36)}${randomUUID().slice(0, 4)}`;
  const correoAdmin = `ana.${sufijo}@e2e.validasoft.co`;
  const slug = `pi-${sufijo}`;
  const nombre = `Grupo Identidad ${sufijo}`;
  const creado = await llamar<{ tenant: { id: string }; apiKeyAdministrador: { valor: string } }>('POST', '/api/v1/plataforma/tenants', {
    llave: CLAVE_PLATAFORMA,
    cuerpo: { slug, nombre, region: 'co', zonaHoraria: 'America/Bogota', ingreso: { modos: ['Directo'], sesionHoras: 8, inactividadMinutos: 30, passkeyParaTodos: false } },
  });
  const admin = creado.apiKeyAdministrador.valor;
  const empresa = await llamar<{ id: string }>('POST', '/api/v1/empresas', {
    llave: admin,
    cuerpo: { identificacion: nitAleatorio(), nombre: 'Constructora Norte S.A.S.', prefijoFolio: `P${sufijo.slice(-5).toUpperCase()}`, logoUrl: null, replyTo: null },
  });
  await llamar('POST', '/api/v1/usuarios', {
    llave: admin,
    cuerpo: { nombre: 'Ana Administradora', correo: correoAdmin, identificacion: null, roles: [{ rol: 'Administrador', empresaId: null }], identidades: null },
  });
  return { admin, empresaId: empresa.id, correoAdmin, tenantId: creado.tenant.id, slug, nombre };
}

/** La primera entrada de una administradora: código y registro de su primera passkey. */
async function entrarYRegistrarPasskey(page: Page, g: Grupo): Promise<{ cdp: CDPSession; id: string }> {
  const virtual = await autenticadorVirtual(page);
  await page.goto('/app/');
  await entrarConCodigo(page, g.correoAdmin);
  await expect(page.getByRole('heading', { name: 'Registra tu passkey' })).toBeVisible();
  await page.getByRole('button', { name: 'Registrar passkey' }).click();
  await expect(barraDeSecciones(page)).toBeVisible();
  return virtual;
}

/** Si la acción pidió confirmar con la passkey (step-up), la confirma: el cliente de la API repite la petición solo. */
async function confirmarConPasskey(page: Page): Promise<void> {
  const dialogo = page.getByRole('dialog', { name: 'Confirma que eres tú' });
  await expect(dialogo).toBeVisible();
  await dialogo.getByRole('button', { name: 'Usar mi passkey' }).click();
  await expect(dialogo).toBeHidden();
}

test('Plataforma: uso de IA con su tarifa, ingresos de los operadores y la clave de arranque', async ({ page }) => {
  const consola = await abrirConsolaDePlataforma(page);

  // Uso de IA: el mes, los totales y la tarifa del adicional (que se cambia).
  await consola.getByRole('link', { name: 'Uso de IA' }).click();
  await expect(consola.getByRole('heading', { level: 1, name: 'Uso de IA' })).toBeVisible();
  await expect(consola.getByText(/Total de /)).toBeVisible();
  await expect(consola.getByText('por validación adicional')).toBeVisible();
  await capturar(page, 'plataforma-uso-ia');
  await consola.getByRole('button', { name: 'Cambiar' }).click();
  const tarifa = consola.getByRole('dialog', { name: 'Cambiar la tarifa general' });
  await tarifa.getByLabel('Precio').fill('1750');
  await capturar(page, 'plataforma-uso-ia-tarifa');
  await tarifa.getByRole('button', { name: 'Guardar' }).click();
  await expect(consola.getByText('Guardaste la tarifa.')).toBeVisible();
  await expect(consola.getByText(/1\.750/).first()).toBeVisible();

  // Ingresos de los operadores: la tabla y un filtro que viaja a la URL.
  await consola.getByRole('link', { name: 'Ingresos' }).click();
  await expect(consola.getByRole('heading', { level: 1, name: 'Ingresos de los operadores' })).toBeVisible();
  await expect(consola.getByRole('table', { name: 'Ingresos de los operadores' })).toBeVisible();
  await capturar(page, 'plataforma-ingresos');
  await consola.getByRole('button', { name: /Resultado/ }).click();
  await consola.getByRole('menuitem', { name: 'Fallido' }).click();
  await expect(consola.getByRole('button', { name: 'Limpiar filtros' })).toBeVisible();
  await consola.getByRole('button', { name: 'Limpiar filtros' }).click();

  // Secretos: la clave de arranque (no se desactiva aquí: las demás pruebas dependen de ella).
  await consola.getByRole('link', { name: 'Secretos' }).click();
  await expect(consola.getByRole('heading', { name: 'Clave de arranque' })).toBeVisible();
  await expect(consola.getByText('Activa', { exact: true })).toBeVisible();
  await expect(consola.getByText(/con passkey$/)).toBeVisible();
  await capturar(page, 'plataforma-clave-de-arranque');
});

test('Plataforma: agregar una empresa a un grupo y desactivar y reactivar uno de sus sistemas', async ({ page }) => {
  const g = await crearGrupoDirecto();
  const claves = clavesDeSistema();
  const sistema = await llamar<{ id: string }>('POST', '/api/v1/sistemas', {
    llave: g.admin,
    cuerpo: { codigo: 'ERP', nombre: 'ERP de Andina', clavePublicaPem: claves.publica, origenesPermitidos: [] },
  });
  const estadoDelSistema = async () =>
    (await llamar<{ id: string; activo: boolean }[]>('GET', `/api/v1/plataforma/tenants/${g.tenantId}/sistemas`, { llave: CLAVE_PLATAFORMA })).find((s) => s.id === sistema.id)?.activo;

  const consola = await abrirConsolaDePlataforma(page);
  await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(g.slug);
  await expect(consola.getByRole('region', { name: g.nombre })).toBeVisible();

  // Agregar una empresa: el mismo formulario del grupo, ya para ese grupo.
  const nit = nitAleatorio();
  await consola.getByRole('button', { name: `Agregar una empresa a ${g.nombre}` }).click();
  const dialogo = consola.getByRole('dialog', { name: /Agregar empresa/ });
  await expect(dialogo.getByText(`Para el grupo ${g.nombre}`)).toBeVisible();
  await dialogo.getByRole('textbox', { name: /Número de identificación/ }).fill(nit.numero);
  await dialogo.getByRole('textbox', { name: /Razón social/ }).fill('Obras del Sur S.A.S.');
  await dialogo.getByRole('textbox', { name: /Prefijo de folio/ }).fill(`S${randomUUID().replace(/-/g, '').slice(0, 5).toUpperCase()}`);
  await capturar(page, 'plataforma-agregar-empresa');
  await dialogo.getByRole('button', { name: 'Agregar empresa' }).click();
  await expect(dialogo).toBeHidden();
  await expect(consola.getByRole('region', { name: g.nombre }).getByText('Obras del Sur S.A.S.')).toBeVisible();
  const empresas = await llamar<{ id: string; nombre: string }[]>('GET', '/api/v1/empresas', { llave: g.admin });
  expect(empresas.some((e) => e.nombre === 'Obras del Sur S.A.S.')).toBe(true);

  // Sistemas del grupo: desactivar (revoca sus llaves y sesiones) y reactivar.
  await consola.getByRole('button', { name: `Ver los sistemas de ${g.nombre}` }).click();
  const lista = consola.getByRole('dialog', { name: /Sistemas integrados/ });
  await expect(lista.getByText('ERP de Andina')).toBeVisible();
  await capturar(page, 'plataforma-sistemas-del-grupo');
  await lista.getByRole('button', { name: 'Desactivar ERP de Andina' }).click();
  await consola.getByRole('dialog', { name: /^Desactivar ERP de Andina/ }).getByRole('button', { name: 'Desactivar' }).click();
  await expect(consola.getByText('ERP de Andina quedó desactivado.')).toBeVisible();
  await expect.poll(estadoDelSistema).toBe(false);
  await lista.getByRole('button', { name: 'Reactivar ERP de Andina' }).click();
  await consola.getByRole('dialog', { name: /^Reactivar ERP de Andina/ }).getByRole('button', { name: 'Reactivar' }).click();
  await expect(consola.getByText('ERP de Andina está activo de nuevo.')).toBeVisible();
  await expect.poll(estadoDelSistema).toBe(true);
});

test('Mi cuenta: generar códigos de recuperación pide la passkey y uno de ellos devuelve el acceso sin ella', async ({ page }) => {
  const g = await crearGrupoDirecto();
  const virtual = await entrarYRegistrarPasskey(page, g);

  await page.goto(`/app/#/e/${g.empresaId}/mi-cuenta`);
  await expect(page.getByRole('heading', { name: 'Códigos de recuperación' })).toBeVisible();
  await expect(page.getByText('Aún no tienes códigos de recuperación.')).toBeVisible();
  await capturar(page, 'mi-cuenta-codigos-de-recuperacion');

  // Generarlos es una acción sensible: pide confirmar con la passkey y después los muestra una sola vez.
  await page.getByRole('button', { name: 'Generar códigos' }).click();
  await page.getByRole('dialog', { name: 'Generar códigos de recuperación' }).getByRole('button', { name: 'Generar códigos' }).click();
  await capturar(page, 'reautenticacion-passkey');
  await confirmarConPasskey(page);
  const codigos = page.getByRole('dialog', { name: 'Tus códigos de recuperación' });
  await expect(codigos).toBeVisible();
  const lista = (await codigos.getByRole('listitem').allTextContents()).map((c) => c.trim());
  expect(lista).toHaveLength(10);
  for (const c of lista) expect(c).toMatch(/^[A-Z0-9]{5}-[A-Z0-9]{5}$/);
  await capturar(page, 'mi-cuenta-codigos-generados');
  await expect(codigos.getByRole('button', { name: 'Listo' })).toBeDisabled();
  await codigos.getByLabel('Ya los guardé en un lugar seguro').check();
  await codigos.getByRole('button', { name: 'Listo' }).click();
  await expect(page.getByText(/Te quedan 10 de 10 códigos sin usar/)).toBeVisible();
  // Una vez cerrados, los códigos no vuelven a verse.
  await expect(page.getByText(lista[0] ?? 'x')).toHaveCount(0);

  // Pierde la passkey: sale, pide el código del correo y, en el paso de la passkey, usa un código de recuperación.
  await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
  await page.getByRole('menuitem', { name: 'Cerrar sesión' }).click();
  await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
  await virtual.cdp.send('WebAuthn.clearCredentials', { authenticatorId: virtual.id });
  await entrarConCodigo(page, g.correoAdmin);
  await expect(page.getByRole('heading', { name: 'Confirma que eres tú' })).toBeVisible();
  await page.getByRole('button', { name: 'No tengo mi passkey' }).click();
  await expect(page.getByRole('heading', { name: 'Usa un código de recuperación' })).toBeVisible();
  await capturar(page, 'ingreso-codigo-de-recuperacion');
  await page.getByLabel('Código de recuperación').fill(lista[0]?.toLowerCase().replace('-', '') ?? '');
  await expect(page.getByLabel('Código de recuperación')).toHaveValue(lista[0] ?? '');
  await page.getByRole('button', { name: 'Usar el código' }).click();

  // El código no abre la sesión: lleva a registrar una passkey nueva.
  await expect(page.getByRole('heading', { name: 'Registra tu passkey' })).toBeVisible();
  await page.getByRole('button', { name: 'Registrar passkey' }).click();
  await expect(barraDeSecciones(page)).toBeVisible();
  await page.goto(`/app/#/e/${g.empresaId}/mi-cuenta`);
  await expect(page.getByText(/Te quedan 9 de 10 códigos sin usar/)).toBeVisible();

  // Y el mismo código no sirve dos veces.
  await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
  await page.getByRole('menuitem', { name: 'Cerrar sesión' }).click();
  await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
  await virtual.cdp.send('WebAuthn.clearCredentials', { authenticatorId: virtual.id });
  await entrarConCodigo(page, g.correoAdmin);
  await expect(page.getByRole('heading', { name: 'Confirma que eres tú' })).toBeVisible();
  await page.getByRole('button', { name: 'No tengo mi passkey' }).click();
  await page.getByLabel('Código de recuperación').fill(lista[0] ?? '');
  await page.getByRole('button', { name: 'Usar el código' }).click();
  await expect(page.getByRole('alert')).toContainText('código de recuperación');
});

test('Configuración › Inicio de sesión único: el secreto no vuelve, el dominio pide verificarse y guardar pide la passkey', async ({ page }) => {
  const g = await crearGrupoDirecto();
  await entrarYRegistrarPasskey(page, g);
  const sufijo = randomUUID().replace(/-/g, '').slice(0, 6);

  await page.goto(`/app/#/e/${g.empresaId}/sso`);
  await expect(page.getByRole('heading', { level: 1, name: 'Inicio de sesión único' })).toBeVisible();
  await expect(page.getByText('Sin configurar')).toBeVisible();
  await capturar(page, 'configuracion-sso-vacia');

  await page.getByLabel('Nombre del botón').fill('Andina');
  await page.getByLabel('Emisor (issuer)').fill('http://localhost:9/v2.0');
  await page.getByLabel('Client ID').fill('validasoft-e2e');
  await page.getByRole('button', { name: 'Escribir secreto' }).click();
  await page.getByLabel('Secreto del cliente').fill('secreto-e2e-12345');
  await page.getByLabel('Dominio', { exact: true }).fill(`sso-${sufijo}.e2e.validasoft.co`);
  // El botón «Agregar» tiene el mismo alto que su campo (no se estira con la etiqueta del campo).
  const cajaDelCampo = await page.getByLabel('Dominio', { exact: true }).locator('xpath=ancestor::div[contains(@class, "MuiOutlinedInput-root")]').boundingBox();
  const cajaDelBoton = await page.getByRole('button', { name: 'Agregar', exact: true }).boundingBox();
  expect(Math.abs((cajaDelBoton?.height ?? 0) - (cajaDelCampo?.height ?? 99))).toBeLessThanOrEqual(2);
  await page.getByRole('button', { name: 'Agregar', exact: true }).click();
  // Agregar un dominio puede pedir confirmar con la passkey (configurar el SSO es una acción sensible).
  if (await page.getByRole('dialog', { name: 'Confirma que eres tú' }).isVisible({ timeout: 2_000 }).catch(() => false)) await confirmarConPasskey(page);
  await expect(page.getByText(`@sso-${sufijo}.e2e.validasoft.co`)).toBeVisible();
  await expect(page.getByText('Por verificar')).toBeVisible();
  await expect(page.getByText(/validasoft-verificacion=/)).toBeVisible();
  await capturar(page, 'configuracion-sso-dominio');

  await page.getByRole('button', { name: 'Guardar' }).click();
  await confirmarConPasskey(page);
  await expect(page.getByText('Guardaste el inicio de sesión único.')).toBeVisible();

  // Al recargar solo se ve con qué cuatro caracteres termina el secreto; el secreto nunca vuelve.
  await page.reload();
  await expect(page.getByText('Secreto guardado, termina en 2345')).toBeVisible();
  await expect(page.getByText('secreto-e2e-12345')).toHaveCount(0);
  await expect(page.getByLabel('Secreto del cliente')).toHaveCount(0);
  await expect(page.getByText('Apagado')).toBeVisible();
  await capturar(page, 'configuracion-sso-guardada');
});
