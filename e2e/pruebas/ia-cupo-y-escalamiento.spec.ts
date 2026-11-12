import { expect, test, type Browser, type FrameLocator } from '@playwright/test';
import { llamar } from '../soporte/api';
import { pdf } from '../soporte/archivos';
import { capturar } from '../soporte/capturas';
import { esperarCorreo } from '../soporte/correo';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion, regionDeArea } from '../soporte/empresa';
import { completarInvitacion, completarInvitacionYEsperarRevisionOAprobacion, esperarEstadoDelTercero } from '../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha, type Escenario, type Tercero } from '../soporte/semilla';

/**
 * IA con el proveedor simulado (ADR-0012, IA-1, IA-4, IA-6): lo que pasa cuando la empresa agota su cupo de validaciones y cuando un
 * documento dudoso escala al modelo superior. Todo por pantalla: el tercero sube sus documentos, la revisora decide, la administradora
 * ve el consumo. El analizador simulado lee marcas `[[clave=valor]]` del PDF (`confianza` por omisión 0,97) y nunca sale a OpenAI.
 */

const RUT = 'Registro Único Tributario';
const BANCARIA = 'Certificación bancaria';

/** PDF que lee el analizador simulado con los datos del tercero; `marcas` agrega, p. ej., `[[confianza=0.55]]`. */
function rut(tercero: Tercero, marcas = '') {
  return pdf(`RUT ${tercero.nombre} [[nit=${tercero.identificacion.numero}]][[razonSocial=${tercero.nombre}]]${marcas}`);
}

async function paginaDe(browser: Browser) {
  return (await browser.newContext()).newPage();
}

/** La tarjeta de un documento en el expediente que ve la revisora. */
function tarjetaDeDocumento(app: FrameLocator, documento: string) {
  return app.getByRole('listitem').filter({ has: app.getByRole('heading', { level: 4, name: documento, exact: true }) }).first();
}

/** La tarjeta de una cifra de «Plan y uso de IA» por su título. */
function cifra(app: FrameLocator, titulo: string) {
  return app.getByRole('region', { name: titulo, exact: true });
}

/** Va a Excepciones pasando antes por otra sección, para que la lista se monte de nuevo y se pida otra vez. */
async function abrirExcepciones(app: FrameLocator): Promise<void> {
  await irASeccion(app, 'Vínculos');
  const barra = app.getByRole('navigation', { name: 'Secciones' });
  const pestana = barra.getByRole('link', { name: /^Excepciones/ });
  if ((await pestana.count()) > 0) await pestana.click();
  else {
    await barra.locator('button[aria-haspopup="menu"]').click();
    await app.getByRole('menuitem', { name: /^Excepciones/ }).click();
  }
  await expect(app.getByRole('heading', { level: 1, name: 'Excepciones' })).toBeVisible();
}

function excepcionDe(app: FrameLocator, tercero: Tercero) {
  return app.getByRole('listitem').filter({ has: app.getByRole('heading', { level: 3, name: tercero.nombre }) });
}

async function esperarExcepcion(app: FrameLocator, tercero: Tercero): Promise<void> {
  await expect(async () => {
    await abrirExcepciones(app);
    await expect(excepcionDe(app, tercero)).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 60_000 });
}

async function configurarRevision(e: Escenario, cuerpo: Record<string, unknown>): Promise<void> {
  await llamar('PUT', '/api/v1/configuracion/revision-automatica', { llave: e.admin, empresa: e.sur.id, cuerpo });
}

