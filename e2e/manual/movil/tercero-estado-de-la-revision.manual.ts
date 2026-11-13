import { expect, paso, recorrido } from '../nucleo';
import { abrirEmpresa, fraseDeEstado, tarjeta } from '../../soporte/proveedor';
import { RUT, terceroConDosEmpresas } from './siembra';

recorrido(
  {
    dominio: 'movil',
    actor: 'Tercero',
    titulo: 'Revisa en el celular en qué va tu validación con cada empresa',
    resumen:
      'En el celular, Mis empresas muestra el estado de cada validación. El tercero abre una de ellas y ve, en una frase, en qué va la revisión y, en tarjetas, cada documento con su estado.',
    precondiciones: [
      'Un tercero invitado por Constructora Norte y por Constructora Sur, que entregó sus documentos y quedó «En revisión» en ambas.',
      'La pantalla se ve a 390 × 844 (celular).',
    ],
    relacionados: ['movil/tercero-subir-documento', 'movil/tercero-campana'],
  },
  async ({ page }) => {
    await terceroConDosEmpresas(page, 'Aceros del Pacífico S.A.S.');

    // 1. El estado en Mis empresas.
    await page.goto('/empresas');
    const norte = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: 'Constructora Norte S.A.S.' }) });
    await expect(norte).toContainText('En revisión');
    await paso(page, {
      titulo: 'Mira el estado de cada empresa',
      explicacion:
        'Cada tarjeta de Mis empresas dice en qué va tu validación con esa empresa: «En revisión», «Observado», «Aprobado» u otro estado. Un toque sobre la tarjeta abre su revisión.',
      resaltar: norte,
      variantes: [
        { cuando: 'La empresa te pidió corregir algo', entonces: 'La tarjeta cambia a «Observado» y te lleva a corregirlo al abrirla.' },
        { cuando: 'Tu validación está aprobada', entonces: 'La tarjeta dice «Aprobado» y tu vínculo queda activo.' },
      ],
    });

    // 2. La frase de estado.
    await abrirEmpresa(page, 'Constructora Norte S.A.S.');
    await expect(page.getByRole('heading', { level: 1, name: 'Tu revisión con Constructora Norte S.A.S.' })).toBeVisible();
    await expect(fraseDeEstado(page, 'EnRevision')).toBeVisible();
    await paso(page, {
      titulo: 'Lee la frase de estado',
      explicacion:
        'Arriba de la revisión, una frase dice en qué va: «Constructora Norte S.A.S. está revisando tu información». Debajo aparece el folio de tu proceso y la fecha de la última actualización.',
      resaltar: fraseDeEstado(page, 'EnRevision'),
      variantes: [
        { cuando: 'Te falta subir documentos', entonces: 'La frase dice cuántos documentos faltan y te invita a subirlos.' },
      ],
    });

    // 3. Las tarjetas de documentos.
    await expect(tarjeta(page, RUT)).toBeVisible();
    await paso(page, {
      titulo: 'Revisa cada documento',
      explicacion:
        'Cada documento que te pide la empresa es una tarjeta con su estado: «Cargado», «Vigente» o «Pendiente». Si una fecha de vencimiento se acerca, la tarjeta lo dice con «Vence el» y la fecha.',
      resaltar: tarjeta(page, RUT),
    });
  },
);
