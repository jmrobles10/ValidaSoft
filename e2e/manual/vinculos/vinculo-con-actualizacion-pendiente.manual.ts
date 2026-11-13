import type { Browser, FrameLocator, Page } from '@playwright/test';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, regionDeArea } from '../../soporte/empresa';
import { publicarFlujoDeFicha } from '../../soporte/ficha';
import { crearTipoDeVinculo, invitarVinculo } from '../../soporte/flujos';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, nuevoTercero, type Empresa, type Escenario, type Persona } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

/** Una ventana aparte para una persona de la empresa (siembra: aprueba el área sin documentarlo). */
async function ventanaDe(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa): Promise<FrameLocator> {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, empresa, 'bandeja');
}

/** Aprueba el área desde la bandeja cuando el expediente ya llegó (puede tardar por la cola de revisión). */
async function aprobarDesdeBandeja(app: FrameLocator, tercero: string, area: string): Promise<void> {
  await expect(async () => {
    await abrirDesdeBandeja(app, tercero, area);
    await expect(regionDeArea(app, area).getByRole('button', { name: /^Aprobar área/ })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await aprobarArea(app, area);
}

/** El tercero sube una versión nueva del RUT desde su bóveda (siembra). */
async function subirVersionNueva(page: Page, version: number): Promise<void> {
  await page.goto('/boveda');
  await page.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf(`RUT en revisión v${String(version)}`));
  await expect(page.getByText(`Es tu versión ${String(version)} de ${RUT}`)).toBeVisible({ timeout: 30_000 });
}

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Ve un vínculo con una actualización en revisión',
    resumen:
      'El tercero, ya aprobado por Constructora Norte, sube un RUT nuevo. Mientras las áreas lo revisan, el vínculo sigue activo pero aparece «En revisión» en la lista, en el filtro y en el detalle, y su certificado se ve «En verificación».',
    precondiciones: [
      'Un grupo con Constructora Norte y Constructora Sur, con el tipo Aliado y un flujo que pide el RUT en cada empresa.',
      'Un tercero aprobado por las dos empresas, que luego sube una versión nueva del RUT que ambas exigen.',
    ],
    relacionados: ['vinculos/bloqueado-no-recibe-version', 'vinculos/lista-y-filtros', 'revision/actualizacion-de-un-documento'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    const requisitos = [{ tipoDocumento: 'RUT' }];
    await publicarFlujoDeFicha(e, e.norte, 'ALIADOS_NORTE', e.norte.areas.compras!, 'ALIADO', requisitos);
    await publicarFlujoDeFicha(e, e.sur, 'ALIADOS_SUR', e.sur.areas.revision!, 'ALIADO', requisitos);
    const tercero = nuevoTercero('Aceros en Revisión S.A.S.');
    const enNorte = await invitarVinculo(e, e.norte, tercero, 'ALIADO');
    const enSur = await invitarVinculo(e, e.sur, tercero, 'ALIADO');

    // Siembra: el tercero acepta a las dos con su RUT y cada empresa lo aprueba.
    const paginaTercero = await (await browser.newContext()).newPage();
    await entrarPorInvitacion(paginaTercero, enNorte);
    await aceptarInvitacion(paginaTercero, enNorte);
    await subirDocumento(paginaTercero, RUT, pdf('RUT en revisión v1'));
    await esperarEstadoDelTercero(paginaTercero, 'EnRevision');
    await entrarPorInvitacion(paginaTercero, enSur);
    await aceptarInvitacion(paginaTercero, enSur);
    await esperarEstadoDelTercero(paginaTercero, 'EnRevision');
    await aprobarDesdeBandeja(await ventanaDe(browser, e, e.ruben, e.norte), tercero.nombre, 'Compras');
    await aprobarDesdeBandeja(await ventanaDe(browser, e, e.sofia, e.sur), tercero.nombre, 'Revisión');

    // Siembra: el tercero sube un RUT nuevo; las dos empresas lo tienen que revisar.
    await subirVersionNueva(paginaTercero, 2);
    await expect(async () => {
      await paginaTercero.goto('/empresas');
      await expect(paginaTercero.getByRole('listitem').filter({ has: paginaTercero.getByRole('heading', { level: 2, name: e.norte.nombre, exact: true }) }).getByText('En revisión', { exact: true })).toBeVisible({ timeout: 1500 });
    }).toPass({ timeout: 45_000 });

    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
    await expect(app.getByRole('heading', { level: 1, name: 'Vínculos' })).toBeVisible();
    const fila = app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` });
    await expect(fila).toContainText('En revisión', { timeout: 45_000 });
    await paso(page, {
      titulo: 'Localiza el vínculo en la lista',
      explicacion:
        'Cuando el tercero sube una versión nueva de un documento que tu empresa exige, el vínculo se muestra «En revisión» en la lista aunque siga activo. ' +
        'Así sabes que hay una actualización por decidir antes de dar el asunto por resuelto.',
      resaltar: fila,
      variantes: [
        {
          cuando: 'El tercero no ha subido una versión nueva',
          entonces: 'La fila muestra el estado normal del vínculo, por ejemplo «Activo».',
        },
      ],
    });

    await app.getByRole('button', { name: /^Estado/ }).click();
    await app.getByRole('menuitem', { name: 'En revisión', exact: true }).click();
    await expect(fila).toBeVisible();
    await paso(page, {
      titulo: 'Filtra los vínculos en revisión',
      explicacion:
        'El filtro «Estado» con «En revisión» deja solo los vínculos con una actualización pendiente de decisión. Así ves de un vistazo qué tiene que revisar tu empresa.',
      resaltar: app.getByRole('button', { name: /^Estado/ }),
    });

    await fila.click();
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(app.getByText('En verificación').first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el detalle y el certificado',
      explicacion:
        'El detalle muestra el vínculo como «En revisión» mientras las áreas revisan el RUT nuevo. Mientras dure la revisión, nada se suspende ni se bloquea: el certificado sigue vigente pero se ve «En verificación», igual que en la página pública de verificación.',
      resaltar: app.getByText('En verificación').first(),
    });
  },
);
