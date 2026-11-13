import { pedirVerificaciones, enRevision, esperarListas } from '../../soporte/cumplimiento';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'Aceros del Norte S.A.S.';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Revisa las listas restrictivas consultadas al pasar a revisión',
    resumen:
      'El oficial de cumplimiento abre Cumplimiento y encuentra el expediente de un proveedor cuyas listas restrictivas ya se consultaron solas al llegar a revisión. Revisa a quién se consultó, cuándo y con qué resultado.',
    precondiciones: [
      'Un grupo con Constructora Norte, con sus áreas Compras (Rubén) y Cumplimiento (Olga).',
      'El flujo de proveedores de Norte pide la verificación de listas restrictivas como bloqueante.',
      'Un proveedor que ya entregó su RUT y su Cámara de comercio y quedó «En revisión».',
    ],
    relacionados: ['cumplimiento/analizar-una-coincidencia'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
    // El tercero llega desde su propia ventana: la de la prueba solo muestra a la persona de la empresa.
    await enRevision(await (await browser.newContext()).newPage(), e, NOMBRE);

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await expect(olga.getByRole('heading', { level: 1, name: 'Cumplimiento' })).toBeVisible();
    // La consulta de listas es automática: espera a que quede satisfecha antes de mostrarla.
    await esperarListas(olga, NOMBRE, 'Cumplimiento', 'Satisfecha');
    await expect(olga.getByRole('heading', { level: 1, name: NOMBRE })).toBeVisible();

    await paso(page, {
      titulo: 'Abre el expediente desde Cumplimiento',
      explicacion:
        'La pestaña Cumplimiento lista los expedientes de la empresa que tienen verificaciones pendientes o por decidir. Abre el de «Aceros del Norte S.A.S.» para ver sus listas restrictivas. ' +
        'La consulta no la pediste tú: Validasoft la hizo sola cuando el expediente pasó a revisión.',
      resaltar: olga.getByRole('heading', { level: 1, name: NOMBRE }),
      configuracion: { ajuste: 'Flujo de proveedores con verificación de listas', ruta: 'Configuración › Flujos aprobatorios (/flujos)', efecto: 'Solo se consultan las listas que el flujo pide como verificación.' },
    });

    await paso(page, {
      titulo: 'Revisa el resultado de las listas',
      explicacion:
        'La sección «Listas restrictivas» muestra el estado («Satisfecha» si no hay coincidencias), el proveedor con el que se consultó, la fecha y quién la hizo («Sistema» en una consulta automática), hasta cuándo sirve y las personas consultadas: el tercero y, si los hay, su representante legal, socios y PEP vinculados. ' +
        'Si ninguna persona aparece en las listas, lo dice en una línea.',
      resaltar: olga.getByRole('region', { name: 'Listas restrictivas' }),
      variantes: [
        { cuando: 'Hay coincidencias', entonces: 'Aparecen debajo, una por una, y el estado cambia a «Coincidencia por analizar» hasta que alguien decida (ver el recorrido de coincidencias).' },
        { cuando: 'La última consulta automática falló', entonces: 'Aparece un aviso con el error y la hora del próximo reintento; el sistema vuelve a consultar solo.' },
        { cuando: 'Se hizo una consulta manual por fuera', entonces: 'La fuente aparece como manual y se indica quién la registró.' },
      ],
      configuracion: {
        ajuste: 'Proveedor de listas restrictivas y cuota mensual',
        ruta: 'Configuración › Listas restrictivas (/listas-restrictivas)',
        efecto: 'Define con qué proveedor se consulta (el de la instalación o uno propio de la empresa) y cuántas búsquedas puede hacer el grupo al mes.',
      },
    });

    await paso(page, {
      titulo: 'Reconsulta o descarga la evidencia',
      explicacion:
        'El botón «Evidencia (PDF)» descarga el soporte de la consulta, para guardarlo en el expediente. «Reconsultar» vuelve a consultar las listas ahora, sin esperar al monitoreo periódico; ' +
        'la consulta vale hasta la fecha «Vigente hasta» y, cuando vence, el sistema la rehace sola.',
      resaltar: [
        olga.getByRole('button', { name: 'Evidencia (PDF)' }),
        olga.getByRole('button', { name: 'Reconsultar', exact: true }),
      ],
      variantes: [
        { cuando: 'Eres revisor y no oficial de cumplimiento', entonces: 'Ves las listas y la evidencia, pero no puedes reconsultar ni decidir coincidencias.' },
      ],
    });
  },
);
