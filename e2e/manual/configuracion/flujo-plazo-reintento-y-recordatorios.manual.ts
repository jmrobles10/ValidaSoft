import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Fija el plazo de cargue, los recordatorios y el reintento',
    resumen:
      'La administradora define en un flujo de proveedores cuántos días tiene el tercero para completar su expediente, cuándo se le recuerda, y si un tercero rechazado puede volver a postularse y tras cuántos días.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y su área «Revisión» con miembros.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/flujo-vigencia-del-vinculo', 'operacion/plazo-de-cargue-y-caducidad'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'flujos');
    await expect(app.getByRole('heading', { level: 1, name: `Flujos aprobatorios de ${e.sur.nombre}` })).toBeVisible();

    // 1. Datos básicos y el plazo de cargue.
    await app.getByRole('button', { name: 'Nuevo flujo' }).click();
    await app.getByRole('textbox', { name: /^Código/ }).fill('PROV_PLAZO');
    await app.getByRole('textbox', { name: /^Nombre/ }).fill('Proveedores con plazo de cargue');
    await app.getByRole('combobox', { name: /^Tipo de vínculo/ }).click();
    await app.getByRole('option', { name: 'Proveedor', exact: true }).click();
    await app.getByRole('region', { name: 'Etapa 1' }).getByRole('combobox', { name: 'Área' }).click();
    await app.getByRole('option', { name: /^Revisión/ }).click();
    await app.getByRole('group', { name: 'Plazo de cargue' }).getByRole('textbox', { name: 'Días', exact: true }).fill('30');
    await paso(page, {
      titulo: 'Fija el plazo de cargue',
      explicacion:
        '«Plazo de cargue» son los días que tiene el tercero para completar el expediente inicial, y también para corregir cada devolución. Con 0 no hay plazo: el expediente no caduca. ' +
        'Si el plazo se cumple sin completar el expediente, este caduca: así funciona [[operacion/plazo-de-cargue-y-caducidad]].',
      resaltar: app.getByRole('group', { name: 'Plazo de cargue' }).getByRole('textbox', { name: 'Días', exact: true }),
      configuracion: {
        ajuste: 'Plazo de cargue del flujo',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Define cuánto tiene el tercero para entregar el expediente antes de que caduque.',
      },
      variantes: [{ cuando: 'El plazo es 0', entonces: 'Aparece «Sin plazo»: el expediente no caduca y los recordatorios no se pueden configurar.' }],
    });

    // 2. Recordatorios antes de vencer.
    await app.getByRole('textbox', { name: 'Recordatorios (días antes, separados por coma)' }).fill('10, 3');
    await expect(app.getByRole('textbox', { name: 'Recordatorios (días antes, separados por coma)' })).toHaveValue('10, 3');
    await paso(page, {
      titulo: 'Define cuándo se recuerda el plazo',
      explicacion:
        'En «Recordatorios» escribes cuántos días antes del vencimiento se le avisa al tercero, separados por coma. Con «10, 3» recibe un recordatorio diez días antes y otro tres días antes. ' +
        'Cada recordatorio debe ser menor que el plazo y son hasta cinco; si escribes uno inválido, el campo lo marca y no se puede guardar.',
      resaltar: app.getByRole('textbox', { name: 'Recordatorios (días antes, separados por coma)' }),
      variantes: [
        { cuando: 'Un recordatorio es igual o mayor que el plazo', entonces: 'El campo dice que cada recordatorio debe ser menor que el plazo.' },
        { cuando: 'El plazo es 0', entonces: 'El campo de recordatorios queda desactivado porque no hay plazo que vigilar.' },
      ],
    });

    // 3. Reintento tras un rechazo.
    await app.getByLabel('Permitir que el tercero rechazado vuelva a postularse').check();
    await app.getByRole('textbox', { name: 'Días de espera' }).fill('15');
    await paso(page, {
      titulo: 'Permite que un rechazado vuelva a postularse',
      explicacion:
        'Si el expediente se rechaza, por defecto solo tú puedes abrir uno nuevo desde el vínculo. Al permitir el reintento, el tercero ve los motivos en su portal y puede abrir un expediente nuevo por su cuenta. ' +
        '«Días de espera» cuenta desde el rechazo: con 15, el tercero debe esperar quince días; con 0, puede hacerlo de inmediato.',
      resaltar: [app.getByLabel('Permitir que el tercero rechazado vuelva a postularse'), app.getByRole('textbox', { name: 'Días de espera' })],
      variantes: [
        { cuando: 'No permites el reintento', entonces: 'Solo la empresa puede abrir un expediente nuevo desde el vínculo; el tercero no lo ve en su portal.' },
        { cuando: 'Los días de espera son 0', entonces: 'El tercero puede volver a postularse en cuanto lo rechacen.' },
      ],
    });

    // 4. Guardar el borrador.
    await app.getByRole('button', { name: 'Crear borrador' }).click();
    await expect(app.getByRole('button', { name: 'Publicar v1' })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda el flujo con sus plazos',
      explicacion:
        'Con «Crear borrador» el plazo, los recordatorios y el reintento quedan en el borrador v1. Los expedientes que ya están abiertos conservan la configuración con la que empezaron: estos cambios aplican a los expedientes nuevos cuando publiques.',
      resaltar: app.getByRole('button', { name: 'Publicar v1' }),
      avisos: true,
    });
  },
);
