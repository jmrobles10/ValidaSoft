import { expect, test, type FrameLocator } from '@playwright/test';
import { join } from 'node:path';
import { ErrorDeApi, llamar, nitAleatorio } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { capturar } from '../soporte/capturas';
import { esperarCorreo } from '../soporte/correo';
import { abrirEmbebidoConLlave, llaveConHabeas } from '../soporte/derechos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirConsolaDePlataforma } from '../soporte/plataforma';
import { completarInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../soporte/semilla';

/**
 * Derechos del tercero, plan del grupo y correo de la empresa (OD-1…OD-22): el tercero se lleva una copia firmada de su bóveda y ejerce
 * sus derechos de habeas data ante una empresa o ante la plataforma; Plataforma fija el plan y los límites de un grupo; y una empresa
 * saca su correo desde su propio dominio una vez verificado.
 */

/**
 * Fotografía una pantalla embebida de la web: desde su inicio, en claro y en celular (el contenido de la web tiene su propio scroll, y en
 * el celular la barra de Configuración ocupa toda la pantalla: la captura `-celular-contenido` baja hasta el título de la página).
 */
async function fotografiar(marco: FrameLocator, nombre: string): Promise<void> {
  const carpeta = process.env.E2E_CAPTURAS;
  if (!carpeta) return;
  const pagina = marco.owner().page();
  await marco.locator('body').evaluate(() => {
    document.scrollingElement?.scrollTo(0, 0);
    document.querySelector('main')?.scrollTo(0, 0);
  });
  await capturar(pagina, nombre);
  const original = pagina.viewportSize();
  await pagina.setViewportSize({ width: 390, height: 844 });
  await marco.getByRole('heading', { level: 1 }).evaluate((titulo) => titulo.scrollIntoView());
  await pagina.screenshot({ path: join(carpeta, `${nombre}-celular-contenido.png`) });
  if (original) await pagina.setViewportSize(original);
}

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

test('el tercero exporta su bóveda firmada y le pide a la empresa conocer sus datos; la administradora responde dentro del plazo', async ({ page, browser }) => {
  const e = await crearEscenario();
  const llave = await llaveConHabeas(e);
  const tercero = nuevoTercero('Aceros del Sur S.A.S.');
  const invitacion = await invitar(e, e.norte, tercero);
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Aceros del Sur') },
    { nombre: CAMARA, archivo: pdf('Camara Aceros del Sur') },
  ]);

  // 1. «Privacidad y mis datos»: la exportación firmada y las solicitudes, juntas.
  await page.goto('/privacidad');
  await expect(page.getByRole('heading', { level: 1, name: 'Privacidad y mis datos' })).toBeVisible();
  await expect(page.getByText('Aún no has hecho solicitudes')).toBeVisible();
  await capturar(page, 'privacidad-tercero-vacia');

  // 2. La exportación se prepara en segundo plano y se baja con una URL prefirmada: un ZIP con el manifiesto y su firma.
  await page.getByRole('button', { name: 'Pedir exportación' }).click();
  await expect(page.getByText(/\d+ archivos · .* · disponible hasta/)).toBeVisible({ timeout: 90_000 });
  const descarga = page.waitForEvent('download');
  await page.getByRole('button', { name: 'Descargar' }).click();
  const zip = await descarga;
  expect(zip.suggestedFilename()).toMatch(/\.zip$/);
  const ruta = await zip.path();
  const { readFileSync } = await import('node:fs');
  const bytes = readFileSync(ruta);
  expect(bytes.subarray(0, 2).toString('latin1')).toBe('PK');
  for (const nombre of ['manifiesto.json', 'manifiesto.firma.json', 'LEEME.txt']) expect(bytes.includes(Buffer.from(nombre))).toBe(true);
  await capturar(page, 'privacidad-tercero-exportacion');

  // 3. Le pide a Constructora Norte conocer sus datos: queda con su radicado y la fecha límite en días hábiles.
  await page.getByRole('button', { name: 'Nueva solicitud' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Nueva solicitud sobre mis datos' });
  await dialogo.getByRole('radio', { name: /Conocer mis datos/ }).check();
  await dialogo.getByRole('combobox', { name: 'Se la pides a' }).click();
  await page.getByRole('option', { name: e.norte.nombre }).click();
  await expect(dialogo.getByText(`${e.norte.nombre} recibe tu solicitud ahora y te responde en máximo 10 días hábiles`, { exact: false })).toBeVisible();
  await dialogo.getByRole('button', { name: 'Enviar solicitud' }).click();
  const tarjeta = page.getByRole('article').filter({ hasText: 'Conocer mis datos' });
  await expect(tarjeta).toBeVisible();
  await expect(tarjeta).toContainText(/HD-\d{4}-[0-9A-F]{8}/);
  await expect(tarjeta).toContainText(`Dirigida a ${e.norte.nombre}`);
  await expect(tarjeta).toContainText(/quedan 10 días hábiles|queda 1 día hábil|quedan \d+ días hábiles/);
  await capturar(page, 'privacidad-tercero-solicitud');
  const radicado = (await tarjeta.textContent())?.match(/HD-\d{4}-[0-9A-F]{8}/)?.[0] ?? '';

  // 4. La administradora la ve en Configuración › Habeas data con el titular y el plazo, y la responde.
  const ana = await abrirEmbebidoConLlave(await (await browser.newContext()).newPage(), e, llave, e.ana, e.norte, 'habeas-data');
  await expect(ana.getByRole('heading', { level: 1, name: 'Habeas data' })).toBeVisible();
  const solicitud = ana.getByRole('article').filter({ hasText: radicado });
  await expect(solicitud).toContainText(tercero.nombre);
  await expect(solicitud).toContainText(tercero.correo);
  await expect(solicitud).toContainText(/Responde antes del .*: quedan \d+ días hábiles\./);
  await fotografiar(ana, 'habeas-empresa');
  await solicitud.getByRole('button', { name: 'Responder' }).click();
  const responder = ana.getByRole('dialog', { name: `Responder la solicitud ${radicado}` });
  await expect(responder.getByText(`le llega al titular a ${tercero.correo}`, { exact: false })).toBeVisible();
  await responder.getByRole('button', { name: 'Responder al titular' }).click();
  await expect(responder.getByText('Escribe una respuesta de al menos 10 caracteres.')).toBeVisible();
  await responder.getByRole('textbox', { name: /Respuesta al titular/ }).fill('Tenemos tu RUT y tu cámara de comercio vigentes, y los datos de contacto de tu ficha.');
  await responder.getByRole('button', { name: 'Responder al titular' }).click();
  await expect(responder).toBeHidden();
  await expect(ana.getByText('No hay solicitudes abiertas')).toBeVisible();

  // 5. El titular recibe el acuse por correo y ve la respuesta con su constancia, sin datos personales.
  const correo = await esperarCorreo(tercero.correo, /Respondimos tu solicitud de/);
  expect(correo.texto).toContain(radicado);
  await page.reload();
  const atendida = page.getByRole('article').filter({ hasText: radicado });
  await expect(atendida).toContainText('Atendida');
  await expect(atendida).toContainText('Tenemos tu RUT y tu cámara de comercio vigentes');
  await atendida.getByRole('button', { name: 'Ver constancia' }).click();
  const constancia = page.getByRole('dialog', { name: `Constancia de la solicitud ${radicado}` });
  await expect(constancia).toContainText(radicado);
  await expect(constancia).toContainText(/[0-9a-f]{64}/);
  await capturar(page, 'privacidad-tercero-constancia');
});

test('una supresión ante la plataforma muestra antes qué se elimina y qué se conserva por ley, y el titular recibe la constancia', async ({ page }) => {
  const e = await crearEscenario();
  const tercero = nuevoTercero('Tornillos del Valle S.A.S.');
  const invitacion = await invitar(e, e.norte, tercero);
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Tornillos del Valle') },
    { nombre: CAMARA, archivo: pdf('Camara Tornillos del Valle') },
  ]);

  // El tercero pide suprimir todo; la plataforma responde por su bóveda.
  await page.goto('/privacidad');
  await page.getByRole('button', { name: 'Nueva solicitud' }).click();
  const dialogo = page.getByRole('dialog', { name: 'Nueva solicitud sobre mis datos' });
  await dialogo.getByRole('radio', { name: /Suprimir mis datos/ }).check();
  await expect(dialogo.getByText('la ley nos obliga a guardarlo', { exact: false })).toBeVisible();
  await dialogo.getByRole('button', { name: 'Enviar solicitud' }).click();
  const tarjeta = page.getByRole('article').filter({ hasText: 'Suprimir mis datos' });
  await expect(tarjeta).toBeVisible();
  const radicado = (await tarjeta.textContent())?.match(/HD-\d{4}-[0-9A-F]{8}/)?.[0] ?? '';
  expect(radicado).not.toBe('');

  // El operador de Plataforma la ve y, antes de ejecutarla, el plan: nada se borra hasta que confirma.
  const consola = await abrirConsolaDePlataforma(await (await page.context().browser()!.newContext()).newPage());
  await consola.getByRole('link', { name: 'Habeas data' }).click();
  await expect(consola.getByRole('heading', { level: 1, name: 'Habeas data' })).toBeVisible();
  const solicitud = consola.getByRole('article').filter({ hasText: radicado });
  await expect(solicitud).toContainText(tercero.nombre);
  await fotografiar(consola, 'habeas-plataforma');
  await solicitud.getByRole('button', { name: 'Responder' }).click();
  const responder = consola.getByRole('dialog', { name: `Responder la solicitud ${radicado}` });
  await expect(responder.getByText('Se eliminan')).toBeVisible();
  await expect(responder.getByText('Se conservan')).toBeVisible();
  await expect(responder.getByText(/lo que se elimina no se puede recuperar/)).toBeVisible();
  await capturar(consola.owner().page(), 'habeas-plataforma-supresion');
  await responder.getByRole('textbox', { name: /Respuesta al titular/ }).fill('Eliminamos lo que no estamos obligados a conservar y guardamos, por ley, lo que usó la empresa para aprobarte.');
  await responder.getByRole('button', { name: 'Suprimir y responder' }).click();
  await expect(responder).toBeHidden();

  // Al suprimir todo, también se cierran las sesiones y los enlaces del titular (el plan lo decía): su constancia le llega por correo,
  // con lo que se hizo y la huella para comprobarla; no lleva datos personales.
  const correo = await esperarCorreo(tercero.correo, /Respondimos tu solicitud de supresión/);
  expect(correo.texto).toContain(radicado);
  expect(correo.texto).toContain('la atendimos conservando lo que la ley nos obliga a guardar');
  expect(correo.texto).toMatch(/Constancia \(huella SHA-256\): [0-9a-f]{64}/);
  await page.reload();
  await expect(page.getByRole('heading', { level: 1, name: 'Entrar con mi correo' })).toBeVisible();
});

