import { aprobarUltimaArea, enRevision, esperarListas, pedirVerificaciones } from '../../soporte/cumplimiento';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'Ferretería Coincidencia S.A.S.';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Analiza una coincidencia en las listas y la descarta por homónimo',
    resumen:
      'La consulta de listas encontró una persona parecida en OFAC. La oficial de cumplimiento compara los datos, comprueba que es un homónimo, lo descarta con una justificación y el expediente puede aprobarse.',
    precondiciones: [
      'Un grupo con Constructora Norte, sus áreas Compras (Rubén) y Cumplimiento (Olga) y el flujo de proveedores con la verificación de listas bloqueante.',
      'Un proveedor cuyo nombre coincide con una entrada simulada de OFAC, que ya entregó sus documentos y quedó «En revisión».',
      'Compras ya aprobó: falta solo la decisión de Cumplimiento sobre la coincidencia.',
    ],
    relacionados: ['cumplimiento/pedir-listas-restrictivas', 'cumplimiento/coincidencia-confirmada'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
    const pTercero = await (await browser.newContext()).newPage();
    await enRevision(pTercero, e, NOMBRE);
    // Rubén aprueba Compras en su propia ventana (lo que necesita el recorrido es la decisión de Cumplimiento).
    const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(ruben, NOMBRE, 'Compras');
    await aprobarArea(ruben, 'Compras');

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await esperarListas(olga, NOMBRE, 'Cumplimiento', 'Coincidencia por analizar');
    const coincidencia = olga.getByRole('listitem', { name: `${NOMBRE} · OFAC (SDN)` });
    await expect(coincidencia.getByRole('button', { name: 'Descartar' })).toBeVisible();
    await expect(coincidencia).not.toContainText('Descartada');

    await paso(page, {
      titulo: 'Abre la coincidencia encontrada',
      explicacion:
        'La lista de la coincidencia muestra la persona consultada (tercero, representante, socio o PEP), su rol y su identificación, la lista donde apareció, el puntaje de parecido y el nombre con el que aparece en la lista. ' +
        'Mientras la coincidencia esté por analizar, la verificación no se satisface y el expediente no puede aprobarse.',
      resaltar: coincidencia,
      variantes: [
        { cuando: 'La consulta es de un proveedor que muestra puntaje', entonces: 'El porcentaje indica el parecido del nombre; un puntaje alto no decide nada por sí solo: lo decides tú con los datos.' },
      ],
    });

    // Tu área aprueba, pero el expediente no se aprueba: la verificación bloquea.
    await aprobarUltimaArea(olga, 'Cumplimiento', /Listas restrictivas \(coincidencia por analizar\)/);
    await paso(page, {
      titulo: 'Aprueba tu área: el sistema lo impide',
      explicacion:
        'Al aprobar el área de Cumplimiento, Validasoft comprueba las verificaciones bloqueantes del expediente. Como la coincidencia sigue por analizar, el área queda aprobada pero el aviso dice que falta cumplimiento para aprobar el vínculo: «Listas restrictivas (coincidencia por analizar)». ' +
        'El expediente sigue en revisión hasta que decidas la coincidencia.',
      resaltar: olga.getByRole('alert').filter({ hasText: 'Falta cumplimiento para aprobar' }),
    });

    await coincidencia.getByRole('button', { name: 'Descartar' }).click();
    const dialogo = olga.getByRole('dialog', { name: 'Descartar coincidencia' });
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('button', { name: 'Guardar decisión' }).click();
    await expect(dialogo.getByText('Escribe la justificación.')).toBeVisible();
    await paso(page, {
      titulo: 'Descarta la coincidencia con su justificación',
      explicacion:
        'Descartar significa que la persona de la lista no es el tercero o su representante: es un homónimo. Compara el nombre y la identificación de la lista con los del expediente antes de decidir. ' +
        'La justificación es obligatoria (al menos 10 caracteres): queda en el historial y en el informe SAGRILAFT, y el tercero no la lee. La decisión vale 12 meses para las reconsultas.',
      resaltar: dialogo,
      variantes: [
        { cuando: 'Eres revisor de un área (no de Cumplimiento)', entonces: 'No ves los botones «Descartar» ni «Confirmar»: solo Cumplimiento decide sobre las coincidencias.' },
      ],
    });

    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Homónimo: el NIT de la lista es otro y la dirección no coincide.');
    await dialogo.getByRole('button', { name: 'Guardar decisión' }).click();
    await expect(dialogo).toBeHidden();
    await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();

    await paso(page, {
      titulo: 'Revisa la decisión registrada',
      explicacion:
        'La coincidencia queda como «Descartada», con quién la decidió, cuándo y la justificación. La verificación pasa a «Satisfecha» y, como ninguna otra área pide algo, el expediente se aprueba y el vínculo queda activo. ' +
        'Si una coincidencia se confirma en lugar de descartarse, la verificación falla y el expediente no se aprueba (ver «Coincidencia confirmada»).',
      resaltar: [coincidencia, olga.getByRole('region', { name: 'Listas restrictivas' })],
      configuracion: {
        ajuste: 'Proveedor de listas restrictivas',
        ruta: 'Configuración › Listas restrictivas (/listas-restrictivas)',
        efecto: 'Las coincidencias se analizan con el mismo proveedor con el que se consultó la lista.',
      },
    });
    await esperarEstadoDelTercero(pTercero, 'Aprobado');
  },
);
