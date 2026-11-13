import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { abrirEmpresa, aceptarInvitacion, completarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Entiende un documento que comparten dos empresas',
    resumen:
      'Norte y Sur piden el mismo RUT. Lo subes una sola vez y cada empresa lo revisa por su lado: Sur lo aprueba mientras Norte sigue revisando. Cuando subes una versión nueva, cada empresa debe revisarla por separado.',
    precondiciones: [
      'Dos empresas del mismo tercero: Constructora Norte (RUT y Cámara de comercio, con Compras que aprueba) y Constructora Sur (RUT y certificado de seguridad, con un área «Revisión» que aprueba).',
      'Un tercero que aceptó las dos invitaciones, subió el RUT una sola vez y el área Revisión de Sur ya aprobó su expediente, mientras Norte sigue en revisión.',
    ],
    relacionados: ['tercero/boveda-mis-documentos', 'tercero/subir-version-nueva'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería La Esquina S.A.S.');
    const enNorte = await invitar(e, e.norte, tercero, 'PROVEEDOR');
    const enSur = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await completarInvitacion(page, enNorte, [
      { nombre: RUT, archivo: pdf(`RUT ${tercero.identificacion.numero}`) },
      { nombre: CAMARA, archivo: pdf(`Camara ${tercero.identificacion.numero}`) },
    ]);
    await entrarPorInvitacion(page, enSur);
    await aceptarInvitacion(page, enSur);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await subirDocumento(page, SST.nombre, pdf(`SST ${tercero.identificacion.numero}`), {
      campos: { 'Fecha de expedición': new Date(Date.now() - 3 * 86_400_000).toISOString().slice(0, 10) },
    });
    await esperarEstadoDelTercero(page, 'EnRevision');
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión');
    await aprobarArea(sofia, 'Revisión');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // 1. Una sola tarjeta, dos revisiones.
    await page.goto('/boveda');
    const tarjetaRut = page.getByRole('article').filter({ has: page.getByRole('heading', { name: RUT, exact: true }) });
    await expect(tarjetaRut).toHaveCount(1);
    const lista = tarjetaRut.getByRole('list', { name: 'Con tus empresas' });
    await expect(lista).toContainText(e.sur.nombre);
    await expect(lista).toContainText('Aprobó la versión 1, la vigente.');
    await expect(lista).toContainText('Revisando la versión 1.');
    await paso(page, {
      titulo: 'Mira qué pasa con cada empresa',
      explicacion:
        'El RUT está una sola vez en tu bóveda, con la versión 1. En «Con tus empresas» ves la revisión de cada empresa por separado: Sur ya lo aprobó, y Norte sigue revisándolo. ' +
        'Una empresa no ve la decisión de la otra, aunque el documento sea el mismo.',
      resaltar: lista,
    });

    // 2. Subir una versión nueva afecta a las dos.
    await tarjetaRut.getByRole('link', { name: 'Subir versión' }).click();
    await expect(page.getByRole('button', { name: 'Elegir archivo' })).toBeVisible();
    const selector = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf(`RUT nuevo ${tercero.identificacion.numero}`));
    await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });

    await page.goto('/boveda');
    const tarjetaNueva = page.getByRole('article').filter({ has: page.getByRole('heading', { name: RUT, exact: true }) });
    const listaNueva = tarjetaNueva.getByRole('list', { name: 'Con tus empresas' });
    await expect(listaNueva).toContainText('Debe revisar la versión 2, la vigente: revalidación abierta.');
    await expect(listaNueva).toContainText('Revisando la versión 2.');
    await paso(page, {
      titulo: 'Entiende lo que pasa con la versión nueva',
      explicacion:
        'Al subir la versión 2, la vigente cambia para las dos empresas. Sur, que ya había aprobado la versión 1, debe revisar la nueva: aparece como «revalidación abierta». ' +
        'Norte, que todavía no había decidido, sigue revisando, ahora la versión 2. Lo que ya estaba aprobado sigue vigente mientras la empresa revisa.',
      resaltar: listaNueva,
    });
  },
);
