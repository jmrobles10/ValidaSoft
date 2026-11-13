import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Concede un acceso de soporte por unas horas y luego lo revoca',
    resumen:
      'El soporte de Validasoft no ve nada del grupo salvo que se lo concedan. La administradora concede acceso a Constructora Sur por cuatro horas, con una razón, revisa cuánto le queda y, cuando ya no hace falta, lo revoca.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y sin accesos de soporte vigentes.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/permisos-por-rol'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'accesos-soporte');
    await expect(app.getByRole('heading', { level: 1, name: 'Accesos de soporte' })).toBeVisible();
    await expect(app.getByText('No has concedido accesos de soporte.')).toBeVisible();

    await paso(page, {
      titulo: 'Abre Configuración › Accesos de soporte',
      explicacion:
        'El soporte de Validasoft no entra a tu grupo por su cuenta. Esta pantalla lista los accesos que has concedido: la empresa, para qué, cuándo expira y su estado. ' +
        'Si no hay ninguno, la tabla lo dice; cuando el soporte necesite revisar algo, concedes un acceso temporal desde «Conceder acceso».',
      resaltar: app.getByRole('button', { name: 'Conceder acceso' }),
      configuracion: {
        ajuste: 'Accesos de soporte',
        ruta: 'Configuración › Accesos de soporte (/accesos-soporte)',
        efecto: 'El soporte solo ve lo de la empresa y el tiempo que concedas.',
      },
    });

    await app.getByRole('button', { name: 'Conceder acceso' }).click();
    const dialogo = app.getByRole('dialog');
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('combobox', { name: 'Empresa' }).click();
    await app.getByRole('option', { name: e.sur.nombre, exact: true }).click();
    await dialogo.getByRole('combobox', { name: 'Duración' }).click();
    await app.getByRole('option', { name: '4 horas', exact: true }).click();
    await dialogo.getByRole('textbox', { name: /^Para qué/ }).fill('Revisar el error al importar vínculos desde el ERP');
    await paso(page, {
      titulo: 'Concede el acceso con una razón',
      explicacion:
        'Eliges la empresa, la duración (de 1 a 72 horas) y escribes para qué lo necesitan. La razón queda en la auditoría, así que escríbela como la leerá alguien después. ' +
        'El soporte podrá leer la configuración, los flujos, los vínculos y los expedientes de esa empresa. Nunca verá ni descargará documentos, ni podrá cambiar nada.',
      resaltar: dialogo,
      variantes: [{ cuando: 'La razón supera los 500 caracteres', entonces: 'La pantalla dice «Máximo 500 caracteres» y no deja conceder el acceso.' }],
    });

    await dialogo.getByRole('button', { name: 'Conceder acceso' }).click();
    await expect(app.getByText('Acceso de soporte concedido.')).toBeVisible();
    const fila = app.getByRole('row', { name: new RegExp(e.sur.nombre) });
    await expect(fila).toContainText('Vigente');
    await paso(page, {
      titulo: 'Revisa el acceso concedido',
      explicacion:
        'La fila muestra la empresa, la razón, la hora de expiración y el tiempo que queda, por ejemplo «Vigente · quedan 4 h 0 min». Al vencer, pasa a «Expirado» sin que tengas que hacer nada.',
      resaltar: fila,
      avisos: true,
    });

    await fila.getByRole('button', { name: `Revocar el acceso a ${e.sur.nombre}` }).click();
    const confirmacion = app.getByRole('dialog', { name: new RegExp(`Revocar el acceso de soporte a ${e.sur.nombre}`) });
    await expect(confirmacion).toBeVisible();
    await paso(page, {
      titulo: 'Revoca el acceso antes de que expire',
      explicacion:
        'Si el soporte ya no lo necesita, «Revocar» corta el acceso de inmediato: deja de entrar a la empresa aunque le falten horas. Si lo vuelve a necesitar, concedes un acceso nuevo.',
      resaltar: confirmacion,
      variantes: [{ cuando: 'El acceso ya expiró', entonces: 'La fila muestra «Expirado» y no ofrece revocar.' }],
    });
    await confirmacion.getByRole('button', { name: 'Revocar', exact: true }).click();
    await expect(app.getByText('Acceso de soporte revocado.')).toBeVisible();
    await expect(fila).toContainText('Revocado');
    await paso(page, {
      titulo: 'Comprueba que el acceso quedó revocado',
      explicacion:
        'La fila queda con estado «Revocado». Queda registrado en la auditoría quién lo concedió, para qué y cuándo se revocó.',
      resaltar: fila,
      avisos: true,
    });
  },
);
