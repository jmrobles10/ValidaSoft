import { afterEach, describe, expect, it, vi } from 'vitest';
import { abrirConexionSse, esperaDeReconexion, type ConexionSse, type EstadoConexion } from './conexionSse';
import { crearFetchSse } from './pruebas/fetchSse';
import type { EventoSse } from './sse';

const RAPIDO = { base: 5, maximo: 20, fallosParaRespaldo: 2, inactividad: 1_000, estable: 10_000 };

let conexion: ConexionSse | null = null;

afterEach(() => {
  conexion?.cerrar();
  conexion = null;
});

function abrir(opciones: { fetch: typeof fetch; token?: () => string | null }) {
  const eventos: EventoSse[] = [];
  const estados: EstadoConexion[] = [];
  const token = opciones.token ?? (() => 'tok');
  conexion = abrirConexionSse({
    url: '/api/v1/notificaciones/stream',
    fetch: opciones.fetch,
    esperas: RAPIDO,
    aleatorio: () => 0,
    cabeceras: () => {
      const valor = token();
      return valor ? { Authorization: `Bearer ${valor}`, 'X-Empresa': 'emp' } : null;
    },
    alEvento: (e) => eventos.push(e),
    alEstado: (e) => estados.push(e),
  });
  return { eventos, estados };
}

describe('esperaDeReconexion', () => {
  it('crece al doble con cada falla seguida, hasta el máximo, más hasta un 20 % de azar', () => {
    const esperas = { base: 1_000, maximo: 30_000 };
    expect([1, 2, 3, 4, 5, 6, 7].map((f) => esperaDeReconexion(f, esperas))).toEqual([1_000, 2_000, 4_000, 8_000, 16_000, 30_000, 30_000]);
    expect(esperaDeReconexion(1, esperas, 1)).toBe(1_200);
  });
});

describe('abrirConexionSse', () => {
  it('conecta con el token Bearer y la empresa, entrega los eventos y al caerse reconecta con Last-Event-ID', async () => {
    const { fetchFalso, peticiones } = crearFetchSse();
    const { eventos, estados } = abrir({ fetch: fetchFalso });

    await vi.waitFor(() => expect(peticiones).toHaveLength(1));
    const primera = peticiones[0]!;
    expect(primera.url).toBe('/api/v1/notificaciones/stream');
    expect(primera.cabeceras.get('Authorization')).toBe('Bearer tok');
    expect(primera.cabeceras.get('X-Empresa')).toBe('emp');
    expect(primera.cabeceras.get('Accept')).toBe('text/event-stream');
    expect(primera.cabeceras.has('Last-Event-ID')).toBe(false);

    primera.cuerpo.enviar(': conectado\n\nid: 101\nevent: notificacion\ndata: {"id":"n1"}\n\n');
    primera.cuerpo.enviar('id: 102\nevent: no_le');
    primera.cuerpo.enviar('idas\ndata: 4\n\n');
    await vi.waitFor(() => expect(eventos).toHaveLength(2));
    expect(eventos[1]).toEqual({ id: '102', evento: 'no_leidas', datos: '4' });
    expect(estados).toEqual(['conectando', 'conectado']);

    primera.cuerpo.cerrar();
    await vi.waitFor(() => expect(peticiones).toHaveLength(2));
    expect(peticiones[1]!.cabeceras.get('Last-Event-ID')).toBe('102');
    expect(estados.slice(2)).toEqual(['reconectando', 'conectado']);
  });

  it('tras varias fallas seguidas pasa a respaldo, sigue reintentando y vuelve a conectado', async () => {
    const { fetchFalso, peticiones } = crearFetchSse();
    let fallas = 3;
    const fetchConFallas = vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      if (fallas > 0) {
        fallas -= 1;
        return Promise.resolve(new Response('no', { status: 503 }));
      }
      return fetchFalso(entrada, init);
    });
    const { estados } = abrir({ fetch: fetchConFallas });

    await vi.waitFor(() => expect(peticiones).toHaveLength(1));
    expect(fetchConFallas).toHaveBeenCalledTimes(4);
    expect(estados).toEqual(['conectando', 'reconectando', 'respaldo', 'respaldo', 'conectado']);
  });

  it('sin token no llama a la API; con uno nuevo, reconectarYa reintenta sin esperar', async () => {
    const { fetchFalso, peticiones } = crearFetchSse();
    let token: string | null = null;
    abrir({ fetch: fetchFalso, token: () => token });
    await new Promise((r) => setTimeout(r, 15));
    expect(fetchFalso).not.toHaveBeenCalled();

    token = 'nuevo';
    conexion?.reconectarYa();
    await vi.waitFor(() => expect(peticiones).toHaveLength(1));
    expect(peticiones[0]!.cabeceras.get('Authorization')).toBe('Bearer nuevo');
  });

  it('cerrar aborta el stream abierto y no vuelve a conectar', async () => {
    const { fetchFalso, peticiones } = crearFetchSse();
    abrir({ fetch: fetchFalso });
    await vi.waitFor(() => expect(peticiones).toHaveLength(1));

    conexion?.cerrar();
    await new Promise((r) => setTimeout(r, 30));
    expect(fetchFalso).toHaveBeenCalledTimes(1);
  });

  it('si no llega nada (ni el latido) en el tiempo de inactividad, da la conexión por caída y reconecta', async () => {
    const { fetchFalso, peticiones } = crearFetchSse();
    conexion = abrirConexionSse({
      url: '/s',
      fetch: fetchFalso,
      esperas: { ...RAPIDO, inactividad: 20 },
      aleatorio: () => 0,
      cabeceras: () => ({ Authorization: 'Bearer tok' }),
      alEvento: () => undefined,
      alEstado: () => undefined,
    });
    await vi.waitFor(() => expect(peticiones.length).toBeGreaterThanOrEqual(2));
  });
});
