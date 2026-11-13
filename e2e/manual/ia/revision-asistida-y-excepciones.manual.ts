import { pdf } from '../../soporte/archivos';
import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, regionDeArea } from '../../soporte/empresa';
import { completarInvitacionYEsperarRevisionOAprobacion } from '../../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'ia',
    actor: 'Revisor/área',
    titulo: 'Atiende una excepción de la revisión asistida',
    resumen:
      'Con el nivel Asistido la IA lee el RUT y sugiere, pero nunca aprueba sola. Cuando la lectura es dudosa, la IA sugiere revisar y muestra el motivo en el expediente; Sofía, de Revisión, lo abre y decide ella.',
    precondiciones: [
      'Un grupo con Constructora Sur, la IA activa, un flujo de Cliente que pide el RUT al área Revisión y la revisión automática en nivel Asistido.',
      'Un cliente que sube un RUT con lectura dudosa (confianza 55 %).',
    ],
    relacionados: ['ia/revision-automatica-niveles', 'ia/revision-automatica-aprueba-sola'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur);
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_ASISTIDO', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }]);
    await llamar('PUT', '/api/v1/configuracion/revision-automatica', {
      llave: e.admin,
      empresa: e.sur.id,
      cuerpo: {
        nivel: 'Asistido',
        umbralConfianza: 0.95,
        porcentajeMuestreo: 0,
        topeMensualAprobaciones: null,
        tiposAutorizados: ['RUT'],
        areasAutorizadas: [e.sur.areas.revision!.id],
        esperaIaMinutos: 10,
      },
    });

    const cliente = nuevoTercero('Aceros Dudosos S.A.S.');
    const invitacion = await invitar(e, e.sur, cliente, 'CLIENTE');
    const paginaCliente = await (await browser.newContext()).newPage();
    await completarInvitacionYEsperarRevisionOAprobacion(paginaCliente, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${cliente.nombre} [[nit=${cliente.identificacion.numero}]][[razonSocial=${cliente.nombre}]][[confianza=0.55]]`) },
    ]);

    const paginaSofia = await (await browser.newContext()).newPage();
    const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur, 'bandeja');
    await abrirDesdeBandeja(sofia, cliente.nombre, 'Revisión', 'Cliente');
    await expect(sofia.getByRole('heading', { level: 1, name: cliente.nombre })).toBeVisible();
    const decision = sofia.getByRole('region', { name: 'Decisión automática' });
    await expect(decision).toContainText('La IA sugiere revisar');
    await expect(decision).toContainText('Confianza baja');
    const aprobar = regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ });
    await paso(paginaSofia, {
      titulo: 'Abre el expediente con la sugerencia de la IA',
      explicacion:
        'En Asistido, la IA lee el documento y deja en cada expediente su sugerencia y su confianza. Cuando la lectura es dudosa, el bloque «Decisión automática» dice «La IA sugiere revisar» y el motivo, por ejemplo «Confianza baja». La IA nunca aprueba sola: la decisión es de quien revisa.',
      resaltar: [decision, sofia.getByRole('region', { name: 'Revisión automática', exact: true }).first()],
      variantes: [{ cuando: 'La lectura es limpia', entonces: 'La sugerencia aparece igual, pero la aprobación sigue siendo de una persona.' }],
    });

    await paso(paginaSofia, {
      titulo: 'Decide el área después de revisar el documento',
      explicacion:
        'Revisa el documento y sus datos extraídos. Si todo está bien, «Aprobar área» registra la decisión de Revisión con tu nombre. Si no, usa las acciones de observar o rechazar del área como con cualquier expediente.',
      resaltar: aprobar,
    });
  },
);
