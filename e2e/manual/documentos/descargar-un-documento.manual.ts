import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { irASeccion } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'documentos',
    actor: 'Administrador de empresa',
    titulo: 'Descarga un documento del proveedor y comprueba su registro',
    resumen:
      'La administradora abre el vínculo de un proveedor, ve el RUT que compartió, lo descarga desde el visor y comprueba que la descarga quedó registrada en Auditoría › Accesos, con quién la hizo y cuándo.',
    precondiciones: [
      'Un grupo con Constructora Sur y una administradora con permiso para ver y descargar documentos.',
      'Un proveedor invitado por Sur que ya subió su RUT y su certificado de SST.',
    ],
    relacionados: ['revision/visor-y-datos-de-un-documento', 'informes/auditoria', 'informes/exportar-un-expediente'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Tornillería Descargable S.A.S.');
    const invitacion = await invitar(e, e.sur, proveedor);
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT descargable') },
      { nombre: SST.nombre, archivo: pdf('SST descargable'), campos: { 'Fecha de expedición': new Date(Date.now() - 86_400_000).toISOString().slice(0, 10) } },
    ]);

    // 1) El documento en el vínculo.
    const app = await abrirEmbebido(page, e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
    await expect(app.getByRole('heading', { level: 1, name: proveedor.nombre })).toBeVisible();
    const ver = app.getByRole('button', { name: new RegExp(`^Ver o descargar ${RUT}, versión 1`) });
    await expect(ver).toBeVisible();
    await paso(page, {
      titulo: 'Encuentra el documento en el vínculo',
      explicacion:
        'En el detalle del vínculo, «Documentos compartidos» lista lo que el proveedor tiene en su bóveda para esta empresa: el tipo de documento, la versión, la fecha de carga y la huella SHA-256 del archivo. ' +
        '«Ver o descargar» (1) abre esa versión exacta. La sección solo aparece a quien tiene permiso para leer documentos, y el botón, a quien puede descargarlos.',
      resaltar: ver,
      variantes: [
        { cuando: 'La versión es un formulario sin archivo', entonces: 'El botón dice «Ver datos»: no hay archivo para descargar y se ven los datos que declaró el tercero.' },
        { cuando: 'Tu rol no puede descargar documentos', entonces: 'Ves la lista, pero no el botón de ver o descargar.' },
      ],
      configuracion: { ajuste: 'Permisos del rol (documentos: leer y descargar)', ruta: 'Configuración › Usuarios y roles (/usuarios)', efecto: 'Decide quién ve la lista de documentos y quién puede abrirlos.' },
    });

    // 2) El visor.
    await ver.click();
    const visor = app.getByRole('dialog', { name: new RegExp(`^${RUT} · `) });
    const descargar = visor.getByRole('link', { name: 'Descargar', exact: true });
    await expect(descargar).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el documento en el visor',
      explicacion:
        'El visor abre los PDF y las imágenes dentro de la pantalla, con el nombre del documento y su versión. Pedirle al servidor el archivo ya cuenta como un acceso y queda registrado. ' +
        'El botón «Descargar» (1) guarda una copia en tu equipo; los tipos de archivo que el visor no muestra se abren solo descargándolos.',
      resaltar: descargar,
    });

    // 3) La descarga.
    const bajada = page.waitForEvent('download');
    await descargar.click();
    const archivo = await bajada;
    expect(archivo.suggestedFilename()).toMatch(/\.pdf$/i);
    await paso(page, {
      titulo: 'Descarga el archivo',
      explicacion:
        `El navegador guarda el archivo original del proveedor (aquí, «${archivo.suggestedFilename()}») tal como lo subió: no se modifica. ` +
        'Cada descarga queda registrada en la auditoría de la empresa, para poder demostrar después quién consultó qué documento y cuándo.',
      resaltar: descargar,
    });
    await page.keyboard.press('Escape');
    await expect(visor).toBeHidden();

    // 4) El registro en Auditoría › Accesos.
    await irASeccion(app, 'Auditoría');
    await expect(app.getByRole('heading', { level: 1, name: 'Auditoría' })).toBeVisible();
    await app.getByRole('tab', { name: 'Accesos' }).click();
    const fila = app.getByRole('row').filter({ hasText: 'Descargado' }).first();
    await expect(fila).toContainText('Documento');
    await expect(fila).toContainText('Ana Administradora');
    await paso(page, {
      titulo: 'Comprueba el registro en Auditoría › Accesos',
      explicacion:
        'La pestaña «Accesos» de Auditoría lista las descargas de documentos y certificados de la empresa: la fecha y hora, quién las hizo (aquí, Ana, desde el sistema integrado), la entidad «Documento» y la acción «Descargado». ' +
        'Haz clic en una fila para ver el detalle.',
      resaltar: fila,
      configuracion: { ajuste: 'Permiso de auditoría (leer)', ruta: 'Configuración › Usuarios y roles (/usuarios)', efecto: 'Quien lo tiene ve esta pestaña y puede exportarla a Excel.' },
    });

    // 5) El detalle del acceso.
    await fila.click();
    const detalle = app.getByRole('dialog');
    await expect(detalle.getByText('Documento', { exact: true }).first()).toBeVisible();
    await expect(detalle.getByText('Dirección IP')).toBeVisible();
    await paso(page, {
      titulo: 'Abre el detalle del acceso',
      explicacion:
        'El detalle muestra qué documento y qué versión se descargó (1), el motivo (2), quién lo hizo y desde dónde: la dirección IP y el navegador. Es la evidencia para responder «¿quién bajó este documento?» sin depender de la memoria de nadie.',
      resaltar: [detalle.getByText(`${RUT} · versión 1`), detalle.getByText('Descarga', { exact: true })],
    });
  },
);
