import { pdf } from '../../soporte/archivos';
import { aprobarUltimaArea, enRevision, esperarListas, pedirVerificaciones, personaEmbebida } from '../../soporte/cumplimiento';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'Constructora Soporte S.A.S.';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Adjunta los soportes al estudio de cumplimiento',
    resumen:
      'Mientras registra el estudio de cumplimiento, la oficial adjunta el archivo que lo respalda. El archivo queda en el estudio y en el historial del expediente, y se puede abrir después.',
    precondiciones: [
      'Un grupo con Constructora Norte y el flujo de proveedores con listas restrictivas y estudio de cumplimiento bloqueantes.',
      'Un proveedor en revisión sin coincidencias. Compras ya aprobó; falta el estudio de Cumplimiento.',
      'Un PDF que soporta el estudio (por ejemplo, la consulta de referencias del tercero).',
    ],
    relacionados: ['cumplimiento/estudio-de-cumplimiento'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [
      { tipo: 'ListasRestrictivas', bloqueante: true },
      { tipo: 'EstudioDeCumplimiento', bloqueante: true },
    ]);
    const pTercero = await (await browser.newContext()).newPage();
    await enRevision(pTercero, e, NOMBRE);
    const ruben = await personaEmbebida(browser, e, e.ruben);
    await abrirDesdeBandeja(ruben, NOMBRE, 'Compras');
    await aprobarArea(ruben, 'Compras');

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await esperarListas(olga, NOMBRE, 'Cumplimiento', 'Satisfecha');
    await aprobarUltimaArea(olga, 'Cumplimiento', /Estudio de cumplimiento \(pendiente\)/);
    await olga.getByRole('button', { name: 'Registrar estudio' }).click();
    const dialogo = olga.getByRole('dialog', { name: /Estudio de cumplimiento/ });
    await paso(page, {
      titulo: 'Abre la sección de archivos del estudio',
      explicacion:
        'Dentro del formulario del estudio, la sección «Archivos del estudio» reúne los soportes: PDF o imagen (PNG, JPG o WEBP), de hasta 15 MB cada uno. Los archivos se guardan en el expediente y también quedan ligados al estudio. ' +
        'Puedes adjuntar varios; los que no sirvan se quitan con su botón antes de registrar.',
      resaltar: dialogo.getByRole('region', { name: 'Archivos del estudio' }),
      variantes: [
        { cuando: 'Subes un archivo que no es PDF ni imagen', entonces: 'El sistema no lo acepta y dice que solo se admiten PDF e imágenes.' },
      ],
    });

    await dialogo.locator('input[type="file"]').setInputFiles(pdf('Soporte estudio'));
    await expect(dialogo.getByText('soporte-estudio.pdf')).toBeVisible();
    await paso(page, {
      titulo: 'Sube el soporte del estudio',
      explicacion:
        'Con «Subir archivo» eliges el PDF desde tu equipo. Aparece con su nombre en la lista de archivos, con un botón para abrirlo y otro para quitarlo. ' +
        'Al registrar el estudio, el archivo queda como soporte del estudio y en el historial del expediente.',
      resaltar: dialogo.getByRole('region', { name: 'Archivos del estudio' }),
    });

    await dialogo.getByRole('combobox', { name: 'Concepto' }).click();
    await olga.getByRole('option', { name: 'Favorable', exact: true }).click();
    await dialogo.getByRole('button', { name: '1 · Bajo' }).click();
    await dialogo.getByRole('button', { name: 'Registrar estudio' }).click();
    await expect(dialogo).toBeHidden();
    await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el archivo guardado en el estudio',
      explicacion:
        'El estudio registrado muestra el archivo «soporte-estudio.pdf» y se puede abrir desde ahí. El historial del expediente anota que el oficial adjuntó el archivo. ' +
        'Como el estudio era lo último que faltaba, el expediente se aprueba.',
      resaltar: [olga.getByRole('region', { name: 'Estudio de cumplimiento' }), olga.getByText('El oficial adjuntó un archivo: soporte-estudio.pdf')],
    });
    await esperarEstadoDelTercero(pTercero, 'Aprobado');
  },
);
