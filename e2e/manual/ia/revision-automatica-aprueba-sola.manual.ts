import { pdf } from '../../soporte/archivos';
import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { completarInvitacionYEsperarRevisionOAprobacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { activarIa, crearEscenario, invitar, nuevoTercero, publicarFlujoConFicha } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'ia',
    actor: 'Revisor/área',
    titulo: 'Revisa un documento que el sistema aprobó solo',
    resumen:
      'Con el nivel Automático, el RUT limpio de un cliente lo aprueba el sistema sin que nadie lo vea. Sofía, de Revisión, abre el expediente y encuentra la decisión automática, con su confianza y quién la tomó en el historial.',
    precondiciones: [
      'Un grupo con Constructora Sur, la IA activa, un flujo de Cliente que pide el RUT al área Revisión y la revisión automática en nivel Automático con muestra 0 %.',
      'Un cliente que sube un RUT con lectura limpia (confianza 97 %).',
    ],
    relacionados: ['ia/revision-automatica-niveles', 'ia/revision-asistida-y-excepciones'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur);
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_APRUEBA', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }]);
    await llamar('PUT', '/api/v1/configuracion/revision-automatica', {
      llave: e.admin,
      empresa: e.sur.id,
      cuerpo: {
        nivel: 'Automatico',
        umbralConfianza: 0.95,
        porcentajeMuestreo: 0,
        topeMensualAprobaciones: null,
        tiposAutorizados: ['RUT'],
        areasAutorizadas: [e.sur.areas.revision!.id],
        esperaIaMinutos: 10,
      },
    });

    const cliente = nuevoTercero('Aceros Limpios S.A.S.');
    const invitacion = await invitar(e, e.sur, cliente, 'CLIENTE');
    const paginaCliente = await (await browser.newContext()).newPage();
    await completarInvitacionYEsperarRevisionOAprobacion(paginaCliente, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${cliente.nombre} [[nit=${cliente.identificacion.numero}]][[razonSocial=${cliente.nombre}]]`) },
    ]);
    await esperarEstadoDelTercero(paginaCliente, 'Aprobado');
    const expedientes = await llamar<{ datos: { id: string }[] }>('GET', '/api/v1/expedientes', { llave: e.admin, empresa: e.sur.id });
    const expedienteId = expedientes.datos[0]!.id;

    const paginaSofia = await (await browser.newContext()).newPage();
    const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur, `expedientes/${expedienteId}`);
    await expect(sofia.getByRole('heading', { level: 1, name: cliente.nombre })).toBeVisible();
    const decision = sofia.getByRole('region', { name: 'Decisión automática' });
    await expect(decision).toContainText('Aprobado por el sistema');
    await paso(paginaSofia, {
      titulo: 'Abre el expediente aprobado por el sistema',
      explicacion:
        'El expediente ya está aprobado para el área Revisión, pero no lo aprobó una persona. El bloque «Decisión automática» dice que el sistema aprobó el RUT porque la lectura fue limpia y no tuvo excepciones, y muestra la confianza del análisis.',
      resaltar: decision,
      variantes: [
        { cuando: 'La lectura del documento es dudosa', entonces: 'El sistema no lo aprueba: el documento pasa a Excepciones y una persona decide.' },
        { cuando: 'El nivel es Asistido o Manual', entonces: 'Ningún documento lo aprueba el sistema; la decisión la toma una persona.' },
      ],
    });

    await paso(paginaSofia, {
      titulo: 'Revisa quién registró la aprobación',
      explicacion:
        'En el historial del expediente, la aprobación del RUT figura como «Sistema (IA)», no como una persona. Así queda claro que nadie lo vio antes de aprobarse.',
      resaltar: sofia.getByText('Sistema (IA)').first(),
    });
  },
);
