import { abrirEmbebido } from '../../soporte/embebido';
import { publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const CODIGO_NUEVO = 'OBRA';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Cambia el flujo por defecto de un tipo de vínculo',
    resumen:
      'La administradora de Constructora Sur tiene un flujo de proveedores ya en uso y publica otro para el mismo tipo. Lo deja como flujo por defecto: desde ahora las invitaciones a proveedores usan el nuevo.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» y un flujo de Proveedor ya publicado como por defecto.',
      'Un segundo flujo de Proveedor publicado (se siembra por la API del sistema de pruebas, sin documentos).',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/tipo-de-documento-y-flujo', 'configuracion/flujo-etapas-y-areas-en-paralelo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await publicarFlujoSinDocumentos(e, e.sur, CODIGO_NUEVO, 'PROVEEDOR', [[{ area: e.sur.areas.revision! }]]);
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'flujos');
    await expect(app.getByRole('heading', { level: 1, name: `Flujos aprobatorios de ${e.sur.nombre}` })).toBeVisible();
    const fila = app.getByRole('row', { name: new RegExp(`Abrir flujo Flujo ${CODIGO_NUEVO}`) });
    await expect(fila).toBeVisible();

    await paso(page, {
      titulo: 'Revisa qué flujo usa cada tipo de vínculo',
      explicacion:
        'Cuando una invitación no trae un flujo propio, la empresa usa el flujo por defecto de su tipo de vínculo. La tarjeta «Flujo por defecto por tipo de vínculo» muestra cuál es para cada tipo; ' +
        'en la tabla, el flujo por defecto lleva la marca «Por defecto». Publicar un flujo nuevo no lo cambia: solo los flujos publicados del mismo tipo pueden ser por defecto.',
      resaltar: app.getByText('Flujo por defecto por tipo de vínculo', { exact: true }),
      configuracion: {
        ajuste: 'Flujo por defecto por tipo de vínculo',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Las invitaciones nuevas de ese tipo usan este flujo, salvo que traigan uno propio.',
      },
    });

    await fila.getByRole('button', { name: `Abrir flujo Flujo ${CODIGO_NUEVO}`, exact: false }).or(fila).first().click();
    await app.getByRole('button', { name: 'Usar por defecto para Proveedor' }).click();
    await expect(app.getByText('Ahora es el flujo por defecto de Proveedor.')).toBeVisible();
    await app.getByRole('link', { name: 'Flujos aprobatorios' }).first().click();
    const nueva = app.getByRole('row', { name: new RegExp(`Abrir flujo Flujo ${CODIGO_NUEVO}`) });
    await expect(nueva).toContainText('Por defecto');
    await paso(page, {
      titulo: 'Marca el flujo como el nuevo por defecto',
      explicacion:
        'Dentro del flujo, «Usar por defecto para Proveedor» lo convierte en el flujo por defecto de ese tipo de vínculo. El botón solo aparece en flujos publicados del mismo tipo. ' +
        'El flujo anterior deja de ser el por defecto, pero sigue vigente para las revisiones que ya están abiertas.',
      resaltar: nueva,
      avisos: true,
      variantes: [{ cuando: 'El flujo es un borrador sin publicar', entonces: 'El botón no aparece: primero hay que publicar el flujo.' }],
    });

    await paso(page, {
      titulo: 'Comprueba el resultado en la tarjeta',
      explicacion:
        'La tarjeta de flujo por defecto ahora muestra el flujo nuevo para Proveedor. Si una empresa no tiene flujo por defecto para un tipo, la tarjeta dice «Sin flujo por defecto» y los terceros que aceptan quedan en aperturas pendientes.',
      resaltar: nueva,
    });
  },
);
