import { areaDe } from '../nucleo/semillas';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { abrirFichaAceptada, abrirSeccion, guardarSeccionCompleta, llenarGeneralJuridica, publicarFlujoDeFicha } from '../../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const RAZON_SOCIAL = 'Aceros Cambio Posterior S.A.S.';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Cambia un dato de tu ficha después de la aprobación',
    resumen:
      'Ya aprobada tu información, puedes cambiar un dato como la dirección. El cambio se guarda en tu ficha y la empresa lo ve como un cambio posterior: debe volver a revisar esa sección para que siga valiendo.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.» y un flujo de Cliente que pide el RUT y la información general de la persona jurídica.',
      'Un tercero jurídico cuyo expediente fue aprobado por el área Revisión, con la información general aceptada por Sur.',
    ],
    relacionados: ['tercero/ficha-actualizar-desde-mi-rut', 'tercero/vinculo-por-vencer-renovar'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await publicarFlujoDeFicha(e, e.sur, 'CLIENTE_CAMBIO_POSTERIOR', areaDe(e.sur, 'revision'), 'CLIENTE', [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }]);
    const tercero = nuevoTercero(RAZON_SOCIAL);
    const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await llenarGeneralJuridica(page, RAZON_SOCIAL);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
    await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero} v1`));
    await esperarEstadoDelTercero(page, 'EnRevision');
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    const ventanaSofia = sofia;
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await aprobarArea(sofia, 'Revisión');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // 1. Cambia la dirección en Información general y guarda.
    await abrirSeccion(page, 'general', 'Información general');
    await page.getByRole('textbox', { name: 'Dirección', exact: true }).fill('Calle 100 # 19-54');
    await page.getByRole('group', { name: 'Documento que respalda esta información' }).getByRole('checkbox', { name: new RegExp(`^${RUT} · versión 1`) }).check();
    const barra = page.getByRole('group', { name: 'Guardar la sección' });
    await expect(barra.getByText('Tienes cambios sin guardar')).toBeVisible();
    await paso(page, {
      titulo: 'Cambia un dato aprobado',
      explicacion:
        'En «Información general» puedes corregir la dirección aunque la empresa ya la haya aceptado. Como la sección ya estaba completa, un cambio te pide de nuevo el documento que la respalda: aquí marcas el RUT. La barra de abajo dice que tienes cambios sin guardar, y el cambio no llega a la empresa hasta que guardes.',
      resaltar: barra,
    });

    // 2. Guarda: la empresa lo ve como un cambio posterior.
    await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
    await paso(page, {
      titulo: 'Guarda el cambio y revisa su efecto',
      explicacion:
        'Al guardar, la sección queda con la dirección nueva y Sur recibe el cambio posterior: la empresa debe volver a revisar esta sección para que siga valiendo. ' +
        'Lo que la empresa aceptó sigue siendo la versión 1 hasta que revise la nueva, y tu pantalla pasa a decir que la empresa está revisando tu información (versión 2).',
      resaltar: page.getByRole('main'),
    });

    // Lo que afirma la explicación: la empresa ve el cambio como pendiente de revisión (lo aceptado sigue siendo la versión 1) y el tercero ve que la empresa está revisando su información.
    await expect(async () => {
      await abrirFichaAceptada(ventanaSofia, tercero.nombre);
      await expect(ventanaSofia.getByText('1 sección tiene una versión nueva pendiente de revisión')).toBeVisible({ timeout: 1500 });
    }).toPass({ timeout: 45_000 });
    await expect(ventanaSofia.getByRole('region', { name: 'Información general' })).toContainText('Aceptada · versión 1');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
    await expect(page.getByText(`${e.sur.nombre} está revisando tu información.`, { exact: false })).toBeVisible();
    await expect(page.getByText('Versión 2 de tu información', { exact: false })).toBeVisible();
  },
);
