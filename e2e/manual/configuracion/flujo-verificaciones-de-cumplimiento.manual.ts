import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Pide listas restrictivas y el estudio SAGRILAFT en el flujo',
    resumen:
      'La administradora agrega al flujo de proveedores dos verificaciones de cumplimiento: la consulta de listas restrictivas, que bloquea la aprobación si hay coincidencias, y el estudio del oficial de cumplimiento, con vigencia y reconsulta propias.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con miembros, y el proveedor de listas de la plataforma disponible.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['cumplimiento/matriz-de-riesgo-configuracion', 'cumplimiento/listas-restrictivas-de-la-empresa'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'flujos');
    await expect(app.getByRole('heading', { level: 1, name: `Flujos aprobatorios de ${e.sur.nombre}` })).toBeVisible();

    // 1. Activar las dos verificaciones.
    await app.getByRole('button', { name: 'Nuevo flujo' }).click();
    await app.getByRole('textbox', { name: /^Código/ }).fill('PROV_CUMPLIMIENTO');
    await app.getByRole('textbox', { name: /^Nombre/ }).fill('Proveedores con verificaciones');
    await app.getByRole('combobox', { name: /^Tipo de vínculo/ }).click();
    await app.getByRole('option', { name: 'Proveedor', exact: true }).click();
    await app.getByRole('region', { name: 'Etapa 1' }).getByRole('combobox', { name: 'Área' }).click();
    await app.getByRole('option', { name: /^Revisión/ }).click();
    await app.getByLabel('Pedir Listas restrictivas').check();
    await app.getByLabel('Pedir Estudio del oficial de cumplimiento (SAGRILAFT)').check();
    await paso(page, {
      titulo: 'Pide las verificaciones de cumplimiento',
      explicacion:
        'Las verificaciones son lo que debe estar satisfecho antes de aprobar el expediente, además de las áreas. «Listas restrictivas» se consulta sola al pasar a revisión, con el tercero, su representante, suplentes, socios y PEP declarados en su ficha. ' +
        '«Estudio del oficial de cumplimiento» es la debida diligencia que hace el oficial: nivel de riesgo, conflicto de interés, observaciones y concepto.',
      resaltar: [app.getByLabel('Pedir Listas restrictivas'), app.getByLabel('Pedir Estudio del oficial de cumplimiento (SAGRILAFT)')],
      configuracion: {
        ajuste: 'Verificaciones de cumplimiento del flujo',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Un expediente de este flujo no se aprueba hasta que estas verificaciones estén satisfechas.',
      },
      variantes: [{ cuando: 'No activas ninguna', entonces: 'El expediente se aprueba solo con las áreas, como en los flujos sin verificaciones.' }],
    });

    // 2. Bloqueo, vigencia y reconsulta.
    await app.getByLabel('Bloquea la aprobación').nth(0).check();
    await app.getByLabel('Vigencia (días)').fill('180');
    await app.getByLabel('Reconsultar cada (meses)').fill('6');
    await paso(page, {
      titulo: 'Define si bloquea y cada cuánto se vuelve a consultar',
      explicacion:
        '«Bloquea la aprobación» hace que una verificación pendiente, con coincidencias por analizar, fallida o vencida impida aprobar el expediente. Sin esa marca, la verificación solo informa. ' +
        '«Vigencia (días)» dice cuánto vale una consulta o un estudio antes de vencer; vacío son 30 días. «Reconsultar cada (meses)» es la frecuencia con que se vuelve a consultar al vínculo activo; vacío son 12 meses.',
      resaltar: [app.getByLabel('Bloquea la aprobación').nth(0), app.getByLabel('Vigencia (días)'), app.getByLabel('Reconsultar cada (meses)')],
      variantes: [
        { cuando: 'La verificación no bloquea', entonces: 'Aparece como «Informativa»: se muestra al oficial, pero no impide la aprobación.' },
        { cuando: 'Dejas la vigencia vacía', entonces: 'La verificación vence a los 30 días de configuración.' },
      ],
    });

    // 3. Guardar el borrador.
    await app.getByRole('button', { name: 'Crear borrador' }).click();
    await expect(app.getByRole('button', { name: 'Publicar v1' })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda las verificaciones en el borrador',
      explicacion:
        'Con «Crear borrador» las verificaciones quedan en el borrador v1. Las consultas se hacen cuando el expediente pasa a revisión, no al guardar el flujo; el flujo empieza a usarse al publicarlo.',
      resaltar: app.getByRole('button', { name: 'Publicar v1' }),
      avisos: true,
    });
  },
);
