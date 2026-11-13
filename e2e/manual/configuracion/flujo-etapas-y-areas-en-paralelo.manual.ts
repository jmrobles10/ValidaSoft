import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Arma una etapa con dos áreas en paralelo y una automática',
    resumen:
      'La administradora de Constructora Norte arma un flujo de proveedores con una etapa en la que Compras y Cumplimiento revisan al mismo tiempo. Define los aprobadores y el plazo de Compras, deja Cumplimiento como automática y guarda el flujo como borrador.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Norte S.A.S.» y sus áreas Compras (con Rubén) y Cumplimiento (con Olga).',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/tipo-de-documento-y-flujo', 'configuracion/areas-y-miembros'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'flujos');
    await expect(app.getByRole('heading', { level: 1, name: `Flujos aprobatorios de ${e.norte.nombre}` })).toBeVisible();

    await paso(page, {
      titulo: 'Abre Configuración › Flujos aprobatorios',
      explicacion:
        'Un flujo define qué áreas revisan a cada tipo de vínculo, en qué orden y qué documentos se piden. Cada etapa se revisa después de la anterior; las áreas de una misma etapa revisan al mismo tiempo y la etapa termina cuando todas aprueban. ' +
        'El botón «Nuevo flujo» crea uno desde cero.',
      resaltar: app.getByRole('button', { name: 'Nuevo flujo' }),
    });

    await app.getByRole('button', { name: 'Nuevo flujo' }).click();
    await app.getByRole('textbox', { name: /^Código/ }).fill('PROV_PARALELO');
    await app.getByRole('textbox', { name: /^Nombre/ }).fill('Proveedores con revisión en paralelo');
    await app.getByRole('combobox', { name: /^Tipo de vínculo/ }).click();
    await app.getByRole('option', { name: 'Proveedor', exact: true }).click();

    // Etapa 1: dos áreas que revisan al mismo tiempo.
    const etapa1 = app.getByRole('region', { name: 'Etapa 1' });
    await etapa1.getByRole('combobox', { name: 'Área' }).click();
    await app.getByRole('option', { name: /^Compras/ }).click();
    await etapa1.getByRole('button', { name: 'Agregar área en paralelo' }).click();
    await etapa1.getByRole('combobox', { name: 'Área' }).nth(1).click();
    await app.getByRole('option', { name: /^Cumplimiento/ }).click();
    await paso(page, {
      titulo: 'Elige las áreas que revisan la etapa',
      explicacion:
        'En «Etapa 1» eliges un área y con «Agregar área en paralelo» sumas otra que revisa al mismo tiempo: aquí Compras y Cumplimiento. ' +
        'La etapa termina cuando todas sus áreas aprueban, y la etapa siguiente empieza entonces. Cada área debe ser distinta de las demás áreas del flujo.',
      resaltar: etapa1,
      variantes: [
        { cuando: 'Agregas otra área a la misma etapa', entonces: 'Las dos revisan en paralelo y la etapa termina cuando ambas aprueban.' },
        { cuando: 'Agregas otra etapa', entonces: 'Sus áreas solo ven el expediente cuando la etapa anterior terminó.' },
      ],
      configuracion: {
        ajuste: 'Áreas de la empresa y sus miembros',
        ruta: 'Configuración › Áreas (/areas)',
        efecto: 'Solo se pueden elegir áreas activas; quién decide en cada una sale de sus miembros.',
      },
    });

    // Compras: aprobadores, plazo y rechazo. Cumplimiento: automática.
    await etapa1.getByRole('textbox', { name: 'SLA (horas)' }).first().fill('48');
    await etapa1.getByRole('switch', { name: 'Puede rechazar' }).first().check();
    await paso(page, {
      titulo: 'Define aprobadores, plazo y rechazo de cada área',
      explicacion:
        '«Aprobadores» es cuántas personas distintas del área deben aprobar, de 1 a 10. El «SLA (horas)» es el plazo para revisar; si el área lo incumple, el informe de cumplimiento lo marca. ' +
        '«Puede rechazar» deja que el área rechace el expediente en lugar de solo observarlo. Bajo cada área ves sus miembros: si son menos que los aprobadores pedidos, el editor lo marca.',
      resaltar: etapa1,
      variantes: [{ cuando: 'Pides más aprobadores que miembros tiene el área', entonces: 'El editor marca el área y no deja publicar el flujo hasta corregirlo.' }],
    });

    await etapa1.getByText('Automática', { exact: true }).last().click();
    await paso(page, {
      titulo: 'Marca un área como automática',
      explicacion:
        '«Automática» hace que el área apruebe sola al empezar la etapa, sin que nadie de ella revise. Úsala solo cuando no haga falta una decisión humana: ' +
        'el expediente sigue su curso cuando Compras apruebe. Con «Automática» el área no pide aprobadores y no puede rechazar.',
      resaltar: etapa1.getByRole('region', { name: /Cumplimiento/ }).first().or(etapa1),
      variantes: [{ cuando: 'Marcas «Automática»', entonces: 'El área aprueba al empezar la etapa, sin que nadie de ella decida.' }],
    });

    await app.getByRole('button', { name: 'Crear borrador' }).click();
    await expect(app.getByRole('button', { name: 'Publicar v1' })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda el flujo como borrador',
      explicacion:
        'Con «Crear borrador» el flujo queda guardado como borrador v1: todavía no lo usa ningún expediente. Si una etapa tiene un dato inválido, el editor lo marca con el campo en error. ' +
        'Para usarlo hay que publicarlo; mientras tanto puedes seguir editándolo.',
      resaltar: app.getByRole('button', { name: 'Publicar v1' }),
      avisos: true,
    });
  },
);
