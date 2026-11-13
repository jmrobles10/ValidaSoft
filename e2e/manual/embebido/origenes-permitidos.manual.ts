import { API, ErrorDeApi, llamar } from '../../soporte/api';
import { enlace, esperarCorreo, literal } from '../../soporte/correo';
import { crearEscenario, invitar, nuevoTercero, ORIGEN_SISTEMA, type Escenario, type Invitacion } from '../../soporte/semilla';
import { expect, mostrarCodigo, paso, recorrido } from '../nucleo';

const ORIGEN_OTRO_DEL_SISTEMA = 'http://localhost:5198';
const ORIGEN_AJENO = 'http://localhost:5197';

/** El tercero acepta su invitación por el enlace (sin pantalla): sin ella no hay vínculo que embeber. */
async function aceptarPorApi(invitacion: Invitacion): Promise<void> {
  const correo = await esperarCorreo(invitacion.correo, new RegExp(`^${literal(invitacion.empresa.nombre)} te invita a validar tu vínculo como cliente$`));
  const token = new URL(enlace(correo, '/e/')).pathname.split('/').pop() ?? '';
  const canje = await fetch(`${API}/public/v1/enlaces/${token}/canjear`, { method: 'POST' });
  expect(canje.status).toBe(200);
  const { accessToken } = (await canje.json()) as { accessToken: string };
  const cabeceras = { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' };
  const ver = (await (await fetch(`${API}/public/v1/invitaciones/${invitacion.vinculoId}`, { headers: cabeceras })).json()) as {
    textosPorAceptar: { tipo: string; textoVersion: number }[];
  };
  const aceptada = await fetch(`${API}/public/v1/invitaciones/${invitacion.vinculoId}/aceptar`, {
    method: 'POST',
    headers: cabeceras,
    body: JSON.stringify({ consentimientos: ver.textosPorAceptar.map((t) => ({ tipo: t.tipo, textoVersion: t.textoVersion, aceptado: true })) }),
  });
  expect(aceptada.status).toBe(200);
}

async function idDelSistema(e: Escenario): Promise<string> {
  const sistemas = await llamar<{ id: string; codigo: string }[]>('GET', '/api/v1/sistemas', { llave: e.admin });
  const sistema = sistemas.find((s) => s.codigo === 'E2E');
  if (!sistema) throw new Error('El sistema E2E no existe.');
  return sistema.id;
}

recorrido(
  {
    dominio: 'embebido',
    actor: 'Sistema integrador',
    titulo: 'Declara qué páginas pueden mostrar Validasoft',
    resumen:
      'Tu sistema declara los orígenes (dirección y puerto) desde los que se muestran sus pantallas de Validasoft. Solo esos orígenes pueden embeber la app y pedir sesiones para ella: ' +
      'un origen que no declaraste recibe un rechazo y su página no muestra nada del tercero.',
    precondiciones: [
      'Un grupo con Constructora Norte, un tercero que aceptó su invitación de cliente y el sistema integrado E2E con la llave de administración.',
    ],
    relacionados: ['integracion/sistema-integrador', 'embebido/tercero-embebido', 'embebido/app-de-la-empresa-en-un-iframe'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const sistema = await idDelSistema(e);
    const invitacion = await invitar(e, e.norte, nuevoTercero('Tuberías Embebidas S.A.S.'), 'CLIENTE');
    await aceptarPorApi(invitacion);

    const origenes = [ORIGEN_SISTEMA, ORIGEN_OTRO_DEL_SISTEMA];
    const guardados = await llamar<unknown>('PUT', `/api/v1/sistemas/${sistema}/origenes`, { llave: e.admin, cuerpo: { origenes } });
    await mostrarCodigo(page, {
      titulo: 'Declara los orígenes de tu sistema',
      peticion: `PUT /api/v1/sistemas/${sistema}/origenes`,
      cabeceras: { 'Content-Type': 'application/json', 'X-Api-Key': '<llave de administración>' },
      cuerpo: { origenes },
      respuesta: { estado: 200, cuerpo: guardados },
    });
    await paso(page, {
      titulo: 'Declara los orígenes permitidos',
      explicacion:
        'Con PUT /api/v1/sistemas/{id}/origenes reemplazas la lista de orígenes de tu sistema. Cada origen es esquema, dominio y puerto, como «https://portal.tuempresa.co», sin ruta ni comodines: ' +
        'Validasoft rechaza una página o un asterisco. Esa misma lista decide qué páginas pueden embeber la app y qué orígenes pueden pedir sesiones para ella.',
      resaltar: page.locator('#peticion'),
      variantes: [
        { cuando: 'Escribes una ruta o un comodín', entonces: 'La llamada se rechaza: el origen es el sitio, no una página.' },
        { cuando: 'Cambias la lista', entonces: 'Los cambios valen desde ya; la página que embebe la app recibe la lista nueva en la siguiente consulta.' },
      ],
    });

    const publicos = await fetch(`${API}/public/v1/embed/origenes-permitidos`).then((r) => r.json() as Promise<unknown>);
    await mostrarCodigo(page, {
      titulo: 'Consulta los orígenes que ve la app',
      peticion: 'GET /public/v1/embed/origenes-permitidos',
      cabeceras: { Accept: 'application/json' },
      respuesta: { estado: 200, cuerpo: publicos },
    });
    await paso(page, {
      titulo: 'Revisa los orígenes que acepta la app',
      explicacion:
        'La app consulta esta lista en tiempo de ejecución y la usa para dos cosas: acepta mensajes solo de esos orígenes y, en la cabecera de seguridad frame-ancestors, solo esos sitios pueden alojarla en un marco. ' +
        'La consulta se guarda en caché unos 60 segundos, así que un cambio puede tardar ese tiempo en llegar.',
      resaltar: page.locator('#respuesta'),
    });

    const rechazo = await llamar('POST', '/api/v1/sesiones-tercero', {
      llave: e.llave,
      empresa: invitacion.empresa.id,
      cuerpo: { vinculoId: invitacion.vinculoId, componente: 'boveda', origen: ORIGEN_AJENO, minutos: 10 },
    }).catch((error: unknown) => error);
    expect(rechazo).toBeInstanceOf(ErrorDeApi);
    const motivo = rechazo as ErrorDeApi;
    await mostrarCodigo(page, {
      titulo: 'Un origen que no declaraste no recibe sesión',
      peticion: 'POST /api/v1/sesiones-tercero',
      cabeceras: { 'Content-Type': 'application/json', 'X-Api-Key': '<llave de API del sistema>' },
      cuerpo: { vinculoId: '<vínculo del tercero>', componente: 'boveda', origen: ORIGEN_AJENO, minutos: 10 },
      respuesta: { estado: motivo.estado, cuerpo: { codigo: motivo.codigo } },
    });
    await paso(page, {
      titulo: 'Un origen ajeno no obtiene sesión',
      explicacion:
        'Cuando una página pide una sesión para un origen que no está en la lista, la API responde 403 «origen-no-permitido» y no emite ningún token. ' +
        'Sin token, la página no puede mostrar la bóveda ni la ficha del tercero, aunque cargue el marco de Validasoft.',
      resaltar: page.locator('#respuesta'),
    });

    const sesion = await llamar<{ origen: string; componente: string }>('POST', '/api/v1/sesiones-tercero', {
      llave: e.llave,
      empresa: invitacion.empresa.id,
      cuerpo: { vinculoId: invitacion.vinculoId, componente: 'boveda', origen: ORIGEN_SISTEMA, minutos: 10 },
    });
    expect(sesion.origen).toBe(ORIGEN_SISTEMA);
    await mostrarCodigo(page, {
      titulo: 'Un origen declarado sí obtiene sesión',
      peticion: 'POST /api/v1/sesiones-tercero',
      cabeceras: { 'Content-Type': 'application/json', 'X-Api-Key': '<llave de API del sistema>' },
      cuerpo: { vinculoId: '<vínculo del tercero>', componente: 'boveda', origen: ORIGEN_SISTEMA, minutos: 10 },
      respuesta: { estado: 200, cuerpo: { origen: sesion.origen, componente: sesion.componente, token: '<token de 10 minutos>' } },
    });
    await paso(page, {
      titulo: 'Un origen declarado sí obtiene sesión',
      explicacion:
        'Si la página está en la lista, la API emite el token para ese origen y para esa pantalla. El token solo sirve en el origen para el que se emitió: ' +
        'otro origen declarado no puede usarlo, aunque sea del mismo sistema.',
      resaltar: page.locator('#respuesta'),
    });
  },
);
