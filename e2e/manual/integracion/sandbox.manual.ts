import { llamar } from '../../soporte/api';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { expect, mostrarCodigo, paso, recorrido } from '../nucleo';

/** Lo que necesita el sistema integrador para probar su integración (la llave de prueba lleva los permisos que llevaría en producción). */
const PERMISOS = ['configuracion:leer', 'vinculos:leer', 'vinculos:escribir', 'certificados:leer', 'consultas:leer', 'eventos:leer', 'webhooks:administrar', 'sandbox:administrar'];

interface LlaveDePrueba {
  id: string;
  nombre: string;
  sandbox: boolean;
  valor: string | null;
}

interface EstadoDelSandbox {
  existe: boolean;
  entorno: string;
  tenantId: string;
  sistemas: string[];
  empresas: { id: string; produccionId: string; nombre: string; prefijoFolio: string; activa: boolean }[];
  correos: number;
}

/** El correo de la invitación llega al buzón de forma asíncrona: espera a que aparezca (hasta 30 s). */
interface PaginaDeCorreos {
  datos: { id: string; asunto: string; para: string }[];
  siguienteCursor: string | null;
}

async function esperarCorreosDelBuzon(llave: string): Promise<PaginaDeCorreos> {
  const limite = Date.now() + 30_000;
  let pagina: PaginaDeCorreos = { datos: [], siguienteCursor: null };
  while (Date.now() < limite) {
    pagina = await llamar<PaginaDeCorreos>('GET', '/api/v1/sandbox/correos', { llave });
    if (pagina.datos.length > 0) break;
    await new Promise((r) => setTimeout(r, 500));
  }
  return pagina;
}

