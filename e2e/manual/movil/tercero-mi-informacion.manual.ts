import { expect, paso, recorrido } from '../nucleo';
import { escenarioConFichaCompleta } from '../nucleo/semillas';
import { llenarContactoYDomicilio } from '../../soporte/ficha';
import { aceptarInvitacion, entrarPorInvitacion } from '../../soporte/proveedor';
import { invitar, nuevoTercero } from '../../soporte/semilla';

recorrido(
  {
    dominio: 'movil',
    actor: 'Tercero',
    titulo: 'Completa una sección de Mi información desde el celular',
    resumen:
      'Mi información es la ficha del tercero, dividida en secciones. En el celular, el tercero ve su avance, abre la sección General, la completa y la guarda con la barra de guardar que queda a la vista.',
    precondiciones: [
      'Un grupo cuya empresa pide una ficha de seis secciones para proveedores (Información general, Información legal, Información tributaria, Información financiera, Seguridad y salud en el trabajo y Especialidades).',
      'Un tercero jurídico invitado por esa empresa, que acepta la invitación y entra al portal.',
      'La pantalla se ve a 390 × 844 (celular).',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica', 'movil/tercero-declaracion-pep-firma'],
  },
  async ({ page }) => {
    const e = await escenarioConFichaCompleta();
    const tercero = nuevoTercero('Aceros del Pacífico S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero);
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);

    // 1. El avance de la ficha.
    await page.goto('/ficha');
    await expect(page.getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
    await paso(page, {
      titulo: 'Mira tu avance en Mi información',
      explicacion:
        'Mi información es tu ficha: está dividida en secciones y cada una dice si está completa, en borrador o pendiente. En el celular, una barra arriba muestra en qué sección estás y cuánto llevas; ' +
        'tócala para desplegar la lista de secciones y saltar a la que quieras. Si una sección no aplica a tu tipo de persona, al abrirla verás que no tienes que diligenciarla.',
      resaltar: page.getByRole('heading', { level: 1, name: 'Mi información' }),
      variantes: [
        { cuando: 'Eres persona natural', entonces: 'Las secciones que piden datos de una empresa pueden no aplicarte: al abrir una, la pantalla dice «Esta sección no aplica para ti».' },
      ],
    });

    // 2. Abrir y llenar la sección General.
    await page.goto('/ficha/general');
    await expect(page.getByRole('heading', { level: 1, name: 'Información general' })).toBeVisible();
    await llenarContactoYDomicilio(page, '601 555 1234', 'Calle 45 # 12-08');
    await paso(page, {
      titulo: 'Completa los datos de la sección',
      explicacion:
        'Cada sección tiene sus campos en el orden en que se piden. Lo que ya conoce Validasoft, como la razón social y la identificación, llega prellenado; el resto lo escribes tú: en este caso, el teléfono y el domicilio. ' +
        'Los campos obligatorios muestran el dato que falta; cuando estén listos, la barra de guardar dice que la sección está lista.',
      resaltar: page.getByRole('group', { name: 'Guardar la sección' }),
      variantes: [
        { cuando: 'Dejas un campo obligatorio vacío', entonces: 'La barra dice qué falta y no deja guardar la sección como completa; puedes guardarla como borrador.' },
      ],
    });

    // 3. Guardar.
    const barra = page.getByRole('group', { name: 'Guardar la sección' });
    await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
    await expect(page.getByText('Guardaste Información general: está completa.')).toBeVisible();
    await paso(page, {
      titulo: 'Guarda la sección',
      explicacion:
        'Con «Guardar» la sección queda guardada. Validasoft te avisa si quedó completa o como borrador; una sección completa cuenta para tu avance y la empresa puede revisarla. ' +
        'Si sales sin guardar, el portal te pregunta primero para que no pierdas lo escrito.',
      resaltar: barra,
      avisos: true,
    });
  },
);
