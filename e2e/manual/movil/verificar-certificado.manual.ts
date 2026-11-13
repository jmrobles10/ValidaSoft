import { expect, paso, recorrido } from '../nucleo';
import { abrirEmbebido } from '../../soporte/embebido';
import { terceroAprobadoPorSur } from './siembra';

recorrido(
  {
    dominio: 'movil',
    actor: 'Público',
    titulo: 'Verifica un certificado desde el celular',
    resumen:
      'Quien escanea el código QR de un certificado con el celular llega a la página de verificación: ve el estado, comprueba la autenticidad en su propio teléfono y descarga el PDF, sin iniciar sesión.',
    precondiciones: [
      'Un proveedor aprobado por Constructora Sur con su certificado vigente.',
      'Quien verifica escanea el enlace del código QR desde un celular, sin sesión.',
    ],
    relacionados: ['publico/verificar-certificado-autentico', 'publico/certificado-en-otros-estados'],
  },
  async ({ page, browser }) => {
    const { e, invitacion } = await terceroAprobadoPorSur(page, browser, 'Andamios Seguros S.A.S.');
    const ana = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
    await expect(ana.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    const enlace = await ana.getByRole('link', { name: 'Página de verificación' }).getAttribute('href');
    const publica = await (await browser.newContext()).newPage();
    const url = new URL(enlace ?? '');
    await publica.goto(`${url.pathname}${url.search}`);

    // 1. El estado en la pantalla del celular.
    await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' })).toBeVisible();
    await paso(publica, {
      titulo: 'Lee el estado del certificado',
      explicacion:
        'Al escanear el QR se abre la verificación con el estado en grande: «Este certificado está vigente» y la empresa que validó al proveedor. Debajo, los datos del certificado en una columna: titular, folio, fecha de emisión y hasta cuándo vale.',
      resaltar: publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' }),
      celular: true,
      variantes: [
        { cuando: 'El certificado está suspendido o revocado', entonces: 'El título cambia al estado correspondiente y el PDF no se entrega.' },
      ],
    });

    // 2. La autenticidad.
    await expect(publica.getByRole('status').filter({ hasText: 'Auténtico: el contenido está íntegro y lo selló Validasoft.' })).toBeVisible();
    await paso(publica, {
      titulo: 'Comprueba la autenticidad en tu teléfono',
      explicacion:
        'La sección «Autenticidad» la calcula tu propio teléfono con las cuatro comprobaciones: contenido íntegro, huella del enlace, clave de la plataforma y sello válido. Si todas dicen «Correcto», el mensaje dice «Auténtico».',
      resaltar: publica.getByRole('status').filter({ hasText: 'Auténtico' }),
      celular: true,
    });

    // 3. El PDF.
    await expect(publica.getByRole('button', { name: 'Descargar PDF' })).toBeVisible();
    await paso(publica, {
      titulo: 'Descarga el PDF del certificado',
      explicacion:
        'El botón «Descargar PDF» baja el certificado para guardarlo o compartirlo. Mientras el certificado esté vigente, el PDF se entrega; si no, la página lo explica en lugar de bajarlo.',
      resaltar: publica.getByRole('button', { name: 'Descargar PDF' }),
      celular: true,
    });
  },
);
