import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'ERP Norte producción';

recorrido(
  {
    dominio: 'integracion',
    actor: 'Administrador de empresa',
    titulo: 'Crea una API key con sus permisos y empresas, rótala y revócala',
    resumen:
      'La administradora emite una clave para que un sistema llame a la API: elige sus permisos y las empresas a las que alcanza, ve la clave una sola vez, la rota con un periodo de solapamiento y, al final, la revoca.',
    precondiciones: [
      'Un grupo empresarial con dos empresas, el sistema integrado E2E registrado y una administradora que puede administrar API keys.',
    ],
    relacionados: ['integracion/sistema-integrador', 'integracion/invitar-por-api', 'integracion/consulta-de-certificacion'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'api-keys');
    await expect(app.getByRole('heading', { level: 1, name: 'API keys' })).toBeVisible();
    await expect(app.getByRole('table', { name: 'API keys' })).toBeVisible();

    await paso(page, {
      titulo: 'Abre Configuración › API keys',
      explicacion:
        'La tabla lista las claves del grupo: su nombre, la clave enmascarada, los permisos que tienen, las empresas de alcance, el sistema al que pertenecen, cuándo expiran y su estado (vigente, vencida o reemplazada). ' +
        'Una clave nunca se vuelve a mostrar completa: lo que ves es solo su final. Para crear una, pulsa «Nueva API key».',
      resaltar: [app.getByRole('table', { name: 'API keys' }), app.getByRole('button', { name: 'Nueva API key' })],
      configuracion: {
        ajuste: 'API keys',
        ruta: 'Configuración › API keys (/api-keys)',
        efecto: 'Cada clave autentica las llamadas a la API con los permisos y en las empresas que se le dieron.',
      },
    });

    await app.getByRole('button', { name: 'Nueva API key' }).click();
    const dialogo = app.getByRole('dialog', { name: 'Nueva API key' });
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('textbox', { name: /^Nombre/ }).fill(NOMBRE);
    await dialogo.getByRole('combobox', { name: /^Sistema integrado/ }).click();
    await app.getByRole('option', { name: /E2E/ }).click();
    await paso(page, {
      titulo: 'Pon un nombre y el sistema que la usará',
      explicacion:
        'El nombre sirve para reconocerla después: indica el sistema y el ambiente, por ejemplo «ERP producción». Si la clave es la llave de un sistema integrado registrado, elígelo: al desactivar ese sistema, la clave deja de autenticar. ' +
        'Sin sistema, la clave es de uso general.',
      resaltar: [dialogo.getByRole('textbox', { name: /^Nombre/ }), dialogo.getByRole('combobox', { name: /^Sistema integrado/ })],
      variantes: [
        { cuando: 'Eliges un sistema integrado', entonces: 'La clave queda ligada a ese sistema y se desactiva con él.' },
        { cuando: 'No eliges sistema', entonces: 'La clave no depende de ningún sistema integrado.' },
      ],
    });

    await dialogo.getByRole('checkbox', { name: /Consultar la certificación/ }).check();
    await dialogo.getByRole('radio', { name: 'Solo algunas empresas' }).check();
    await dialogo.getByRole('checkbox', { name: /Constructora Norte/ }).check();
    await paso(page, {
      titulo: 'Elige los permisos y las empresas de alcance',
      explicacion:
        'Marca solo lo que el sistema necesita: cada permiso habilita un tipo de llamada, y la lista dice cuántos marcaste. Después elige en qué empresas puede operar: «Todo el grupo» incluye también las que se agreguen después; ' +
        '«Solo algunas empresas» limita la clave a las que marques.',
      resaltar: [dialogo.getByRole('group', { name: /Permisos/ }), dialogo.getByRole('radiogroup', { name: /Empresas de alcance/ })],
      variantes: [
        { cuando: 'Eliges «Todo el grupo»', entonces: 'La clave alcanza también las empresas que se creen después en el grupo.' },
        { cuando: 'Eliges «Solo algunas empresas»', entonces: 'La clave solo opera en las empresas marcadas.' },
      ],
    });

    await paso(page, {
      titulo: 'Revisa las opciones de expiración y sandbox',
      explicacion:
        'La expiración es opcional: vacía, la clave no expira; si pones una fecha, vale hasta el final de ese día. La llave de sandbox opera en un entorno de pruebas aislado, sin datos reales y con los correos retenidos en un buzón, ' +
        'así que sirve para probar la integración sin tocar al grupo.',
      resaltar: dialogo.getByRole('checkbox', { name: /sandbox/i }),
      variantes: [
        { cuando: 'Dejas la expiración vacía', entonces: 'La clave no expira hasta que la rotes o la reveques.' },
        { cuando: 'Marcas «Llave de sandbox»', entonces: 'Se crea en el entorno de pruebas del grupo, no en el real.' },
      ],
    });

    await dialogo.getByRole('button', { name: 'Crear API key' }).click();
    const aviso = app.getByRole('dialog', { name: `API key «${NOMBRE}» creada` });
    await expect(aviso).toBeVisible();
    await paso(page, {
      titulo: 'Copia la clave: solo se muestra una vez',
      explicacion:
        'Al crear la clave, Validasoft la muestra completa una sola vez. Cópiala con «Copiar clave» y guárdala en el gestor de secretos del sistema que la va a usar. Ese sistema la envía en la cabecera X-Api-Key de cada llamada. ' +
        'Si la pierdes, no hay forma de recuperarla: crea otra y revoca esta.',
      resaltar: aviso.getByText(/no la volveremos a mostrar/),
    });

    await aviso.getByRole('button', { name: 'Listo, ya la guardé' }).click();
    const fila = app.getByRole('row', { name: new RegExp(NOMBRE) });
    await expect(fila).toBeVisible();
    await paso(page, {
      titulo: 'Revisa la clave en la tabla',
      explicacion:
        'La clave aparece como vigente, con sus permisos resumidos, su alcance y el sistema al que pertenece. Desde su fila puedes rotarla o revocarla. ' +
        'Una clave sin fecha de expiración se muestra como «No expira».',
      resaltar: fila,
    });

    await app.getByRole('button', { name: `Rotar ${NOMBRE}` }).click();
    const rotar = app.getByRole('dialog', { name: `Rotar ${NOMBRE}` });
    await rotar.getByRole('radio', { name: 'Una hora más' }).check();
    await paso(page, {
      titulo: 'Rota la clave con solapamiento',
      explicacion:
        'Rotar emite una clave nueva con los mismos permisos, alcance y sistema, y la anterior sigue valiendo el tiempo que elijas: ahora mismo, una hora, 24 horas o siete días. ' +
        'Así cambias la clave en el sistema que la usa sin cortarle el servicio. Coordina el cambio antes de que la anterior deje de valer.',
      resaltar: rotar.getByRole('radiogroup'),
      variantes: [
        { cuando: 'Eliges «No: deja de valer ahora»', entonces: 'La clave anterior deja de autenticar en ese momento; el sistema debe cambiarla al instante.' },
        { cuando: 'Eliges un solapamiento', entonces: 'La anterior sigue valiendo hasta la hora que muestra la pantalla de la clave nueva.' },
      ],
    });

    await rotar.getByRole('button', { name: 'Rotar clave' }).click();
    const nueva = app.getByRole('dialog', { name: `Clave nueva de «${NOMBRE}»` });
    await expect(nueva).toBeVisible();
    await expect(nueva.getByText(/La clave anterior vale hasta/)).toBeVisible();
    await paso(page, {
      titulo: 'Lee hasta cuándo vale la clave anterior',
      explicacion:
        'La clave nueva se muestra una sola vez, como la primera. Además, la pantalla dice hasta qué hora vale la anterior: ese es el plazo para cambiarla en el sistema. ' +
        'Cuando pase esa hora, las llamadas con la clave anterior responden 401.',
      resaltar: nueva.getByText(/La clave anterior vale hasta/),
    });

    await nueva.getByRole('button', { name: 'Listo, ya la guardé' }).click();
    await paso(page, {
      titulo: 'Mira la clave anterior como reemplazada',
      explicacion:
        'En la tabla, la clave anterior aparece como «Reemplazada» con la hora en que deja de valer, y la nueva como vigente. Mientras el plazo no pase, ambas autentican.',
      resaltar: app.getByRole('row', { name: new RegExp(NOMBRE) }).first(),
    });

    await app.getByRole('button', { name: `Revocar ${NOMBRE}` }).first().click();
    const revocar = app.getByRole('dialog', { name: `Revocar ${NOMBRE}` });
    await expect(revocar.getByText(/No se puede deshacer/)).toBeVisible();
    await paso(page, {
      titulo: 'Revoca una clave',
      explicacion:
        'Revocar deja de autenticar la clave, como mucho cinco minutos después de confirmar. Si un sistema la usa, deja de funcionar hasta que le pongas otra clave. ' +
        'No se puede deshacer: la fila queda en la tabla como historial.',
      resaltar: revocar.getByRole('button', { name: 'Revocar', exact: true }),
    });

    await revocar.getByRole('button', { name: 'Revocar', exact: true }).click();
    await expect(app.getByText(`${NOMBRE} quedó revocada.`)).toBeVisible();
    await paso(page, {
      titulo: 'Confirma que la clave quedó revocada',
      explicacion:
        'Validasoft confirma con un aviso que la clave quedó revocada, y su fila muestra el estado correspondiente. Desde ese momento, las llamadas con esa clave responden 401.',
      resaltar: app.getByRole('row', { name: new RegExp(NOMBRE) }).first(),
      avisos: true,
    });
  },
);
