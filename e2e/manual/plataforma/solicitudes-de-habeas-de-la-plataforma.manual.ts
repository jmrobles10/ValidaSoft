import { pdf } from '../../soporte/archivos';
import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Responde una supresión de datos sobre la bóveda de la plataforma',
    resumen:
      'Un tercero pide suprimir sus datos y la solicitud llega a la consola de Plataforma, porque la bóveda es global. El operador la abre, ve qué se elimina y qué se conserva por ley antes de ejecutar nada, y responde. El titular recibe la constancia por correo.',
    precondiciones: [
      'Un tercero con documentos en la bóveda y un vínculo con una empresa del grupo.',
      'Una solicitud de supresión ya enviada por el tercero desde su página de privacidad.',
    ],
    relacionados: ['privacidad/solicitud-sobre-mis-datos', 'privacidad/exportar-mi-boveda'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Tornillos del Valle S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero);
    await completarInvitacion(page, invitacion, [
      { nombre: 'Registro Único Tributario', archivo: pdf('RUT Tornillos del Valle') },
      { nombre: 'Certificado de existencia y representación legal', archivo: pdf('Camara Tornillos del Valle') },
    ]);
    await page.goto('/privacidad');
    await page.getByRole('button', { name: 'Nueva solicitud' }).click();
    const dialogo = page.getByRole('dialog', { name: 'Nueva solicitud sobre mis datos' });
    await dialogo.getByRole('radio', { name: /Suprimir mis datos/ }).check();
    await dialogo.getByRole('button', { name: 'Enviar solicitud' }).click();
    const tarjeta = page.getByRole('article').filter({ hasText: 'Suprimir mis datos' });
    await expect(tarjeta).toBeVisible();
    const radicado = (await tarjeta.textContent())?.match(/HD-\d{4}-[0-9A-F]{8}/)?.[0] ?? '';
    expect(radicado).not.toBe('');

    const consola = await abrirConsolaDePlataforma(await (await browser.newContext()).newPage());
    await consola.getByRole('link', { name: 'Habeas data' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Habeas data' })).toBeVisible();
    const solicitud = consola.getByRole('article').filter({ hasText: radicado });
    await expect(solicitud).toContainText(tercero.nombre);

    await paso(consola.owner().page(), {
      titulo: 'Abre las solicitudes de habeas data',
      explicacion:
        'Cada solicitud de un titular se muestra con su número de radicado, quién la hizo, el tipo (consulta, corrección o supresión) y su plazo. ' +
        'Como la bóveda de documentos es global, las solicitudes sobre ella llegan a Plataforma y no a una sola empresa: aquí se atienden.',
      resaltar: solicitud,
      configuracion: {
        ajuste: 'Solicitudes de habeas data de la plataforma',
        ruta: 'Plataforma › Habeas data (/plataforma/habeas)',
        efecto: 'Las solicitudes sobre la bóveda global se responden aquí, con plazo y constancia.',
      },
    });

    await solicitud.getByRole('button', { name: 'Responder' }).click();
    const responder = consola.getByRole('dialog', { name: `Responder la solicitud ${radicado}` });
    await expect(responder.getByText('Se eliminan')).toBeVisible();
    await expect(responder.getByText('Se conservan')).toBeVisible();
    await paso(consola.owner().page(), {
      titulo: 'Revisa qué se elimina y qué se conserva',
      explicacion:
        'Antes de responder, el diálogo separa tres grupos: lo que se elimina (no se puede recuperar), lo que se anonimiza (sin datos que identifiquen al titular) y lo que se conserva por obligación legal, con su base legal al lado. ' +
        'Nada se borra hasta que confirmas la respuesta. Puedes también elegir «Rechazar» y explicar por qué no procede.',
      resaltar: [responder.getByText('Se eliminan'), responder.getByText('Se conservan'), responder.getByText('Se anonimizan')],
      configuracion: {
        ajuste: 'Retención de datos por ley',
        efecto: 'Define qué se puede eliminar en una supresión y qué debe conservarse por obligación legal.',
      },
    });

    await responder.getByRole('textbox', { name: /Respuesta al titular/ }).fill('Eliminamos lo que no estamos obligados a conservar. Guardamos, por ley, lo que usó la empresa para aprobarte.');
    await responder.getByRole('button', { name: 'Suprimir y responder' }).click();
    await expect(responder).toBeHidden();
    await paso(consola.owner().page(), {
      titulo: 'Confirma la respuesta y la constancia',
      explicacion:
        'Al confirmar, tu respuesta le llega al titular a su correo y a su portal, con una constancia sellada. Queda en la auditoría y no se puede cambiar después. ' +
        'Las sesiones y enlaces del titular se cierran al suprimir todo.',
      resaltar: consola.getByRole('heading', { level: 1, name: 'Habeas data' }),
      configuracion: {
        ajuste: 'Constancia al titular',
        efecto: 'El titular la recibe por correo y puede guardarla como prueba de la atención.',
      },
    });
  },
);
