import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, invitar, nuevoTercero, type Empresa, type Escenario, type Tercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

/** Vínculo creado por el sistema integrado con su propia referencia (siembra por API). */
async function vincularConReferencia(e: Escenario, empresa: Empresa, tercero: Tercero, id: string): Promise<void> {
  await llamar('POST', '/api/v1/vinculos', {
    llave: e.llave,
    empresa: empresa.id,
    cuerpo: {
      tercero: {
        identificacion: tercero.identificacion,
        tipoPersona: tercero.tipoPersona ?? 'Juridica',
        nombre: tercero.nombre,
        nombreComercial: null,
        contactos: [{ nombre: 'Contacto comercial', correo: tercero.correo, telefono: null, rol: 'Comercial' }],
      },
      tipoVinculo: 'PROVEEDOR',
      atributos: {},
      correoInvitacion: tercero.correo,
      referenciaExterna: { sistema: 'E2E', id },
      enviarInvitacion: true,
    },
  });
}

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Sistema integrador',
    titulo: 'Localiza un vínculo por la referencia de un sistema externo',
    resumen:
      'Un sistema integrado crea vínculos con su propio identificador, como el código de proveedor de su sistema. En Validasoft, la lista muestra esas referencias, el buscador abre un vínculo escribiendo el sistema y su referencia, y el detalle las muestra con su sistema.',
    precondiciones: [
      'Un grupo con Constructora Norte y una administradora que consulta los vínculos.',
      'Dos vínculos de proveedor: uno creado por el sistema integrado con la referencia E2E · PRV-204, y otro creado a mano sin referencia.',
    ],
    relacionados: ['vinculos/lista-y-filtros', 'vinculos/detalle-del-vinculo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const conReferencia = nuevoTercero('Tornillería del Valle S.A.S.');
    const otro = nuevoTercero('Aceros Integrados S.A.S.');
    await vincularConReferencia(e, e.norte, conReferencia, 'PRV-204');
    await invitar(e, e.norte, otro, 'PROVEEDOR');

    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
    await expect(app.getByRole('heading', { level: 1, name: 'Vínculos' })).toBeVisible();
    const fila = app.getByRole('row', { name: `Abrir vínculo de ${conReferencia.correo}` });
    await expect(fila).toContainText('PRV-204');
    await paso(page, {
      titulo: 'Mira las referencias externas en la lista',
      explicacion:
        'La columna «Referencias externas» muestra, por cada vínculo, el sistema y el identificador que ese sistema le asignó al crearlo. ' +
        'Un vínculo que nadie creó desde un sistema integrado no tiene referencia y la celda dice «Sin referencias».',
      resaltar: [app.getByRole('columnheader', { name: 'Referencias externas' }), fila],
    });

    const buscador = app.getByPlaceholder(/Buscar por referencia externa/).first();
    await buscador.fill('E2E · PRV-204');
    await paso(page, {
      titulo: 'Escribe la referencia con su sistema',
      explicacion:
        'En el buscador escribe el sistema y la referencia separados por «·», por ejemplo «E2E · PRV-204». Al confirmar con Enter, se abre directamente el vínculo que tiene esa referencia. ' +
        'Si escribes solo la referencia, sin el sistema, el buscador te pide el formato correcto.',
      resaltar: buscador,
    });

    await buscador.press('Enter');
    await expect(app.getByRole('heading', { level: 1, name: conReferencia.nombre })).toBeVisible();
    await expect(app.locator('section', { hasText: 'Referencias externas' }).first()).toContainText('PRV-204');
    await paso(page, {
      titulo: 'Revisa las referencias en el detalle',
      explicacion:
        'En el detalle, «Referencias externas» muestra el sistema que creó el vínculo y su referencia. Así sabes con qué código aparece el mismo proveedor en ese sistema.',
      resaltar: app.locator('section', { hasText: 'Referencias externas' }).first(),
    });
  },
);
