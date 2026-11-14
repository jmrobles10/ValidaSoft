import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { configurarCliente, type SesionValidasoft } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { createElement, type ReactNode } from 'react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { vi } from 'vitest';
import { useSesion } from '../../../sesion/sesionStore';
import { crearCuerpoSse, type CuerpoSse } from '@validasoft/api-client/pruebas/sse';

export const json = (estado: number, cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

export interface PeticionApi {
  metodo: string;
  ruta: string;
  cabeceras: Headers;
  cuerpo: unknown;
}

/**
 * API falsa: `responder` contesta cada petición por método y ruta (sin query). El stream SSE queda abierto y la prueba
 * le escribe con `streams`.
 */
export function prepararApi(responder: (metodo: string, ruta: string) => Response | Promise<Response>) {
  const peticiones: PeticionApi[] = [];
  const streams: CuerpoSse[] = [];
  configurarCliente({
    obtenerToken: () => 'tok',
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const texto = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const ruta = texto.split('?')[0] ?? texto;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : undefined;
      peticiones.push({ metodo, ruta, cabeceras: new Headers(init?.headers), cuerpo });
      if (ruta.endsWith('/notificaciones/stream')) {
        const stream = crearCuerpoSse(init?.signal);
        streams.push(stream);
        return Promise.resolve(stream.respuesta);
      }
      return Promise.resolve(responder(metodo, ruta));
    }),
  });
  return { peticiones, streams };
}

export function iniciarSesionDePrueba() {
  const sesion: SesionValidasoft = {
    token: 'tok',
    expira: new Date(Date.now() + 30 * 60_000),
    usuarioId: 'u-verif',
    tenantId: 't-1',
    nombre: null,
    sistema: 'SRM',
    sesionId: null,
    metodoIngreso: null,
    actorTipo: 'Usuario',
    scopesDeTenant: new Set(),
    scopesPorEmpresa: new Map(),
    empresas: '*',
  };
  useSesion.setState({ sesion, estado: 'activa', empresaSugerida: null, sugerirPasskey: false });
}

/** Monta `elemento` en `/e/emp/bandeja` con rutas de destino que muestran a dónde se navegó. */
export function montar(elemento: ReactNode) {
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity }, mutations: { retry: false } } });
  // `MemoryRouter` y no un router de datos: en jsdom, el `Request` de las navegaciones de datos no acepta su AbortSignal.
  // Sin JSX: este archivo es `.ts` (solo exporta funciones).
  const rutas = createElement(
    Routes,
    null,
    createElement(Route, { path: '/e/:empresaId/bandeja', element: elemento }),
    createElement(Route, { path: '/e/:empresaId/expedientes/:expedienteId', element: createElement(UbicacionActual) }),
  );
  render(
    createElement(
      ThemeProvider,
      { theme: createValidasoftTheme('light') },
      createElement(QueryClientProvider, { client: cliente }, createElement(MemoryRouter, { initialEntries: ['/e/emp/bandeja'] }, rutas)),
    ),
  );
  return { cliente };
}

function UbicacionActual() {
  return createElement('p', null, `Detalle del expediente ${useLocation().pathname}`);
}
