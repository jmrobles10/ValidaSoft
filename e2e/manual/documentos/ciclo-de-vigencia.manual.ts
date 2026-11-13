import type { Browser } from '@playwright/test';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST, type Escenario, type Tercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

/** Fecha `YYYY-MM-DD` de hoy menos `dias` (en Bogotá). */
function haceDias(dias: number): string {
  const hoy = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
  const [a, m, d] = hoy.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d - dias)).toISOString().slice(0, 10);
}

/** Proveedor de Sur con el certificado de SST expedido hace `dias` (siembra). */
async function proveedorConCertificado(browser: Browser, e: Escenario, tercero: Tercero, dias: number): Promise<void> {
  const invitacion = await invitar(e, e.sur, tercero);
  await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${tercero.nombre}`) },
    { nombre: SST.nombre, archivo: pdf(`SST ${tercero.nombre}`), campos: { 'Fecha de expedición': haceDias(dias) } },
  ]);
}

recorrido(
  {
    dominio: 'documentos',
    actor: 'Administrador de empresa',
    titulo: 'Sigue el ciclo de vigencia de un certificado',
    resumen:
      'El administrador de Constructora Sur ve los cuatro momentos por los que pasa un certificado de seguridad y salud en el trabajo: vigente, por vencer, en periodo de gracia y vencido. Cada uno cambia la fila de Vencimientos y lo que el proveedor puede hacer.',
    precondiciones: [
      'Un grupo con Constructora Sur, con el certificado de SST como requisito del flujo de proveedor (30 días de vigencia desde la expedición, 10 de aviso y 5 de gracia).',
      'Cuatro proveedores con el certificado expedido hace 3, 25, 32 y 40 días, uno en cada estado.',
    ],
    relacionados: ['documentos/vencimientos', 'documentos/vigencia-del-vinculo'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const vigente = nuevoTercero('Tornillería Vigente S.A.S.');
    const porVencer = nuevoTercero('Metalúrgica Por Vencer S.A.S.');
    const enGracia = nuevoTercero('Ferretería En Gracia S.A.S.');
    const vencido = nuevoTercero('Aceros Vencidos S.A.S.');
    await proveedorConCertificado(browser, e, vigente, 3);
    await proveedorConCertificado(browser, e, porVencer, 25);
    await proveedorConCertificado(browser, e, enGracia, 32);
    await proveedorConCertificado(browser, e, vencido, 40);

    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'vencimientos');
    await expect(app.getByRole('heading', { level: 1, name: 'Vencimientos' })).toBeVisible();
    const filaVigente = app.getByRole('row').filter({ hasText: vigente.nombre });
    const filaPorVencer = app.getByRole('row').filter({ hasText: porVencer.nombre });
    const filaGracia = app.getByRole('row').filter({ hasText: enGracia.nombre });
    const filaVencido = app.getByRole('row').filter({ hasText: vencido.nombre });
    await expect(filaVigente).toContainText('Vigente');
    await expect(filaPorVencer).toContainText('Por vencer');
    await expect(filaGracia).toContainText('En gracia');
    await expect(filaVencido).toContainText('Vencida');

    await paso(page, {
      titulo: 'Mira el certificado vigente',
      explicacion:
        'Con la expedición hace 3 días, el certificado está vigente: la fila muestra «Vigente hasta» y los días que faltan. Mientras falte más que el plazo de aviso, no hay nada que hacer.',
      resaltar: filaVigente,
      configuracion: {
        ajuste: 'Vigencia del tipo de documento',
        ruta: 'Configuración › Tipos de documento (/tipos-documento)',
        efecto: 'Los días de vigencia, de aviso y de gracia de cada tipo deciden en qué estado queda la fila.',
      },
    });

    await paso(page, {
      titulo: 'Entra en aviso: por vencer',
      explicacion:
        'Con la expedición hace 25 días, faltan pocos días para el vencimiento y el certificado queda «Por vencer». Sigue sirviendo, pero la fila lo marca para que el proveedor lo renueve a tiempo.',
      resaltar: filaPorVencer,
    });

    await paso(page, {
      titulo: 'Vence la vigencia: en gracia',
      explicacion:
        'Con la expedición hace 32 días, la vigencia ya venció, pero el periodo de gracia sigue abierto: la fila dice «En gracia» y «Se acepta hasta» una fecha. El proveedor aún puede subir una versión nueva hasta esa fecha.',
      resaltar: filaGracia,
    });

    await paso(page, {
      titulo: 'Pasa la gracia: vencida',
      explicacion:
        'Con la expedición hace 40 días, pasó también el periodo de gracia y el certificado queda «Vencida». El proveedor recibe un aviso por correo de que el documento venció para tu empresa, y debe subir una versión nueva.',
      resaltar: filaVencido,
      avisos: true,
    });
  },
);
