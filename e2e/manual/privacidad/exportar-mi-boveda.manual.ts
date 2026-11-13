import { expect, paso, recorrido } from '../nucleo';
import { pdf } from '../../soporte/archivos';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';

recorrido(
  {
    dominio: 'privacidad',
    actor: 'Tercero',
    titulo: 'Descarga una copia firmada de tu bóveda',
    resumen:
      'El tercero pide una exportación de su bóveda: Validasoft la prepara en segundo plano, avisa por correo cuando está lista y el tercero la descarga como un ZIP con sus archivos, sus datos y un manifiesto firmado para comprobar que nada cambió.',
    precondiciones: [
      'Un grupo con Constructora Norte, que invitó a un tercero que aceptó la invitación y subió el RUT y el certificado de existencia y representación legal.',
      'El tercero entra al portal con su sesión abierta.',
    ],
    relacionados: ['privacidad/solicitud-sobre-mis-datos'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros del Sur S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero);
    await completarInvitacion(page, invitacion, [
      { nombre: 'Registro Único Tributario', archivo: pdf('RUT Aceros del Sur') },
      { nombre: 'Certificado de existencia y representación legal', archivo: pdf('Camara Aceros del Sur') },
    ]);

    // 1. La tarjeta de exportación en Privacidad y mis datos.
    await page.goto('/privacidad');
    await expect(page.getByRole('heading', { level: 1, name: 'Privacidad y mis datos' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Pedir exportación' })).toBeVisible();
    await paso(page, {
      titulo: 'Abre la exportación de tu bóveda',
      explicacion:
        'En la parte de arriba de «Privacidad y mis datos» está «Exportar mi bóveda». La copia incluye todos tus archivos, tus datos y un manifiesto firmado por Validasoft, con el que se puede comprobar que el contenido no cambió después de la exportación. ' +
        'No tienes que esperar en esta pantalla: se prepara sola y te avisamos por correo cuando esté lista.',
      resaltar: page.getByRole('button', { name: 'Pedir exportación' }),
    });

    // 2. Pedir la exportación.
    await page.getByRole('button', { name: 'Pedir exportación' }).click();
    await expect(page.getByText('Estamos preparando tu exportación. Te avisamos por correo cuando esté lista.')).toBeVisible();
    await expect(page.getByText(/\d+ archivos · .* · disponible hasta/)).toBeVisible({ timeout: 90_000 });
    await paso(page, {
      titulo: 'Pide la exportación y espera su preparación',
      explicacion:
        'Al pedirla aparece el aviso de que la estamos preparando y queda en «Mis exportaciones» con su fecha. Cuando termina, aparecen el número de archivos, el tamaño y la fecha hasta la que puedes descargarla; ' +
        'después de esa fecha la eliminamos y tendrás que pedir otra.',
      resaltar: page.getByText(/\d+ archivos · .* · disponible hasta/),
      variantes: [
        { cuando: 'La exportación falla', entonces: 'El detalle dice que no pudimos prepararla; pide otra, y si se repite, escríbenos.' },
        { cuando: 'Pasó el plazo de descarga', entonces: 'El detalle dice que venció y se eliminó: pide una nueva si la necesitas.' },
        { cuando: 'Ya pediste varias exportaciones hoy', entonces: 'Validasoft no prepara otra ese día y te pide intentarlo al día siguiente.' },
      ],
    });

    // 3. Descargar el ZIP.
    const descarga = page.waitForEvent('download');
    await page.getByRole('button', { name: 'Descargar' }).click();
    const zip = await descarga;
    expect(zip.suggestedFilename()).toMatch(/\.zip$/);
    await expect(page.getByText('Empezó la descarga de tu exportación.')).toBeVisible();
    await paso(page, {
      titulo: 'Descarga el ZIP',
      explicacion:
        'Con «Descargar» baja un archivo ZIP. Dentro encuentras tus archivos, manifiesto.json con la huella SHA-256 de cada uno, manifiesto.firma.json con la firma de Validasoft y LEEME.txt con los pasos para verificarla. ' +
        'Descárgalo antes de la fecha «disponible hasta», porque después Validasoft lo elimina.',
      resaltar: page.getByRole('button', { name: 'Descargar' }),
      avisos: true,
    });

    // 4. Cómo verificarla.
    const ayuda = page.getByText('Cómo verificarla', { exact: true });
    await ayuda.click();
    await expect(page.getByText(/manifiesto\.json lista el SHA-256/)).toBeVisible();
    await paso(page, {
      titulo: 'Entiende cómo verificar la copia',
      explicacion:
        'Debajo de la exportación, «Cómo verificarla» explica el proceso: manifiesto.json lista la huella SHA-256 de cada archivo y manifiesto.firma.json trae la firma de Validasoft. ' +
        'La firma se comprueba con la clave pública que Validasoft publica en /.well-known/validasoft-certificados.json; si todo coincide, el contenido no cambió desde la exportación.',
      resaltar: page.getByText(/manifiesto\.json lista el SHA-256/),
    });
  },
);
