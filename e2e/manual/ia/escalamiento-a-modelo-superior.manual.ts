import { pdf } from '../../soporte/archivos';
import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, regionDeArea } from '../../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'ia',
    actor: 'Revisor/área',
    titulo: 'Revisa una lectura dudosa que la IA escaló a un modelo superior',
    resumen:
      'Cuando la lectura de un documento es dudosa, la IA vuelve a analizarlo con un modelo de mayor capacidad. Sofía, de Revisión, ve la confianza final, que el documento fue escalado y que sigue dudoso; la decisión es suya.',
    precondiciones: [
      'Un grupo con Constructora Sur, la IA activa y revisión automática en nivel Asistido.',
      'Un cliente que sube un RUT con lectura dudosa (confianza 55 %) y una certificación bancaria limpia.',
    ],
    relacionados: ['ia/revision-asistida-y-excepciones', 'ia/plan-y-uso-de-ia'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur);
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_ESC', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }, { tipoDocumento: 'CERTIFICACION_BANCARIA' }]);
    await llamar('PUT', '/api/v1/configuracion/revision-automatica', { llave: e.admin, empresa: e.sur.id, cuerpo: { nivel: 'Asistido' } });

    const tercero = nuevoTercero('Aceros Escalados S.A.S.');
    const paginaTercero = await (await browser.newContext()).newPage();
    await completarInvitacion(paginaTercero, await invitar(e, e.sur, tercero, 'CLIENTE'), [
      {
        nombre: RUT,
        archivo: pdf(`RUT ${tercero.nombre} [[nit=${tercero.identificacion.numero}]][[razonSocial=${tercero.nombre}]][[confianza=0.55]]`),
      },
      { nombre: 'Certificación bancaria', archivo: pdf(`Certificacion bancaria ${tercero.nombre}`) },
    ]);

    const paginaSofia = await (await browser.newContext()).newPage();
    const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur, 'bandeja');
    await expect(async () => {
      await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión', 'Cliente');
      await expect(sofia.getByRole('region', { name: 'Decisión automática' }).first()).toContainText('La IA sugiere revisar', { timeout: 1500 });
    }).toPass({ timeout: 60_000 });

    const panelRut = sofia.getByRole('region', { name: 'Revisión automática', exact: true }).first();
    await expect(panelRut).toContainText('Revisado por un modelo de mayor capacidad');
    await paso(paginaSofia, {
      titulo: 'Revisa la confianza final del análisis',
      explicacion:
        'La confianza del análisis aparece junto al documento. Cuando la lectura es dudosa, el sistema la analiza de nuevo con un modelo de mayor capacidad y lo indica con «Revisado por un modelo de mayor capacidad». Aun así, si la confianza sigue baja, el bloque de decisión dice «Confianza baja» y la decisión es de una persona.',
      resaltar: panelRut,
      configuracion: {
        ajuste: 'Confianza mínima de la revisión automática',
        ruta: 'Configuración › Revisión automática (/revision-automatica)',
        efecto: 'Por debajo de esa confianza, el sistema no sugiere ni aprueba: lo revisa una persona.',
      },
    });

    await expect(sofia.getByRole('region', { name: 'Decisión automática' }).first()).toContainText('Confianza baja');
    await expect(regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ })).toBeVisible();
    await paso(paginaSofia, {
      titulo: 'Decide el documento escalado',
      explicacion:
        'El escalamiento no aprueba nada: el documento sigue con la sugerencia de la IA y la persona decide. El botón «Aprobar área» queda disponible como siempre. El tercero no ve la confianza, el modelo ni el escalamiento: esa información es solo de la empresa.',
      resaltar: regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ }),
    });

    await esperarEstadoDelTercero(paginaTercero, 'EnRevision');
    await expect(paginaTercero.getByText(/Confianza|gpt-6|modelo de mayor capacidad/)).toHaveCount(0);
  },
);
