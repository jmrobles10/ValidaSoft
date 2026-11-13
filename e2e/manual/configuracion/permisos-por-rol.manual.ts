import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Comprueba qué secciones ve cada rol',
    resumen:
      'La administradora compara lo que ve una persona con rol Administrador, una Revisora y una Oficial de cumplimiento. Cada rol ve solo las secciones que le corresponden; las demás muestran un aviso de «Esta sección es de…» en lugar de su contenido.',
    precondiciones: [
      'Un grupo empresarial con las empresas «Constructora Norte S.A.S.» y «Constructora Sur S.A.S.».',
      'Personas con roles distintos: Ana (Administradora del grupo), Sofía (Revisora de Constructora Sur) y Olga (Oficial de cumplimiento de Constructora Norte).',
    ],
    relacionados: ['configuracion/usuarios-y-roles'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();

    // 1. Lo que ve una Revisora al abrir una sección de configuración.
    const sofia = await abrirEmbebido(page, e, e.sofia, e.sur, 'usuarios');
    await expect(sofia.getByRole('alert')).toContainText('Tu rol en esta empresa no la incluye');
    await paso(page, {
      titulo: 'Abre una sección que tu rol no incluye',
      explicacion:
        'Sofía es Revisora de Constructora Sur: su trabajo es la bandeja de revisión, no la configuración. Si abre una sección que su rol no incluye, la pantalla no muestra el contenido: dice «Esta sección es de…» y explica que su rol no la incluye y que debe pedir acceso a un administrador del grupo. ' +
        'Así no hay botones que aparezcan y fallen: lo que no le corresponde no se ofrece.',
      resaltar: sofia.getByRole('alert'),
      configuracion: {
        ajuste: 'Roles por empresa',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'Cada rol decide qué secciones de la empresa ve la persona.',
      },
      variantes: [{ cuando: 'El rol es de todo el grupo', entonces: 'La persona ve las secciones del grupo en todas las empresas, y las de cada empresa según el rol de esa empresa.' }],
    });

    // 2. Lo que ve el Administrador en la misma sección.
    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'usuarios');
    await expect(ana.getByRole('heading', { level: 1, name: 'Usuarios y roles' })).toBeVisible();
    await expect(ana.getByRole('button', { name: 'Agregar persona' }).first()).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Compara con lo que ve el Administrador',
      explicacion:
        'El Administrador del grupo ve la misma sección completa, con el botón «Agregar persona» y las acciones de cada fila. Un rol que no la incluye, como el de Revisora, ve en cambio el aviso del paso anterior.',
      resaltar: ana.getByRole('button', { name: 'Agregar persona' }).first(),
    });

    // 3. Una Oficial de cumplimiento ante una sección de administración.
    const paginaOlga = await (await browser.newContext()).newPage();
    const olga = await abrirEmbebido(paginaOlga, e, e.olga, e.norte, 'dominio-correo');
    await expect(olga.getByRole('alert')).toContainText('Tu rol en esta empresa no la incluye');
    await paso(paginaOlga, {
      titulo: 'Revisa una sección de administración con otro rol',
      explicacion:
        'Olga es Oficial de cumplimiento de Constructora Norte. Su rol atiende cumplimiento y estudios SAGRILAFT, no la administración del dominio de correo: la pantalla le muestra el mismo aviso que a Sofía. ' +
        'Con el rol correcto la sección aparece completa; si el rol cambia, cambia lo que ve en su próximo ingreso.',
      resaltar: olga.getByRole('alert'),
      variantes: [{ cuando: 'Olga recibe el rol de Administrador en la empresa', entonces: 'Ve la sección completa en su próximo ingreso desde su sistema.' }],
    });
  },
);
