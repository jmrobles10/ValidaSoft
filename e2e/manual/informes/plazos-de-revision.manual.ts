import { CLAVE_PLATAFORMA, llamar } from '../../soporte/api';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { irASeccion } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Escenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'informes',
    actor: 'Administrador de empresa',
    titulo: 'Mide el cumplimiento de los plazos de revisión',
    resumen:
      'La administradora de Constructora Norte configura un plazo de 10 horas para Compras y luego abre el informe de plazo de revisión: ve cuántas revisiones se resolvieron a tiempo, cuántas quedaron fuera de plazo y cuál es el cumplimiento de cada área.',
    precondiciones: [
      'Un grupo con Constructora Norte, con las áreas Compras y Cumplimiento, y un flujo de proveedor con RUT y Cámara de comercio.',
      'Un proveedor cuyo expediente llegó a revisión de Compras: el plazo de 10 horas empieza a correr desde ese momento.',
    ],
    relacionados: ['configuracion/areas-y-miembros', 'revision/bandeja-de-revision', 'informes/estado-de-expedientes'],
  },
  async ({ page, browser }) => {
    const e: Escenario = await crearEscenario();

    // Siembra: Compras con plazo de 10 horas (se configura por la interfaz, antes de que llegue el expediente).
    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'plazos-de-revision');
    await expect(app.getByRole('heading', { level: 1, name: 'Plazos de revisión' })).toBeVisible();
    await app.getByRole('textbox', { name: 'Plazo en horas de Compras' }).fill('10');
    await app.getByRole('button', { name: 'Guardar cambios' }).click();
    await expect(app.getByText('Plazos de revisión guardados.')).toBeVisible();

    // Siembra: un proveedor entrega RUT y Cámara; su expediente llega a Compras y Cumplimiento.
    const invitacion = await invitar(e, e.norte, nuevoTercero('Aceros del Plazo S.A.S.'));
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT plazo') },
      { nombre: CAMARA, archivo: pdf('Cámara plazo') },
    ]);

    // Siembra: el reloj del job de plazos pasa 9 horas (aviso) y luego 11 horas (vencido) desde la llegada.
    const expedientes = await llamar<{ datos: { etapas: { areas: { codigo: string; slaVence: string | null }[] }[] }[] }>('GET', '/api/v1/expedientes', {
      llave: e.admin,
      empresa: e.norte.id,
    });
    const compras = expedientes.datos[0]!.etapas.flatMap((x) => x.areas).find((a) => a.codigo === 'COMPRAS' || a.codigo === 'compras');
    if (!compras?.slaVence) throw new Error('El expediente no tiene plazo de revisión en Compras.');
    const inicio = new Date(new Date(compras.slaVence).getTime() - 10 * 3600 * 1000);
    await llamar('POST', '/api/v1/plataforma/sla/revisar', { llave: CLAVE_PLATAFORMA, cuerpo: { tenantId: e.tenantId, fecha: new Date(inicio.getTime() + 9 * 3600 * 1000).toISOString() } });
    await llamar('POST', '/api/v1/plataforma/sla/revisar', { llave: CLAVE_PLATAFORMA, cuerpo: { tenantId: e.tenantId, fecha: new Date(inicio.getTime() + 11 * 3600 * 1000).toISOString() } });

    // 1. El resumen del informe.
    await irASeccion(app, 'Informes');
    await app.getByRole('tab', { name: 'Plazo de revisión', exact: true }).or(app.getByRole('link', { name: 'Plazo de revisión', exact: true })).click();
    await expect(app.getByRole('heading', { level: 1, name: 'Informes' })).toBeVisible();
    await expect(app.getByRole('heading', { level: 2, name: 'Cumplimiento del plazo de revisión' }).or(app.getByText('Cumplimiento del plazo de revisión', { exact: true }))).toBeVisible();
    const resumen = app.getByText('Cumplimiento', { exact: true }).first();
    await expect(resumen).toBeVisible();
    await paso(page, {
      titulo: 'Abre el informe de plazo de revisión',
      explicacion:
        'El resumen cuenta las revisiones que tenían plazo: «A tiempo» las que se decidieron dentro del plazo, «Fuera de plazo» las decididas después, y «Vencidos ahora» las que siguen esperando al área pasado su plazo. ' +
        'Con el porcentaje de cumplimiento. Solo cuentan las áreas que tienen un plazo configurado.',
      resaltar: resumen,
      configuracion: {
        ajuste: 'Plazo de revisión (horas) por área',
        ruta: 'Configuración › Plazos de revisión (/plazos-de-revision)',
        efecto: 'Las horas que tiene cada área para decidir. Al acercarse el plazo se avisa por correo; al vencer, la bandeja marca la revisión como vencida.',
      },
    });

    // 2. El cumplimiento por área.
    const porArea = app.getByRole('table', { name: 'Cumplimiento por área' });
    await expect(porArea.getByRole('row', { name: /Compras/ })).toBeVisible();
    await paso(page, {
      titulo: 'Compara el cumplimiento de cada área',
      explicacion:
        'La tabla «Cumplimiento por área» muestra el plazo de cada área en horas, cuántas revisiones tuvo, cuántas fueron a tiempo y cuántas fuera de plazo, con su cumplimiento, el promedio de días y los vencidos. ' +
        'Un área sin plazo aparece como «Sin plazo» y no entra en el cálculo.',
      resaltar: porArea.getByRole('row', { name: /Compras/ }),
    });
  },
);
