import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { configurarCliente } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import type { ReactElement } from 'react';
import { MemoryRouter } from 'react-router';
import { vi } from 'vitest';

/** Ayudas de las pruebas de H7: una API simulada por «MÉTODO /ruta» que registra lo que se le pide, y el render con tema y consultas. */

export interface LlamadaApi {
  metodo: string;
  ruta: string;
  url: URL;
  cuerpo: unknown;
}

export type Manejador = (llamada: LlamadaApi) => unknown;

/** Una respuesta de error de la API con su `codigo` (ProblemDetails). */
export function problema(estado: number, codigo: string, detalle = 'Detalle del servidor.'): Response {
  return new Response(JSON.stringify({ status: estado, title: codigo, detail: detalle, codigo }), { status: estado, headers: { 'Content-Type': 'application/problem+json' } });
}

/**
 * Configura el cliente con un `fetch` que atiende cada «MÉTODO /ruta» con su manejador (el valor que devuelve va como JSON 200,
 * o una `Response` tal cual). Lo que no tiene manejador responde 404. Devuelve la lista de llamadas, en orden.
 */
export function simularApi(manejadores: Readonly<Record<string, Manejador>>): LlamadaApi[] {
  const llamadas: LlamadaApi[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost');
      const metodo = (init?.method ?? 'GET').toUpperCase();
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined;
      const llamada: LlamadaApi = { metodo, ruta: url.pathname, url, cuerpo };
      llamadas.push(llamada);
      const manejador = manejadores[`${metodo} ${url.pathname}`];
      if (!manejador) return Promise.resolve(problema(404, 'no-encontrado', `Sin manejador para ${metodo} ${url.pathname}`));
      const respuesta = manejador(llamada);
      return Promise.resolve(respuesta instanceof Response ? respuesta : new Response(JSON.stringify(respuesta), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    }),
  });
  return llamadas;
}

export function renderizar(ui: ReactElement, ruta = '/') {
  return render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>{ui}</MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
}