test('al agotar el cupo de IA el documento nuevo cae a una persona de inmediato, con el aviso a la administradora y el consumo en 100 %', async ({ browser }) => {
  test.setTimeout(360_000);
  const e = await crearEscenario();
  // Una sola validación incluida al mes y sin adicional: el primer tercero la consume; el segundo se queda sin cupo.
  await activarIa(e, e.sur, 1);
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_CUPO', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }]);
  // En Automático lo limpio se aprueba solo: si el cupo no cortara el paso, el segundo RUT también se aprobaría sin que nadie lo viera.
  await configurarRevision(e, {
    nivel: 'Automatico',
    umbralConfianza: 0.95,
    porcentajeMuestreo: 0,
    topeMensualAprobaciones: null,
    tiposAutorizados: ['RUT'],
    areasAutorizadas: [e.sur.areas.revision!.id],
    esperaIaMinutos: 10,
  });

  // Antes de usarlo, la administradora ve el cupo entero.
  const paginaAna = await paginaDe(browser);
  const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'uso-ia');
  await expect(ana.getByRole('heading', { level: 1, name: 'Plan y uso de IA' })).toBeVisible();
  await expect(cifra(ana, 'Validaciones incluidas')).toContainText('0 de 1');
  await expect(cifra(ana, 'Validaciones incluidas')).toContainText('Queda 1 este mes');
  await expect(cifra(ana, 'Validaciones adicionales')).toContainText('Sin adicional');

  // El primer tercero sube su RUT: la IA lo analiza con la única validación y, como no tiene excepciones, el sistema lo aprueba.
  const primero = nuevoTercero('Aceros Primero S.A.S.');
  const paginaPrimero = await paginaDe(browser);
  await completarInvitacionYEsperarRevisionOAprobacion(paginaPrimero, await invitar(e, e.sur, primero, 'CLIENTE'), [{ nombre: RUT, archivo: rut(primero) }]);
  await esperarEstadoDelTercero(paginaPrimero, 'Aprobado');

  // El segundo ya no tiene cupo: su RUT llega a revisión sin análisis y nada se aprueba solo.
  const segundo = nuevoTercero('Aceros Segundo S.A.S.');
  const paginaSegundo = await paginaDe(browser);
  await completarInvitacion(paginaSegundo, await invitar(e, e.sur, segundo, 'CLIENTE'), [{ nombre: RUT, archivo: rut(segundo) }]);

  // Sofía lo ve en Excepciones con el motivo en español, sin esperar los diez minutos del análisis que ya no va a llegar.
  const paginaSofia = await paginaDe(browser);
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur, 'excepciones');
  await esperarExcepcion(sofia, segundo);
  const excepcion = excepcionDe(sofia, segundo);
  await expect(excepcion).toContainText('Excepción');
  await expect(excepcion.getByRole('list', { name: 'Motivos' })).toContainText('Sin análisis de IA');
  await expect(excepcionDe(sofia, primero)).toHaveCount(0);
  await capturar(paginaSofia, 'ia-cupo-excepcion');

  // En el expediente, el documento dice que se revisa a mano y el área lo decide como siempre.
  await excepcion.getByRole('link', { name: /^Abrir el expediente/ }).click();
  await expect(sofia.getByRole('heading', { level: 1, name: segundo.nombre })).toBeVisible();
  const documento = tarjetaDeDocumento(sofia, RUT);
  await expect(documento).toContainText('Sin análisis automático: revísalo manualmente.');
  await expect(documento.getByRole('region', { name: 'Revisión automática', exact: true })).toHaveCount(0);
  await expect(sofia.getByRole('region', { name: 'Decisión automática' })).toContainText('Requiere una persona');
  await expect(sofia.getByRole('region', { name: 'Decisión automática' })).toContainText('Sin análisis de IA');
  await aprobarArea(sofia, 'Revisión');
  await esperarEstadoDelTercero(paginaSegundo, 'Aprobado');
  // El tercero no ve nada de la IA de la empresa: ni la confianza ni el modelo.
  await expect(paginaSegundo.getByText(/Confianza|gpt-6/)).toHaveCount(0);

  // La administradora ve el cupo agotado: 1 de 1, sin adicional y el consumo del único análisis pagado.
  await paginaAna.reload();
  const incluidas = cifra(ana, 'Validaciones incluidas');
  await expect(incluidas).toContainText('1 de 1');
  await expect(incluidas).toContainText('Agotaste las incluidas de este mes.');
  await expect(incluidas.getByRole('progressbar', { name: /^1 de 1 validaciones incluidas usadas \(100\s?%\)$/ })).toHaveAttribute('data-nivel', 'agotado');
  await expect(cifra(ana, 'Análisis pagados').getByText('1', { exact: true })).toBeVisible();
  await expect(ana.getByText('Al agotar las incluidas, los documentos nuevos pasan a revisión manual.')).toBeVisible();
  await capturar(paginaAna, 'ia-cupo-agotado');

  // Y le avisaron: campana y correo, claros sobre lo que pasa ahora.
  const correo = await esperarCorreo(e.ana.correo, /^Validaciones con IA agotadas: /);
  expect(correo.texto).toContain('usó 1 de 1 validaciones con IA incluidas');
  expect(correo.texto).toContain('Los documentos nuevos quedan sin análisis automático, para revisión humana, hasta el próximo mes.');
  const inicio = await abrirEmbebido(await paginaDe(browser), e, e.ana, e.sur);
  await inicio.getByRole('button', { name: /^Notificaciones, \d+ sin leer/ }).click();
  const aviso = inicio.getByText('Validaciones con IA agotadas').first();
  await expect(aviso).toBeVisible();
  await aviso.click();
  await expect(inicio.getByRole('heading', { level: 1, name: 'Plan y uso de IA' })).toBeVisible();
});

