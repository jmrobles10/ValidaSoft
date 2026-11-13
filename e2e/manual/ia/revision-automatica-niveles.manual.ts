import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { activarIa, crearEscenario, publicarFlujoConFicha } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'ia',
    actor: 'Administrador de empresa',
    titulo: 'Elige el nivel de revisión automática con IA',
    resumen:
      'La administradora decide cuánto hace la IA en cada documento: Manual (nada), Asistido (la IA lee y una persona decide) o Automático (el sistema aprueba lo limpio). Elige los tipos de documento y las áreas que alcanza, y el porcentaje de muestra que una persona confirma.',
    precondiciones: [
      'Un grupo con Constructora Sur, la IA activa para la empresa y un flujo de Cliente que pide el RUT al área Revisión.',
    ],
    relacionados: ['ia/revision-automatica-aprueba-sola', 'ia/revision-asistida-y-excepciones'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    await activarIa(e, e.sur);
    await publicarFlujoConFicha(e, e.sur, 'CLIENTES_NIVELES', e.sur.areas.revision!, [{ tipoDocumento: 'RUT' }]);

    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'revision-automatica');
    await expect(ana.getByRole('heading', { level: 1, name: 'Revisión automática' })).toBeVisible();
    await expect(ana.getByRole('radio', { name: /^Manual/ })).toBeChecked();

    await paso(paginaAna, {
      titulo: 'Abre la revisión automática',
      explicacion:
        'Sin configuración guardada, la empresa está en Manual. Manual: la IA analiza y solo informa; cada área revisa todo, como hasta ahora. Asistido: la IA sugiere aprobar o revisar cada documento, pero nunca aprueba: decide siempre una persona. Automático: la IA aprueba sola lo que no tiene excepciones, una persona confirma una muestra y atiende el resto.',
      resaltar: ana.getByRole('radiogroup').first(),
      configuracion: {
        ajuste: 'Nivel de revisión automática',
        ruta: 'Configuración › Revisión automática (/revision-automatica)',
        efecto: 'Define si la IA participa en la revisión de los documentos de la empresa y qué puede decidir sola.',
      },
    });

    await ana.getByRole('radio', { name: /^Automático/ }).check();
    await expect(ana.getByText('En Automático el sistema aprobará documentos sin que una persona los vea, salvo el muestreo.')).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Elige Automático y lo que alcanza',
      explicacion:
        'En Automático el sistema aprueba los documentos limpios sin que una persona los vea, salvo el muestreo. Solo lo que elijas: «Tipos de documento» y «Áreas cuya revisión hace el sistema». Un documento dudoso nunca se aprueba solo: pasa a Excepciones para una persona. Sin tipos ni áreas no se puede guardar.',
      resaltar: [ana.getByRole('combobox', { name: 'Tipos de documento' }), ana.getByRole('combobox', { name: 'Áreas cuya revisión hace el sistema' })],
    });

    await ana.getByRole('combobox', { name: 'Tipos de documento' }).click();
    await ana.getByRole('option', { name: RUT }).click();
    await paginaAna.keyboard.press('Escape');
    await ana.getByRole('combobox', { name: 'Áreas cuya revisión hace el sistema' }).click();
    await ana.getByRole('option', { name: 'Revisión' }).click();
    await paginaAna.keyboard.press('Escape');
    await ana.getByRole('textbox', { name: 'Muestra para confirmar (%)' }).fill('0');
    await paso(paginaAna, {
      titulo: 'Define la muestra para confirmar',
      explicacion:
        'El porcentaje de muestra es la parte de los documentos aprobados solos que una persona confirma después, en Excepciones. Con 0 %, todo lo limpio se aprueba sin revisión humana; con 100 %, todo lo aprobado por el sistema queda para confirmar.',
      resaltar: ana.getByRole('textbox', { name: 'Muestra para confirmar (%)' }),
      variantes: [{ cuando: 'La muestra es mayor que 0', entonces: 'Algunos documentos limpios aparecen en Excepciones como «Muestreo» para que una persona los confirme.' }],
    });

    await ana.getByRole('button', { name: 'Guardar', exact: true }).click();
    const aviso = ana.getByRole('dialog', { name: 'Activar la aprobación automática' });
    await expect(aviso).toContainText('1 tipo de documento en 1 área');
    await paso(paginaAna, {
      titulo: 'Confirma antes de activar',
      explicacion:
        'Antes de guardar, el aviso dice cuántos tipos de documento y cuántas áreas quedan bajo aprobación automática. Solo al confirmar se guarda el nivel Automático.',
      resaltar: aviso,
      completa: false,
    });
    await aviso.getByRole('button', { name: 'Guardar y activar' }).click();
    await expect(ana.getByText('Guardaste la revisión automática: nivel Automático.')).toBeVisible();
    const guardada = await llamar<{ nivel: string }>('GET', '/api/v1/configuracion/revision-automatica', { llave: e.admin, empresa: e.sur.id });
    expect(guardada.nivel).toBe('Automatico');
  },
);
