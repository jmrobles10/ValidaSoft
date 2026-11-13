import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Abre la ficha de un tercero que trabaja con dos empresas del grupo',
    resumen:
      'La administradora de Constructora Norte abre la ficha de un tercero que también tiene vínculo con Constructora Sur. Ve sus datos y sus vínculos con Norte, y solo el nombre de la otra empresa: lo que Sur decidió no se muestra.',
    precondiciones: [
      'Un grupo con Constructora Norte y Constructora Sur, y una administradora del grupo.',
      'Un mismo tercero invitado por Norte y por Sur, cada uno con su vínculo en Invitado.',
    ],
    relacionados: ['vinculos/detalle-del-vinculo', 'vinculos/ficha-aceptada-del-vinculo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Tornillería El Perno S.A.S.');
    await invitar(e, e.norte, tercero, 'PROVEEDOR');
    await invitar(e, e.sur, tercero, 'PROVEEDOR');

    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
    await app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` }).click();
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();

    await paso(page, {
      titulo: 'Abre el vínculo del tercero',
      explicacion:
        'Desde el detalle de un vínculo, el nombre del tercero es un enlace a su ficha, que reúne todo lo que el grupo sabe del tercero. ' +
        'La ficha es la misma para cualquier empresa del grupo, pero lo que muestra de cada una depende de tu permiso para ver terceros.',
      resaltar: app.getByRole('link', { name: `Abrir la ficha de ${tercero.nombre}` }),
      configuracion: {
        ajuste: 'Usuarios y roles',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'El permiso de ver terceros decide si el nombre lleva a la ficha.',
      },
    });

    await app.getByRole('link', { name: `Abrir la ficha de ${tercero.nombre}` }).click();
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(app.getByText('Identificación y contactos')).toBeVisible();
    await paso(page, {
      titulo: 'Revisa la identificación del tercero',
      explicacion:
        'La primera sección reúne la identificación, el tipo de persona y los contactos registrados por el tercero. Esos datos son del tercero y son iguales para todas las empresas del grupo.',
      resaltar: app.getByRole('region', { name: 'Identificación y contactos' }).or(app.getByText('Identificación y contactos')),
    });

    const vinculos = app.getByRole('region', { name: 'Vínculos con Constructora Norte S.A.S.' }).or(app.getByRole('region', { name: /^Vínculos con / }));
    await expect(vinculos.first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa los vínculos con tu empresa',
      explicacion:
        'Esta sección lista solo los vínculos con tu empresa: el estado, el tipo y el certificado si existe. Desde aquí puedes ir al vínculo para actuar sobre él.',
      resaltar: vinculos.first(),
      variantes: [
        {
          cuando: 'Tu permiso no incluye ver vínculos',
          entonces: 'La sección no muestra los vínculos y dice que pidas acceso a un administrador.',
        },
      ],
    });

    await expect(app.getByText('Otras empresas del grupo')).toBeVisible();
    await paso(page, {
      titulo: 'Mira las otras empresas del grupo',
      explicacion:
        'La sección «Otras empresas del grupo» indica que el tercero también tiene vínculo con otras empresas del grupo, como Constructora Sur. Por cada una ves el nombre y el estado del vínculo; las decisiones de esas empresas no se muestran.',
      resaltar: app.getByText('Otras empresas del grupo'),
    });
  },
);
