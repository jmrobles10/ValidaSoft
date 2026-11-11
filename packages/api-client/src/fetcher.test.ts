import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { configurarCliente, reiniciarCliente } from './configuracion';
import { ErrorApi, ErrorDeRed } from './errores';
import { clienteValidasoft, construirUrl, descargarArchivo, nombreDeArchivo } from './fetcher';
import { bloquearVinculo, crearVinculo, listarVinculos } from './generado/endpoints/vinculos/vinculos';
import { revocarApiKey } from './generado/endpoints/api-keys/api-keys';

interface Llamada {
  url: string;
  metodo: string;
  cabeceras: Headers;
  cuerpo: string | null;
}

function json(estado: number, cuerpo: unknown, cabeceras: Record<string, string> = {}): Response {
  return new Response(JSON.stringify(cuerpo), {
    status: estado,
    headers: { 'Content-Type': estado >= 400 ? 'application/problem+json' : 'application/json', ...cabeceras },
  });
}

function prepararFetch(respuesta: () => Response | Promise<Response>) {
  const llamadas: Llamada[] = [];
  const fetchFalso = vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
    llamadas.push({
      url: entrada instanceof Request ? entrada.url : entrada.toString(),
      metodo: init?.method ?? 'GET',
      cabeceras: new Headers(init?.headers),
      cuerpo: typeof init?.body === 'string' ? init.body : null,
    });
    return Promise.resolve(respuesta());
  });
  configurarCliente({ fetch: fetchFalso });
  return llamadas;
}

let secuencia = 0;

beforeEach(() => {
  secuencia = 0;
  configurarCliente({
    baseUrl: 'http://api.prueba',
    obtenerToken: () => 'token-de-sesion',
    obtenerEmpresa: () => 'empresa-1',
    generarId: () => `id-${String(++secuencia)}`,
  });
});

afterEach(() => {
  reiniciarCliente();
});

describe('construirUrl', () => {
  it('omite parámetros vacíos y repite arreglos', () => {
    expect(construirUrl('http://x/', '/api/v1/vinculos', { estado: 'Activo', cursor: undefined, tipo: '', ids: ['a', 'b'] })).toBe(
      'http://x/api/v1/vinculos?estado=Activo&ids=a&ids=b',
    );
  });
});

