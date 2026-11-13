import type { Browser, FrameLocator } from '@playwright/test';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, regionDeArea } from '../../soporte/empresa';
import { publicarFlujoDeFicha } from '../../soporte/ficha';
import { crearTipoDeVinculo, invitarVinculo } from '../../soporte/flujos';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, nuevoTercero, type Empresa, type Escenario, type Persona } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { areaDe } from '../nucleo/semillas';

const RUT = 'Registro Único Tributario';

/** Una ventana aparte para una persona de la empresa (siembra, no se documenta). */
async function ventanaDe(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa): Promise<FrameLocator> {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, empresa, 'bandeja');
}

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Bloquea un vínculo y lo desbloquea con una versión nueva pendiente',
    resumen:
      'La administradora bloquea a un proveedor por una revisión de sanciones. Mientras está bloqueado, el proveedor sube un RUT nuevo y el vínculo no abre revisión con él. Al desbloquearlo, la actualización se abre y el vínculo queda «En revisión».',
    precondiciones: [
      'Un grupo con Constructora Norte, con el tipo Aliado y un flujo que pide el RUT.',
      'Un proveedor aprobado por Compras (vínculo activo, certificado vigente) que, mientras está bloqueado, sube una versión nueva del RUT.',
    ],
    relacionados: ['vinculos/bloquear-y-desbloquear', 'vinculos/vinculo-con-actualizacion-pendiente'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    await publicarFlujoDeFicha(e, e.norte, 'ALIADOS_NORTE', areaDe(e.norte, 'compras'), 'ALIADO', [{ tipoDocumento: 'RUT' }]);
    const tercero = nuevoTercero('Aceros Bloqueados S.A.S.');
    const invitacion = await invitarVinculo(e, e.norte, tercero, 'ALIADO');

    // Siembra: el proveedor se valida con su RUT y Compras lo aprueba.
    const paginaTercero = await (await browser.newContext()).newPage();
    await entrarPorInvitacion(paginaTercero, invitacion);
    await aceptarInvitacion(paginaTercero, invitacion);
    await subirDocumento(paginaTercero, RUT, pdf('RUT bloqueo v1'));
    await esperarEstadoDelTercero(paginaTercero, 'EnRevision');
    const ruben = await ventanaDe(browser, e, e.ruben, e.norte);
    await expect(async () => {
      await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
      await expect(regionDeArea(ruben, 'Compras').getByRole('button', { name: /^Aprobar área/ })).toBeVisible({ timeout: 1500 });
    }).toPass({ timeout: 45_000 });
    await aprobarArea(ruben, 'Compras');
    await esperarEstadoDelTercero(paginaTercero, 'Aprobado');

    const app = await abrirEmbebido(page, e, e.ana, e.norte, `vinculos/${invitacion.vinculoId}`);
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(app.getByText('Activo', { exact: true }).first()).toBeVisible();

    await app.getByRole('button', { name: 'Más acciones' }).click();
    await app.getByRole('menuitem', { name: /^Bloquear/ }).click();
    const bloquear = app.getByRole('dialog', { name: /^Bloquear vínculo/ });
    await bloquear.getByRole('textbox', { name: 'Motivo' }).fill('Revisión de sanciones.');
    await paso(page, {
      titulo: 'Bloquea el vínculo con un motivo',
      explicacion:
        'Bloquear suspende la relación con el proveedor mientras dure la revisión. El motivo queda en la auditoría de la empresa y el proveedor no lo ve. ' +
        'Mientras el vínculo esté bloqueado, las versiones nuevas de sus documentos no abren una revisión.',
      resaltar: bloquear,
    });

    await bloquear.getByRole('button', { name: 'Bloquear vínculo' }).click();
    await expect(bloquear).toBeHidden();
    await expect(app.getByText('Revisión de sanciones.').first()).toBeVisible();

    // Siembra: el proveedor sube un RUT nuevo mientras el vínculo está bloqueado.
    await paginaTercero.goto('/boveda');
    await paginaTercero.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
    const selector = paginaTercero.waitForEvent('filechooser');
    await paginaTercero.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf('RUT bloqueo v2'));
    await expect(paginaTercero.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });

    await page.reload();
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(app.getByRole('main')).not.toContainText('En verificación');
    await paso(page, {
      titulo: 'Comprueba que el RUT nuevo no abrió revisión',
      explicacion:
        'El vínculo sigue bloqueado con el mismo motivo y el certificado no pasa a «En verificación»: la versión nueva que subió el proveedor no abre una revisión mientras el bloqueo esté vigente. ' +
        'La revisión empieza cuando desbloqueas.',
      resaltar: app.getByText('Revisión de sanciones.').first(),
      avisos: true,
    });

    await app.getByRole('button', { name: /^Desbloquear/ }).click();
    const desbloquear = app.getByRole('dialog', { name: /^Desbloquear vínculo/ });
    await desbloquear.getByRole('textbox', { name: 'Justificación' }).fill('Se levantó la sanción.');
    await paso(page, {
      titulo: 'Desbloquea con una justificación',
      explicacion:
        'Al desbloquear, el sistema abre la actualización pendiente del RUT nuevo. La justificación queda en el historial y el motivo del bloqueo no se borra.',
      resaltar: desbloquear,
    });

    await desbloquear.getByRole('button', { name: 'Desbloquear vínculo' }).click();
    await expect(desbloquear).toBeHidden();
    await expect(async () => {
      await page.reload();
      await expect(app.getByText(/^En revisión\. En revisión desde el /).first()).toBeVisible({ timeout: 4000 });
    }).toPass({ timeout: 60_000 });
    await expect(app.getByRole('main')).toContainText(new RegExp(`En verificación\\s*${e.norte.prefijo}-C-\\d{4}-\\d{6}-1`));
    await paso(page, {
      titulo: 'Comprueba que el vínculo quedó en revisión',
      explicacion:
        'El vínculo queda «En revisión» y su certificado se ve «En verificación» mientras las áreas revisan el RUT nuevo. El proveedor ve lo mismo en «Mis empresas», donde su revisión con Constructora Norte vuelve a estar en curso.',
      resaltar: app.getByText(/^En revisión\. En revisión desde el /).first(),
      avisos: true,
    });
  },
);
