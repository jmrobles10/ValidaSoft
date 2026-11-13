import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja } from '../../soporte/empresa';
import { etapa } from '../../soporte/devolucion';
import { abrirExpedienteManual, crearArea, crearTipoDeVinculo, invitarVinculo, publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Sigue un expediente que pasa por tres etapas',
    resumen:
      'El flujo de un aliado tiene tres etapas en orden: Compras, Legal y Cumplimiento. Rubén aprueba la primera; el expediente pasa a la segunda y Olga lo ve en su bandeja solo cuando le toca.',
    precondiciones: [
      'Un grupo con Constructora Norte, con las áreas Compras, Legal (miembro: Olga) y Cumplimiento (también Olga; puede rechazar).',
      'Un flujo de tipo Aliado con tres etapas en secuencia: Compras, luego Legal, luego Cumplimiento. Sin documentos que subir.',
    ],
    relacionados: ['revision/aprobar-un-area', 'revision/dos-aprobadores-y-separacion-de-funciones'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    const legal = await crearArea(e, e.norte, 'legal', 'Legal', [e.olga]);
    await publicarFlujoSinDocumentos(e, e.norte, 'SECUENCIA', 'ALIADO', [
      [{ area: e.norte.areas.compras! }],
      [{ area: legal }],
      [{ area: e.norte.areas.cumplimiento!, puedeRechazar: true }],
    ]);
    const tercero = nuevoTercero('Aliados Logísticos S.A.S.');
    const invitacion = await invitarVinculo(e, e.norte, tercero, 'ALIADO');
    await abrirExpedienteManual(e, invitacion);

    // 1) Las tres etapas: solo la primera está en curso.
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(app, tercero.nombre, 'Compras');
    await expect(etapa(app, 1)).toContainText('En curso');
    await expect(etapa(app, 2)).toContainText('En espera');
    await expect(etapa(app, 3)).toContainText('En espera');
    await paso(page, {
      titulo: 'Mira las etapas del expediente',
      explicacion:
        'En «Etapas y áreas» cada etapa aparece con su estado. La etapa en curso es la que decide ahora; las siguientes están «En espera» hasta que la anterior se apruebe por completo. ' +
        'Las áreas de una misma etapa revisan al mismo tiempo, pero una etapa no empieza mientras la anterior no termina.',
      resaltar: [etapa(app, 1), etapa(app, 2), etapa(app, 3)],
      variantes: [
        { cuando: 'Todas las áreas están en la misma etapa', entonces: 'Todas revisan al mismo tiempo, y el expediente se aprueba cuando todas aprueban.' },
      ],
    });

    // 2) Compras aprueba su etapa.
    await app.getByRole('button', { name: 'Emitir veredicto' }).first().click();
    const dialogo = app.getByRole('dialog', { name: /^Veredicto de Compras/ });
    await paso(page, {
      titulo: 'Emite el veredicto de tu etapa',
      explicacion:
        'Con «Emitir veredicto» se abre la decisión de tu área. Aprobar cierra tu parte de la etapa; si observas, el expediente vuelve al tercero. ' +
        'Al aprobar, la etapa pasa a «Aprobada» y la siguiente empieza sola.',
      resaltar: dialogo,
    });
    await dialogo.getByRole('button', { name: 'Aprobar', exact: true }).click();
    await expect(dialogo).toBeHidden();

    // 3) La etapa siguiente empieza y la bandeja de Olga se llena solo entonces.
    await expect(etapa(app, 1)).toContainText('Aprobada');
    await expect(etapa(app, 2)).toContainText('En curso');
    await expect(etapa(app, 3)).toContainText('En espera');
    await paso(page, {
      titulo: 'Ve cómo avanza el expediente a la siguiente etapa',
      explicacion:
        'La etapa 1 queda «Aprobada» y la etapa 2, Legal, pasa a «En curso». La etapa 3 sigue «En espera». ' +
        'El expediente solo aparece en la bandeja de las áreas de la etapa en curso: Legal lo verá ahora y Cumplimiento lo verá cuando Legal apruebe.',
      resaltar: [etapa(app, 1), etapa(app, 2), etapa(app, 3)],
    });

    // 4) Olga, de Legal y Cumplimiento: ya ve Legal, que es la etapa en curso, pero no Cumplimiento.
    const olga = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.olga, e.norte, 'bandeja');
    await expect(olga.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
    await expect(olga.getByRole('row', { name: /Abrir expediente .* de Aliados Logísticos S\.A\.S\. \(Legal\)/ })).toBeVisible();
    await expect(olga.getByRole('row', { name: /Abrir expediente .* de Aliados Logísticos S\.A\.S\. \(Cumplimiento\)/ })).toHaveCount(0);
    await paso(page, {
      titulo: 'Revisa la bandeja de Olga con la etapa 2 en curso',
      explicacion:
        'Olga es de Legal y de Cumplimiento, pero solo le llega el expediente de Legal, que es la etapa en curso. El de Cumplimiento aparecerá cuando Legal apruebe. ' +
        'Así cada área ve el expediente solo cuando le toca decidir.',
      resaltar: olga.getByRole('table'),
    });
  },
);
