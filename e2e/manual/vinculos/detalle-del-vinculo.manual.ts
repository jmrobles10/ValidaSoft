import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Revisa el detalle de un vínculo',
    resumen:
      'La administradora abre un vínculo desde la lista y revisa sus datos, el estado de la invitación, las referencias que le dieron los sistemas integrados y el historial de eventos.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Norte» y una administradora del grupo.',
      'Un vínculo de proveedor invitado por el sistema integrado, con su referencia externa.',
    ],
    relacionados: ['vinculos/lista-y-filtros', 'vinculos/reenviar-invitacion'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros del Norte S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero, 'PROVEEDOR');

    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` })).toBeVisible();
    await app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` }).click();
    const datos = app.locator('section').filter({ has: app.getByRole('heading', { name: 'Datos del vínculo', exact: true }) });
    await expect(datos).toBeVisible();

    // 1. Datos del vínculo.
    await paso(page, {
      titulo: 'Lee los datos del vínculo',
      explicacion:
        'El detalle abre con el tercero, su identificación, el tipo de vínculo y la empresa. «Contacto» es el correo al que se envían las invitaciones y los avisos de esta relación. ' +
        '«Creado» y «Última actualización» muestran cuándo se registró y cuándo cambió por última vez; el estado aparece junto al nombre del tercero.',
      resaltar: datos,
      variantes: [
        { cuando: 'El tercero ya tiene ficha en Validasoft', entonces: 'El nombre es un enlace a su ficha, y la identificación se ve completa.' },
        { cuando: 'El vínculo tiene atributos definidos por el tipo', entonces: 'Aparece una tarjeta «Atributos» con sus valores.' },
      ],
    });

    // 2. Invitación.
    await paso(page, {
      titulo: 'Revisa el estado de la invitación',
      explicacion:
        'En «Invitación» ves cuántos correos se enviaron y hasta cuándo vale el enlace. Mientras el tercero no lo acepte, el vínculo sigue en «Invitado»; cuando lo acepta, aparece «Aceptada» con la fecha. ' +
        'Si el vínculo se creó sin enviar correo, dice «Sin invitación enviada».',
      resaltar: datos.getByText(/Envíos:|Sin invitación enviada/).first(),
      variantes: [{ cuando: 'El vínculo lo creó un sistema integrado sin enviar correo', entonces: 'La invitación dice «Sin invitación enviada» y el tercero no recibe nada hasta reenviarla.' }],
    });

    // 3. Referencias externas.
    const referencias = app.locator('section').filter({ has: app.getByRole('heading', { name: 'Referencias externas', exact: true }) });
    await paso(page, {
      titulo: 'Revisa las referencias de cada sistema',
      explicacion:
        'Cuando un sistema integrado crea o enlaza el vínculo, su identificador queda en «Referencias externas» con el nombre del sistema y su referencia (aquí «E2E» y «' + invitacion.referencia + '»). ' +
        'Un mismo vínculo puede tener una referencia por cada sistema; con esa referencia se encuentra también desde la lista.',
      resaltar: referencias,
      configuracion: {
        ajuste: 'Sistemas integrados',
        ruta: 'Configuración › Sistemas (/sistemas)',
        efecto: 'Cada sistema que crea vínculos deja su referencia aquí.',
      },
    });

    // 4. Historial.
    const historial = app.locator('section').filter({ has: app.getByRole('heading', { name: 'Historial', exact: true }) });
    await expect(historial.getByText(/Invitación enviada|Vínculo creado|invit/i).first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el historial del vínculo',
      explicacion:
        'El historial es la línea de tiempo de lo que pasó con el vínculo: cuándo se creó, cuándo se envió la invitación y cada cambio de estado o de datos, con quién lo hizo. ' +
        'Es lo primero que revisas cuando alguien pregunta qué pasó con un tercero.',
      resaltar: historial,
    });
  },
);
