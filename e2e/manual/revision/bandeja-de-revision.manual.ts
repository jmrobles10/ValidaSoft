import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';
const TERCEROS = ['Ladrillera del Valle S.A.S.', 'Tejidos del Caribe S.A.S.', 'Cementos del Llano S.A.S.'];

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Filtra la bandeja de revisión y abre un expediente',
    resumen:
      'Rubén revisa la bandeja de su área: ve una fila por expediente con su tercero, tipo, plazo y estado. Filtra por el estado de su área, limpia los filtros y abre uno de los expedientes.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Compras y Rubén como miembro.',
      'Tres proveedores invitados por Norte que subieron su RUT y su Cámara de comercio; Rubén ya aprobó el área Compras de uno de ellos.',
    ],
    relacionados: ['revision/inicio-lo-que-te-toca', 'revision/aprobar-un-area'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    for (const nombre of TERCEROS) {
      const invitacion = await invitar(e, e.norte, nuevoTercero(nombre));
      await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
        { nombre: RUT, archivo: pdf(`RUT ${nombre}`) },
        { nombre: CAMARA, archivo: pdf(`Camara ${nombre}`) },
      ]);
    }

    // Rubén aprueba el área Compras de Tejidos del Caribe antes del recorrido (queda «Aprobada» para el filtro).
    const ventanaRuben = await (await browser.newContext()).newPage();
    const rubenPrevio = await abrirEmbebido(ventanaRuben, e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(rubenPrevio, 'Tejidos del Caribe S.A.S.', 'Compras');
    await aprobarArea(rubenPrevio, 'Compras');

    // 1) La bandeja: una fila por expediente y área de la que eres miembro.
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja');
    await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
    await expect(app.getByRole('row', { name: /Abrir expediente .* de Cementos del Llano S\.A\.S\. \(Compras\)/ })).toBeVisible();
    await paso(page, {
      titulo: 'Mira la bandeja de revisión',
      explicacion:
        'La bandeja lista un expediente por cada área que debe decidir: el folio, el tercero, el tipo de vínculo, el área, el estado de tu área, las aprobaciones que lleva, los documentos por decidir, desde cuándo espera y su plazo de revisión. ' +
        'Ves solo los expedientes de las áreas de las que eres miembro.',
      resaltar: app.getByRole('table'),
      variantes: [
        { cuando: 'El área tiene plazo de revisión', entonces: 'La columna «Plazo de revisión» muestra «A tiempo», «Por vencer» o «Vencido», con la fecha en que vence.' },
        { cuando: 'El área no tiene plazo', entonces: 'La celda queda con un guion y no hay avisos de vencimiento para ese expediente.' },
      ],
      configuracion: {
        ajuste: 'Plazos de revisión por área',
        ruta: 'Configuración › Plazos de revisión (/configuracion/plazos-de-revision)',
        efecto: 'El plazo de cada área decide cuándo aparece «Por vencer» o «Vencido». Si el flujo define su propio plazo, ese manda.',
      },
    });

    // 2) Filtro por el estado del área: solo lo que Compras ya aprobó.
    await app.getByRole('button', { name: /^Estado del área/ }).click();
    await app.getByRole('menuitem', { name: 'Aprobada', exact: true }).click();
    await expect(app.getByRole('row', { name: /Abrir expediente .* de Tejidos del Caribe S\.A\.S\. \(Compras\)/ })).toBeVisible();
    await expect(app.getByRole('row', { name: /Abrir expediente .* de Ladrillera del Valle S\.A\.S\./ })).toHaveCount(0);
    await paso(page, {
      titulo: 'Filtra por el estado de tu área',
      explicacion:
        'El filtro «Estado del área» deja ver solo los expedientes en el estado que elijas: «En espera», «Pendiente», «Aprobada» u «Observada». ' +
        'Aquí quedó «Aprobada»: el expediente de Tejidos del Caribe, que ya aprobaste. El filtro «Área» hace lo mismo por área, y el botón «Limpiar filtros» los quita todos.',
      resaltar: [app.getByRole('button', { name: /^Área/ }), app.getByRole('button', { name: /^Estado del área/ }), app.getByRole('table')],
    });

    // 3) Limpia los filtros y abre un expediente pendiente.
    await app.getByRole('button', { name: 'Limpiar filtros' }).click();
    await expect(app.getByRole('row', { name: /Abrir expediente .* de Ladrillera del Valle S\.A\.S\. \(Compras\)/ })).toBeVisible();
    await paso(page, {
      titulo: 'Limpia los filtros y abre un expediente',
      explicacion:
        'Al limpiar los filtros vuelven a aparecer todos los expedientes que esperan a tus áreas. Toca una fila para abrir el expediente y revisar sus documentos y decidir.',
      resaltar: app.getByRole('table'),
    });
    await abrirDesdeBandeja(app, 'Ladrillera del Valle S.A.S.', 'Compras');
    await expect(app.getByRole('heading', { level: 1, name: 'Ladrillera del Valle S.A.S.' })).toBeVisible();
  },
);
