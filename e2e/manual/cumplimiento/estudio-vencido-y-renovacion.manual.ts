import { aprobarUltimaArea, enRevision, esperarListas, pedirVerificaciones, personaEmbebida, revisarVerificaciones } from '../../soporte/cumplimiento';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'Cementos Estudio Vencido S.A.S.';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Renueva un estudio de cumplimiento vencido',
    resumen:
      'Un estudio de cumplimiento venció mientras el expediente seguía abierto. La oficial lo ve marcado como vencido, recibe el aviso, comprueba que el expediente no se puede aprobar con un estudio vencido y registra uno nuevo.',
    precondiciones: [
      'Un grupo con Constructora Norte y el flujo de proveedores con listas restrictivas y estudio de cumplimiento bloqueantes. El estudio vale 1 día.',
      'Un proveedor en revisión, con las listas satisfechas, y un estudio de riesgo bajo ya registrado por Cumplimiento que vence al pasar dos días.',
    ],
    relacionados: ['cumplimiento/estudio-de-cumplimiento', 'cumplimiento/monitoreo-periodico'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [
      { tipo: 'ListasRestrictivas', bloqueante: true },
      { tipo: 'EstudioDeCumplimiento', bloqueante: true, vigenciaDias: 1 },
    ]);
    const pTercero = await (await browser.newContext()).newPage();
    await enRevision(pTercero, e, NOMBRE);

    // Cumplimiento registra un estudio de riesgo bajo, favorable (precondición): Compras aún no decide.
    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await esperarListas(olga, NOMBRE, 'Cumplimiento', 'Satisfecha');
    await olga.getByRole('button', { name: 'Registrar estudio' }).click();
    const alta = olga.getByRole('dialog', { name: /Estudio de cumplimiento/ });
    await alta.getByRole('button', { name: '1 · Bajo' }).click();
    await alta.getByRole('combobox', { name: 'Concepto' }).click();
    await olga.getByRole('option', { name: 'Favorable', exact: true }).click();
    await alta.getByRole('button', { name: 'Registrar estudio' }).click();
    await expect(alta).toBeHidden();

    // Pasan dos días (simulado): el estudio venció con el expediente abierto.
    const resultado = await revisarVerificaciones(e, new Date(Date.now() + 2 * 86_400_000));
    expect(resultado.expiraciones).toBeGreaterThanOrEqual(1);

    await abrirDesdeBandeja(olga, NOMBRE, 'Cumplimiento');
    const estudio = olga.getByRole('region', { name: 'Estudio de cumplimiento' });
    await expect(estudio).toContainText('Vencida');
    await expect(olga.getByRole('button', { name: 'Registrar un estudio nuevo' })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el estudio vencido',
      explicacion:
        'Cuando un estudio vence, el expediente lo muestra como «Vencida» con el aviso «El estudio venció: hay que volver a hacerlo antes de aprobar». El botón «Registrar un estudio nuevo» abre el mismo formulario de siempre. ' +
        'Un estudio vence según los días que fija el flujo; si el expediente se cerró antes, el vencimiento no lo reabre.',
      resaltar: [estudio, olga.getByRole('button', { name: 'Registrar un estudio nuevo' })],
      configuracion: {
        ajuste: 'Vigencia del estudio de cumplimiento',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Los días de vigencia del estudio los fija cada flujo: al vencer, vuelve a pedirse.',
      },
    });

    await olga.getByRole('button', { name: /^Notificaciones, \d+ sin leer/ }).click();
    await expect(olga.getByText('Verificación vencida').first()).toBeVisible();
    await paso(page, {
      titulo: 'Recibe el aviso de vencimiento',
      explicacion:
        'Cumplimiento recibe una notificación en la campana: «Verificación vencida», con el nombre del tercero y la instrucción de volver a hacer el estudio antes de cerrar el expediente. ' +
        'El aviso se conserva en la campana hasta que lo leas.',
      resaltar: olga.getByText(/Venció el estudio de cumplimiento/).first(),
    });
    await page.keyboard.press('Escape');

    const ruben = await personaEmbebida(browser, e, e.ruben);
    await abrirDesdeBandeja(ruben, NOMBRE, 'Compras');
    await aprobarArea(ruben, 'Compras');
    await abrirDesdeBandeja(olga, NOMBRE, 'Cumplimiento');
    await aprobarUltimaArea(olga, 'Cumplimiento', /Estudio de cumplimiento \(vencid[ao]\)/);
    await paso(page, {
      titulo: 'El vencimiento bloquea la aprobación',
      explicacion:
        'Compras y Cumplimiento aprueban sus áreas, pero el expediente no se cierra: el aviso dice que falta el estudio de cumplimiento, que está vencido. ' +
        'Mientras el estudio esté vencido, el vínculo no se aprueba aunque todas las áreas hayan decidido.',
      resaltar: olga.getByRole('alert').filter({ hasText: 'Falta cumplimiento para aprobar' }),
    });
    await esperarEstadoDelTercero(pTercero, 'EnRevision');

    await olga.getByRole('button', { name: 'Registrar un estudio nuevo' }).click();
    const nuevo = olga.getByRole('dialog', { name: /Estudio de cumplimiento/ });
    await nuevo.getByRole('button', { name: '1 · Bajo' }).click();
    await nuevo.getByRole('combobox', { name: 'Concepto' }).click();
    await olga.getByRole('option', { name: 'Favorable', exact: true }).click();
    await nuevo.getByRole('button', { name: 'Registrar estudio' }).click();
    await expect(nuevo).toBeHidden();
    await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
    await paso(page, {
      titulo: 'Registra un estudio nuevo y aprueba',
      explicacion:
        'El estudio nuevo reemplaza al vencido: queda vigente otra vez por los días del flujo. Como ya no falta nada, el expediente se aprueba y el vínculo queda activo; el tercero recibe el correo con el resultado.',
      resaltar: olga.getByRole('region', { name: 'Estudio de cumplimiento' }),
      variantes: [
        { cuando: 'El estudio nuevo es desfavorable', entonces: 'La verificación queda fallida y el expediente no se aprueba: se rechaza por cumplimiento o se corrige.' },
      ],
    });
    await esperarEstadoDelTercero(pTercero, 'Aprobado');
  },
);
