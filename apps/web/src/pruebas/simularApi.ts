import { configurarCliente } from '@validasoft/api-client';
import { vi } from 'vitest';

/** Una petición que la app le hizo a la API simulada. `ruta` va sin la consulta (`?a=b`), `url` con ella. */
export interface PeticionSimulada {
  metodo: string;
  ruta: string;
  url: string;
  cuerpo: unknown;
}

/** Cómo responde la API simulada a una ruta (`'POST /api/v1/ingreso/codigo'`): datos (200), `undefined` (204), una respuesta con otro estado o una función de la petición. */
export type RespuestaSimulada = unknown;
export type ManejadorSimulado = RespuestaSimulada | ((peticion: PeticionSimulada) => RespuestaSimulada);

/** Una respuesta de error ProblemDetails como las de la API (`codigo` estable, `detail` en español). */
export class ProblemaSimulado {
  constructor(
    readonly estado: number,
    readonly codigo: string,
    readonly detalle: string,
    readonly extensiones: Record<string, unknown> = {},
  ) {}
}

function aRespuesta(valor: RespuestaSimulada): Response {
  if (valor instanceof Response) return valor;
  if (valor instanceof ProblemaSimulado) {
    return new Response(JSON.stringify({ status: valor.estado, codigo: valor.codigo, detail: valor.detalle, ...valor.extensiones }), {
      status: valor.estado,
      headers: { 'Content-Type': 'application/problem+json' },
    });
  }
  if (valor === undefined) return new Response(null, { status: 204 });
  return new Response(JSON.stringify(valor), { status: 200, headers: { 'Content-Type': 'application/json' } });
}

/**
 * Pone en el cliente de la API un `fetch` que responde según `rutas` (`'GET /api/v1/mi-cuenta/codigos-de-recuperacion': { … }`). Lo que no esté en la tabla
 * responde 404 `no-encontrado`. Devuelve la lista (viva) de peticiones hechas, en orden, para comprobar qué envió la pantalla.
 */
export function simularApi(rutas: Record<string, ManejadorSimulado>): PeticionSimulada[] {
  const peticiones: PeticionSimulada[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = (init?.method ?? 'GET').toUpperCase();
      const ruta = url.split('?')[0] ?? url;
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      const peticion: PeticionSimulada = { metodo, ruta, url, cuerpo };
      peticiones.push(peticion);
      const manejador = rutas[`${metodo} ${ruta}`];
      if (manejador === undefined) return Promise.resolve(aRespuesta(new ProblemaSimulado(404, 'no-encontrado', `Sin simular: ${metodo} ${ruta}`)));
      return Promise.resolve(aRespuesta(typeof manejador === 'function' ? (manejador as (p: PeticionSimulada) => RespuestaSimulada)(peticion) : manejador));
    }),
  });
  return peticiones;
}
