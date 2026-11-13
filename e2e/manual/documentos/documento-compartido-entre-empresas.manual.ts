import type { Browser, FrameLocator, Page } from '@playwright/test';
import { areaDe } from '../nucleo/semillas';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, regionDeArea } from '../../soporte/empresa';
import { abrirFichaAceptada, abrirSeccion, guardarSeccionCompleta, llenarGeneralJuridica, publicarFlujoDeFicha } from '../../soporte/ficha';
import { crearTipoDeVinculo, invitarVinculo } from '../../soporte/flujos';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, nuevoTercero, type Empresa, type Escenario, type Persona } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const DIRECCION_VIEJA = 'Calle 45 # 12-08';
const DIRECCION_NUEVA = 'Calle 100 # 19-54';

async function ventanaDe(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa): Promise<FrameLocator> {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, empresa);
}

/** Aprueba lo que hay en el área: abre el expediente desde la bandeja cuando ya está (llega por una cola) y pulsa «Aprobar área». */
async function aprobarDesdeBandeja(app: FrameLocator, tercero: string, area: string): Promise<void> {
  await expect(async () => {
    await abrirDesdeBandeja(app, tercero, area);
    await expect(regionDeArea(app, area).getByRole('button', { name: /^Aprobar área/ })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await aprobarArea(app, area);
}

/** «Mis empresas» → la tarjeta de la empresa → su revisión (la vista del tercero). */
async function abrirRevisionCon(page: Page, empresa: string): Promise<void> {
  await page.goto('/empresas');
  const tarjeta = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: empresa, exact: true }) });
  await expect(tarjeta).toBeVisible();
  await expect(async () => {
    if (/\/empresas\/?$/.test(new URL(page.url()).pathname)) await tarjeta.getByRole('link').first().click({ timeout: 3000 });
    await expect(page).toHaveURL(/\/expediente\//, { timeout: 3000 });
  }).toPass({ timeout: 20_000 });
  await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${empresa}` })).toBeVisible();
}

recorrido(
  {
    dominio: 'documentos',
    actor: 'Revisor/área',
    titulo: 'Revisa una versión nueva de un documento que comparten dos empresas',
    resumen:
      'Sofía, del área Revisión de Constructora Sur, ve que el tercero cambió su información después de que ella la aprobó. Su revalidación muestra solo lo que cambió. Lo que decide Constructora Norte no aparece en su pantalla.',
    precondiciones: [
      'Dos empresas del grupo, Constructora Norte y Constructora Sur, con un tipo de vínculo «Aliado» que exige el RUT y la información general.',
      'Un tercero que aceptó a las dos empresas y que ambas aprobaron en su versión 1. Después subió un RUT nuevo y cambió su dirección; Norte ya aprobó esa versión y Sur no.',
    ],
    relacionados: ['tercero/boveda-documento-compartido', 'revision/aprobar-un-area', 'documentos/vencimientos'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    const requisitos = [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }];
    await publicarFlujoDeFicha(e, e.norte, 'ALIADOS_NORTE', areaDe(e.norte, 'compras'), 'ALIADO', requisitos);
    await publicarFlujoDeFicha(e, e.sur, 'ALIADOS_SUR', areaDe(e.sur, 'revision'), 'ALIADO', requisitos);
    const tercero = nuevoTercero('Aceros Compartidos S.A.S.');
    const enNorte = await invitarVinculo(e, e.norte, tercero, 'ALIADO');
    const enSur = await invitarVinculo(e, e.sur, tercero, 'ALIADO');

    // El tercero acepta a Norte, diligencia su información general y sube su RUT una sola vez; luego acepta a Sur.
    const pTercero = await (await browser.newContext()).newPage();
    await entrarPorInvitacion(pTercero, enNorte);
    await aceptarInvitacion(pTercero, enNorte);
    await llenarGeneralJuridica(pTercero, tercero.nombre);
    await abrirRevisionCon(pTercero, e.norte.nombre);
    await subirDocumento(pTercero, RUT, pdf('RUT compartido v1'));
    await esperarEstadoDelTercero(pTercero, 'EnRevision');
    await entrarPorInvitacion(pTercero, enSur);
    await aceptarInvitacion(pTercero, enSur);
    await esperarEstadoDelTercero(pTercero, 'EnRevision');

    // Cada empresa aprueba la versión 1 en su área.
    await aprobarDesdeBandeja(await ventanaDe(browser, e, e.ruben, e.norte), tercero.nombre, 'Compras');
    await aprobarDesdeBandeja(await ventanaDe(browser, e, e.sofia, e.sur), tercero.nombre, 'Revisión');
    await abrirRevisionCon(pTercero, e.norte.nombre);
    await esperarEstadoDelTercero(pTercero, 'Aprobado');
    await abrirRevisionCon(pTercero, e.sur.nombre);
    await esperarEstadoDelTercero(pTercero, 'Aprobado');

    // El tercero sube un RUT nuevo y cambia su dirección: una versión nueva para las dos empresas.
    await pTercero.goto('/boveda');
    await pTercero.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
    const selector = pTercero.waitForEvent('filechooser');
    await pTercero.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf('RUT compartido v2'));
    await expect(pTercero.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });
    await abrirSeccion(pTercero, 'general', 'Información general');
    await expect(pTercero.getByRole('textbox', { name: 'Dirección', exact: true })).toHaveValue(DIRECCION_VIEJA);
    await pTercero.getByRole('textbox', { name: 'Dirección', exact: true }).fill(DIRECCION_NUEVA);
    await pTercero.getByRole('checkbox', { name: /^Registro Único Tributario · versión 2/ }).check();
    await guardarSeccionCompleta(pTercero, 'Guardaste Información general: está completa.');

    // Norte resuelve su revalidación; Sur queda pendiente.
    await aprobarDesdeBandeja(await ventanaDe(browser, e, e.ruben, e.norte), tercero.nombre, 'Compras');

    // Lo que se documenta: la vista de Sofía en Sur.
    const app = await abrirEmbebido(page, e, e.sofia, e.sur, 'bandeja');
    await expect(async () => {
      await abrirFichaAceptada(app, tercero.nombre);
      await expect(app.getByText('1 sección tiene una versión nueva pendiente de revisión')).toBeVisible({ timeout: 1500 });
    }).toPass({ timeout: 45_000 });

    await paso(page, {
      titulo: 'Mira la ficha que aceptaste del tercero',
      explicacion:
        'En la ficha aceptada de Sur se ve lo que la empresa aprobó de cada sección: la información general quedó «Aceptada · versión 1». ' +
        'Aparte, el aviso dice que una sección tiene una versión nueva pendiente de revisión, porque el tercero cambió algo después de tu aprobación.',
      resaltar: [app.getByRole('region', { name: 'Información general' }), app.getByText('1 sección tiene una versión nueva pendiente de revisión')],
    });

    const posterior = app.getByRole('region', { name: 'Versión nueva del tercero, pendiente de revisión' });
    await expect(posterior.getByRole('table', { name: 'Qué cambió frente a la versión 1 aceptada' })).toContainText(DIRECCION_NUEVA);
    await paso(page, {
      titulo: 'Compara lo que cambió',
      explicacion:
        'La tabla «Qué cambió frente a la versión 1 aceptada» muestra cada campo con su valor anterior y el nuevo. Aquí, la dirección pasó de «' +
        DIRECCION_VIEJA +
        '» a «' +
        DIRECCION_NUEVA +
        '». Con esa tabla decides si aceptas el cambio de la sección.',
      resaltar: posterior.getByRole('table', { name: 'Qué cambió frente a la versión 1 aceptada' }),
    });

    await abrirDesdeBandeja(app, tercero.nombre, 'Revisión');
    const botonAprobar = regionDeArea(app, 'Revisión').getByRole('button', { name: /^Aprobar área/ });
    await expect(botonAprobar).toBeVisible();
    await paso(page, {
      titulo: 'Aprueba la revisión de tu área',
      explicacion:
        'La revalidación de tu área sigue abierta hasta que la decidas: el botón «Aprobar área» aprueba los cambios del tercero para Constructora Sur. ' +
        'Tu pantalla muestra solo esta revisión: no muestra la decisión que tomó Constructora Norte ni quién de Norte la tomó, porque cada empresa decide en privado.',
      resaltar: botonAprobar,
    });
  },
);
