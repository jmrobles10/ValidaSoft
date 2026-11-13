import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'informes',
    actor: 'Administrador de empresa',
    titulo: 'Consulta el informe de terceros y vínculos',
    resumen:
      'La administradora de Constructora Sur abre el informe de terceros y vínculos: ve un vínculo por fila con su estado, su certificado y lo que falta por validar. Filtra por estado y por tipo de vínculo, y lo descarga en Excel.',
    precondiciones: [
      'Un grupo con Constructora Sur, con un tipo de vínculo de Proveedor y otro de Cliente.',
      'Dos vínculos invitados: un proveedor que aún no acepta y un cliente en la misma situación.',
    ],
    relacionados: ['informes/estado-de-expedientes', 'vinculos/lista-y-filtros', 'informes/certificados'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const aceros = await invitar(e, e.sur, nuevoTercero('Aceros del Sur S.A.S.'), 'PROVEEDOR');
    await invitar(e, e.sur, nuevoTercero('Transportes del Sur S.A.S.'), 'CLIENTE');

    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'informes/vinculos');
    await expect(app.getByRole('heading', { level: 1, name: 'Informes' })).toBeVisible();
    const tabla = app.getByRole('table', { name: 'Terceros y vínculos' });
    await expect(tabla.getByRole('row', { name: `Abrir el vínculo de ${aceros.nombre}` })).toBeVisible();

    // 1. La tabla de vínculos.
    await paso(page, {
      titulo: 'Abre el informe de terceros y vínculos',
      explicacion:
        'La pestaña «Terceros y vínculos» trae un vínculo por fila, con el tercero, su estado, si tiene certificado vigente, el expediente en curso, los documentos por vencer o vencidos y las secciones que faltan por validar. ' +
        'Arriba, el resumen cuenta los vínculos y cuántos tienen algún pendiente. Haz clic en una fila para abrir el vínculo.',
      resaltar: tabla,
    });

    // 2. Filtro por estado y por tipo.
    await app.getByRole('button', { name: /^Estado/ }).click();
    await app.getByRole('menuitem', { name: 'Invitado', exact: true }).click();
    await page.keyboard.press('Escape');
    await expect(tabla.getByRole('row', { name: `Abrir el vínculo de ${aceros.nombre}` })).toBeVisible();
    await app.getByRole('button', { name: /^Tipo de vínculo/ }).click();
    await app.getByRole('menuitem', { name: 'Cliente', exact: true }).click();
    await page.keyboard.press('Escape');
    await expect(tabla.getByRole('row', { name: `Abrir el vínculo de ${aceros.nombre}` })).toHaveCount(0);
    await paso(page, {
      titulo: 'Filtra por estado y tipo de vínculo',
      explicacion:
        'El filtro «Estado» usa los mismos estados que la lista de vínculos (Invitado, En validación, En revisión, Activo, Por vencer, Vencido, Bloqueado, Desvinculado o Anulado). ' +
        'El filtro «Tipo de vínculo» separa a los proveedores de los clientes. Los dos se combinan: aquí solo queda el cliente invitado.',
      resaltar: [app.getByRole('button', { name: /^Estado/ }), app.getByRole('button', { name: /^Tipo de vínculo/ })],
      configuracion: {
        ajuste: 'Tipos de vínculo',
        ruta: 'Configuración › Tipos de vínculo (/tipos-vinculo)',
        efecto: 'Los tipos que existen en la empresa son las opciones de este filtro.',
      },
    });

    // 3. Descarga en Excel.
    const descarga = page.waitForEvent('download');
    await app.getByRole('button', { name: 'Descargar Excel' }).click();
    await descarga;
    await paso(page, {
      titulo: 'Descarga el informe en Excel',
      explicacion:
        '«Descargar Excel» baja el informe con los filtros que tengas puestos en ese momento. El archivo trae las mismas filas que ves en la tabla, para trabajar con ellas fuera de Validasoft.',
      resaltar: app.getByRole('button', { name: 'Descargar Excel' }),
    });
  },
);
