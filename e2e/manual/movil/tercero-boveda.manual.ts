import { expect, paso, recorrido } from '../nucleo';
import { pdf } from '../../soporte/archivos';
import { subirDocumento } from '../../soporte/proveedor';
import { RUT, terceroEnRevision } from './siembra';

recorrido(
  {
    dominio: 'movil',
    actor: 'Tercero',
    titulo: 'Revisa tus documentos y sus versiones en el celular',
    resumen:
      'Mi bóveda guarda cada documento una sola vez. En el celular, el tercero ve la lista de sus documentos y abre las versiones anteriores de un documento que ya subió dos veces.',
    precondiciones: [
      'Un tercero invitado por Constructora Norte que entregó el RUT, lo volvió a subir como versión 2 y entregó su certificado de existencia y representación legal.',
      'La pantalla se ve a 390 × 844 (celular).',
    ],
    relacionados: ['movil/tercero-subir-documento'],
  },
  async ({ page }) => {
    await terceroEnRevision(page, 'Aceros del Sur S.A.S.');
    await subirDocumento(page, RUT, pdf('RUT Aceros del Sur v2'), { version: 2 });

    // 1. La lista de documentos.
    await page.goto('/boveda');
    await expect(page.getByRole('heading', { level: 1, name: 'Mi bóveda' })).toBeVisible();
    const tarjetaRut = page.getByRole('article').filter({ has: page.getByRole('heading', { name: RUT, exact: true }) });
    await expect(tarjetaRut).toBeVisible();
    await paso(page, {
      titulo: 'Abre tu bóveda',
      explicacion:
        'Mi bóveda lista tus documentos una sola vez, aunque los uses con varias empresas. Cada tarjeta muestra la versión vigente y cuándo se cargó. ' +
        'Desde la tarjeta puedes subir una versión nueva o ver las anteriores.',
      resaltar: page.getByRole('heading', { level: 1, name: 'Mi bóveda' }),
      variantes: [
        { cuando: 'Aún no tienes documentos', entonces: 'La bóveda dice que aún no tienes documentos y te invita a subir el RUT o tu Cámara de comercio.' },
      ],
    });

    // 2. Las versiones.
    await tarjetaRut.getByRole('button', { name: /^Ver versiones \(\d+\)$/ }).click();
    await expect(tarjetaRut.getByText(/Versión 1/)).toBeVisible();
    await paso(page, {
      titulo: 'Mira las versiones anteriores de un documento',
      explicacion:
        'Con «Ver versiones» se despliega cada versión que subiste de ese documento, con su fecha de carga. La vigente es la que usan las empresas; debajo, en «Con tus empresas», ves qué versión revisa cada una.',
      resaltar: tarjetaRut,
    });
  },
);
