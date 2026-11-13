import { publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const CODIGO = 'PROV_VERSIONES';
const NOMBRE_NUEVO = 'Proveedores con revisión de Compras';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Cambia un flujo publicado con un borrador nuevo',
    resumen:
      'La administradora abre un flujo ya publicado, crea un borrador con cambios, lo guarda, descarta otros cambios sin guardar y publica la versión 2. La versión 1 queda retirada en el historial y se puede consultar en solo lectura.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de proveedores ya publicado (versión 1).',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/flujo-por-defecto', 'configuracion/flujo-etapas-y-areas-en-paralelo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const revision = e.sur.areas.revision;
    await publicarFlujoSinDocumentos(e, e.sur, CODIGO, 'PROVEEDOR', [[{ area: revision }]]);
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'flujos');
    await app.getByRole('row', { name: new RegExp(`Abrir flujo Flujo ${CODIGO}`) }).click();
    await expect(app.getByRole('heading', { level: 1, name: `Flujo ${CODIGO}` })).toBeVisible();
    const aviso = app.getByRole('alert').filter({ hasText: 'Al guardar se crea el borrador v2' });
    await expect(aviso).toBeVisible();

    // 1. La versión publicada solo se lee.
    await paso(page, {
      titulo: 'Abre un flujo publicado',
      explicacion:
        'Al abrir un flujo publicado ves sus datos y sus etapas, pero no se pueden cambiar. El aviso (1) dice que, al guardar, se crea el borrador v2 y que la versión publicada sigue vigente hasta que publiques; el botón «Editar» (2) empieza ese borrador. ' +
        'La versión publicada es la que usan los expedientes nuevos; los que ya están abiertos siguen con la versión con que empezaron.',
      resaltar: [aviso, app.getByRole('button', { name: 'Editar', exact: true })],
      configuracion: {
        ajuste: 'Versiones del flujo',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'La versión publicada define el flujo de los expedientes nuevos.',
      },
    });

    // 2. Editar: el borrador aún no se guarda.
    await app.getByRole('button', { name: 'Editar', exact: true }).click();
    const nombre = app.getByRole('textbox', { name: /^Nombre/ });
    await nombre.fill(NOMBRE_NUEVO);
    const guardar = app.getByRole('button', { name: 'Guardar borrador' });
    await expect(guardar).toBeVisible();
    await paso(page, {
      titulo: 'Edita el flujo',
      explicacion:
        'Con «Editar» el formulario se abre con una copia de la versión publicada. Cambias lo que necesites (aquí, el nombre (1)) y aparece «Guardar borrador» (2). ' +
        'Mientras no lo guardes, nada se crea en el servidor: si sales de la pantalla, los cambios se pierden.',
      resaltar: [nombre, guardar],
      variantes: [{ cuando: 'Faltan datos obligatorios o hay un error', entonces: 'Al guardar, el editor marca los campos con error, muestra «Revisa los campos marcados.» y no guarda.' }],
    });

    // 3. Guardar: nace el borrador v2.
    await guardar.click();
    await expect(app.getByText('Borrador guardado.')).toBeVisible();
    const publicar = app.getByRole('button', { name: 'Publicar v2' });
    await expect(publicar).toBeVisible();
    await expect(app.getByRole('button', { name: /^v2 .*Borrador$/ })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda el borrador v2',
      explicacion:
        'Al guardar se crea el borrador v2. Aparece «Publicar v2» (1) y, en «Versiones» (2), la v2 con el estado «Borrador» junto a la v1 «Publicada». ' +
        'Mientras el borrador no se publique, la v1 sigue vigente y los expedientes nuevos siguen usándola.',
      resaltar: [publicar, app.getByRole('button', { name: /^v2 .*Borrador$/ })],
      avisos: true,
    });

    // 4. Descartar lo que no se guardó.
    await nombre.fill('Un nombre que no se guarda');
    const descartar = app.getByRole('button', { name: 'Descartar cambios' });
    await expect(descartar).toBeVisible();
    await paso(page, {
      titulo: 'Descarta lo que no quieres guardar',
      explicacion:
        'Si cambias algo y te arrepientes, «Descartar cambios» (1) deshace lo escrito desde el último guardado. Está disponible cuando el borrador ya existe y el formulario tiene cambios sin guardar; ' +
        'mientras tanto, «Publicar» desaparece para que no publiques una versión distinta de la guardada.',
      resaltar: [descartar],
    });
    await descartar.click();
    await expect(nombre).toHaveValue(NOMBRE_NUEVO);
    await expect(publicar).toBeVisible();

    // 5. Publicar la v2.
    await publicar.click();
    const confirmacion = app.getByRole('dialog', { name: /^Publicar v2 de/ });
    await expect(confirmacion).toBeVisible();
    await paso(page, {
      titulo: 'Publica la versión 2',
      explicacion:
        'Publicar pide confirmación: los expedientes nuevos usarán esta versión y los que ya están abiertos siguen con la suya. La versión publicada anterior queda retirada.',
      resaltar: confirmacion,
    });
    await confirmacion.getByRole('button', { name: 'Publicar v2' }).click();
    await expect(app.getByText('Versión 2 publicada.')).toBeVisible();

    // 6. El historial: v2 vigente, v1 retirada.
    const v2 = app.getByRole('button', { name: /^v2 .*Publicada$/ });
    const v1 = app.getByRole('button', { name: /^v1 .*Retirada$/ });
    await expect(v2).toBeVisible();
    await expect(v1).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el historial de versiones',
      explicacion:
        'La lista «Versiones» muestra la v2 «Publicada» (1) con su fecha y la v1 «Retirada» (2) con la fecha en que se retiró. El historial no se borra: cada versión se puede abrir para ver cómo era.',
      resaltar: [v2, v1],
      variantes: [{ cuando: 'La empresa ya tenía expedientes abiertos con la v1', entonces: 'Siguen con la v1 hasta terminar; solo los expedientes nuevos usan la v2.' }],
    });

    // 7. Ver una versión anterior en solo lectura.
    await v1.click();
    const lectura = app.getByRole('alert').filter({ hasText: 'Estás viendo la v1 (retirada).' });
    await expect(lectura).toBeVisible();
    await paso(page, {
      titulo: 'Consulta una versión anterior',
      explicacion:
        'Al elegir una versión de la lista, el formulario muestra esa versión en solo lectura y el aviso (1) dice cuál estás viendo. «Volver a la versión vigente» regresa a la actual. ' +
        'Ver una versión no cambia cuál está vigente.',
      resaltar: [lectura],
    });
    await lectura.getByRole('button', { name: 'Volver a la versión vigente' }).click();
    await expect(lectura).toBeHidden();
  },
);
