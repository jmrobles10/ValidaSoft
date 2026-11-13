import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'informes',
    actor: 'Administrador de empresa',
    titulo: 'Revisa quién cambió la configuración en Auditoría',
    resumen:
      'La administradora de Constructora Norte abre Auditoría para saber quién creó y cambió las áreas de la empresa. Abre un cambio y ve, campo por campo, qué cambió. Luego revisa la pestaña de accesos, donde quedan las descargas de documentos.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Compras y sus miembros configurados (la siembra registra esos cambios).',
      'Una administradora del grupo con permiso para ver la auditoría.',
    ],
    relacionados: ['configuracion/areas-y-miembros', 'informes/exportar-un-expediente'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'auditoria');
    await expect(app.getByRole('heading', { level: 1, name: 'Auditoría' })).toBeVisible();
    await expect(app.getByRole('tab', { name: 'Configuración' })).toHaveAttribute('aria-selected', 'true');
    const fila = app.getByRole('row').filter({ hasText: 'Compras' }).filter({ hasText: 'Modificado' }).first();
    await expect(fila).toContainText('Esta empresa');

    // 1. La lista de cambios.
    await paso(page, {
      titulo: 'Abre la auditoría de configuración',
      explicacion:
        'Auditoría muestra cada cambio de configuración de la empresa, con la fecha y hora, quién lo hizo, qué entidad cambió (aquí, el área Compras), la acción (creado o modificado) y su alcance. ' +
        'La pestaña «Configuración» es la que se abre por defecto. Haz clic en una fila para ver el detalle.',
      resaltar: fila,
      configuracion: {
        ajuste: 'Permiso de auditoría (leer y exportar)',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'Quien tiene permiso de auditoría puede ver estos registros y exportarlos a Excel.',
      },
    });

    // 2. El detalle, campo por campo.
    await fila.click();
    const detalle = app.getByRole('dialog');
    await expect(detalle.getByText('Cambios campo por campo')).toBeVisible();
    await expect(detalle.getByText('miembros')).toBeVisible();
    await paso(page, {
      titulo: 'Mira qué campo cambió',
      explicacion:
        'El detalle muestra «Cambios campo por campo»: cada campo que cambió, con su valor antes y su valor después. En el caso de Compras se ve el campo «miembros», con la lista antes y después (las personas aparecen por su identificador). ' +
        'Así no hace falta adivinar qué se tocó en un cambio.',
      resaltar: detalle.getByText('miembros'),
    });

    // 3. Accesos.
    await detalle.getByRole('button', { name: 'Cerrar el detalle' }).click();
    await app.getByRole('tab', { name: 'Accesos' }).click();
    await expect(app.getByText('No hay registros de auditoría.')).toBeVisible();
    await paso(page, {
      titulo: 'Revisa los accesos a documentos',
      explicacion:
        'La pestaña «Accesos» registra quién descargó documentos o exportó expedientes. Sin descargas, aparece «No hay registros de auditoría». ' +
        'Los eventos de cada vínculo y de cada expediente están en su propio historial, no aquí.',
      resaltar: app.getByText('No hay registros de auditoría.'),
    });
  },
);
