import { esperarCorreo, literal } from '../../soporte/correo';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, nuevaPersonaNatural, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'operacion',
    actor: 'Administrador de empresa',
    titulo: 'Invita a varios terceros con un archivo',
    resumen:
      'La administradora descarga la plantilla, sube un archivo con varios terceros, revisa fila por fila qué está bien y qué tiene errores, y crea el lote. Las filas válidas se invitan en segundo plano.',
    precondiciones: ['Un grupo con Constructora Norte y su administradora Ana, sin lotes previos.'],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const aceros = nuevoTercero('Aceros del Lote S.A.S.');
    const ana = nuevaPersonaNatural('Ana Lote Pérez');
    const csv = [
      'Tipo de identificación;Número de identificación;DV;Razón social o nombres;Correo;Tipo de vínculo',
      `NIT;${aceros.identificacion.numero};${aceros.identificacion.digitoVerificacion};${aceros.nombre};${aceros.correo};PROVEEDOR`,
      `CC;${ana.identificacion.numero};;${ana.nombre};${ana.correo};PROVEEDOR`,
      `NIT;${nuevoTercero().identificacion.numero};;Sin correo válido;no-es-correo;PROVEEDOR`,
    ].join('\r\n');

    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
    await expect(app.getByRole('button', { name: 'Invitar por lote' })).toBeVisible();
    await paso(page, {
      titulo: 'Abre la invitación por lote',
      explicacion:
        'Desde Vínculos, el botón «Invitar por lote» muestra los lotes de la empresa. «Nuevo lote» abre el asistente de tres pasos: descargar la plantilla, subir el archivo y revisar antes de invitar.',
      resaltar: app.getByRole('button', { name: 'Invitar por lote' }),
      configuracion: { ajuste: 'Invitación por lote', ruta: 'Vínculos › Invitar por lote', efecto: 'Cada fila crea o reutiliza un tercero y un vínculo de la empresa.' },
    });
    await app.getByRole('button', { name: 'Invitar por lote' }).click();
    await app.getByRole('button', { name: 'Nuevo lote' }).click();
    await expect(app.getByRole('heading', { name: 'Nuevo lote de invitaciones' })).toBeVisible();

    await paso(page, {
      titulo: 'Descarga la plantilla',
      explicacion:
        'La plantilla trae las columnas obligatorias: tipo y número de identificación, razón social o nombres, correo y tipo de vínculo. Las opcionales son país, DV, tipo de persona, nombre comercial, flujo, contacto y teléfono. ' +
        'Puedes bajarla en Excel o en CSV; las dos tienen las mismas columnas.',
      resaltar: [app.getByRole('button', { name: 'Plantilla Excel' }), app.getByRole('button', { name: 'Plantilla CSV' })],
      variantes: [{ cuando: 'El archivo no tiene una de las columnas obligatorias', entonces: 'Validasoft lo rechaza: «Al archivo le faltan columnas de la plantilla».' }],
    });

    await app.locator('input[type="file"]').setInputFiles({ name: 'proveedores.csv', mimeType: 'text/csv', buffer: Buffer.from(csv, 'utf8') });
    await expect(app.getByText('3 filas: 2 válidas, 1 con errores, 0 repetidas.')).toBeVisible();
    await expect(app.getByRole('table').getByText('«no-es-correo» no es un correo válido.')).toBeVisible();

    await paso(page, {
      titulo: 'Revisa la vista previa fila por fila',
      explicacion:
        'Al subir el archivo se valida cada fila antes de invitar. El resumen cuenta filas válidas, con errores y repetidas. La fila con un correo mal escrito aparece con su motivo y no se invitará. ' +
        'Corrige el archivo en tu hoja de cálculo y vuelve a subirlo para revisar de nuevo.',
      resaltar: [app.getByText('3 filas: 2 válidas, 1 con errores, 0 repetidas.'), app.getByRole('table')],
      variantes: [{ cuando: 'Ninguna fila es válida', entonces: 'No se puede crear el lote: «Ninguna fila es válida: corrige el archivo y vuelve a subirlo».' }],
    });

    await app.getByLabel('Nombre del lote').fill('Proveedores E2E');
    await paso(page, {
      titulo: 'Nombra el lote y confirma',
      explicacion:
        'Escribe un nombre para reconocer el lote luego; si lo dejas vacío se usa el nombre del archivo. «Vigencia de cada invitación» dice cuántos días tiene cada tercero para aceptar (7 por omisión). El botón indica cuántos terceros se van a invitar: solo las filas válidas. ' +
        'Al crearlo, las invitaciones se envían en segundo plano y no tienes que esperar en esta pantalla.',
      resaltar: [app.getByLabel('Nombre del lote'), app.getByLabel('Vigencia de cada invitación (días)'), app.getByRole('button', { name: 'Invitar a 2 terceros' })],
    });
    await app.getByRole('button', { name: 'Invitar a 2 terceros' }).click();

    await expect(app.getByRole('heading', { name: 'Proveedores E2E' })).toBeVisible();
    await expect(app.getByText('Completado', { exact: true })).toBeVisible({ timeout: 30_000 });
    await expect(app.getByText('2 de 2')).toBeVisible();
    const filas = app.getByRole('table', { name: 'Filas del lote' });
    await expect(filas.getByRole('link', { name: 'Invitado' })).toHaveCount(2);
    await expect(filas.getByText('Con errores')).toBeVisible();

    await paso(page, {
      titulo: 'Comprueba el resultado del lote',
      explicacion:
        'El detalle del lote muestra el avance («2 de 2»), el estado final y una fila por cada registro del archivo. Las válidas quedan con estado «Exitosa» y detalle «Invitado», que enlaza a su vínculo; la que tenía errores queda «Con errores» y no recibió invitación. ' +
        'Cada tercero invitado recibe el correo de la empresa con su enlace.',
      resaltar: [app.getByRole('heading', { name: 'Proveedores E2E' }), filas],
    });

    await filas.getByRole('row').filter({ hasText: aceros.nombre }).getByRole('link', { name: 'Invitado' }).click();
    await expect(app.getByText(aceros.nombre).first()).toBeVisible();
    await paso(page, {
      titulo: 'Abre el vínculo de un tercero del lote',
      explicacion:
        'Desde la fila, el enlace «Invitado» lleva al vínculo del tercero, que queda en estado Invitado hasta que acepte. Desde ahí se sigue el proceso igual que con una invitación individual.',
      resaltar: app.getByText(aceros.nombre).first(),
    });

    await esperarCorreo(aceros.correo, new RegExp(`^${literal(e.norte.nombre)} te invita a validar tu vínculo como proveedor$`));
  },
);
