import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Pide secciones de «Mi información» como requisito del flujo',
    resumen:
      'La administradora agrega a un flujo de proveedores dos secciones de «Mi información»: la financiera, por periodos, y la legal, solo para personas jurídicas. Define la vigencia y el plazo de revalidación de cada una y guarda el borrador.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con miembros y los tipos de documento base.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/flujo-condiciones-por-tipo-de-persona', 'configuracion/flujo-vigencia-del-vinculo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'flujos');
    await expect(app.getByRole('heading', { level: 1, name: `Flujos aprobatorios de ${e.sur.nombre}` })).toBeVisible();

    // 1. Empezar el flujo y elegir la primera sección.
    await app.getByRole('button', { name: 'Nuevo flujo' }).click();
    await app.getByRole('textbox', { name: /^Código/ }).fill('PROV_SECCIONES');
    await app.getByRole('textbox', { name: /^Nombre/ }).fill('Proveedores con información de la ficha');
    await app.getByRole('combobox', { name: /^Tipo de vínculo/ }).click();
    await app.getByRole('option', { name: 'Proveedor', exact: true }).click();
    await app.getByRole('region', { name: 'Etapa 1' }).getByRole('combobox', { name: 'Área' }).click();
    await app.getByRole('option', { name: /^Revisión/ }).click();
    await app.getByRole('button', { name: 'Agregar sección de la información' }).click();
    await app.getByRole('combobox', { name: 'Sección de la información del tercero' }).nth(0).click();
    await app.getByRole('option', { name: 'Información financiera', exact: true }).click();
    await paso(page, {
      titulo: 'Agrega la sección financiera',
      explicacion:
        '«Agregar sección de la información» pide al tercero una sección de «Mi información» en lugar de un documento: la diligencia una sola vez y la empresa la revisa con las áreas que elijas. ' +
        'La sección financiera se diligencia por periodos: el campo «Periodos» dice cuántos cierres se piden, de 1 a 5. Vacío significa el último cierre.',
      resaltar: app.getByRole('combobox', { name: 'Sección de la información del tercero' }).nth(0),
      configuracion: {
        ajuste: 'Secciones de la información como requisito',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'El tercero la llena en «Mi información» y la empresa la revisa con las áreas del flujo.',
      },
    });

    // 2. Periodos, soporte, vigencia y revalidación.
    await app.getByRole('textbox', { name: 'Periodos' }).fill('2');
    await app.getByRole('checkbox', { name: 'Exige soporte' }).check();
    await app.getByRole('textbox', { name: 'Vigencia (días)' }).fill('365');
    await app.getByRole('textbox', { name: 'Plazo de revalidación (días)' }).fill('20');
    await paso(page, {
      titulo: 'Define periodos, soporte, vigencia y revalidación',
      explicacion:
        '«Exige soporte» pide que la sección venga con un documento que la respalde (por ejemplo, los estados financieros). «Vigencia (días)» dice cuánto vale lo que la empresa acepta: desde esa aceptación vence y se ratifica; vacío significa que no vence. ' +
        '«Plazo de revalidación (días)» es el tiempo que tiene el tercero para resolver una actualización que incluya esta sección; si lo dejas vacío, son 15 días.',
      resaltar: [app.getByRole('textbox', { name: 'Periodos' }), app.getByRole('textbox', { name: 'Vigencia (días)' }), app.getByRole('textbox', { name: 'Plazo de revalidación (días)' })],
      variantes: [
        { cuando: 'La sección no es financiera', entonces: 'El campo «Periodos» no aparece: solo la información financiera va por periodos.' },
        { cuando: 'Dejas la vigencia vacía', entonces: 'La sección «No vence» y no se pide ratificarla.' },
      ],
    });

    // 3. Segunda sección: solo personas jurídicas.
    await app.getByRole('button', { name: 'Agregar sección de la información' }).click();
    await app.getByRole('combobox', { name: 'Sección de la información del tercero' }).nth(1).click();
    await app.getByRole('option', { name: 'Información legal', exact: true }).click();
    await expect(app.getByRole('region', { name: 'Información legal' }).getByText('Legal y Financiera no aplican a persona natural', { exact: false })).toBeVisible();
    await paso(page, {
      titulo: 'Agrega la sección legal',
      explicacion:
        'La sección legal (socios, representante legal, revisor fiscal) solo aplica a personas jurídicas. Al elegirla, la pantalla recuerda que a una persona natural no se le pide: el tercero natural nunca la ve. ' +
        'Las secciones de un flujo se pueden mezclar con documentos: cada fila se configura por separado.',
      resaltar: app.getByRole('combobox', { name: 'Sección de la información del tercero' }).nth(1),
      variantes: [{ cuando: 'El tercero es persona natural', entonces: 'La sección legal no se le pide y no aparece en su lista.' }],
    });

    // 4. Guardar el borrador.
    await app.getByRole('button', { name: 'Crear borrador' }).click();
    await expect(app.getByRole('button', { name: 'Publicar v1' })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda el flujo con las secciones',
      explicacion:
        'Con «Crear borrador» las dos secciones quedan en el borrador v1. Si una fila tiene un dato mal, por ejemplo más de 5 periodos o una vigencia fuera de rango, el editor marca esa fila en rojo y dice qué corregir antes de guardar.',
      resaltar: app.getByRole('button', { name: 'Publicar v1' }),
      avisos: true,
    });
  },
);