test('Plataforma fija el plan de un grupo: al llegar al límite no se crean más empresas y al ampliarlo se puede de nuevo', async ({ page, browser }) => {
  const e = await crearEscenario();

  // 1. El operador abre el plan del grupo desde su tarjeta y fija el límite de empresas en las que ya tiene.
  const consola = await abrirConsolaDePlataforma(page);
  await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(`Grupo E2E ${e.sufijo}`);
  await consola.getByRole('link', { name: `Plan y límites de Grupo E2E ${e.sufijo}` }).click();
  await expect(consola.getByRole('heading', { level: 1, name: 'Plan y límites' })).toBeVisible();
  await expect(consola.getByText('Este grupo aún no tiene plan', { exact: false })).toBeVisible();
  await fotografiar(consola, 'plan-plataforma-sin-plan');
  await consola.getByRole('textbox', { name: 'Código del plan' }).fill('empresarial');
  await consola.getByRole('textbox', { name: 'Empresas' }).fill('2');
  await consola.getByRole('textbox', { name: 'Usuarios' }).fill('5');
  await expect(consola.getByText('Al llegar a un límite solo se avisa', { exact: false })).toBeVisible();
  await consola.getByRole('switch', { name: 'Bloquear al llegar al límite' }).check();
  await expect(consola.getByText('Al llegar a un límite, el grupo no podrá crear más de eso', { exact: false })).toBeVisible();
  await consola.getByRole('button', { name: 'Guardar el plan' }).click();
  await expect(consola.getByText('Empresas', { exact: true }).first()).toBeVisible();
  await expect(consola.getByText('2 de 2', { exact: true })).toBeVisible();
  await expect(consola.getByText('En el límite', { exact: true }).first()).toBeVisible();
  await fotografiar(consola, 'plan-plataforma');

  // 2. El grupo ve su plan y su uso, y que está en el límite.
  const ana = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.norte, 'plan-grupo');
  await expect(ana.getByRole('heading', { level: 1, name: 'Plan del grupo' })).toBeVisible();
  await expect(ana.getByText('empresarial', { exact: true })).toBeVisible();
  await expect(ana.getByText('2 de 2', { exact: true })).toBeVisible();
  await expect(ana.getByText('Llegaste al límite de tu plan', { exact: false })).toBeVisible();
  await fotografiar(ana, 'plan-grupo-empresa');

  // 3. Con el bloqueo suave, una tercera empresa se rechaza con un mensaje que dice por qué.
  const tercera = { identificacion: nitAleatorio(), nombre: 'Constructora Oriente S.A.S.', prefijoFolio: `O${e.sufijo.slice(-5).toUpperCase()}`, logoUrl: null, replyTo: null };
  await expect(llamar('POST', '/api/v1/empresas', { llave: e.admin, cuerpo: tercera })).rejects.toMatchObject({ estado: 409, codigo: 'limite-del-plan-alcanzado' });

  // 4. Plataforma amplía el límite y la empresa ya se puede crear (lo que existe nunca dejó de funcionar).
  await consola.getByRole('textbox', { name: 'Empresas' }).fill('3');
  await consola.getByRole('button', { name: 'Guardar el plan' }).click();
  await expect(consola.getByText('2 de 3', { exact: true })).toBeVisible();
  const creada = await llamar<{ id: string }>('POST', '/api/v1/empresas', { llave: e.admin, cuerpo: tercera });
  expect(creada.id).not.toBe('');
  await expect(llamar('POST', '/api/v1/empresas', { llave: e.admin, cuerpo: { ...tercera, identificacion: nitAleatorio(), prefijoFolio: `P${e.sufijo.slice(-5).toUpperCase()}` } })).rejects.toBeInstanceOf(ErrorDeApi);
});

