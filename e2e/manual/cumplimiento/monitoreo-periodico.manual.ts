import { enRevision, esperarListas, pedirVerificaciones, personaEmbebida, revisarVerificaciones } from '../../soporte/cumplimiento';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion } from '../../soporte/empresa';
import { esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'Cementos Vigilados S.A.S.';
const dentroDe = (dias: number) => new Date(Date.now() + dias * 86_400_000);

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Sigue el monitoreo de listas de un vínculo activo',
    resumen:
      'Un proveedor ya aprobado sigue en monitoreo: Validasoft lo reconsulta en las listas cada 30 días. Cuando el proveedor agrega a un socio que aparece en una lista, la novedad llega a la bandeja de Cumplimiento y la oficial la confirma.',
    precondiciones: [
      'Un grupo con Constructora Norte y el flujo de proveedores con listas restrictivas bloqueantes.',
      'Un proveedor aprobado sin coincidencias, cuyo vínculo está activo y en monitoreo.',
      'El proveedor agrega después a un socio cuyo nombre aparece en una entrada simulada de OFAC.',
    ],
    relacionados: ['cumplimiento/coincidencia-confirmada', 'cumplimiento/bandeja-del-oficial'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [{ tipo: 'ListasRestrictivas', bloqueante: true }]);
    const pTercero = await (await browser.newContext()).newPage();
    const invitacion = await enRevision(pTercero, e, NOMBRE);
    const ruben = await personaEmbebida(browser, e, e.ruben);
    await abrirDesdeBandeja(ruben, NOMBRE, 'Compras');
    await aprobarArea(ruben, 'Compras');
    const olgaAprueba = await personaEmbebida(browser, e, e.olga);
    await esperarListas(olgaAprueba, NOMBRE, 'Cumplimiento', 'Satisfecha');
    await aprobarArea(olgaAprueba, 'Cumplimiento');
    await esperarEstadoDelTercero(pTercero, 'Aprobado');

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, `vinculos/${invitacion.vinculoId}`);
    const monitoreo = olga.getByRole('region', { name: 'Monitoreo de listas' });
    await expect(monitoreo).toContainText('Satisfecha');
    await paso(page, {
      titulo: 'Revisa el monitoreo del vínculo activo',
      explicacion:
        'Un vínculo activo tiene su propio apartado de «Monitoreo de listas»: el estado de la última consulta, cuándo se hizo, cuándo es la próxima y las personas que se reconsultan. ' +
        'Sin cambios, la consulta sale «Satisfecha» y se reconsulta sola cada 30 días; no hace falta que nadie la pida.',
      resaltar: monitoreo,
      configuracion: {
        ajuste: 'Días de reconsulta de listas del vínculo activo',
        ruta: 'Configuración › Listas restrictivas (/listas-restrictivas)',
        efecto: 'Cada cuántos días se reconsulta un vínculo activo. El estudio de cumplimiento vence en su propio plazo.',
      },
    });

    // Pasan 31 días: la reconsulta automática corre sin novedades.
    const resultado = await revisarVerificaciones(e, dentroDe(31));
    expect(resultado.monitoreos).toBe(1);
    await page.reload();
    await expect(monitoreo).toContainText('Sistema');
    await paso(page, {
      titulo: 'La reconsulta automática corre sin novedades',
      explicacion:
        'Al cumplirse el plazo, Validasoft reconsulta el vínculo sin que nadie lo pida. Si las personas siguen sin aparecer en las listas, no hay novedad: el monitoreo queda registrado como hecho por «Sistema» y la siguiente reconsulta se programa otros 30 días.',
      resaltar: monitoreo,
    });

    // El proveedor agrega un socio que sí aparece en la lista: el monitoreo lo encuentra en la siguiente reconsulta.
    await pTercero.goto('/ficha/legal');
    await expect(pTercero.getByRole('heading', { level: 1, name: 'Información legal' })).toBeVisible();
    await pTercero.getByRole('button', { name: 'Agregar persona' }).click();
    const persona = pTercero.getByRole('dialog', { name: 'Agregar persona' });
    await persona.getByRole('combobox', { name: 'Rol' }).click();
    await pTercero.getByRole('option', { name: 'Socio o accionista' }).click();
    await persona.getByRole('combobox', { name: 'Tipo de identificación' }).click();
    await pTercero.getByRole('option', { name: 'CC', exact: true }).click();
    await persona.getByRole('textbox', { name: 'Número de identificación' }).fill('1012345678');
    await persona.getByRole('textbox', { name: 'Nombres' }).fill('Roberto');
    await persona.getByRole('textbox', { name: 'Apellidos' }).fill('Coincidencia Prueba');
    await persona.getByRole('button', { name: 'Listo' }).click();
    await pTercero.getByRole('group', { name: 'Guardar la sección' }).getByRole('button', { name: 'Guardar', exact: true }).click();
    await expect(pTercero.getByRole('listitem', { name: 'Roberto Coincidencia Prueba' })).toBeVisible();
    await revisarVerificaciones(e, dentroDe(62));

    const fila = olga.getByRole('row').filter({ hasText: NOMBRE });
    await expect(async () => {
      await irASeccion(olga, 'Vínculos');
      await irASeccion(olga, 'Cumplimiento');
      await expect(fila).toContainText('Monitoreo del vínculo', { timeout: 1500 });
    }).toPass({ timeout: 60_000 });
    await expect(fila).toContainText('Coincidencia por analizar');
    await paso(page, {
      titulo: 'La novedad llega a la bandeja de Cumplimiento',
      explicacion:
        'Cuando el monitoreo encuentra una coincidencia nueva, la fila aparece en la bandeja de Cumplimiento con el origen «Monitoreo del vínculo» y la coincidencia por analizar. Te llega también una notificación en la campana. ' +
        'La fila dice el número de coincidencias por atender; «Abrir» lleva al vínculo para decidirla.',
      resaltar: fila,
    });

    await fila.getByRole('link', { name: 'Abrir' }).click();
    await expect(olga.getByRole('heading', { level: 1, name: NOMBRE })).toBeVisible();
    const novedad = olga.getByRole('region', { name: 'Monitoreo de listas' });
    const coincidencia = novedad.getByRole('listitem', { name: 'Roberto Coincidencia Prueba · OFAC (SDN)' });
    await expect(coincidencia).toContainText('Socio · CC 1012345678');
    await coincidencia.getByRole('button', { name: 'Confirmar' }).click();
    const confirmar = olga.getByRole('dialog', { name: 'Confirmar coincidencia' });
    await confirmar.getByRole('textbox', { name: /Justificación/ }).fill('Es la misma persona del listado: sin duda.');
    await confirmar.getByRole('button', { name: 'Guardar decisión' }).click();
    await expect(confirmar).toBeHidden();
    await expect(coincidencia).toContainText('Confirmada');
    await paso(page, {
      titulo: 'Confirma la coincidencia del socio',
      explicacion:
        'Confirmar aquí deja la coincidencia como confirmada con tu justificación. El vínculo sigue activo: no se bloquea por sí solo. Es un administrador de la empresa quien decide si lo bloquea o lo desvincula, con su motivo. ' +
        'La coincidencia queda en el informe SAGRILAFT y sale de tu bandeja, porque ya no tienes nada por decidir.',
      resaltar: novedad,
    });
  },
);
