import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

recorrido(
  {
    dominio: 'informes',
    actor: 'Administrador de empresa',
    titulo: 'Consulta el estado de los expedientes y sus tiempos',
    resumen:
      'La administradora de Constructora Sur abre el informe de estado de expedientes: ve cada expediente con su folio y cuánto tardó cada área en decidir. Filtra por estado y baja el resultado en Excel con los mismos filtros.',
    precondiciones: [
      'Un grupo con Constructora Sur y su área Revisión con Sofía como miembro; el certificado de seguridad y salud en el trabajo es requisito del flujo de proveedor.',
      'Un proveedor cuyo expediente ya fue aprobado por Revisión, para que el informe muestre tiempos reales.',
    ],
    relacionados: ['informes/exportar-un-expediente', 'informes/plazos-de-revision', 'revision/aprobar-un-area'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const invitacion = await invitar(e, e.sur, nuevoTercero('Ferretería El Remache S.A.S.'));
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT Remache') },
      { nombre: SST.nombre, archivo: pdf('SST Remache'), campos: { 'Fecha de expedición': haceDias(2) } },
    ]);
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
    await aprobarArea(sofia, 'Revisión');

    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'informes/estado-expedientes');
    await expect(app.getByRole('heading', { level: 1, name: 'Informes' })).toBeVisible();
    const fila = app.getByRole('row').filter({ hasText: invitacion.nombre });
    await expect(fila).toContainText('Aprobado');
    await expect(fila).toContainText('2 de 2');

    // 1. El expediente en el informe.
    await paso(page, {
      titulo: 'Abre el informe de estado de expedientes',
      explicacion:
        'Informes muestra un expediente por fila, con su folio, el tercero, el estado y cuántos de sus requisitos están aprobados («2 de 2»). ' +
        'Las pestañas de arriba llevan a los terceros y vínculos y al plazo de revisión. Haz clic en una fila para llegar al expediente.',
      resaltar: fila,
      configuracion: {
        ajuste: 'Permisos del rol (leer expedientes)',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'Quien no tiene permiso para leer expedientes no ve el informe de estado: la pestaña que se abre por defecto es la de vínculos.',
      },
    });

    // 2. Los tiempos por área.
    const tiempos = app.getByRole('table', { name: 'Tiempos por área' });
    await expect(tiempos.getByRole('row', { name: /Revisión/ })).toBeVisible();
    await paso(page, {
      titulo: 'Lee cuánto tarda cada área',
      explicacion:
        'La tabla «Tiempos por área» cuenta, para cada área, los expedientes que pasaron por ella, los que siguen pendientes, y el promedio, la mediana y el máximo de días que tardó en decidir. ' +
        'Los tiempos salen de los expedientes aprobados: la apertura hasta la aprobación se reparte entre la carga del tercero, la devolución y la revisión.',
      resaltar: tiempos,
    });

    // 3. Filtro por estado.
    await app.getByRole('button', { name: /^Estado/ }).click();
    await app.getByRole('menuitem', { name: 'Aprobado' }).click();
    await expect(fila).toBeVisible();
    await paso(page, {
      titulo: 'Filtra por estado',
      explicacion:
        'El filtro «Estado» deja solo los expedientes en esa situación: en carga, en revisión, observado, aprobado, rechazado, anulado o caducado. ' +
        'Los filtros de tipo de vínculo y de fechas se combinan con este. «Limpiar filtros» los quita todos.',
      resaltar: app.getByRole('button', { name: /^Estado/ }),
    });

    // 4. Descarga en Excel con los filtros.
    const descarga = page.waitForEvent('download');
    await app.getByRole('button', { name: 'Descargar Excel' }).click();
    expect((await descarga).suggestedFilename()).toMatch(/^estado-expedientes-constructora-sur-sas-\d{4}-\d{2}-\d{2}\.xlsx$/);
    await paso(page, {
      titulo: 'Descarga el informe en Excel',
      explicacion:
        '«Descargar Excel» baja el informe con los mismos filtros que ves en pantalla, en un archivo que se llama «estado-expedientes» seguido del nombre de la empresa y la fecha. ' +
        'Si cambias los filtros antes de descargar, el archivo sale con los nuevos.',
      resaltar: app.getByRole('button', { name: 'Descargar Excel' }),
    });
  },
);
