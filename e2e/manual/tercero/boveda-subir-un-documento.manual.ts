import { pdf } from '../../soporte/archivos';
import { abrirEmpresa, aceptarInvitacion, completarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Sube un documento a tu bóveda',
    resumen:
      'Desde «Subir documento» llevas a tu bóveda un documento que quizá una empresa te pida después. Eliges el tipo, subes el archivo y, si ninguna empresa lo exige todavía, queda guardado y listo para cuando lo pidan.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.», su flujo de Proveedor (RUT y certificado de seguridad y salud en el trabajo).',
      'Un tercero que ya completó su invitación de Sur, sin invitaciones pendientes, y que no tiene en su bóveda un certificado de existencia y representación legal.',
    ],
    relacionados: ['tercero/boveda-mis-documentos', 'tercero/boveda-documento-compartido'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería La Esquina S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');
    await completarInvitacion(page, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${tercero.identificacion.numero}`) },
      { nombre: 'Certificado de seguridad y salud en el trabajo', archivo: pdf(`SST ${tercero.identificacion.numero}`), campos: { 'Fecha de expedición': new Date(Date.now() - 3 * 86_400_000).toISOString().slice(0, 10) } },
    ]);
    await esperarEstadoDelTercero(page, 'EnRevision');

    // 1. Elegir el tipo de documento.
    await page.goto('/boveda/subir');
    await expect(page.getByRole('heading', { level: 1, name: 'Subir documento' })).toBeVisible();
    const tipo = page.getByRole('combobox', { name: 'Tipo de documento' });
    await tipo.click();
    await page.getByRole('option', { name: 'Cámara de comercio', exact: true }).click();
    await expect(page.getByText('Certificado de existencia y representación legal expedido por tu Cámara de comercio.')).toBeVisible();
    await paso(page, {
      titulo: 'Elige el tipo de documento',
      explicacion:
        'En «Tipo de documento» eliges qué vas a subir. Solo aparecen los tipos que Validasoft acepta en la bóveda. Al elegirlo, debajo ves qué debe contener el archivo. ' +
        'Si una empresa te pide ese documento más adelante, se lo compartimos sin que lo vuelvas a cargar.',
      resaltar: tipo,
    });

    // 2. Subir el archivo.
    const selector = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf(`Camara ${tercero.identificacion.numero}`));
    await expect(page.getByText('Listo. Es tu versión 1 de Cámara de comercio.')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByText('Por ahora ninguna empresa la exige; queda en tu bóveda.')).toBeVisible();
    await paso(page, {
      titulo: 'Sube el archivo y revisa el resultado',
      explicacion:
        'Elige el archivo o arrástralo a la zona. Validasoft lo guarda como la versión 1 del documento, y el resultado te dice si alguna empresa lo está pidiendo. ' +
        'Aquí ninguna empresa la exige, así que queda en tu bóveda para cuando la pidan. Desde «Ver mi bóveda» la encuentras con su fecha de carga.',
      resaltar: page.getByText('Listo. Es tu versión 1 de Cámara de comercio.'),
      variantes: [
        { cuando: 'Una empresa ya te pide ese documento', entonces: 'El resultado dice «Ya la tiene» seguido del nombre de la empresa, y la empresa empieza a revisarla.' },
        { cuando: 'Subes el mismo archivo que ya tienes', entonces: 'Validasoft lo detecta y no lo vuelve a guardar: indica que es idéntico a tu versión vigente.' },
      ],
    });
  },
);
