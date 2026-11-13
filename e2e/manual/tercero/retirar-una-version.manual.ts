import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { abrirEmpresa, completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Retira una versión que subiste por error',
    resumen:
      'Subiste una versión del RUT que no era la correcta. En tu bóveda la retiras con un motivo, y antes de confirmar ves qué pasa con cada empresa: cuál versión queda vigente y a quién le toca revisar de nuevo.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Norte», su flujo de Proveedor (RUT y Cámara de comercio) y las áreas «Compras» y «Cumplimiento» con sus miembros.',
      'Un tercero cuyo RUT versión 2 ya fue aprobada por Norte, con la versión 1 aprobada antes; la versión 2 es la vigente.',
    ],
    relacionados: ['tercero/subir-version-nueva', 'tercero/boveda-documento-compartido'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros del Retiro S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero, 'PROVEEDOR');
    await completarInvitacion(page, invitacion, [
      { nombre: RUT, archivo: pdf('RUT Retiro v1') },
      { nombre: CAMARA, archivo: pdf('Camara Retiro v1') },
    ]);
    const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben, e.norte);
    await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
    await aprobarArea(ruben, 'Compras');
    const olga = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.olga, e.norte);
    await abrirDesdeBandeja(olga, tercero.nombre, 'Cumplimiento');
    await aprobarArea(olga, 'Cumplimiento');

    // Sube una versión 2 del RUT (la equivocada) desde la bóveda; las dos áreas la aprueban.
    await page.goto('/boveda');
    await page.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
    await expect(page.getByRole('button', { name: 'Elegir archivo' })).toBeVisible();
    const selector = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf('RUT Retiro v2'));
    await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });
    await expect(async () => {
      await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
      await expect(ruben.getByRole('region', { name: 'Compras', exact: true }).first()).toContainText(`${RUT} · v2`, { timeout: 1500 });
    }).toPass({ timeout: 45_000 });
    await aprobarArea(ruben, 'Compras');
    await abrirDesdeBandeja(olga, tercero.nombre, 'Cumplimiento');
    await aprobarArea(olga, 'Cumplimiento');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.norte.nombre, 'PROVEEDOR');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // 1. La versión 2 es la vigente y Norte la aprobó.
    const tarjetaRut = page.getByRole('article', { name: RUT });
    const lista = tarjetaRut.getByRole('list', { name: 'Con tus empresas' });
    await expect(async () => {
      await page.goto('/boveda');
      await expect(lista).toContainText('Aprobó la versión 2, la vigente.', { timeout: 1500 });
    }).toPass({ timeout: 45_000 });
    await paso(page, {
      titulo: 'Ubica la versión que quieres retirar',
      explicacion:
        'La tarjeta del RUT muestra la versión 2 como la vigente, y en «Con tus empresas» Norte dice que aprobó esa versión. ' +
        'Para retirarla, abre el menú de la versión (los tres puntos) y elige «Retirar esta versión».',
      resaltar: [tarjetaRut.getByRole('button', { name: `Versión 2: ${RUT}` }), lista],
    });

    // 2. El diálogo dice qué pasará antes de pedir el motivo.
    await tarjetaRut.getByRole('button', { name: `Versión 2: ${RUT}` }).click();
    await page.getByRole('menuitem', { name: 'Retirar esta versión' }).click();
    const dialogo = page.getByRole('dialog', { name: `Retirar la versión 2 de ${RUT}` });
    await expect(dialogo.getByText(`Tu ${RUT} vigente pasará a ser la versión 1.`)).toBeVisible();
    await expect(dialogo.getByText(`${e.norte.nombre} la aprobó: tendrá que revisar de nuevo tu ${RUT} versión 1.`)).toBeVisible();
    await expect(dialogo.getByRole('button', { name: 'Retirar versión' })).toBeDisabled();
    await dialogo.getByLabel(/Motivo/).fill('Subí el RUT de otra empresa');
    await paso(page, {
      titulo: 'Revisa lo que pasará antes de retirar',
      explicacion:
        'Antes de pedir el motivo, el diálogo dice qué versión queda vigente: al retirar la 2, tu RUT vuelve a ser la versión 1. ' +
        'También dice qué pasa con cada empresa: Norte la aprobó y tendrá que revisar de nuevo la versión 1. Escribe el motivo; lo ven las empresas a las que les afecta. ' +
        'Si el problema era el archivo y no quieres retirarlo, usa «Subir una versión corregida»: tus empresas la revisan una sola vez.',
      resaltar: dialogo,
    });

    // 3. Confirmar y ver el resultado en la bóveda.
    await dialogo.getByRole('button', { name: 'Retirar versión' }).click();
    await expect(page.getByText('Retiraste la versión 2.')).toBeVisible();
    await expect(dialogo).toBeHidden();
    await expect(async () => {
      await page.goto('/boveda');
      await expect(tarjetaRut).toContainText('Versión 1 ·', { timeout: 1500 });
      await expect(lista).toContainText('Aprobó la versión 2, que retiraste. Debe revisar la versión 1: revalidación abierta.', { timeout: 1500 });
    }).toPass({ timeout: 45_000 });
    await paso(page, {
      titulo: 'Confirma qué queda en tu bóveda',
      explicacion:
        'La versión 1 vuelve a ser la vigente. La versión 2 ya no aparece como vigente y Norte, que la había aprobado, debe revisar la versión 1 de nuevo: la línea lo dice con «revalidación abierta». ' +
        'Norte recibe el motivo que escribiste, y la decisión sobre la versión 1 queda en su revisión.',
      resaltar: [tarjetaRut, lista],
    });
  },
);
