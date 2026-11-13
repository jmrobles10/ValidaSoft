import { enRevision, esperarListas, pedirVerificaciones } from '../../soporte/cumplimiento';
import { abrirEmbebido } from '../../soporte/embebido';
import { expect, paso, recorrido } from '../nucleo';
import { crearEscenario } from '../../soporte/semilla';

const NOMBRE = 'Ferretería Coincidencia S.A.S.';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Rechaza el expediente por cumplimiento',
    resumen:
      'Tras confirmar una coincidencia de listas, la oficial de cumplimiento rechaza el expediente directamente desde Cumplimiento, sin esperar a que decida un área. Elige los motivos del catálogo y escribe la justificación. El rechazo es definitivo.',
    precondiciones: [
      'Un grupo con Constructora Norte, sus áreas Compras y Cumplimiento y el flujo de proveedores con listas restrictivas bloqueantes.',
      'Un proveedor en revisión cuya consulta de listas tiene una coincidencia por analizar.',
      'La empresa tiene motivos de rechazo configurados (Configuración › Motivos).',
    ],
    relacionados: ['cumplimiento/coincidencia-confirmada', 'cumplimiento/estudio-de-cumplimiento'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
    const pTercero = await (await browser.newContext()).newPage();
    await enRevision(pTercero, e, NOMBRE);

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await esperarListas(olga, NOMBRE, 'Cumplimiento', 'Coincidencia por analizar');
    await expect(olga.getByRole('button', { name: 'Rechazar por cumplimiento' })).toHaveCount(0);

    const coincidencia = olga.getByRole('listitem', { name: `${NOMBRE} · OFAC (SDN)` });
    await coincidencia.getByRole('button', { name: 'Confirmar' }).click();
    const confirmar = olga.getByRole('dialog', { name: 'Confirmar coincidencia' });
    await confirmar.getByRole('textbox', { name: /Justificación/ }).fill('Es la misma persona: el NIT y el representante coinciden.');
    await confirmar.getByRole('button', { name: 'Guardar decisión' }).click();
    await expect(confirmar).toBeHidden();
    await expect(olga.getByRole('region', { name: 'Listas restrictivas' })).toContainText('Fallida');

    await olga.getByRole('button', { name: 'Rechazar por cumplimiento' }).click();
    const dialogo = olga.getByRole('dialog', { name: /Rechazar por cumplimiento/ });
    await expect(dialogo).toContainText('El rechazo es definitivo');
    await dialogo.getByRole('checkbox', { name: /^No corresponde al tercero / }).check();
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Coincidencia confirmada con OFAC: no se puede vincular.');
    await paso(page, {
      titulo: 'Abre el rechazo por cumplimiento y elige los motivos',
      explicacion:
        'El rechazo se ofrece solo cuando hay un hallazgo grave confirmado: una coincidencia confirmada en listas o un estudio desfavorable. El diálogo lo dice: el expediente se cierra, el vínculo vuelve a Invitado y el tercero recibe por correo los textos de los motivos que elijas. ' +
        'Marca uno o más motivos del catálogo y escribe la justificación: queda en el historial y en el informe SAGRILAFT, pero el tercero no la lee.',
      resaltar: dialogo,
      variantes: [
        { cuando: 'El hallazgo es un estudio de cumplimiento desfavorable', entonces: 'El diálogo cita ese hallazgo en lugar de la coincidencia y el rechazo se ofrece igual.' },
        { cuando: 'No hay ningún hallazgo grave', entonces: 'El botón «Rechazar por cumplimiento» no aparece: solo se rechaza por una verificación fallida.' },
        { cuando: 'Eres revisor de un área', entonces: 'No ves el botón de rechazo por cumplimiento; las áreas rechazan desde su propia decisión.' },
      ],
      configuracion: {
        ajuste: 'Motivos de rechazo',
        ruta: 'Configuración › Motivos (/motivos)',
        efecto: 'Son los motivos que puedes marcar al rechazar. Sin motivos configurados, el rechazo no se puede hacer.',
      },
    });

    await dialogo.getByRole('button', { name: 'Rechazar expediente' }).click();
    await expect(dialogo).toBeHidden();
    await expect(olga.getByRole('alert').filter({ hasText: 'Cumplimiento lo rechazó el' })).toContainText('El vínculo volvió a Invitado.');
    await paso(page, {
      titulo: 'Confirma el rechazo',
      explicacion:
        'El expediente se cierra y el aviso queda con la fecha y «El vínculo volvió a Invitado». El tercero recibe por correo los textos de los motivos que elegiste. ' +
        'En el historial queda quién rechazó, cuándo y la justificación.',
      resaltar: olga.getByRole('alert').filter({ hasText: 'Cumplimiento lo rechazó el' }),
    });
  },
);