test('un documento dudoso escala al modelo superior: la revisora ve la confianza y el escalamiento, y la administradora ve el costo de las dos llamadas', async ({ browser }) => {
  test.setTimeout(300_000);
  const e = await crearEscenario();
  await activarIa(e, e.sur);
  await publicarFlujoConFicha(e, e.sur, 'CLIENTES_ESC', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { tipoDocumento: 'CERTIFICACION_BANCARIA' }]);
  // Asistido: la IA no decide, solo sugiere; así se ve qué hace con lo dudoso y con lo limpio sin que el sistema apruebe nada.
  await configurarRevision(e, { nivel: 'Asistido' });

  // El RUT llega con una lectura dudosa (0,55) y la certificación, limpia (0,97).
  const tercero = nuevoTercero('Aceros Escalados S.A.S.');
  const paginaTercero = await paginaDe(browser);
  await completarInvitacion(paginaTercero, await invitar(e, e.sur, tercero, 'CLIENTE'), [
    { nombre: RUT, archivo: rut(tercero, '[[confianza=0.55]]') },
    { nombre: BANCARIA, archivo: pdf(`Certificacion bancaria ${tercero.nombre}`) },
  ]);

  const paginaSofia = await paginaDe(browser);
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  // El análisis y la sugerencia corren en segundo plano: se vuelve a abrir el expediente hasta que traiga las dos sugerencias.
  await expect(async () => {
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await expect(tarjetaDeDocumento(sofia, RUT).getByRole('region', { name: 'Decisión automática' })).toContainText('La IA sugiere revisar', { timeout: 1500 });
    await expect(tarjetaDeDocumento(sofia, BANCARIA).getByRole('region', { name: 'Decisión automática' })).toContainText('La IA sugiere aprobar', { timeout: 1500 });
  }).toPass({ timeout: 60_000 });

  // El RUT dudoso se revisó con un modelo de mayor capacidad y sigue siendo dudoso: la persona lo decide.
  const panelRut = tarjetaDeDocumento(sofia, RUT).getByRole('region', { name: 'Revisión automática', exact: true });
  await expect(panelRut).toContainText(/Confianza 55\s?%/);
  await expect(panelRut).toContainText('Revisado por un modelo de mayor capacidad');
  await expect(panelRut).toContainText('gpt-6.1-terra');
  await expect(tarjetaDeDocumento(sofia, RUT).getByRole('region', { name: 'Decisión automática' })).toContainText('Confianza baja');

  // La certificación limpia la resolvió el modelo por omisión, sin escalar.
  const panelBancaria = tarjetaDeDocumento(sofia, BANCARIA).getByRole('region', { name: 'Revisión automática', exact: true });
  await expect(panelBancaria).toContainText(/Confianza 97\s?%/);
  await expect(panelBancaria).toContainText('gpt-6-luna');
  await expect(panelBancaria).not.toContainText('Revisado por un modelo de mayor capacidad');
  await expect(regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ })).toBeVisible();
  await capturar(paginaSofia, 'ia-escalamiento-expediente');

  // El tercero no ve la confianza, el modelo ni el escalamiento: solo la empresa que recibió el análisis.
  await esperarEstadoDelTercero(paginaTercero, 'EnRevision');
  await expect(paginaTercero.getByText(/Confianza|gpt-6|modelo de mayor capacidad/)).toHaveCount(0);

  // En el consumo: tres llamadas (la de la certificación y las dos del RUT) pero solo dos análisis pagados y dos validaciones consumidas.
  const ana = await abrirEmbebido(await paginaDe(browser), e, e.ana, e.sur, 'uso-ia');
  await expect(ana.getByRole('heading', { level: 1, name: 'Plan y uso de IA' })).toBeVisible();
  await expect(async () => {
    await ana.owner().page().reload();
    await expect(cifra(ana, 'Validaciones incluidas')).toContainText('2 de 20', { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await expect(cifra(ana, 'Análisis pagados')).toContainText('2');
  // El simulado gasta 1.000 tokens de entrada y 300 de salida por llamada: 3 llamadas.
  await expect(cifra(ana, 'Tokens')).toContainText('3.000');
  await expect(cifra(ana, 'Tokens')).toContainText('900');
  // Costo real: dos llamadas del modelo por omisión (US$ 0,00025 cada una) y una del superior (US$ 0,0025).
  await expect(cifra(ana, 'Costo real')).toContainText(/0,0030/);
  await capturar(ana.owner().page(), 'ia-escalamiento-consumo');
});