describe('clienteValidasoft', () => {
  it('envía Bearer, X-Empresa y X-Correlation-Id y devuelve el JSON', async () => {
    const llamadas = prepararFetch(() => json(200, { datos: [], siguienteCursor: null }));

    const pagina = await listarVinculos({ estado: 'Activo', limite: 20 });

    expect(pagina).toEqual({ datos: [], siguienteCursor: null });
    const [llamada] = llamadas;
    expect(llamada?.url).toBe('http://api.prueba/api/v1/vinculos?estado=Activo&limite=20');
    expect(llamada?.metodo).toBe('GET');
    expect(llamada?.cabeceras.get('Authorization')).toBe('Bearer token-de-sesion');
    expect(llamada?.cabeceras.get('X-Empresa')).toBe('empresa-1');
    expect(llamada?.cabeceras.get('X-Correlation-Id')).toBe('id-1');
    expect(llamada?.cabeceras.has('Idempotency-Key')).toBe(false);
  });

  it('genera Idempotency-Key en los POST que crean recursos', async () => {
    const llamadas = prepararFetch(() => json(201, { id: 'v1' }));

    await crearVinculo({
      terceroId: 't1',
      tercero: null,
      tipoVinculo: 'PROVEEDOR',
      atributos: null,
      correoInvitacion: 'a@b.co',
      referenciaExterna: null,
    });

    expect(llamadas[0]?.cabeceras.get('Idempotency-Key')).toBe('id-2');
    expect(llamadas[0]?.cabeceras.get('Content-Type')).toBe('application/json');
    expect(JSON.parse(llamadas[0]?.cuerpo ?? '{}')).toMatchObject({ tipoVinculo: 'PROVEEDOR' });
  });

  it('respeta la Idempotency-Key de la llamada para que los reintentos no dupliquen', async () => {
    const llamadas = prepararFetch(() => json(201, { id: 'v1' }));

    await crearVinculo(
      { terceroId: 't1', tercero: null, tipoVinculo: 'P', atributos: null, correoInvitacion: 'a@b.co', referenciaExterna: null },
      { idempotencyKey: 'formulario-7' },
    );

    expect(llamadas[0]?.cabeceras.get('Idempotency-Key')).toBe('formulario-7');
  });

  it('no inventa Idempotency-Key en POST de acciones y envía If-Match con la versión', async () => {
    const llamadas = prepararFetch(() => json(200, { id: 'v1', version: 4 }));

    await bloquearVinculo('v1', { motivo: 'Lista restrictiva', hasta: null }, { ifMatch: 3 });

    expect(llamadas[0]?.url).toBe('http://api.prueba/api/v1/vinculos/v1/bloquear');
    expect(llamadas[0]?.cabeceras.has('Idempotency-Key')).toBe(false);
    expect(llamadas[0]?.cabeceras.get('If-Match')).toBe('"3"');
  });

  it('permite omitir la empresa y el token por llamada', async () => {
    const llamadas = prepararFetch(() => json(200, {}));

    await clienteValidasoft({ url: '/api/v1/sesiones-usuario', method: 'POST' }, { token: null, empresa: null, headers: { 'X-Api-Key': 'vsk' } });

    expect(llamadas[0]?.cabeceras.has('Authorization')).toBe(false);
    expect(llamadas[0]?.cabeceras.has('X-Empresa')).toBe(false);
    expect(llamadas[0]?.cabeceras.get('X-Api-Key')).toBe('vsk');
  });

  it('devuelve undefined en 204', async () => {
    prepararFetch(() => new Response(null, { status: 204 }));
    await expect(revocarApiKey('k1')).resolves.toBeUndefined();
  });

  it('traduce ProblemDetails a ErrorApi con su codigo', async () => {
    prepararFetch(() =>
      json(
        409,
        { status: 409, title: 'ya-bloqueado', detail: 'El vínculo ya está bloqueado.', codigo: 'ya-bloqueado', traceId: 'tr-1' },
        { 'X-Correlation-Id': 'corr-9' },
      ),
    );

    const error = await bloquearVinculo('v1', { motivo: 'x', hasta: null }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ErrorApi);
    const api = error as ErrorApi;
    expect(api.codigo).toBe('ya-bloqueado');
    expect(api.estado).toBe(409);
    expect(api.detalle).toBe('El vínculo ya está bloqueado.');
    expect(api.traceId).toBe('tr-1');
    expect(api.correlationId).toBe('corr-9');
    expect(api.esConflicto).toBe(true);
  });

  it('marca el 412 como conflicto de versión', async () => {
    prepararFetch(() => json(412, { codigo: 'version-desactualizada', detail: 'El recurso cambió.' }));
    const error = (await bloquearVinculo('v1', { motivo: 'x', hasta: null }, { ifMatch: 1 }).catch((e: unknown) => e)) as ErrorApi;
    expect(error.esConflictoDeVersion).toBe(true);
  });

  it('lee los errores por campo de la validación de ASP.NET', async () => {
    prepararFetch(() => json(400, { title: 'One or more validation errors occurred.', errors: { Motivo: ['Requerido'] } }));
    const error = (await bloquearVinculo('v1', { motivo: '', hasta: null }).catch((e: unknown) => e)) as ErrorApi;
    expect(error.codigo).toBe('peticion-invalida');
    expect(error.errores).toEqual({ Motivo: ['Requerido'] });
  });

  it('avisa los 401 aunque vengan sin cuerpo', async () => {
    const alNoAutenticado = vi.fn();
    configurarCliente({ alNoAutenticado });
    prepararFetch(() => new Response(null, { status: 401 }));

    const error = (await listarVinculos().catch((e: unknown) => e)) as ErrorApi;

    expect(error.codigo).toBe('no-autenticado');
    expect(error.esNoAutenticado).toBe(true);
    expect(alNoAutenticado).toHaveBeenCalledWith(error, expect.stringContaining('/api/v1/vinculos'));
  });

  it('convierte las fallas de red en ErrorDeRed', async () => {
    configurarCliente({ fetch: () => Promise.reject(new TypeError('Failed to fetch')) });
    await expect(listarVinculos()).rejects.toBeInstanceOf(ErrorDeRed);
  });

  it('deja pasar la cancelación sin convertirla', async () => {
    configurarCliente({ fetch: () => Promise.reject(new DOMException('cancelada', 'AbortError')) });
    await expect(listarVinculos()).rejects.toMatchObject({ name: 'AbortError' });
  });
});

describe('descargarArchivo', () => {
  it('pide el archivo con la sesión y la empresa y devuelve el blob con el nombre de Content-Disposition', async () => {
    const llamadas = prepararFetch(
      () =>
        new Response(new Uint8Array([80, 75, 3, 4]), {
          status: 200,
          headers: {
            'Content-Type': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            'Content-Disposition': "attachment; filename=vencimientos-norte.xlsx; filename*=UTF-8''vencimientos-norte.xlsx",
          },
        }),
    );

    const archivo = await descargarArchivo({ url: '/api/v1/vencimientos/excel', params: { dias: 30, estado: '' } }, { empresa: 'empresa-2' });

    expect(llamadas[0]?.url).toBe('http://api.prueba/api/v1/vencimientos/excel?dias=30');
    expect(llamadas[0]?.metodo).toBe('GET');
    expect(llamadas[0]?.cabeceras.get('Authorization')).toBe('Bearer token-de-sesion');
    expect(llamadas[0]?.cabeceras.get('X-Empresa')).toBe('empresa-2');
    expect(archivo.nombre).toBe('vencimientos-norte.xlsx');
    expect(archivo.blob.size).toBe(4);
  });

  it('traduce el error de la API a ErrorApi', async () => {
    prepararFetch(() => json(400, { codigo: 'estado-invalido', detail: 'El estado es Vigente…', status: 400 }));
    await expect(descargarArchivo({ url: '/api/v1/vencimientos/excel' })).rejects.toMatchObject({ codigo: 'estado-invalido' });
  });
});

describe('nombreDeArchivo', () => {
  it('prefiere filename* (UTF-8) y si no, filename con o sin comillas', () => {
    expect(nombreDeArchivo("attachment; filename=a.xlsx; filename*=UTF-8''informe%20%C3%A1rea.xlsx")).toBe('informe área.xlsx');
    expect(nombreDeArchivo('attachment; filename="con espacios.xlsx"')).toBe('con espacios.xlsx');
    expect(nombreDeArchivo('attachment; filename=simple.xlsx')).toBe('simple.xlsx');
    expect(nombreDeArchivo('attachment')).toBeNull();
    expect(nombreDeArchivo(null)).toBeNull();
  });
});
