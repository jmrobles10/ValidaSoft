import { areaDe } from '../nucleo/semillas';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { llenarGeneralJuridica } from '../../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento } from '../../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const RAZON_SOCIAL = 'Aceros Ficha Actualiza S.A.S.';

/** RUT que lee el analizador de las pruebas (marcas `[[clave=valor]]` en el texto del PDF). */
function rut(nit: string, direccion: string, version: string) {
  return pdf(`RUT ${version} [[nit=${nit}]][[razonSocial=${RAZON_SOCIAL}]][[direccion=${direccion}]][[codigoMunicipio=11001]][[actividadEconomicaPrincipal=4663]]`);
}

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Actualiza tu información desde un RUT nuevo',
    resumen:
      'Cuando subes un RUT nuevo con datos distintos a los de tu información, Validasoft te avisa y te propone los cambios. Revisas lo que dice el RUT campo por campo y aplicas solo lo que es correcto; nada se guarda hasta que tú guardes.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.», lectura automática de documentos activa (IA) y un flujo de Cliente que pide el RUT y las secciones de la ficha.',
      'Un tercero jurídico que aceptó la invitación, completó la información general con la dirección del RUT versión 1 y cuyo expediente ya fue aprobado.',
    ],
    relacionados: ['tercero/ficha-secciones-con-soporte', 'tercero/ficha-cambio-tras-aprobar'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur);
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_FICHA', areaDe(e.sur, 'revision'), [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }]);
    const tercero = nuevoTercero(RAZON_SOCIAL);
    const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
    const nit = tercero.identificacion.numero;

    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await llenarGeneralJuridica(page, RAZON_SOCIAL);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
    await subirDocumento(page, RUT, rut(nit, 'Calle 45 # 12-08', 'v1'));
    await esperarEstadoDelTercero(page, 'EnRevision');

    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
    await aprobarArea(sofia, 'Revisión');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // El tercero sube un RUT nuevo con otra dirección.
    await page.goto('/boveda');
    await page.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
    const selectorNuevo = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selectorNuevo).setFiles(rut(nit, 'Cl 80 # 10-20', 'v2'));
    await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });

    // 1. El aviso de la ficha.
    await page.goto('/ficha');
    const aviso = page.getByRole('alert', { name: 'Tienes documentos más nuevos que tu información' });
    await expect(aviso).toContainText('Tu RUT · versión 2', { timeout: 45_000 });
    await expect(aviso.getByText('1 dato distinto')).toBeVisible();
    await paso(page, {
      titulo: 'Lee el aviso de documentos más nuevos',
      explicacion:
        'Arriba de tu información aparece un aviso: tu RUT versión 2 tiene datos distintos a los que ya aprobaste en la sección. El aviso dice cuántos datos cambian y en qué sección. ' +
        'Hay un solo aviso por documento, con una acción para actualizar desde ese RUT.',
      resaltar: aviso,
    });

    // 2. La propuesta campo por campo.
    await aviso.getByRole('link', { name: /^Actualizar desde mi RUT/ }).click();
    const propuesta = page.getByRole('alert', { name: 'Tienes documentos más nuevos que esta sección' });
    await propuesta.getByRole('button', { name: 'Ver los cambios de mi RUT' }).click();
    const tabla = propuesta.getByRole('table', { name: 'Datos que cambian según tu RUT' });
    await expect(tabla).toContainText('Calle 45 # 12-08');
    await expect(tabla).toContainText('Cl 80 # 10-20');
    await paso(page, {
      titulo: 'Compara lo que tienes con lo que dice el RUT',
      explicacion:
        'La propuesta muestra, campo por campo, el valor que tienes y el que dice el RUT nuevo. Aquí la dirección cambia de «Calle 45 # 12-08» a «Cl 80 # 10-20». ' +
        'Nada cambia todavía: la plataforma solo te muestra la comparación.',
      resaltar: tabla,
    });

    // 3. Aplicar los valores del RUT.
    await propuesta.getByRole('button', { name: 'Usar los valores de mi RUT' }).click();
    await expect(page.getByRole('textbox', { name: 'Dirección', exact: true })).toHaveValue('Cl 80 # 10-20');
    await expect(propuesta.getByRole('status')).toContainText('todavía no se ha guardado nada');
    await paso(page, {
      titulo: 'Usa los valores del RUT y guarda',
      explicacion:
        'Con «Usar estos valores» el formulario toma los datos del RUT. Todavía no se guarda: revisa el formulario y pulsa «Guardar» en la barra de abajo. ' +
        'Si no estás de acuerdo con un dato, escribe el tuyo; esa decisión queda a tu cargo.',
      resaltar: [page.getByRole('textbox', { name: 'Dirección', exact: true }), propuesta.getByRole('status')],
    });
  },
);
