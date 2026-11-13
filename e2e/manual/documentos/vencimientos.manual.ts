import type { Browser, Page } from '@playwright/test';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST, type Escenario, type Tercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

/** Fecha `YYYY-MM-DD` de hoy en Bogotá más `dias` (negativo: en el pasado). */
function fecha(dias: number): string {
  const hoy = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
  const [a, m, d] = hoy.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

/** Proveedor de Sur que entrega RUT y el certificado de SST con la fecha de expedición indicada (siembra). */
async function proveedorConCertificado(browser: Browser, e: Escenario, tercero: Tercero, expedicion: string): Promise<void> {
  const invitacion = await invitar(e, e.sur, tercero);
  const pagina: Page = await (await browser.newContext()).newPage();
  await completarInvitacion(pagina, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${tercero.nombre}`) },
    { nombre: SST.nombre, archivo: pdf(`SST ${tercero.nombre}`), campos: { 'Fecha de expedición': expedicion } },
  ]);
}

recorrido(
  {
    dominio: 'documentos',
    actor: 'Administrador de empresa',
    titulo: 'Revisa los documentos que vencen en la lista de vencimientos',
    resumen:
      'El administrador de Constructora Sur abre Vencimientos y ve, documento por documento, hasta cuándo sirve cada certificado que los proveedores le compartieron. Distingue lo vigente, lo que está por vencer, lo que está en gracia y lo vencido.',
    precondiciones: [
      'Un grupo con Constructora Sur, con el certificado de seguridad y salud en el trabajo como requisito del flujo de proveedor.',
      'Tres proveedores con el certificado expedido hace 3 días, 25 días y 40 días, para que estén vigentes, por vencer y vencidos.',
    ],
    relacionados: ['documentos/ciclo-de-vigencia', 'documentos/recordatorio-desde-vencimientos'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const vigente = nuevoTercero('Tornillería Vigente S.A.S.');
    const porVencer = nuevoTercero('Metalúrgica Por Vencer S.A.S.');
    const vencido = nuevoTercero('Aceros Vencidos S.A.S.');
    await proveedorConCertificado(browser, e, vigente, fecha(-3));
    await proveedorConCertificado(browser, e, porVencer, fecha(-25));
    await proveedorConCertificado(browser, e, vencido, fecha(-40));

    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'vencimientos');
    await expect(app.getByRole('heading', { level: 1, name: 'Vencimientos' })).toBeVisible();
    const filaVigente = app.getByRole('row').filter({ hasText: vigente.nombre });
    const filaPorVencer = app.getByRole('row').filter({ hasText: porVencer.nombre });
    const filaVencida = app.getByRole('row').filter({ hasText: vencido.nombre });
    await expect(filaVigente).toContainText('Vigente');
    await expect(filaPorVencer).toContainText('Por vencer');
    await expect(filaVencida).toContainText('Vencida');

    await paso(page, {
      titulo: 'Abre la lista de vencimientos',
      explicacion:
        'Vencimientos reúne los documentos que los terceros compartieron con tu empresa, con la fecha hasta la que están vigentes para ella. ' +
        'Cada fila muestra el tercero, el documento y su versión, la fecha «Vigente hasta», el estado y los días que faltan o que pasaron.',
      resaltar: app.getByRole('table'),
      configuracion: {
        ajuste: 'Vigencia del tipo de documento',
        ruta: 'Configuración › Tipos de documento (/tipos-documento)',
        efecto: 'La regla de vigencia de cada tipo (desde la expedición, la carga o un vencimiento propio) decide la fecha «Vigente hasta».',
      },
    });

    await paso(page, {
      titulo: 'Distingue los estados de vigencia',
      explicacion:
        'Vigente (1): falta más de la anticipación de aviso. Por vencer (2): el documento sigue sirviendo, pero le quedan pocos días. En gracia: ya venció, pero el tercero aún puede subir una versión nueva hasta la fecha que se indica. ' +
        'Vencida (3): pasó el periodo de gracia y el tercero debe subir una versión nueva.',
      // Los estados (no las filas enteras): tres filas pegadas con su recuadro se encimaban y los números quedaban tapados.
      resaltar: [filaVigente.getByText('Vigente', { exact: true }), filaPorVencer.getByText('Por vencer', { exact: true }), filaVencida.getByText('Vencida', { exact: true })],
    });
  },
);
