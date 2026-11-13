import { expect, paso, recorrido } from '../nucleo';
import { terceroAprobadoPorSur } from './siembra';

recorrido(
  {
    dominio: 'movil',
    actor: 'Tercero',
    titulo: 'Lee tus avisos desde la campana en el celular',
    resumen:
      'La campana de la parte de arriba junta los avisos de tus empresas: observaciones, resultados de tu validación y documentos por vencer. En el celular se abre como panel a pantalla completa; desde ahí marcas los avisos como leídos y cambias qué te llega también por correo.',
    precondiciones: [
      'Un proveedor aprobado por Constructora Sur, que deja un aviso de «Resultado de tu validación» sin leer.',
      'La pantalla se ve a 390 × 844 (celular).',
    ],
    relacionados: ['movil/tercero-navegacion', 'movil/tercero-estado-de-la-revision'],
  },
  async ({ page, browser }) => {
    await terceroAprobadoPorSur(page, browser, 'Andamios Seguros S.A.S.');
    await page.goto('/empresas');

    // 1. El contador de avisos sin leer.
    const campana = page.getByRole('button', { name: /^Notificaciones, \d+ sin leer/ });
    await expect(campana).toBeVisible();
    await paso(page, {
      titulo: 'Toca la campana para ver tus avisos',
      explicacion:
        'La campana está arriba a la derecha y muestra cuántos avisos tienes sin leer. Los avisos vienen de todas tus empresas en una sola lista: por ejemplo, cuando una empresa te pide corregir algo, cuando un documento está por vencer o cuando se decide tu validación.',
      resaltar: campana,
      variantes: [{ cuando: 'No tienes avisos', entonces: 'La campana no muestra contador y la lista dice que no tienes notificaciones.' }],
    });

    // 2. El panel de avisos.
    await campana.click();
    const panel = page.getByRole('heading', { name: 'Notificaciones', exact: true });
    await expect(panel).toBeVisible();
    await paso(page, {
      titulo: 'Lee cada aviso',
      explicacion:
        'Cada aviso dice de qué empresa es, qué pasó y cuándo. Un toque sobre el aviso lo marca como leído y, si tiene una pantalla relacionada, te lleva a ella. «Marcar todas como leídas» limpia el contador de una vez.',
      resaltar: panel,
      avisos: true,
    });

    // 3. Preferencias de correo.
    await page.getByRole('button', { name: 'Preferencias de correo' }).click();
    const preferencias = page.getByRole('dialog', { name: 'Preferencias de correo' });
    await expect(preferencias).toBeVisible();
    await paso(page, {
      titulo: 'Elige qué avisos también llegan por correo',
      explicacion:
        'En las notificaciones siempre ves todo lo que pasa con tus empresas. En «Preferencias de correo» eliges cuáles además te llegan por correo, a la dirección de tu invitación. ' +
        'Un interruptor por cada tipo de aviso; «Guardar» aplica los cambios y, si no cambiaste nada, lo dice.',
      resaltar: preferencias,
      configuracion: {
        ajuste: 'Preferencias de correo del tercero',
        ruta: 'Campana de avisos (arriba a la derecha) › Preferencias de correo',
        efecto: 'Solo cambia qué avisos llegan por correo; la campana muestra siempre todos.',
      },
    });
  },
);
