import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { abrirEmpresa, completarInvitacion, esperarEstadoDelTercero, esperarTarjeta, tarjeta } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Mira tu vínculo cuando una actualización está en revisión',
    resumen:
      'Tu vínculo con la empresa sigue activo, pero al subir una versión nueva de un documento la empresa vuelve a revisarlo. «Mis empresas» y la revisión dicen lo mismo: que la empresa está revisando tu actualización, y no que está aprobada.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.», su flujo de Proveedor (RUT y certificado de seguridad y salud en el trabajo) y un área «Revisión» con una persona como miembro.',
      'Un tercero cuyo vínculo con Sur ya fue aprobado; sube una versión nueva del RUT, que queda en revisión.',
    ],
    relacionados: ['tercero/subir-version-nueva', 'tercero/vinculo-por-vencer-renovar'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería Actualiza S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await completarInvitacion(page, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${tercero.identificacion.numero}`) },
      { nombre: SST.nombre, archivo: pdf(`SST ${tercero.identificacion.numero}`), campos: { 'Fecha de expedición': new Date(Date.now() - 3 * 86_400_000).toISOString().slice(0, 10) } },
    ]);
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión');
    await aprobarArea(sofia, 'Revisión');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // El tercero sube una versión nueva del RUT desde la bóveda.
    await page.goto('/boveda');
    await page.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
    await expect(page.getByRole('button', { name: 'Elegir archivo' })).toBeVisible();
    const selector = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf(`RUT nuevo ${tercero.identificacion.numero}`));
    await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });

    // 1. «Mis empresas» dice que la empresa revisa la actualización.
    const tarjetaSur = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true }) });
    await expect(async () => {
      await page.goto('/empresas');
      await expect(tarjetaSur.getByText('En revisión', { exact: true })).toBeVisible({ timeout: 1500 });
    }).toPass({ timeout: 45_000 });
    await expect(tarjetaSur.getByText('Aprobado', { exact: true })).toHaveCount(0);
    await expect(tarjetaSur).toContainText(`${e.sur.nombre} está revisando tu información.`);
    await paso(page, {
      titulo: 'Mira el estado de tu vínculo en «Mis empresas»',
      explicacion:
        'La tarjeta de la empresa muestra «En revisión» y el mensaje de que la empresa está revisando tu información. Ya no dice «Aprobado»: el vínculo sigue activo, pero lo que te toca ahora es la actualización. ' +
        'Mientras la empresa no decida, no tienes que hacer nada más con ese vínculo.',
      resaltar: tarjetaSur,
    });

    // 2. La revisión dice lo mismo que la tarjeta del documento.
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await esperarEstadoDelTercero(page, 'EnRevision');
    await esperarTarjeta(page, RUT, 'En revisión');
    await paso(page, {
      titulo: 'Revisa la versión que está en revisión',
      explicacion:
        'En la revisión, el encabezado dice que la empresa está revisando tu información y la tarjeta del RUT muestra la versión 2 «En revisión». ' +
        'El documento aprobado anterior ya no es el que se está revisando: la empresa decidirá sobre la versión nueva.',
      resaltar: [tarjeta(page, RUT), page.getByRole('main').getByText(/ está revisando tu información\./)],
    });
  },
);
