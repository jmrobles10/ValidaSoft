import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { configurarCliente, type JsonElement, type PaginaDtoOfRegistroAuditoriaDto, type RegistroAuditoriaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { vi } from 'vitest';
import { PaginaAuditoria } from '../PaginaAuditoria';

/** La API tipa los valores JSON como objeto; en las pruebas también son textos, números o listas. */
const json = (valor: unknown) => valor as JsonElement;

export const LARGO = `[${Array.from({ length: 40 }, (_, i) => `"regla-${String(i)}"`).join(',')}]`;

export const CAMBIO_AREA: RegistroAuditoriaDto = {
  id: 'r1',
  tipo: 'Configuracion',
  fecha: '2026-11-08T15:04:05Z',
  empresaId: null,
  actor: { tipo: 'Usuario', id: 'u1', nombre: 'Ana Pérez', sistema: 'SRM' },
  entidad: { tipo: 'Area', id: 'a1', nombre: 'Compras' },
  accion: 'Modificado',
  antes: null,
  despues: null,
  diff: [
    { campo: 'nombre', antes: json('Compras'), despues: json('Compras y suministros'), tipoCambio: 'Modificado' },
    { campo: 'responsable', antes: null, despues: json('u7'), tipoCambio: 'Agregado' },
    { campo: 'activo', antes: json(true), despues: null, tipoCambio: 'Eliminado' },
    { campo: 'reglas', antes: null, despues: json(JSON.parse(LARGO)), tipoCambio: 'Agregado' },
  ],
  justificacion: 'Reorganización del área',
  detalle: null,
  vinculoId: null,
  ip: null,
  userAgent: null,
  correlationId: 'corr-123',
};

export const CAMBIO_WEBHOOK: RegistroAuditoriaDto = {
  ...CAMBIO_AREA,
  id: 'r2',
  empresaId: 'emp',
  actor: { tipo: 'Integracion', id: '0f8fad5b-d9cb-469f-a165-70867728950e', nombre: null, sistema: null },
  entidad: { tipo: 'Webhook', id: 'w1', nombre: 'https://srm.ejemplo.co/hook' },
  accion: 'Creado',
  diff: [],
  justificacion: null,
  correlationId: null,
};

export const DESCARGA_CERTIFICADO: RegistroAuditoriaDto = {
  ...CAMBIO_AREA,
  id: 'r3',
  tipo: 'Acceso',
  empresaId: 'emp',
  actor: { tipo: 'Tercero', id: null, nombre: null, sistema: null },
  entidad: { tipo: 'Certificado', id: 'c1', nombre: 'VS-2026-000001' },
  accion: 'Descargado',
  diff: [],
  justificacion: null,
  detalle: 'Verificación pública',
  vinculoId: 'v9',
  ip: '181.50.1.2',
  userAgent: 'Mozilla/5.0 (Windows NT 10.0) Firefox/140.0',
  correlationId: null,
};

const respuesta = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

/** Parámetros de una URL pedida (para comparar sin depender del orden ni de la codificación). */
export const parametrosDe = (url: string) => Object.fromEntries(new URL(url, 'http://local').searchParams);

/** API falsa: `paginas(url)` decide la respuesta; guarda la URL de cada petición. */
export function montar(paginas: (url: string) => PaginaDtoOfRegistroAuditoriaDto, ruta = '/e/emp/auditoria') {
  const urls: string[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      urls.push(url);
      return Promise.resolve(respuesta(paginas(url)));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/e/:empresaId/auditoria" element={<PaginaAuditoria />} />
            {/* Solo existe la ruta del vínculo de la descarga: si el enlace llevara a otro, no se vería. */}
            <Route path="/e/emp/vinculos/v9" element={<p>Vínculo v9</p>} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return urls;
}
