import { ErrorDeApi, llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, mostrarCodigo, paso, recorrido } from '../nucleo';

/** Carga (payload) de una aserción JWT, decodificada para mostrarla: sin firma ni llave. */
function cargaDeLaAsercion(asercion: string): Record<string, unknown> {
  return JSON.parse(Buffer.from(asercion.split('.')[1] ?? '', 'base64url').toString('utf8')) as Record<string, unknown>;
}

recorrido(
  {
    dominio: 'integracion',
    actor: 'Sistema integrador',
    titulo: 'Entra a una persona de tu sistema con identidad delegada',
    resumen:
      'Tu sistema firma una aserción con su clave privada que dice quién es la persona, la envía con su llave de API y Validasoft devuelve una sesión de 30 minutos con los permisos de esa persona. ' +
      'Cada aserción vale una sola vez: para la siguiente petición, tu sistema firma otra.',
    precondiciones: [
      'Un grupo con Constructora Norte y el sistema integrado E2E registrado con su clave pública.',
      'Rubén, miembro del área Compras de Norte, como persona que entra desde el sistema.',
    ],
    relacionados: ['integracion/sistema-integrador', 'integracion/api-keys', 'embebido/app-de-la-empresa-en-un-iframe'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const asercion = e.delegado(e.ruben);
    const carga = cargaDeLaAsercion(asercion);

    await mostrarCodigo(page, {
      titulo: 'La aserción que firma tu sistema para Rubén',
      peticion: 'Aserción X-Usuario-Delegado (JWT RS256, carga decodificada)',
      cuerpo: carga,
    });
    const aserciones = page.locator('#peticion');
    await paso(page, {
      titulo: 'Firma quién es la persona',
      explicacion:
        'Tu sistema firma con su clave privada un JWT con los datos de la persona: «sub» (su identificador en tu sistema), «name» y «email». ' +
        'El campo «iss» es el código de tu sistema y «aud» es validasoft. La aserción vale como máximo 5 minutos, y su «jti» identifica esta firma: Validasoft lo recuerda para que no se pueda reutilizar.',
      resaltar: aserciones,
      variantes: [
        { cuando: 'La aserción vence en más de 5 minutos', entonces: 'Validasoft la rechaza con 401 «asercion-vigencia-excesiva»: firma una nueva, con vigencia corta.' },
        { cuando: 'El sistema está desactivado', entonces: 'Validasoft responde 401 «sistema-inactivo», aunque la aserción esté bien firmada.' },
      ],
    });

    const sesion = await llamar<{ token: string; expira: string }>('POST', '/api/v1/sesiones-usuario', {
      llave: e.llave,
      delegado: asercion,
    });
    await mostrarCodigo(page, {
      titulo: 'Canjea la aserción por una sesión',
      peticion: 'POST /api/v1/sesiones-usuario',
      cabeceras: { 'X-Api-Key': '<llave de API del sistema>', 'X-Usuario-Delegado': '<la aserción firmada>' },
      respuesta: { estado: 200, cuerpo: { token: '<token de sesión de Validasoft>', expira: sesion.expira } },
    });
    const canje = page.locator('#respuesta');
    await paso(page, {
      titulo: 'Canjea la aserción por una sesión',
      explicacion:
        'Tu sistema llama a POST /api/v1/sesiones-usuario con su llave de API y la aserción en la cabecera X-Usuario-Delegado. ' +
        'La respuesta trae un token de Validasoft que dura 30 minutos (campo «expira») y tiene el permiso efectivo de esa persona en la empresa. ' +
        'Ese token se envía como Authorization: Bearer; tu pantalla embebida lo usa hasta que vence.',
      resaltar: canje,
      variantes: [
        { cuando: 'La persona está desactivada en Validasoft', entonces: 'El canje responde 401: un usuario desactivado no actúa por delegación, aunque su sistema lo autentique.' },
      ],
    });

    const reutilizada = await llamar('POST', '/api/v1/sesiones-usuario', { llave: e.llave, delegado: asercion }).catch((error: unknown) => error);
    expect(reutilizada).toBeInstanceOf(ErrorDeApi);
    const motivo = reutilizada as ErrorDeApi;
    expect(motivo.codigo).toBe('asercion-reutilizada');
    await mostrarCodigo(page, {
      titulo: 'Reutilizar la misma aserción falla',
      peticion: 'POST /api/v1/sesiones-usuario (misma aserción, segunda vez)',
      cabeceras: { 'X-Api-Key': '<llave de API del sistema>', 'X-Usuario-Delegado': '<la misma aserción>' },
      respuesta: { estado: motivo.estado, cuerpo: { codigo: motivo.codigo, detalle: 'Esta aserción X-Usuario-Delegado ya se usó: firma una nueva (con otro jti) para cada petición.' } },
    });
    const rechazo = page.locator('#respuesta');
    await paso(page, {
      titulo: 'Una aserción no sirve dos veces',
      explicacion:
        'Validasoft guarda el «jti» de cada aserción aceptada hasta que vence. Si llega la misma aserción otra vez, responde 401 «asercion-reutilizada». ' +
        'Por eso tu sistema firma una aserción nueva para cada sesión o petición, aunque la persona sea la misma.',
      resaltar: rechazo,
    });

    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja');
    await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
    await paso(page, {
      titulo: 'Entra a la app como esa persona',
      explicacion:
        'La pantalla embebida pide sesión al sistema y el sistema firma una aserción nueva para Rubén y la canjea. ' +
        'Dentro de la app, Rubén ve lo que su rol le permite: aquí, la bandeja de revisión de Constructora Norte.',
      resaltar: app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' }),
    });
  },
);