recorrido(
  {
    dominio: 'integracion',
    actor: 'Sistema integrador',
    titulo: 'Prueba la integración en el sandbox antes de producción',
    resumen:
      'El sistema integrador emite una llave de prueba, que crea el sandbox del grupo: una copia de sus empresas aislada de los datos reales. Ahí invita a un proveedor sin que salga ningún correo real, revisa el buzón de pruebas y, al final, reinicia el sandbox para empezar de cero.',
    precondiciones: [
      'Un grupo con Constructora Norte, su flujo de proveedor publicado y el sistema integrado E2E registrado.',
      'Una administradora que puede emitir llaves de la API.',
    ],
    relacionados: ['integracion/buzon-de-pruebas', 'integracion/api-keys', 'integracion/webhooks'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const [sistema] = await llamar<{ id: string }[]>('GET', '/api/v1/sistemas', { llave: e.admin });

    const cuerpoLlave = { nombre: 'Integración de prueba', scopes: PERMISOS, empresas: null, expiraEn: null, sandbox: true, sistemaId: sistema?.id };
    const llave = await llamar<LlaveDePrueba>('POST', '/api/v1/api-keys', { llave: e.admin, cuerpo: cuerpoLlave });
    expect(llave.valor).toMatch(/^vsk_test_/);
    await mostrarCodigo(page, {
      titulo: 'Emite la llave de prueba del sistema',
      peticion: 'POST /api/v1/api-keys',
      cabeceras: { 'Content-Type': 'application/json' },
      cuerpo: { ...cuerpoLlave, scopes: [`${PERMISOS.length} permisos (los mismos de producción)`] },
      respuesta: { estado: 201, cuerpo: { id: llave.id, nombre: llave.nombre, sandbox: true, valor: 'vsk_test_••••••••' } },
    });
    await paso(page, {
      titulo: 'Emite una llave de prueba',
      explicacion:
        'Una llave marcada como «sandbox» es la que usa tu sistema para probar. Su clave empieza por vsk_test_, así que se distingue a simple vista de una real. ' +
        'Al emitirla se crea el sandbox del grupo: una copia de cada empresa con datos de prueba, aislada de producción. Lleva los mismos permisos que llevaría la llave real, para que la prueba sea fiel.',
      resaltar: page.locator('#respuesta'),
      configuracion: {
        ajuste: 'Llave de sandbox (pruebas)',
        ruta: 'Configuración › API keys (/api-keys)',
        efecto: 'Opera en un entorno aislado: sin datos reales y con los correos retenidos en un buzón.',
      },
    });

    const estado = await llamar<EstadoDelSandbox>('GET', '/api/v1/sandbox', { llave: llave.valor ?? '' });
    expect(estado).toMatchObject({ existe: true, entorno: 'sandbox' });
    const empresaDePrueba = estado.empresas.find((x) => x.produccionId === e.norte.id);
    if (!empresaDePrueba) throw new Error('El sandbox no copió Constructora Norte.');
    await mostrarCodigo(page, {
      titulo: 'Consulta el estado del sandbox',
      peticion: 'GET /api/v1/sandbox',
      cabeceras: { Accept: 'application/json' },
      respuesta: { estado: 200, cuerpo: estado },
    });
    await paso(page, {
      titulo: 'Revisa qué copió el sandbox',
      explicacion:
        'La respuesta dice si el sandbox existe, su entorno («sandbox», distinto de «produccion»), los sistemas que lo usan y las empresas copiadas. ' +
        'Cada empresa trae el id de su copia de pruebas (ese id es el que usas en X-Empresa), el id de producción del que sale y su prefijo de folio, que empieza por T para distinguirla.',
      resaltar: page.locator('#respuesta'),
      variantes: [{ cuando: 'Consultas el mismo estado con una llave de producción', entonces: 'Responde que el entorno es «produccion» y no ve el sandbox.' }],
    });

    const tercero = nuevoTercero('Ferreterías del Sur S.A.S.');
    const cuerpoVinculo = {
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
      referenciaExterna: { sistema: 'E2E', id: 'PRUEBA-001' },
      enviarInvitacion: true,
    };
    await llamar('POST', '/api/v1/vinculos', { llave: llave.valor ?? '', empresa: empresaDePrueba.id, cuerpo: cuerpoVinculo });
    await mostrarCodigo(page, {
      titulo: 'Invita a un proveedor en el sandbox',
      peticion: 'POST /api/v1/vinculos',
      cabeceras: { 'Content-Type': 'application/json', 'X-Empresa': empresaDePrueba.id },
      cuerpo: cuerpoVinculo,
    });
    await paso(page, {
      titulo: 'Invita a un proveedor de prueba',
      explicacion:
        'La invitación se crea igual que en producción, con la misma API y la empresa de prueba en X-Empresa. La diferencia es el destino: el correo no sale a una persona real, queda guardado en el buzón de pruebas del sandbox, ' +
        'donde tu equipo puede abrir el enlace y completar la invitación como lo haría el proveedor.',
      resaltar: page.locator('#peticion'),
      celular: false,
    });

    const correos = await esperarCorreosDelBuzon(llave.valor ?? '');
    expect(correos.datos.length).toBeGreaterThan(0);
    await mostrarCodigo(page, {
      titulo: 'Lee el buzón de pruebas',
      peticion: 'GET /api/v1/sandbox/correos',
      cabeceras: { Accept: 'application/json' },
      respuesta: { estado: 200, cuerpo: correos },
    });
    await paso(page, {
      titulo: 'Revisa el buzón de pruebas',
      explicacion:
        'Cada correo que el sandbox habría enviado queda en el buzón, con su asunto y su destinatario. Así compruebas el correo de invitación sin que nadie reciba nada. ' +
        'Desde la API puedes leer cada correo por su id y vaciar el buzón cuando lo necesites.',
      resaltar: page.locator('#respuesta'),
    });

    const reinicio = await llamar<EstadoDelSandbox>('POST', '/api/v1/sandbox/reiniciar', { llave: llave.valor ?? '' });
    expect(reinicio.correos).toBe(0);
    await mostrarCodigo(page, {
      titulo: 'Reinicia el sandbox',
      peticion: 'POST /api/v1/sandbox/reiniciar',
      cabeceras: { Accept: 'application/json' },
      respuesta: { estado: 200, cuerpo: reinicio },
    });
    await paso(page, {
      titulo: 'Reinicia el sandbox para empezar de cero',
      explicacion:
        'Reiniciar borra los vínculos, los correos y los datos de prueba, pero conserva las mismas empresas y la misma llave. Producción no se entera: ' +
        'ni el vínculo, ni el certificado ni el correo existen allí. Úsalo cuando quieras repetir la prueba desde el principio.',
      resaltar: page.locator('#respuesta'),
    });
  },
);
