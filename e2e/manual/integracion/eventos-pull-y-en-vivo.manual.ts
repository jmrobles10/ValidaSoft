import { API, llamar } from '../../soporte/api';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, mostrarCodigo, paso, recorrido } from '../nucleo';

interface EventoPublico {
  id: string;
  tipo: string;
  cursor: string;
  empresaId: string;
  datos: Record<string, unknown>;
}

interface PaginaDeEventos {
  datos: EventoPublico[];
  cursor: string;
  hayMas: boolean;
}

recorrido(
  {
    dominio: 'integracion',
    actor: 'Sistema integrador',
    titulo: 'Lee los eventos por pull o en vivo con un cursor',
    resumen:
      'El sistema integrado pide los eventos de un tipo desde el último cursor que conoce, y guarda el nuevo cursor para la siguiente vez. Si prefiere tiempo real, abre el flujo de eventos (SSE) y se reanuda con Last-Event-ID si se corta.',
    precondiciones: [
      'Un grupo con Constructora Norte, su flujo de proveedor publicado y la llave del sistema integrado con permiso para leer eventos.',
      'Un vínculo creado desde la invitación, que genera el evento «vinculo.creado».',
    ],
    relacionados: ['integracion/webhooks', 'integracion/api-keys'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await invitar(e, e.norte, nuevoTercero('Tuberías Andinas S.A.S.'));

    const tipos = 'vinculo.creado';
    const ruta = `/api/v1/eventos?desde=0&tipos=${tipos}&limite=5`;
    let pagina: PaginaDeEventos = { datos: [], cursor: '0', hayMas: false };
    const limite = Date.now() + 45_000;
    while (pagina.datos.length === 0 && Date.now() < limite) {
      pagina = await llamar<PaginaDeEventos>('GET', ruta, { llave: e.llave });
      if (pagina.datos.length === 0) await new Promise((r) => setTimeout(r, 500));
    }
    expect(pagina.datos.length).toBeGreaterThan(0);

    await mostrarCodigo(page, {
      titulo: 'Pide los eventos desde tu último cursor',
      peticion: `GET ${ruta}`,
      cabeceras: { Accept: 'application/json' },
      respuesta: { estado: 200, cuerpo: pagina },
    });
    await paso(page, {
      titulo: 'Pide los eventos con un pull',
      explicacion:
        'Tu sistema pide GET /api/v1/eventos indicando desde qué cursor quiere leer (desde), qué tipos le interesan (tipos) y cuántos como máximo (limite). La respuesta trae los eventos en orden, cada uno con su cursor, ' +
        'y el cursor de la última página: guárdalo y pídelo desde ahí la próxima vez. Así no pierdes ni repites eventos aunque tu sistema se detenga un rato.',
      resaltar: page.locator('#respuesta'),
      variantes: [
        { cuando: 'Hay más eventos que el límite', entonces: 'La respuesta marca «hayMas» en verdadero: vuelve a pedir desde el último cursor hasta que sea falso.' },
        { cuando: 'No pides tipos', entonces: 'Recibes todos los tipos de evento que tu llave puede leer.' },
      ],
    });

    const siguiente = await llamar<PaginaDeEventos>('GET', `/api/v1/eventos?desde=${pagina.cursor}&tipos=${tipos}&limite=5`, { llave: e.llave });
    await mostrarCodigo(page, {
      titulo: 'Pide solo lo nuevo desde el último cursor',
      peticion: `GET /api/v1/eventos?desde=${pagina.cursor}&tipos=${tipos}&limite=5`,
      cabeceras: { Accept: 'application/json' },
      respuesta: { estado: 200, cuerpo: siguiente },
    });
    await paso(page, {
      titulo: 'Continúa desde el último cursor',
      explicacion:
        'Cuando ya leíste el último evento, pide de nuevo con el cursor que te devolvió la respuesta anterior. Si no hay nada nuevo de ese tipo, la lista llega vacía y el cursor no cambia. ' +
        'Es la forma de procesar los eventos una sola vez, en orden, sin volver a leer lo que ya tienes.',
      resaltar: page.locator('#respuesta'),
    });

    const res = await fetch(`${API}/api/v1/eventos/stream?desde=0&tipos=${tipos}`, {
      headers: { 'X-Api-Key': e.llave, Accept: 'text/event-stream' },
    });
    const lector = res.body?.getReader();
    if (!lector) throw new Error('El flujo de eventos no devolvió cuerpo.');
    const decodificador = new TextDecoder();
    let texto = '';
    const hasta = Date.now() + 20_000;
    while (!texto.includes('event: vinculo.creado') && Date.now() < hasta) {
      const { value, done } = await lector.read();
      if (done) break;
      texto += decodificador.decode(value, { stream: true });
    }
    await lector.cancel();
    const marcos = texto
      .split('\n\n')
      .map((bloque) => {
        const linea = (prefijo: string) => bloque.split('\n').find((l) => l.startsWith(prefijo))?.slice(prefijo.length).trim();
        const datos = linea('data:');
        return datos ? { id: linea('id:'), evento: linea('event:'), datos: JSON.parse(datos) as unknown } : null;
      })
      .filter((x) => x !== null);
    await mostrarCodigo(page, {
      titulo: 'Recibe los eventos en vivo',
      peticion: `GET /api/v1/eventos/stream?desde=0&tipos=${tipos}`,
      cabeceras: { Accept: 'text/event-stream' },
      respuesta: { estado: 200, cuerpo: marcos },
    });
    await paso(page, {
      titulo: 'Escucha los eventos en vivo',
      explicacion:
        'El flujo de eventos (Server-Sent Events) envía cada evento en cuanto ocurre, sin que tu sistema pregunte. En cada evento, id es el cursor, event es el tipo y data trae el mismo sobre que devuelve el pull. ' +
        'Si la conexión se corta, vuelve a conectarte con el último id en la cabecera Last-Event-ID o con desde, y no pierdes eventos.',
      resaltar: page.locator('#respuesta'),
      variantes: [
        { cuando: 'Tu sistema no puede mantener una conexión abierta', entonces: 'Usa el pull del paso anterior: la información es la misma.' },
      ],
    });
  },
);
