import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Revisa lo que te toca desde Inicio',
    resumen:
      'Rubén, miembro del área Compras, abre Inicio y ve de un vistazo los expedientes que esperan su decisión, los indicadores de la empresa y llega a la bandeja filtrada por su área.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Compras y Rubén como miembro.',
      'Dos proveedores invitados por Norte que ya aceptaron y subieron su RUT y su Cámara de comercio: sus expedientes están en revisión.',
    ],
    relacionados: ['revision/bandeja-de-revision', 'revision/aprobar-un-area'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    for (const nombre of ['Ladrillera del Valle S.A.S.', 'Tejidos del Caribe S.A.S.']) {
      const invitacion = await invitar(e, e.norte, nuevoTercero(nombre));
      await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
        { nombre: RUT, archivo: pdf(`RUT ${nombre}`) },
        { nombre: CAMARA, archivo: pdf(`Camara ${nombre}`) },
      ]);
    }

    // 1) Inicio: lo que le toca a cada área de Norte. Rubén solo ve el área de la que es miembro.
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'inicio');
    await expect(app.getByRole('heading', { level: 1, name: 'Inicio' })).toBeVisible();
    const compras = app.getByRole('link', { name: /Compras: 2 expedientes por revisar/ });
    await expect(compras).toBeVisible();

    await paso(page, {
      titulo: 'Abre Inicio y mira lo que te toca',
      explicacion:
        'Inicio abre primero con «Lo que te toca»: una línea por cada área de la que eres miembro, con los expedientes que esperan una decisión suya y la antigüedad del más viejo. ' +
        'Si no hay nada pendiente, la pantalla dice que estás al día. Ves solo las áreas de las que eres miembro, no las de toda la empresa.',
      resaltar: [app.getByText('Lo que te toca', { exact: true }), compras],
      variantes: [
        { cuando: 'Eres miembro de varias áreas', entonces: 'Cada área aparece en su propia línea, con su número de expedientes.' },
        { cuando: 'No tienes nada pendiente', entonces: 'La sección muestra «Estás al día» en lugar de la lista.' },
      ],
    });

    // 2) Indicadores: cifras de vínculos, expedientes y certificados, con la ventana de tiempo.
    await app.getByRole('group', { name: 'Ventana de los indicadores' }).getByRole('button', { name: '7 días', exact: true }).click();
    await expect(app.getByRole('group', { name: 'Ventana de los indicadores' }).getByRole('button', { name: '7 días', exact: true })).toHaveAttribute('aria-pressed', 'true');
    await paso(page, {
      titulo: 'Revisa los indicadores de la empresa',
      explicacion:
        'Debajo de «Lo que te toca» están los indicadores: vínculos, expedientes y certificados al momento, y los aprobados, rechazados y los días hasta la aprobación en la ventana que elijas (7, 30 o 90 días). ' +
        'Las cifras de «al momento» no cambian con la ventana; las de tiempo sí. Tocar «Aprobados» lleva al informe de estado con la misma ventana.',
      resaltar: app.getByRole('group', { name: 'Ventana de los indicadores' }),
      configuracion: {
        ajuste: 'Ventana de los indicadores',
        efecto: 'Cambia el periodo de los aprobados, rechazados y tiempos; la ventana por defecto es 30 días.',
      },
    });

    // 3) Desde «Lo que te toca» se llega a la bandeja ya filtrada por el área.
    await compras.click();
    await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
    await expect(app.getByRole('row', { name: /Abrir expediente .* de Ladrillera del Valle S\.A\.S\. \(Compras\)/ })).toBeVisible();
    await paso(page, {
      titulo: 'Pasa a la bandeja de tu área',
      explicacion:
        'Al tocar la línea de Compras se abre la bandeja de revisión con el filtro de tu área ya puesto. Cada fila es un expediente que espera tu decisión: el tercero, el tipo de vínculo y el plazo. ' +
        'Desde aquí se abre el expediente para revisarlo y decidir.',
      resaltar: app.getByRole('table'),
    });
  },
);
