import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Administrador de empresa',
    titulo: 'Concede a soporte de Validasoft un acceso temporal y lo revoca',
    resumen:
      'La administradora de una empresa concede al soporte de Validasoft un acceso temporal de lectura: por una empresa, por unas horas y con una razón. Mientras está vigente, el soporte puede leer la configuración, los flujos, los vínculos y los expedientes, pero nunca documentos. Puede revocarlo antes de que venza.',
    precondiciones: ['Una empresa del grupo con su administradora en Validasoft.', 'Sin accesos de soporte vigentes.'],
    relacionados: ['plataforma/ingresos-de-los-operadores', 'plataforma/operadores'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'accesos-soporte');
    await expect(app.getByRole('heading', { level: 1, name: 'Accesos de soporte' })).toBeVisible();
    await expect(app.getByText('No has concedido accesos de soporte.')).toBeVisible();

    await paso(page, {
      titulo: 'Abre Accesos de soporte',
      explicacion:
        'El soporte de Validasoft no ve nada de tu grupo salvo que se lo concedas. Aquí quedan registrados los accesos temporales que concediste, con su estado y cuándo expiran. ' +
        'Mientras no concedas ninguno, la lista está vacía.',
      resaltar: app.getByRole('button', { name: 'Conceder acceso' }),
      configuracion: {
        ajuste: 'Accesos de soporte',
        ruta: 'Configuración › Accesos de soporte (/accesos-soporte)',
        efecto: 'Un acceso de soporte es temporal, por empresa y con una razón; se puede revocar antes de que venza.',
      },
    });

    await app.getByRole('button', { name: 'Conceder acceso' }).click();
    const dialogo = app.getByRole('dialog', { name: 'Conceder acceso' });
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('textbox', { name: 'Para qué lo necesitan' }).fill('Revisar el error al importar vínculos desde el ERP');
    await paso(page, {
      titulo: 'Concede el acceso',
      explicacion:
        'Eliges la empresa, la duración en horas (de 1 a 72) y para qué lo necesitan. La razón es obligatoria y queda en la auditoría. ' +
        'Durante ese tiempo el soporte podrá leer la configuración, los flujos, los vínculos y los expedientes de la empresa. Nunca verá ni descargará documentos, ni podrá cambiar nada.',
      resaltar: [dialogo.getByRole('combobox', { name: 'Empresa' }), dialogo.getByRole('combobox', { name: 'Duración' }), dialogo.getByRole('textbox', { name: 'Para qué lo necesitan' })],
      variantes: [
        { cuando: 'No se escribe para qué', entonces: 'El diálogo no deja conceder el acceso sin una razón.' },
      ],
      configuracion: {
        ajuste: 'Duración del acceso de soporte',
        efecto: 'Pasado ese tiempo, el acceso expira solo; no hace falta revocarlo.',
      },
    });
    await dialogo.getByRole('button', { name: 'Conceder acceso', exact: true }).click();
    await expect(app.getByText('Acceso de soporte concedido.')).toBeVisible();

    const fila = app.getByRole('row', { name: /Revisar el error al importar vínculos/ });
    await expect(fila).toContainText('Vigente');
    await paso(page, {
      titulo: 'Revisa el acceso vigente',
      explicacion:
        'El acceso aparece en la lista con su empresa, la razón, cuándo expira y su estado. Mientras esté vigente indica las horas y minutos que le quedan. ' +
        'Cuando expira, pasa a «Expirado» y el soporte deja de tener acceso sin que tengas que hacer nada.',
      resaltar: fila,
      configuracion: {
        ajuste: 'Estado del acceso de soporte',
        efecto: 'Vigente, revocado o expirado; los revocados y expirados no se pueden reactivar, hay que conceder uno nuevo.',
      },
    });

    await app.getByRole('button', { name: `Revocar el acceso a ${e.norte.nombre}` }).click();
    const confirmar = app.getByRole('dialog', { name: `¿Revocar el acceso de soporte a ${e.norte.nombre}?` });
    await expect(confirmar).toBeVisible();
    await confirmar.getByRole('button', { name: 'Revocar', exact: true }).click();
    await expect(app.getByText('Acceso de soporte revocado.')).toBeVisible();
    await expect(fila).toContainText('Revocado');
    await paso(page, {
      titulo: 'Revoca el acceso antes de que venza',
      explicacion:
        'Si el soporte ya no lo necesita, revócalo: deja de entrar de inmediato. Si vuelve a hacer falta, hay que conceder un acceso nuevo; el revocado no se reactiva.',
      resaltar: fila,
      configuracion: {
        ajuste: 'Revocar un acceso de soporte',
        efecto: 'Corta el acceso de soporte de inmediato.',
      },
    });
  },
);
