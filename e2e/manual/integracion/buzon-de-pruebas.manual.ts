import { llamar } from '../../soporte/api';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { expect, mostrarCodigo, paso, recorrido } from '../nucleo';

interface LlaveDePrueba {
  id: string;
  valor: string | null;
}

interface EstadoDelSandbox {
  empresas: { id: string; produccionId: string }[];
}

interface CorreoDePrueba {
  id: string;
  asunto: string;
  para: string;
}

interface PaginaDeCorreos {
  datos: CorreoDePrueba[];
  siguienteCursor: string | null;
}

recorrido(
  {
    dominio: 'integracion',
    actor: 'Sistema integrador',
    titulo: 'Lee y vacía el buzón de correo de pruebas',
    resumen:
      'En el sandbox, los correos que Validasoft enviaría no salen a nadie: quedan en un buzón. El sistema integrador lista ese buzón, abre un correo para ver su contenido y lo vacía cuando termina sus pruebas.',
    precondiciones: [
      'Un grupo con Constructora Norte y su flujo de proveedor publicado, con el sandbox creado por una llave de prueba.',
    ],
    relacionados: ['integracion/sandbox'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const [sistema] = await llamar<{ id: string }[]>('GET', '/api/v1/sistemas', { llave: e.admin });
    const llave = await llamar<LlaveDePrueba>('POST', '/api/v1/api-keys', {
      llave: e.admin,
      cuerpo: { nombre: 'Pruebas de correo', scopes: ['vinculos:escribir', 'sandbox:administrar'], empresas: null, expiraEn: null, sandbox: true, sistemaId: sistema?.id },
    });
    const llaveValor = llave.valor ?? '';
    const estado = await llamar<EstadoDelSandbox>('GET', '/api/v1/sandbox', { llave: llaveValor });
    const empresaDePrueba = estado.empresas.find((x) => x.produccionId === e.norte.id);
    if (!empresaDePrueba) throw new Error('El sandbox no copió Constructora Norte.');

    const tercero = nuevoTercero('Materiales del Oriente S.A.S.');
    await llamar('POST', '/api/v1/vinculos', {
      llave: llaveValor,
      empresa: empresaDePrueba.id,
      cuerpo: {
        tercero: {
          identificacion: tercero.identificacion,
          tipoPersona: 'Juridica',
          nombre: tercero.nombre,
          nombreComercial: null,
          contactos: [{ nombre: 'Contacto de prueba', correo: tercero.correo, telefono: null, rol: 'Comercial' }],
        },
        tipoVinculo: 'PROVEEDOR',
        atributos: {},
        correoInvitacion: tercero.correo,
        referenciaExterna: null,
        enviarInvitacion: true,
      },
    });

    const limite = Date.now() + 30_000;
    let correos: PaginaDeCorreos = { datos: [], siguienteCursor: null };
    while (Date.now() < limite) {
      correos = await llamar<PaginaDeCorreos>('GET', '/api/v1/sandbox/correos', { llave: llaveValor });
      if (correos.datos.length > 0) break;
      await new Promise((r) => setTimeout(r, 500));
    }
    expect(correos.datos.length).toBeGreaterThan(0);
    await mostrarCodigo(page, {
      titulo: 'Lista el buzón de pruebas',
      peticion: 'GET /api/v1/sandbox/correos',
      cabeceras: { Accept: 'application/json' },
      respuesta: { estado: 200, cuerpo: correos },
    });
    await paso(page, {
      titulo: 'Lista los correos del buzón',
      explicacion:
        'El buzón muestra cada correo que el sandbox retuvo, con su id, su asunto y su destinatario. Son los mismos correos que recibiría una persona real en producción, pero aquí nadie los recibe. ' +
        'Úsalo para comprobar que una invitación o un aviso se envían con el asunto y al destinatario correctos.',
      resaltar: page.locator('#respuesta'),
    });

    const correo = correos.datos[0];
    if (!correo) throw new Error('El buzón no tiene correos.');
    const detalle = await llamar<Record<string, unknown>>('GET', `/api/v1/sandbox/correos/${correo.id}`, { llave: llaveValor });
    await mostrarCodigo(page, {
      titulo: 'Abre un correo del buzón',
      peticion: `GET /api/v1/sandbox/correos/${correo.id}`,
      cabeceras: { Accept: 'application/json' },
      respuesta: { estado: 200, cuerpo: detalle },
    });
    await paso(page, {
      titulo: 'Abre un correo para ver su contenido',
      explicacion:
        'Al abrir un correo ves su contenido completo, con los enlaces que traería. Puedes seguir el enlace de la invitación para probar cómo lo vería el proveedor, ' +
        'sin enviarle nada. El buzón conserva el correo hasta que lo vacíes o reinicies el sandbox.',
      resaltar: page.locator('#respuesta'),
    });

    await llamar('DELETE', '/api/v1/sandbox/correos', { llave: llaveValor });
    const despues = await llamar<PaginaDeCorreos>('GET', '/api/v1/sandbox/correos', { llave: llaveValor });
    expect(despues.datos).toHaveLength(0);
    await mostrarCodigo(page, {
      titulo: 'Vacía el buzón de pruebas',
      peticion: 'DELETE /api/v1/sandbox/correos',
      cabeceras: { Accept: 'application/json' },
      respuesta: { estado: 204 },
    });
    await paso(page, {
      titulo: 'Vacía el buzón cuando termines',
      explicacion:
        'Vaciar el buzón borra los correos retenidos del sandbox. No afecta a producción ni a los vínculos: solo quita los correos. ' +
        'Si vuelves a listar el buzón, la respuesta llega vacía.',
      resaltar: page.locator('#respuesta'),
      avisos: false,
    });
  },
);
