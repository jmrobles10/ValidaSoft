import { expect, test, type Browser, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja } from '../soporte/empresa';
import {
  abrirSeccion,
  agregarPersona,
  guardarSeccionCompleta,
  llenarEspecialidades,
  llenarFinanciera,
  llenarGeneralJuridica,
  llenarGeneralNatural,
  llenarSst,
  llenarTributaria,
  publicarFlujoDeFicha,
  REPRESENTANTE,
  SOCIO,
  soloPara,
} from '../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento, tarjeta } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevaPersonaNatural, nuevoTercero, type Escenario, type Invitacion } from '../soporte/semilla';

/**
 * El asistente «Mi información» del tercero según su tipo de persona (docs/14 §5, FD-8), de punta a punta y hasta enviar a revisión:
 * lo que la empresa le pide cambia (la cédula a la natural, la Cámara de comercio a la jurídica; Legal y Financiera solo a la jurídica),
 * los campos de la información general cambian (nombres, expedición y PEP frente a razón social) y la jurídica no completa la Legal sin
 * un socio. Cuando todo está completo el expediente pasa solo a «En revisión» y el área ve exactamente lo que se le pidió a ese tipo.
 */

const RUT = 'Registro Único Tributario';
const CEDULA = 'Cédula de ciudadanía';
const CAMARA = 'Certificado de existencia y representación legal';
const GENERAL = 'Información general';
const TRIBUTARIA = 'Información tributaria';
const LEGAL = 'Información legal';
/** La financiera va con su periodo en el encabezado («Información financiera 2025»). */
const FINANCIERA = /^Información financiera/;
const SST = 'Seguridad y salud en el trabajo';
const ESPECIALIDADES = 'Especialidades';
const SECCIONES_COMUNES = [GENERAL, TRIBUTARIA, SST, ESPECIALIDADES];

type Requisito = string | RegExp;

/** Encabezado de un requisito: el texto exacto o, para la financiera, el prefijo. */
const nombreDe = (requisito: Requisito) => ({ name: requisito, exact: typeof requisito === 'string' });

/** Grupo con un flujo de CLIENTE de Sur que pide, según el tipo de persona, documentos y las seis secciones de la ficha. */
async function escenarioConFlujoPorTipo(): Promise<Escenario> {
  const e = await crearEscenario();
  await publicarFlujoDeFicha(e, e.sur, 'CLIENTES_POR_TIPO', e.sur.areas.revision!, 'CLIENTE', [
    { tipoDocumento: 'RUT' },
    { tipoDocumento: 'CEDULA', condicion: soloPara('Natural') },
    { tipoDocumento: 'CAMARA_COMERCIO', condicion: soloPara('Juridica') },
    { seccion: 'FICHA_GENERAL' },
    { seccion: 'FICHA_TRIBUTARIA' },
    { seccion: 'FICHA_LEGAL' },
    { seccion: 'FICHA_FINANCIERA' },
    { seccion: 'FICHA_SST' },
    { seccion: 'FICHA_ESPECIALIDADES' },
  ]);
  return e;
}

/** Entra por el correo de la invitación, acepta y queda en «Tu revisión con …». */
async function aceptar(page: Page, invitacion: Invitacion): Promise<void> {
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);
}

/** Lo que se le pide en «Tu revisión con …»: está cada requisito esperado y ninguno de los que no le tocan. */
async function afirmarRequisitos(page: Page, pedidos: Requisito[], noPedidos: Requisito[]): Promise<void> {
  const tarjetaDe = (requisito: Requisito) => page.getByRole('listitem').filter({ has: page.getByRole('heading', nombreDe(requisito)) });
  for (const requisito of pedidos) await expect(tarjetaDe(requisito)).toBeVisible();
  for (const requisito of noPedidos) await expect(tarjetaDe(requisito)).toHaveCount(0);
}

