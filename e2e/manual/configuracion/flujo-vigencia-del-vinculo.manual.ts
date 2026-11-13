import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Define la vigencia periódica del vínculo',
    resumen:
      'La administradora hace que la validación de los proveedores venza cada 365 días, con avisos a 30, 15 y 5 días antes y diez días de gracia. Al llegar el primer aviso, el proveedor ratifica o actualiza la información que pide el flujo.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con miembros y la sección «Información general» disponible.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/flujo-secciones-de-la-ficha', 'configuracion/flujo-plazo-reintento-y-recordatorios'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'flujos');
    await expect(app.getByRole('heading', { level: 1, name: `Flujos aprobatorios de ${e.sur.nombre}` })).toBeVisible();

    // 1. Un flujo con una sección que el proveedor ratifica.
    await app.getByRole('button', { name: 'Nuevo flujo' }).click();
    await app.getByRole('textbox', { name: /^Código/ }).fill('PROV_VIGENCIA');
    await app.getByRole('textbox', { name: /^Nombre/ }).fill('Proveedores con vigencia anual');
    await app.getByRole('combobox', { name: /^Tipo de vínculo/ }).click();
    await app.getByRole('option', { name: 'Proveedor', exact: true }).click();
    await app.getByRole('region', { name: 'Etapa 1' }).getByRole('combobox', { name: 'Área' }).click();
    await app.getByRole('option', { name: /^Revisión/ }).click();
    await app.getByRole('button', { name: 'Agregar sección de la información' }).click();
    await app.getByRole('combobox', { name: 'Sección de la información del tercero' }).nth(0).click();
    await app.getByRole('option', { name: 'Información general', exact: true }).click();
    await paso(page, {
      titulo: 'Mira la vigencia del vínculo antes de llenarla',
      explicacion:
        'La vigencia periódica cuenta días desde la última aprobación del vínculo, no desde que se creó. Cuando faltan los días de aviso, el vínculo queda «Por vencer» y el tercero debe ratificar la información o actualizar las secciones que pide el flujo. ' +
        'Si la validación se aprueba a tiempo, no vence; si no, pasa a vencida al terminar la gracia.',
      resaltar: app.getByRole('group', { name: 'Vigencia del vínculo' }),
      configuracion: {
        ajuste: 'Vigencia del vínculo (por flujo)',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Decide cada cuánto se vuelve a validar a cada proveedor de este flujo.',
      },
      variantes: [{ cuando: 'Días es 0', entonces: 'El vínculo no vence por tiempo: los avisos y la gracia se desactivan.' }],
    });

    // 2. Días, avisos y gracia.
    await app.getByRole('group', { name: 'Vigencia del vínculo' }).getByRole('textbox', { name: 'Días', exact: true }).fill('365');
    await app.getByRole('textbox', { name: 'Avisos (días antes de vencer)' }).fill('30, 15, 5');
    await app.getByRole('textbox', { name: 'Gracia (días)' }).fill('10');
    await expect(app.getByRole('textbox', { name: 'Gracia (días)' })).toHaveValue('10');
    await paso(page, {
      titulo: 'Define los días, los avisos y la gracia',
      explicacion:
        '«Días» es la vigencia: 365 significa que cada año el proveedor vuelve a validarse. «Avisos» son los días antes de vencer en que se avisa, separados por coma: el primero abre el pre-vencimiento y el vínculo queda «Por vencer». ' +
        '«Gracia» son los días que sigue valiendo el vínculo después de la fecha de vencimiento, mientras el proveedor renueva.',
      resaltar: app.getByRole('group', { name: 'Vigencia del vínculo' }),
      variantes: [
        { cuando: 'Un aviso es mayor que la vigencia', entonces: 'El campo de avisos lo marca y no se puede guardar.' },
        { cuando: 'La gracia está fuera de 0 a 365 días', entonces: 'El campo dice que la gracia va de 0 a 365 días.' },
      ],
    });

    // 3. Guardar el borrador.
    await app.getByRole('button', { name: 'Crear borrador' }).click();
    await expect(app.getByRole('button', { name: 'Publicar v1' })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda la vigencia en el borrador',
      explicacion:
        'Con «Crear borrador» la vigencia queda en el borrador v1. Guardar un borrador no cambia ningún vínculo: el flujo todavía no está publicado y ninguna validación lo usa. Para que la vigencia rija, publica la versión.',
      resaltar: app.getByRole('button', { name: 'Publicar v1' }),
      avisos: true,
    });
  },
);
