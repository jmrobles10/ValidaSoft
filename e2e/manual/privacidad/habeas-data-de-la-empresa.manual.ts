import { pdf } from '../../soporte/archivos';
import { abrirEmbebidoConLlave, llaveConHabeas } from '../../soporte/derechos';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'privacidad',
    actor: 'Administrador de empresa',
    titulo: 'Responde una solicitud de habeas data del titular',
    resumen:
      'Un proveedor pidió a la empresa conocer sus datos desde su portal. La solicitud llega a «Habeas data» con su radicado y el plazo en días hábiles; la administradora la responde y el titular recibe la respuesta y su constancia.',
    precondiciones: [
      'Un grupo con Constructora Norte, un proveedor con su expediente completado y una solicitud de habeas data de conocer sus datos dirigida a la empresa.',
    ],
    relacionados: ['privacidad/plazos-y-avisos-de-habeas'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    const llave = await llaveConHabeas(e);
    const tercero = nuevoTercero('Aceros del Sur S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero);
    const paginaTercero = await (await browser.newContext()).newPage();
    await completarInvitacion(paginaTercero, invitacion, [
      { nombre: 'Registro Único Tributario', archivo: pdf('RUT Aceros del Sur') },
      { nombre: 'Certificado de existencia y representación legal', archivo: pdf('Camara Aceros del Sur') },
    ]);
    await paginaTercero.goto('/privacidad');
    await paginaTercero.getByRole('button', { name: 'Nueva solicitud' }).click();
    const dialogo = paginaTercero.getByRole('dialog', { name: 'Nueva solicitud sobre mis datos' });
    await dialogo.getByRole('radio', { name: /Conocer mis datos/ }).check();
    await dialogo.getByRole('combobox', { name: 'Se la pides a' }).click();
    await paginaTercero.getByRole('option', { name: e.norte.nombre }).click();
    await dialogo.getByRole('button', { name: 'Enviar solicitud' }).click();
    const tarjeta = paginaTercero.getByRole('article').filter({ hasText: 'Conocer mis datos' });
    await expect(tarjeta).toContainText(/HD-\d{4}-[0-9A-F]{8}/);
    const radicado = (await tarjeta.textContent())?.match(/HD-\d{4}-[0-9A-F]{8}/)?.[0] ?? '';

    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebidoConLlave(paginaAna, e, llave, e.ana, e.norte, 'habeas-data');
    await expect(ana.getByRole('heading', { level: 1, name: 'Habeas data' })).toBeVisible();
    const solicitud = ana.getByRole('article').filter({ hasText: radicado });
    await expect(solicitud).toContainText(tercero.nombre);
    await expect(solicitud).toContainText(/Responde antes del .*: quedan \d+ días hábiles\./);
    await paso(paginaAna, {
      titulo: 'Abre la solicitud del titular',
      explicacion:
        'En «Habeas data» llegan las solicitudes de los titulares dirigidas a la empresa, con su radicado, quién la pide, su correo y el tipo de solicitud. Cada una dice hasta cuándo debe responderse, en días hábiles.',
      resaltar: solicitud,
      configuracion: {
        ajuste: 'Solicitudes de habeas data',
        ruta: 'Configuración › Habeas data (/habeas-data)',
        efecto: 'El plazo de respuesta cuenta en días hábiles desde que el titular envió la solicitud.',
      },
    });

    await solicitud.getByRole('button', { name: 'Responder' }).click();
    const responder = ana.getByRole('dialog', { name: `Responder la solicitud ${radicado}` });
    await expect(responder.getByText(`le llega al titular a ${tercero.correo}`, { exact: false })).toBeVisible();
    await responder.getByRole('textbox', { name: /Respuesta al titular/ }).fill('Tenemos tu RUT y tu cámara de comercio vigentes, y los datos de contacto de tu ficha.');
    await paso(paginaAna, {
      titulo: 'Escribe la respuesta al titular',
      explicacion:
        'El diálogo dice a qué correo llegará la respuesta. Escribe lo que le respondes al titular: debe tener al menos 10 caracteres. Al enviarla, la solicitud deja la lista de abiertas y el titular recibe el acuse por correo con su constancia.',
      resaltar: responder,
      completa: false,
    });
    await responder.getByRole('button', { name: 'Responder al titular' }).click();
    await expect(responder).toBeHidden();
    await expect(ana.getByText('No hay solicitudes abiertas')).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Comprueba que la solicitud quedó atendida',
      explicacion:
        'La lista de solicitudes abiertas queda vacía y la solicitud pasa a «Atendida». El titular recibe el correo con el radicado y ve la respuesta en su portal, con la constancia de la solicitud.',
      resaltar: ana.getByText('No hay solicitudes abiertas'),
    });
  },
);
