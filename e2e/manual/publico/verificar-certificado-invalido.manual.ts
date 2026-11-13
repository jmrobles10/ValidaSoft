import { expect, paso, recorrido } from '../nucleo';
import { crearEscenario } from '../../soporte/semilla';

recorrido(
  {
    dominio: 'publico',
    actor: 'Público',
    titulo: 'Comprueba un certificado que no existe o tiene una huella mal escrita',
    resumen:
      'Si el folio o la huella no corresponden a ningún certificado de Validasoft, la página no lo da por bueno: explica qué revisar en el PDF o en el código QR y no muestra datos de ninguna empresa ni de ningún titular.',
    precondiciones: [
      'Un grupo empresarial registrado en Validasoft, cuyo prefijo de folio se usa para armar un folio que no existe.',
      'La consulta se hace sin sesión, desde una ventana nueva del navegador.',
    ],
    relacionados: ['publico/verificar-certificado-autentico'],
  },
  async ({ page }) => {
    const e = await crearEscenario();

    // 1. Un folio que no existe.
    await page.goto(`/verificar/${e.sur.prefijo}-C-2026-999999-1`);
    await expect(page.getByRole('heading', { level: 1, name: 'No encontramos este certificado' })).toBeVisible();
    await paso(page, {
      titulo: 'Consulta un folio que no existe',
      explicacion:
        'Cuando el folio no corresponde a ningún certificado, la página dice «No encontramos este certificado» y te pide revisar que el folio y la huella estén completos, como aparecen en el PDF. ' +
        'Si escaneaste el código QR, puede que el documento no sea un certificado de Validasoft. La página no muestra datos de ninguna empresa ni de ningún titular.',
      resaltar: page.getByRole('heading', { level: 1, name: 'No encontramos este certificado' }),
      celular: false,
    });

    // 2. Una huella mal escrita.
    await page.goto(`/verificar/${e.sur.prefijo}-C-2026-999999-1?huella=xyz`);
    await expect(page.getByRole('heading', { level: 1, name: 'La huella no es válida' })).toBeVisible();
    await paso(page, {
      titulo: 'Consulta con una huella mal escrita',
      explicacion:
        'La huella tiene 12 caracteres hexadecimales (0 a 9 y de la A a la F), por ejemplo 5a7e·0c1d·9f22. Si la escribes con otros caracteres, la página dice «La huella no es válida» y muestra un ejemplo. ' +
        'Revisa el enlace o vuelve a escanear el código QR del PDF; no hace falta iniciar sesión para intentarlo de nuevo.',
      resaltar: page.getByRole('heading', { level: 1, name: 'La huella no es válida' }),
      celular: false,
      variantes: [
        { cuando: 'Hay más de un certificado con el mismo folio y no traes la huella', entonces: 'La página dice «Falta la huella» y pide abrir el enlace completo del código QR, que la incluye.' },
      ],
    });
  },
);
