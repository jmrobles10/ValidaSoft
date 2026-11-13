import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { abrirEmpresa, aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, fraseDeEstado, subirDocumento, tarjeta } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Sube una versión nueva de un documento ya aprobado',
    resumen:
      'El tercero tiene su vínculo aprobado. Cuando su RUT cambia, sube otra versión desde la tarjeta del documento: el documento vuelve a «En revisión» y la empresa lo revisa de nuevo, sin que se pierda lo aprobado.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.», su flujo de Proveedor (RUT y certificado de seguridad y salud) y un área «Revisión» con una persona como miembro.',
      'Un tercero cuyo vínculo con Sur ya fue aprobado por esa área, con el RUT en su versión 1.',
    ],
    relacionados: ['tercero/expediente-aprobado', 'tercero/responder-observaciones'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería Andina S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero}`));
    await subirDocumento(page, SST.nombre, pdf(`SST ${tercero.identificacion.numero}`), {
      campos: { 'Fecha de expedición': new Date(Date.now() - 3 * 86_400_000).toISOString().slice(0, 10) },
    });
    await esperarEstadoDelTercero(page, 'EnRevision');
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión');
    await aprobarArea(sofia, 'Revisión');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // 1. El vínculo aprobado.
    await paso(page, {
      titulo: 'Verifica que tu vínculo está aprobado',
      explicacion:
        'La frase de arriba dice que la empresa aprobó tu información y que tu vínculo está activo. Cada documento aprobado sigue visible en «Lo que te pide», con su versión y su vigencia. ' +
        'Un documento aprobado puede cambiar en cualquier momento: si subes una versión nueva, la empresa la revisa de nuevo.',
      resaltar: fraseDeEstado(page, 'Aprobado'),
      variantes: [
        { cuando: 'La empresa aprobó tu información pero un documento vence', entonces: 'El documento aparece como «Por vencer» o «Vencido» y debes subir uno vigente.' },
      ],
    });

    // 2. Desde Mi bóveda, la tarjeta del RUT ofrece «Subir versión».
    await page.goto('/boveda');
    const tarjetaBoveda = page.getByRole('article').filter({ has: page.getByRole('heading', { name: RUT, exact: true }) });
    await expect(tarjetaBoveda.getByText('Versión 1 · cargada')).toBeVisible();
    await paso(page, {
      titulo: 'Abre tu bóveda y elige «Subir versión» del RUT',
      explicacion:
        'En «Mi bóveda» cada documento aparece una sola vez, con su versión vigente y, en «Con tus empresas», la versión que cada una aprobó. Con «Subir versión» llevas el archivo nuevo a la pantalla de carga del documento. ' +
        'Al guardarlo, Validasoft lo registra como la versión 2 y las empresas que usan el RUT reciben el cambio para revisarlo.',
      resaltar: tarjetaBoveda,
      configuracion: { ajuste: 'Vigencia y reglas del tipo de documento', ruta: 'Configuración › Tipos de documento (/tipos-documento)' },
    });
    await tarjetaBoveda.getByRole('link', { name: 'Subir versión' }).click();
    await expect(page.getByRole('button', { name: 'Elegir archivo' })).toBeVisible();
    const selector = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf(`RUT nuevo ${tercero.identificacion.numero}`));
    await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });

    // 3. La versión nueva queda en revisión.
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await expect(tarjeta(page, RUT)).toContainText('Versión 2');
    await paso(page, {
      titulo: 'Confirma que la versión nueva está en revisión',
      explicacion:
        'El RUT aparece con «Versión 2» y estado «En revisión»: la empresa lo revisa de nuevo. ' +
        'Si la empresa lo observa, te llega un correo con los motivos y lo corriges desde la misma tarjeta.',
      resaltar: tarjeta(page, RUT),
    });
  },
);