test('una empresa registra su dominio de correo: mientras falte un registro el correo sale de la plataforma y verificado sale desde su dominio', async ({ page }) => {
  const e = await crearEscenario();
  const dominioIncompleto = `dkim-pendiente-${e.sufijo}.co`;
  const dominio = `proveedores-${e.sufijo}.co`;

  const ana = await abrirEmbebido(page, e, e.ana, e.norte, 'dominio-correo');
  await expect(ana.getByRole('heading', { level: 1, name: 'Dominio de correo' })).toBeVisible();
  await expect(ana.getByText('sigue saliendo desde', { exact: false })).toBeVisible();
  await fotografiar(ana, 'dominio-correo-vacio');

  // 1. Un dominio al que le falta el DKIM: los registros que hay que crear y cuál falta.
  await ana.getByRole('textbox', { name: 'Dominio' }).fill(dominioIncompleto);
  await ana.getByRole('button', { name: 'Registrar el dominio' }).click();
  const registros = ana.getByRole('list', { name: 'Registros DNS' });
  await expect(registros.getByRole('listitem')).toHaveCount(3);
  await expect(ana.getByText('Pendiente de verificar', { exact: true })).toBeVisible();
  await ana.getByRole('button', { name: 'Verificar ahora' }).click();
  await expect(ana.getByText('Falta algún registro', { exact: true })).toBeVisible();
  await expect(registros.getByRole('listitem').filter({ has: ana.getByRole('heading', { name: 'DKIM', exact: true }) })).toContainText('No encontrado');
  await expect(registros.getByRole('listitem').filter({ has: ana.getByRole('heading', { name: 'SPF', exact: true }) })).toContainText('Correcto');
  await fotografiar(ana, 'dominio-correo-incompleto');

  // Mientras no esté verificado, la invitación sale con el remitente de la plataforma.
  const primero = nuevoTercero('Proveedor Uno S.A.S.');
  await invitar(e, e.norte, primero);
  const correoUno = await esperarCorreo(primero.correo, /te invita a validar tu vínculo/);
  expect(correoUno.desde).not.toContain(dominioIncompleto);

  // 2. Cambia al dominio bueno, lo verifica y el correo de la empresa sale desde su dominio.
  const campo = ana.getByRole('textbox', { name: 'Dominio' });
  await campo.fill(dominio);
  await ana.getByRole('textbox', { name: 'Nombre del remitente' }).fill('Compras Constructora Norte');
  await expect(ana.getByText(`Compras Constructora Norte <no-responder@${dominio}>`)).toBeVisible();
  await ana.getByRole('button', { name: 'Guardar los cambios' }).click();
  await expect(ana.getByText('Pendiente de verificar', { exact: true })).toBeVisible();
  await ana.getByRole('button', { name: 'Verificar ahora' }).click();
  await expect(ana.getByText('Verificado', { exact: true })).toBeVisible();
  await expect(ana.getByText('Es tu remitente propio', { exact: false })).toBeVisible();
  await fotografiar(ana, 'dominio-correo-verificado');

  const segundo = nuevoTercero('Proveedor Dos S.A.S.');
  await invitar(e, e.norte, segundo);
  const correoDos = await esperarCorreo(segundo.correo, /te invita a validar tu vínculo/);
  expect(correoDos.desde).toBe(`no-responder@${dominio}`);

  // 3. Quitarlo dice antes que el correo vuelve a salir de la plataforma, y así ocurre.
  await ana.getByRole('button', { name: 'Quitar el dominio' }).first().click();
  const confirmar = ana.getByRole('dialog', { name: `Quitar ${dominio}` });
  await expect(confirmar).toContainText('vuelve a salir desde');
  await confirmar.getByRole('button', { name: 'Quitar el dominio' }).click();
  await expect(ana.getByText('sigue saliendo desde', { exact: false })).toBeVisible();
  const tercero = nuevoTercero('Proveedor Tres S.A.S.');
  await invitar(e, e.norte, tercero);
  const correoTres = await esperarCorreo(tercero.correo, /te invita a validar tu vínculo/);
  expect(correoTres.desde).not.toContain(dominio);
});
