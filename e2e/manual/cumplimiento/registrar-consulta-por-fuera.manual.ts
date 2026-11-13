import { pdf } from '../../soporte/archivos';
import { enRevision, esperarListas, pedirVerificaciones, personaEmbebida } from '../../soporte/cumplimiento';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'Ferretería Manual S.A.S.';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Registra una consulta de listas hecha por fuera',
    resumen:
      'Cuando el proveedor de listas falla o la consulta se hizo en otro sitio, la oficial de cumplimiento sube el PDF de esa consulta, dice dónde y cuándo la hizo y registra lo que encontró. Entra al expediente como una consulta más.',
    precondiciones: [
      'Un grupo con Constructora Norte y el flujo de proveedores con listas restrictivas bloqueantes.',
      'Un proveedor en revisión, con Compras ya aprobada. Las listas automáticas no la consultaron (o fallaron).',
      'Un PDF de la consulta hecha en el sitio del proveedor de listas.',
    ],
    relacionados: ['cumplimiento/pedir-listas-restrictivas', 'cumplimiento/analizar-una-coincidencia'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
    const pTercero = await (await browser.newContext()).newPage();
    await enRevision(pTercero, e, NOMBRE);
    const ruben = await personaEmbebida(browser, e, e.ruben);
    await abrirDesdeBandeja(ruben, NOMBRE, 'Compras');
    await aprobarArea(ruben, 'Compras');

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await esperarListas(olga, NOMBRE, 'Cumplimiento', 'Satisfecha');
    const listas = olga.getByRole('region', { name: 'Listas restrictivas' });

    await olga.getByRole('button', { name: 'Registrar consulta hecha por fuera' }).click();
    const dialogo = olga.getByRole('dialog', { name: /Consulta de listas hecha por fuera/ });
    await dialogo.locator('input[type="file"]').setInputFiles(pdf('Consulta manual Infolaft'));
    await expect(dialogo.getByText(/consulta-manual-infolaft\.pdf/)).toBeVisible();
    await dialogo.getByRole('textbox', { name: /Dónde la hiciste/ }).fill('Infolaft (sitio web)');
    await paso(page, {
      titulo: 'Sube el PDF y dónde hiciste la consulta',
      explicacion:
        'Sube el PDF o la imagen que obtuviste (hasta 15 MB). Escribe dónde la hiciste (entre 3 y 100 caracteres), la fecha de la consulta (hasta 30 días atrás) y cuántos meses vale el resultado. ' +
        'Queda guardada con tu nombre como una consulta más, y el PDF se conserva como evidencia del expediente.',
      resaltar: dialogo,
      variantes: [
        { cuando: 'La consulta se hizo hace más de 30 días', entonces: 'El formulario no la acepta: hay que hacerla de nuevo.' },
      ],
    });

    await dialogo.getByRole('button', { name: 'Sí, hubo' }).click();
    const fila = dialogo.getByRole('listitem', { name: 'Coincidencia 1' });
    await fila.getByRole('combobox', { name: 'Persona' }).click();
    await olga.getByRole('option', { name: new RegExp(`^${NOMBRE.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} · Tercero`) }).click();
    await fila.getByRole('textbox', { name: 'Lista' }).fill('OFAC (SDN)');
    await fila.getByRole('textbox', { name: 'Nombre encontrado' }).fill(NOMBRE.toUpperCase());
    await fila.getByRole('textbox', { name: 'Detalle (opcional)' }).fill('Homónimo con otro NIT.');
    await paso(page, {
      titulo: 'Indica si hubo coincidencias',
      explicacion:
        'Si la consulta no encontró a nadie, elige «No hubo» y las listas quedan satisfechas. Si sí hubo, por cada coincidencia indicas la persona (tercero, representante, socio o PEP), la lista, el nombre con el que aparece y un detalle opcional. ' +
        'Con coincidencias, las listas quedan por analizar: tú decides después si cada una se descarta o se confirma.',
      resaltar: fila,
      variantes: [
        { cuando: 'Elegiste «No hubo»', entonces: 'Se registra sin coincidencias y la verificación queda satisfecha de inmediato.' },
      ],
    });

    await dialogo.getByRole('button', { name: 'Registrar consulta' }).click();
    await expect(dialogo).toBeHidden();
    await expect(listas).toContainText('Coincidencia por analizar');
    await paso(page, {
      titulo: 'Revisa la consulta registrada en el expediente',
      explicacion:
        'La consulta aparece en «Listas restrictivas» con la marca «Manual · Infolaft (sitio web)» y el PDF subido por ti. Como tenía una coincidencia, la verificación queda por analizar y el expediente no se puede aprobar hasta que la decidas. ' +
        'Las consultas anteriores quedan debajo, para no perder el historial.',
      resaltar: listas,
      configuracion: {
        ajuste: 'Proveedor de listas restrictivas',
        ruta: 'Configuración › Listas restrictivas (/listas-restrictivas)',
        efecto: 'Define con qué proveedor se consulta las listas y la cuota mensual de búsquedas del grupo.',
      },
    });

    const coincidencia = olga.getByRole('listitem', { name: `${NOMBRE} · OFAC (SDN)` });
    await coincidencia.getByRole('button', { name: 'Descartar' }).click();
    const decision = olga.getByRole('dialog', { name: 'Descartar coincidencia' });
    await decision.getByRole('textbox', { name: /Justificación/ }).fill('Es un homónimo: el NIT del listado es otro.');
    await decision.getByRole('button', { name: 'Guardar decisión' }).click();
    await expect(decision).toBeHidden();
    await expect(listas).toContainText('Satisfecha');
    await aprobarArea(olga, 'Cumplimiento');
    await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
    await paso(page, {
      titulo: 'Descarta la coincidencia y el vínculo se aprueba',
      explicacion:
        'Descartar la coincidencia, con su justificación, deja las listas satisfechas. Como no falta ninguna otra verificación, al aprobar tu área el vínculo se aprueba y queda activo; el tercero recibe el correo con el resultado.',
      resaltar: olga.getByText(/Todas las áreas aprobaron el /),
    });
    await esperarEstadoDelTercero(pTercero, 'Aprobado');
  },
);
