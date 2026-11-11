import { vi } from 'vitest';

/** Cuerpo SSE controlable desde la prueba; se corta con error si la petición se aborta. */
export interface CuerpoSse {
  respuesta: Response;
  enviar: (texto: string) => void;
  cerrar: () => void;
}

export function crearCuerpoSse(senal?: AbortSignal | null): CuerpoSse {
  let controlador: ReadableStreamDefaultController<Uint8Array> | null = null;
  const codificador = new TextEncoder();
  const stream = new ReadableStream<Uint8Array>({
    start(c) {
      controlador = c;
    },
  });
  senal?.addEventListener('abort', () => {
    try {
      controlador?.error(new DOMException('Abortada', 'AbortError'));
    } catch {
      // Ya estaba cerrado.
    }
  });
  return {
    respuesta: new Response(stream, { status: 200, headers: { 'Content-Type': 'text/event-stream' } }),
    enviar: (texto) => controlador?.enqueue(codificador.encode(texto)),
    cerrar: () => controlador?.close(),
  };
}

export interface PeticionSse {
  url: string;
  cabeceras: Headers;
  cuerpo: CuerpoSse;
}

/** `fetch` falso para el stream: cada llamada abre un cuerpo nuevo y queda registrada con sus cabeceras. */
export function crearFetchSse() {
  const peticiones: PeticionSse[] = [];
  const fetchFalso = vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
    const cuerpo = crearCuerpoSse(init?.signal);
    const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
    peticiones.push({ url, cabeceras: new Headers(init?.headers), cuerpo });
    return Promise.resolve(cuerpo.respuesta);
  });
  return { fetchFalso, peticiones };
}
