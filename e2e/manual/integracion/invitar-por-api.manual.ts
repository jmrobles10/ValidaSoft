import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { expect, mostrarCodigo, paso, recorrido } from '../nucleo';

const REFERENCIA = 'ERP-7741';

recorrido(
  {
    dominio: 'integracion',
    actor: 'Sistema integrador',
    titulo: 'Invita a un proveedor desde tu sistema con la API',
    resumen:
      'El ERP del cliente crea el vínculo con un POST a la API, con el tercero, su correo y su propia referencia. Validasoft envía la invitación y el vínculo aparece en la lista de la empresa, con esa referencia para buscarlo después.',
    precondiciones: [
      'Un grupo con Constructora Norte, su flujo de proveedor publicado y la llave del sistema integrado con permiso para crear vínculos.',
    ],
    relacionados: ['vinculos/referencias-externas', 'integracion/api-keys'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Tornillería del Sur S.A.S.');
    const cuerpo = {
      tercero: {
        identificacion: tercero.identificacion,
        tipoPersona: 'Juridica',
        nombre: tercero.nombre,
        nombreComercial: null,
        contactos: [{ nombre: 'Contacto comercial', correo: tercero.correo, telefono: null, rol: 'Comercial' }],
      },
      tipoVinculo: 'PROVEEDOR',
      atributos: {},
      correoInvitacion: tercero.correo,
      referenciaExterna: { sistema: 'E2E', id: REFERENCIA },
      enviarInvitacion: true,
    };

    const { peticion } = await mostrarCodigo(page, {
      titulo: 'La llamada del ERP para invitar a un proveedor',
      peticion: 'POST /api/v1/vinculos',
      cabeceras: { 'Content-Type': 'application/json', 'X-Empresa': e.norte.id, 'Idempotency-Key': '<uuid único por invitación>' },
      cuerpo,
    });
    await paso(page, {
      titulo: 'Envía la invitación con un POST',
      explicacion:
        'Tu sistema llama a POST /api/v1/vinculos con la llave de la API y la empresa en la cabecera X-Empresa. El cuerpo trae los datos del tercero, su tipo de vínculo, el correo al que va la invitación y tu referencia (sistema e identificador). ' +
        'La cabecera Idempotency-Key identifica la invitación: si tu sistema repite la misma llamada por un fallo de red, Validasoft no crea un segundo vínculo.',
      resaltar: peticion,
      variantes: [
        { cuando: 'Mandas «enviarInvitacion» en verdadero', entonces: 'Validasoft envía de inmediato el correo de invitación al contacto indicado.' },
        { cuando: 'Mandas «enviarInvitacion» en falso', entonces: 'El vínculo se crea sin correo: la invitación se envía después desde la pantalla.' },
      ],
    });

    await llamar('POST', '/api/v1/vinculos', { llave: e.llave, empresa: e.norte.id, cuerpo });

    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
    await expect(app.getByRole('heading', { level: 1, name: 'Vínculos' })).toBeVisible();
    const fila = app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` });
    await expect(fila).toContainText(REFERENCIA);
    await paso(page, {
      titulo: 'Encuentra el vínculo creado en la lista',
      explicacion:
        'El vínculo aparece en la lista de Constructora Norte como cualquier otro, con el tercero y su correo. La columna «Referencias externas» muestra el sistema y tu identificador, ' +
        'así que el equipo de compras ve el mismo código que tiene en el ERP.',
      resaltar: [fila, app.getByRole('columnheader', { name: 'Referencias externas' })],
    });

    await app.getByPlaceholder(/Buscar por referencia externa/).first().fill(REFERENCIA);
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` })).toBeVisible();
    await paso(page, {
      titulo: 'Busca el vínculo por tu referencia',
      explicacion:
        'El buscador acepta la referencia que pusiste al crear el vínculo: escribe el identificador de tu sistema y la lista se reduce al vínculo que corresponde. ' +
        'Así tu sistema y la persona que revisa hablan del mismo proveedor sin copiar códigos a mano.',
      resaltar: app.getByPlaceholder(/Buscar por referencia externa/).first(),
    });
  },
);
