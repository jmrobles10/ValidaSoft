import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Registra el dominio de correo y verifícalo',
    resumen:
      'La administradora registra un dominio propio para que los correos de la empresa salgan desde él, crea los registros DNS que pide Validasoft y lo verifica. Después quita el dominio y el correo vuelve al remitente de la plataforma.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.», sin dominio de correo registrado.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/plantillas-de-correo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const dominio = `proveedores-${e.sufijo}.co`;
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'dominio-correo');
    await expect(app.getByRole('heading', { level: 1, name: 'Dominio de correo' })).toBeVisible();
    await expect(app.getByText('sigue saliendo desde', { exact: false })).toBeVisible();

    // 1. Estado inicial: sin dominio propio.
    await paso(page, {
      titulo: 'Abre Configuración › Dominio de correo',
      explicacion:
        'Por omisión, los correos de la empresa salen desde el remitente de la plataforma. Esta pantalla sirve para que salgan desde tu propio dominio, por ejemplo proveedores.miempresa.com. ' +
        'La tarjeta «Cómo sale hoy el correo» muestra desde qué remitente sale ahora. Mientras no verifiques tu dominio, nada se corta: el correo sigue saliendo desde el de la plataforma.',
      resaltar: app.getByRole('textbox', { name: 'Dominio' }),
      configuracion: {
        ajuste: 'Dominio de correo',
        ruta: 'Configuración › Dominio de correo (/dominio-correo)',
        efecto: 'Define desde qué dirección de correo le escribe Validasoft a los terceros de la empresa.',
      },
      variantes: [{ cuando: 'No tienes permiso para administrar la empresa', entonces: 'La pantalla no muestra el formulario ni los registros: solo se puede ver con permiso de administración.' }],
    });

    // 2. Dominio y remitente. Con el texto escrito se muestra la dirección que saldrá.
    await app.getByRole('textbox', { name: 'Dominio' }).fill(dominio);
    await app.getByRole('textbox', { name: 'Nombre del remitente' }).fill('Compras Constructora Sur');
    await expect(app.getByText(`Compras Constructora Sur <no-responder@${dominio}>`)).toBeVisible();
    await paso(page, {
      titulo: 'Escribe el dominio y el nombre del remitente',
      explicacion:
        'Escribe el dominio sin http:// ni la arroba. «Antes de la arroba» viene como no-responder, y «Nombre del remitente» como el nombre de la empresa; ambos puedes cambiarlos. ' +
        'La línea «Cuando lo verifiques, el correo saldrá de» muestra la dirección exacta mientras escribes, para que la revises antes de guardar.',
      resaltar: [app.getByRole('textbox', { name: 'Dominio' }), app.getByRole('textbox', { name: 'Nombre del remitente' })],
      variantes: [
        { cuando: 'El dominio tiene un formato inválido', entonces: 'El campo se marca en rojo con la explicación del error y el botón «Registrar el dominio» no guarda.' },
        { cuando: 'Ya hay un dominio y cambias el nombre del dominio', entonces: 'El botón dice «Guardar los cambios» y aparece el aviso de que cambiar el dominio pide verificarlo de nuevo.' },
      ],
    });

    // 3. Registrar: aparecen los registros DNS.
    await app.getByRole('button', { name: 'Registrar el dominio' }).click();
    const registros = app.getByRole('list', { name: 'Registros DNS' });
    await expect(registros.getByRole('listitem')).toHaveCount(3);
    await expect(app.getByText('Pendiente de verificar', { exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Crea los registros DNS en tu dominio',
      explicacion:
        'Al registrar el dominio aparecen tres registros que debes crear en el panel donde administras el dominio: SPF, DKIM y DMARC. Cada uno tiene su tipo, su nombre y su valor, y un botón para copiarlo. ' +
        'SPF autoriza a la plataforma a enviar en nombre de tu dominio, DKIM firma cada correo y DMARC indica qué hacer con lo que no pase esas dos comprobaciones. Los cambios de DNS pueden tardar en propagarse.',
      resaltar: registros,
      avisos: true,
      variantes: [{ cuando: 'Cambias o quitas el dominio después', entonces: 'Los registros que ya creaste en tu DNS no se borran automáticamente: quítalos tú en el panel de tu dominio.' }],
    });

    // 4. Verificar.
    await app.getByRole('button', { name: 'Verificar ahora' }).click();
    await expect(app.getByText('Verificado', { exact: true })).toBeVisible();
    await expect(app.getByText('Es tu remitente propio', { exact: false })).toBeVisible();
    await paso(page, {
      titulo: 'Verifica el dominio',
      explicacion:
        '«Verificar ahora» consulta los tres registros en el DNS. Si todos están bien, el estado pasa a «Verificado» y el correo de la empresa sale desde su propio dominio. ' +
        'Si falta alguno, el estado dice «Falta algún registro», cada registro muestra si no se encontró o si tiene otro valor, y el correo sigue saliendo desde el remitente de la plataforma hasta que todo quede bien.',
      resaltar: app.getByText('Verificado', { exact: true }),
      variantes: [
        { cuando: 'Falta un registro', entonces: 'El registro aparece con «No encontrado» o «Distinto» y la verificación dice cuántos faltan.' },
        { cuando: 'El dominio ya está verificado', entonces: 'El botón «Verificar ahora» sigue disponible por si cambia el DNS.' },
      ],
      configuracion: {
        ajuste: 'Registros DNS del dominio',
        ruta: 'Configuración › Dominio de correo (/dominio-correo)',
        efecto: 'Solo cuando los tres registros están bien el correo sale desde el dominio propio.',
      },
    });

    // 5. Quitar el dominio. El diálogo se confirma sin pantallazo.
    await app.getByRole('button', { name: 'Quitar el dominio' }).first().click();
    const confirmacion = app.getByRole('dialog', { name: `Quitar ${dominio}` });
    await expect(confirmacion).toContainText('vuelve a salir desde');
    await confirmacion.getByRole('button', { name: 'Quitar el dominio' }).click();
    await expect(app.getByText('Quitamos el dominio: el correo sale con el remitente de la plataforma.')).toBeVisible();
    await expect(app.getByText('sigue saliendo desde', { exact: false })).toBeVisible();
    await paso(page, {
      titulo: 'Quita el dominio de correo',
      explicacion:
        '«Quitar el dominio» pide confirmación y explica que el correo vuelve a salir desde el remitente de la plataforma. Al confirmar, el formulario queda vacío y la tarjeta «Cómo sale hoy el correo» vuelve a mostrar ese remitente. ' +
        'Los registros DNS que creaste no se borran: quítalos tú en el panel de tu dominio si ya no los necesitas.',
      resaltar: app.getByText('sigue saliendo desde', { exact: false }),
      avisos: true,
    });
  },
);
