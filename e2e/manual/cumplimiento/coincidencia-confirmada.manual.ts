import { pedirVerificaciones, aprobarUltimaArea, esperarListas, personaEmbebida } from '../../soporte/cumplimiento';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion, rechazar } from '../../soporte/empresa';
import { abrirEmpresa, completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'Ferretería Coincidencia Confirmada S.A.S.';
const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Confirma una coincidencia y rechaza el vínculo',
    resumen:
      'La oficial de cumplimiento confirma que una coincidencia de listas es la misma persona. La verificación queda fallida, el expediente no se puede aprobar y Cumplimiento lo rechaza con motivos. La otra empresa del mismo tercero no cambia.',
    precondiciones: [
      'Un grupo con Constructora Norte (flujo con listas restrictivas bloqueantes) y Constructora Sur (sin verificaciones).',
      'Un tercero invitado en ambas empresas, con una coincidencia simulada en OFAC en la consulta de Norte.',
      'Compras de Norte ya aprobó; Sur ya tiene su documento en revisión.',
    ],
    relacionados: ['cumplimiento/analizar-una-coincidencia', 'cumplimiento/rechazo-directo-del-oficial'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
    const tercero = nuevoTercero(NOMBRE);
    const enNorte = await invitar(e, e.norte, tercero);
    const enSur = await invitar(e, e.sur, tercero);
    const pTercero = await (await browser.newContext()).newPage();
    await completarInvitacion(pTercero, enNorte, [
      { nombre: RUT, archivo: pdf(`RUT ${tercero.nombre}`) },
      { nombre: CAMARA, archivo: pdf(`Camara ${tercero.nombre}`) },
    ]);
    await completarInvitacion(pTercero, enSur, [{ nombre: SST.nombre, archivo: pdf(`SST ${tercero.nombre}`), campos: { 'Fecha de expedición': hoy() } }]);

    const ruben = await personaEmbebida(browser, e, e.ruben);
    await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
    await aprobarArea(ruben, 'Compras');

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await esperarListas(olga, tercero.nombre, 'Cumplimiento', 'Coincidencia por analizar');
    const coincidencia = olga.getByRole('listitem', { name: `${tercero.nombre} · OFAC (SDN)` });
    await coincidencia.getByRole('button', { name: 'Confirmar' }).click();
    const confirmar = olga.getByRole('dialog', { name: 'Confirmar coincidencia' });
    await confirmar.getByRole('textbox', { name: /Justificación/ }).fill('Es la misma persona: el NIT y el representante coinciden con el listado.');
    await paso(page, {
      titulo: 'Confirma la coincidencia',
      explicacion:
        'Confirmar dice que la persona de la lista sí corresponde al tercero. La verificación de listas pasa a «Fallida» y el expediente no puede aprobarse. ' +
        'Confirmar no rechaza el vínculo por sí solo: alguien que puede rechazar en un área, o un administrador que bloquea el vínculo, debe hacerlo. La justificación queda en el historial y en el informe SAGRILAFT.',
      resaltar: confirmar,
      variantes: [
        { cuando: 'Descartas en lugar de confirmar', entonces: 'La verificación queda satisfecha y el expediente puede aprobarse (ver «Analizar una coincidencia»).' },
      ],
    });
    await confirmar.getByRole('button', { name: 'Guardar decisión' }).click();
    await expect(confirmar).toBeHidden();
    await expect(olga.getByRole('region', { name: 'Listas restrictivas' })).toContainText('Fallida');

    await aprobarUltimaArea(olga, 'Cumplimiento', /Listas restrictivas \(fallida\)/);
    await paso(page, {
      titulo: 'Aprueba tu área: el expediente sigue bloqueado',
      explicacion:
        'Aunque Compras y Cumplimiento aprueben, el expediente no se aprueba: la verificación bloqueante falló y nada la reemplaza. El aviso lo dice con el nombre de la verificación («Listas restrictivas (fallida)»). ' +
        'El paso siguiente es rechazar el expediente desde Cumplimiento.',
      resaltar: olga.getByRole('alert').filter({ hasText: 'Falta cumplimiento para aprobar' }),
    });

    await pTercero.goto('/empresas');
    await abrirEmpresa(pTercero, e.norte.nombre);
    await esperarEstadoDelTercero(pTercero, 'EnRevision');
    await rechazar(olga, ['No corresponde al tercero'], 'Coincidencia confirmada con OFAC: no se puede vincular.');
    await expect(olga.getByRole('alert').filter({ hasText: 'Cumplimiento lo rechazó el' })).toContainText('El vínculo volvió a Invitado.');
    await paso(page, {
      titulo: 'Rechaza el expediente con motivos',
      explicacion:
        'Cumplimiento rechaza con uno o más motivos y una justificación interna. El vínculo vuelve a «Invitado» y el tercero ve que Constructora Norte cerró la revisión sin aprobarla. ' +
        'El tercero no ve ni la coincidencia ni la justificación: esos datos son de la empresa.',
      resaltar: olga.getByRole('alert').filter({ hasText: 'Cumplimiento lo rechazó el' }),
      configuracion: {
        ajuste: 'Quién puede rechazar por cumplimiento',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'Solo quien tiene el permiso de decidir cumplimiento puede rechazar desde su área.',
      },
    });
    await esperarEstadoDelTercero(pTercero, 'Rechazado');
    await paso(pTercero, {
      titulo: 'El tercero ve el cierre sin la coincidencia',
      explicacion:
        'Desde su lado el tercero solo ve que Constructora Norte cerró la revisión sin aprobarla. No aparece la palabra OFAC, ni la coincidencia, ni la justificación interna de Cumplimiento.',
      resaltar: pTercero.getByRole('main'),
    });

    // La otra empresa del mismo tercero no se toca: Sur sigue como estaba y puede aprobarlo.
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur, 'bandeja');
    await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Proveedor');
    await expect(sofia.getByRole('region', { name: 'Listas restrictivas' })).toHaveCount(0);
    await irASeccion(olga, 'Cumplimiento');
    await olga.getByRole('tab', { name: 'Informe SAGRILAFT' }).click();
    await expect(olga.getByRole('table', { name: 'Informe SAGRILAFT' })).toContainText(tercero.nombre);
    await paso(page, {
      titulo: 'Revisa el informe SAGRILAFT',
      explicacion:
        'El informe de Norte cuenta al tercero con su situación y su coincidencia confirmada. En Constructora Sur, en cambio, el mismo tercero no tiene verificaciones: su revisión sigue igual y la aprueba Sofía como cualquier otro proveedor.',
      resaltar: olga.getByRole('table', { name: 'Informe SAGRILAFT' }),
    });
  },
);

/** Fecha de hoy `YYYY-MM-DD` en Bogotá, para el campo de expedición del certificado de Sur. */
function hoy(): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
}
