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
    titulo: 'Ve que tu expediente quedó aprobado',
    resumen:
      'Cuando el área de la empresa aprueba todo, tu expediente queda aprobado y tu vínculo activo. Mira en «Tu revisión» la frase de aprobado y el estado de cada documento, que sigue vigente mientras no venza.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.», su flujo de Proveedor (RUT y certificado de seguridad y salud en el trabajo) y un área «Revisión» con una persona como miembro.',
      'Un tercero que subió sus dos documentos y cuyo expediente fue aprobado por el área Revisión, que era la única.',
    ],
    relacionados: ['tercero/subir-version-nueva', 'tercero/documento-por-vencer-y-vencido'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Andamios Seguros S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero}`));
    await subirDocumento(page, SST.nombre, pdf(`SST ${tercero.identificacion.numero}`), {
      campos: { 'Fecha de expedición': new Date(Date.now() - 2 * 86_400_000).toISOString().slice(0, 10) },
    });
    await esperarEstadoDelTercero(page, 'EnRevision');
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión');
    await aprobarArea(sofia, 'Revisión');
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // 1. La frase de aprobado.
    await paso(page, {
      titulo: 'Lee que tu información fue aprobada',
      explicacion:
        'La frase de arriba dice que la empresa aprobó tu información y que tu vínculo está activo. Ya no tienes que subir nada para esa empresa mientras tus documentos sigan vigentes. ' +
        'Si el área cierra la revisión sin aprobarla, la frase cambia y ya no dice que tu vínculo está activo.',
      resaltar: fraseDeEstado(page, 'Aprobado'),
    });

    // 2. Cada documento aprobado conserva su versión.
    await expect(tarjeta(page, RUT)).toContainText('Versión 1');
    await expect(tarjeta(page, RUT)).toContainText('Aprobado');
    await paso(page, {
      titulo: 'Revisa los documentos aprobados',
      explicacion:
        'Cada documento muestra la versión que la empresa aprobó y su estado. Aquí el RUT y el certificado de seguridad tienen la versión 1 aprobada. ' +
        'Un documento con fecha de vencimiento cambia su estado cuando se acerca la fecha, y entonces tienes que subir uno nuevo.',
      resaltar: [tarjeta(page, RUT), tarjeta(page, SST.nombre)],
      variantes: [
        { cuando: 'El documento tiene vigencia y se acerca su vencimiento', entonces: 'Aparece como «Por vencer» con el aviso correspondiente, y puedes subir una versión nueva sin esperar a que venza.' },
      ],
    });

    // 3. El vínculo aparece activo en «Mis empresas».
    await page.goto('/empresas');
    const item = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true }) });
    await expect(item).toContainText('Aprobado');
    await paso(page, {
      titulo: 'Confirma el vínculo en «Mis empresas»',
      explicacion:
        'En «Mis empresas» la tarjeta de la empresa muestra el vínculo con el estado Aprobado. Si tienes otras empresas, cada una aparece por separado: aprobar una no cambia lo que las demás revisan.',
      resaltar: item,
    });
  },
);
