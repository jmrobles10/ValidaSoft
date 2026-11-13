import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion } from '../../soporte/empresa';
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
    titulo: 'Exporta un expediente completo',
    resumen:
      'La administradora de Constructora Sur abre un expediente aprobado desde el informe, lo exporta en un ZIP con su resumen, su índice, su línea de tiempo y los documentos, y comprueba que la exportación quedó registrada en la auditoría de accesos.',
    precondiciones: [
      'Un grupo con Constructora Sur, con el RUT y el certificado de seguridad y salud en el trabajo como requisitos del flujo de proveedor.',
      'Un proveedor cuyo expediente ya fue aprobado por el área Revisión.',
    ],
    relacionados: ['informes/estado-de-expedientes', 'informes/auditoria', 'documentos/descargar-un-documento'],
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
    await app.getByRole('row').filter({ hasText: invitacion.nombre }).click();
    await expect(app.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();

    // 1. El botón de exportar en el expediente.
    const boton = app.getByRole('button', { name: 'Exportar expediente' });
    await expect(boton).toBeVisible();
    await paso(page, {
      titulo: 'Abre el expediente aprobado',
      explicacion:
        'Desde el informe, un clic en la fila lleva al expediente. Con el expediente aprobado abierto, el botón «Exportar expediente» genera el paquete completo de ese expediente, con su folio.',
      resaltar: boton,
    });

    // 2. La exportación.
    const descarga = page.waitForEvent('download');
    await boton.click();
    expect((await descarga).suggestedFilename()).toMatch(/^expediente-.*\.zip$/);
    await paso(page, {
      titulo: 'Descarga el expediente en ZIP',
      explicacion:
        'El archivo ZIP se llama «expediente» seguido del folio. Trae el resumen en PDF, un índice, la línea de tiempo del expediente en JSON y cada documento incluido con su versión (aquí, el RUT y el certificado de seguridad y salud en el trabajo). ' +
        'Cada documento que se incluye también queda registrado en la auditoría.',
      resaltar: boton,
      avisos: true,
    });

    // 3. La exportación queda en la auditoría de accesos.
    await irASeccion(app, 'Auditoría');
    await app.getByRole('tab', { name: /Accesos/ }).click();
    await expect(app.getByRole('row').filter({ hasText: 'Exportado' }).first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el registro en la auditoría',
      explicacion:
        'En Auditoría, la pestaña «Accesos» registra quién exportó el expediente y cuándo: una fila «Exportado» con el folio, y una fila «Descargado» por cada documento que se incluyó. Así la empresa sabe qué información salió del sistema y quién la sacó.',
      resaltar: app.getByRole('row').filter({ hasText: 'Exportado' }).first(),
    });
  },
);