/** Las secciones que lista «Mi información» (cada fila se nombra con la sección y, la financiera, su periodo). */
async function seccionesDelResumen(page: Page): Promise<string[]> {
  await page.goto('/ficha');
  await expect(page.getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
  const filas = page.getByRole('list', { name: 'Mi información' }).getByRole('listitem');
  await expect(filas.first()).toBeVisible();
  return filas.evaluateAll((nodos) => nodos.map((n) => n.getAttribute('aria-label') ?? ''));
}

/** El área de la empresa abre el expediente del tercero y ve solo los requisitos de su tipo (cada uno es un encabezado de nivel 4). */
async function afirmarLoQueVeLaEmpresa(browser: Browser, e: Escenario, nombre: string, pedidos: Requisito[], noPedidos: Requisito[]): Promise<void> {
  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await expect(async () => {
    await abrirDesdeBandeja(sofia, nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('heading', { level: 4, name: RUT })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  for (const requisito of pedidos) await expect(sofia.getByRole('heading', { level: 4, ...nombreDe(requisito) }).first()).toBeVisible();
  for (const requisito of noPedidos) await expect(sofia.getByRole('heading', { level: 4, ...nombreDe(requisito) })).toHaveCount(0);
}

// Flujos largos de punta a punta: con la máquina cargada el tope de 120 s por omisión queda justo.
test.setTimeout(180_000);

test('persona natural: se le pide la cédula y cuatro secciones (sin Legal ni Financiera), su general lleva nombres, expedición y PEP, y envía sin la Cámara de comercio', async ({
  page,
  browser,
}) => {
  const e = await escenarioConFlujoPorTipo();
  const tercero = nuevaPersonaNatural('Carlos Andrés Gómez Ruiz');
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  await aceptar(page, invitacion);

  // Lo que le pide la empresa a una persona natural: RUT, cédula y cuatro secciones.
  await afirmarRequisitos(page, [RUT, CEDULA, ...SECCIONES_COMUNES], [CAMARA, LEGAL, FINANCIERA]);

  // Su asistente: cuatro secciones, ninguna completa todavía; Legal y Financiera no le aplican y, si entra por la dirección, se lo dice.
  expect(await seccionesDelResumen(page)).toEqual(SECCIONES_COMUNES);
  await expect(page.getByText('0 de 4 secciones completas').first()).toBeVisible();
  await page.goto('/ficha/legal');
  await expect(page.getByRole('heading', { name: 'Esta sección no aplica para ti' })).toBeVisible();
  await page.goto('/ficha/financiera');
  await expect(page.getByRole('heading', { name: 'Esta sección no aplica para ti' })).toBeVisible();

  // La información general de la natural: nombres y apellidos prellenados (sin razón social), expedición del documento y PEP propio.
  await page.goto('/ficha/general');
  await expect(page.getByRole('heading', { level: 1, name: GENERAL })).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'Nombres', exact: true })).toHaveValue('Carlos Andrés');
  await expect(page.getByRole('textbox', { name: 'Apellidos', exact: true })).toHaveValue('Gómez Ruiz');
  await expect(page.getByRole('textbox', { name: 'Razón social', exact: true })).toHaveCount(0);
  await expect(page.getByRole('textbox', { name: 'Tipo de persona' })).toHaveValue('Persona natural');
  await expect(page.getByRole('radiogroup', { name: '¿Eres una persona expuesta políticamente (PEP)?' })).toBeVisible();
  await llenarGeneralNatural(page);
  await llenarTributaria(page);
  await llenarSst(page);
  await llenarEspecialidades(page);
  await page.goto('/ficha');
  await expect(page.getByText('Tu información está completa', { exact: true })).toBeVisible();
  await expect(page.getByText('4 de 4 secciones completas').first()).toBeVisible();

  // Faltan los dos documentos que le tocan: los sube y el expediente pasa solo a revisión (no hay un botón «Enviar»).
  await page.goto('/empresas');
  await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
  await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero}`));
  await subirDocumento(page, CEDULA, pdf(`Cedula ${tercero.identificacion.numero}`));
  await esperarEstadoDelTercero(page, 'EnRevision');
  for (const nombre of [RUT, CEDULA, ...SECCIONES_COMUNES]) await expect(tarjeta(page, nombre)).toBeVisible();

  // La empresa recibe exactamente lo pedido a una natural.
  await afirmarLoQueVeLaEmpresa(browser, e, tercero.nombre, [RUT, CEDULA, ...SECCIONES_COMUNES], [CAMARA, LEGAL, FINANCIERA]);
});

test('persona jurídica: se le pide la Cámara de comercio y las seis secciones, su general lleva razón social, la Legal exige un socio y envía sin la cédula', async ({
  page,
  browser,
}) => {
  const e = await escenarioConFlujoPorTipo();
  const tercero = nuevoTercero('Ferretería Andina S.A.S.');
  const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
  await aceptar(page, invitacion);

  // Lo que le pide la empresa a una jurídica: RUT, Cámara y las seis secciones.
  await afirmarRequisitos(page, [RUT, CAMARA, GENERAL, TRIBUTARIA, LEGAL, FINANCIERA, SST, ESPECIALIDADES], [CEDULA]);

  // Su asistente: las seis secciones (la financiera con su periodo).
  const secciones = await seccionesDelResumen(page);
  expect(secciones).toHaveLength(6);
  expect(secciones.slice(0, 3)).toEqual([GENERAL, TRIBUTARIA, LEGAL]);
  expect(secciones[3]).toMatch(FINANCIERA);
  expect(secciones.slice(4)).toEqual([SST, ESPECIALIDADES]);
  await expect(page.getByText('0 de 6 secciones completas').first()).toBeVisible();

  // La información general de la jurídica: razón social prellenada, sin nombres, expedición ni PEP propio.
  await page.goto('/ficha/general');
  await expect(page.getByRole('heading', { level: 1, name: GENERAL })).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'Razón social', exact: true })).toHaveValue(tercero.nombre);
  await expect(page.getByRole('textbox', { name: 'Nombres', exact: true })).toHaveCount(0);
  await expect(page.getByRole('radiogroup', { name: '¿Eres una persona expuesta políticamente (PEP)?' })).toHaveCount(0);
  await expect(page.getByRole('textbox', { name: 'Tipo de persona' })).toHaveValue('Persona jurídica');
  await llenarGeneralJuridica(page, tercero.nombre);
  await llenarTributaria(page);

  // La Legal no queda completa con solo el representante legal: pide al menos un socio; con el socio queda completa.
  await abrirSeccion(page, 'legal', LEGAL);
  await agregarPersona(page, REPRESENTANTE);
  const barra = page.getByRole('group', { name: 'Guardar la sección' });
  await expect(barra.getByText(/^Falta 1 dato para continuar$/)).toBeVisible();
  await expect(barra.getByRole('button', { name: 'Guardar y continuar' })).toBeDisabled();
  await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByRole('alert', { name: 'Para completar esta sección falta 1 dato' })).toContainText('Registra al menos un socio o accionista.');
  await agregarPersona(page, SOCIO);
  await guardarSeccionCompleta(page, 'Guardaste Información legal: está completa.');
  await llenarFinanciera(page);
  await llenarSst(page);
  await llenarEspecialidades(page);
  await page.goto('/ficha');
  await expect(page.getByText('Tu información está completa', { exact: true })).toBeVisible();
  await expect(page.getByText('6 de 6 secciones completas').first()).toBeVisible();

  // Con todo diligenciado solo falta lo documental; al subirlo el expediente pasa solo a revisión.
  await page.goto('/empresas');
  await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
  await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero}`));
  await subirDocumento(page, CAMARA, pdf(`Camara ${tercero.identificacion.numero}`));
  await esperarEstadoDelTercero(page, 'EnRevision');

  await afirmarLoQueVeLaEmpresa(browser, e, tercero.nombre, [RUT, CAMARA, GENERAL, TRIBUTARIA, LEGAL, FINANCIERA, SST, ESPECIALIDADES], [CEDULA]);
});
