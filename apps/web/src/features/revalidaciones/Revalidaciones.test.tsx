import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type RevalidacionDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../shared/useAvisos';
import { PaginaRevalidaciones } from './PaginaRevalidaciones';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

function revalidacion(parcial: Partial<RevalidacionDto>): RevalidacionDto {
  return {
    expedienteId: 'e1',
    folio: 'NOR-2026-00001',
    vinculoId: 'v1',
    requisitoId: 'r1',
    tercero: { id: 't1', nombre: 'Aceros del Norte', identificacion: 'NIT 800555111' },
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    tipoDocumentoCodigo: 'RUT',
    tipoDocumento: 'RUT',
    versionAceptada: { numero: 1, fecha: '2026-11-01T00:00:00Z' },
    versionActual: { numero: 2, fecha: '2026-11-08T00:00:00Z' },
    comparacion: 'Distinta',
    aprobableEnLote: false,
    estadoRequisito: 'EnRevision',
    areas: [],
    desde: '2026-11-08T00:00:00Z',
    plazoHasta: '2026-11-23T00:00:00Z',
    diasRestantes: 15,
    bloqueante: false,
    ...parcial,
  };
}

const FILAS: RevalidacionDto[] = [
  revalidacion({ expedienteId: 'e1', requisitoId: 'r1', tipoDocumento: 'RUT', estadoRequisito: 'Observado' }),
  revalidacion({ expedienteId: 'e2', requisitoId: 'r2', tipoDocumento: 'Póliza', tercero: { id: 't2', nombre: 'Ferretería El Tornillo', identificacion: 'NIT 900' }, estadoRequisito: 'EnRevision' }),
  revalidacion({ expedienteId: 'e3', requisitoId: 'r3', tipoDocumento: 'Cámara de comercio', tercero: { id: 't3', nombre: 'Cementos Andes', identificacion: 'NIT 901' }, comparacion: 'SinCambios', aprobableEnLote: true }),
];

function montar() {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = init?.method ?? 'GET';
      peticiones.push({ url, metodo, cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : null });
      if (metodo === 'POST') {
        return Promise.resolve(json({ enviados: 1, omitidos: 0, detalle: [{ expedienteId: 'e1', folio: 'NOR-2026-00001', resultado: 'Enviado' }] }));
      }
      return Promise.resolve(json({ datos: FILAS, siguienteCursor: null }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={['/e/emp/revalidaciones']}>
          <Routes>
            <Route path="/e/:empresaId/revalidaciones" element={<PaginaRevalidaciones />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.getState().cerrar();
  vi.restoreAllMocks();
});

describe('Revalidaciones: recordar al tercero lo devuelto', () => {
  it('solo deja marcar lo devuelto o aprobable; confirma antes y recuerda a los expedientes devueltos', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.vinculosEscribir]);
    const usuario = userEvent.setup();
    const peticiones = montar();

    // La devuelta al tercero y la aprobable se pueden marcar; la que sigue en revisión y cambió, no.
    const devuelta = await screen.findByRole('checkbox', { name: 'Seleccionar RUT de Aceros del Norte' });
    expect(devuelta).toBeEnabled();
    expect(screen.getByRole('checkbox', { name: 'Seleccionar Cámara de comercio de Cementos Andes' })).toBeEnabled();
    expect(screen.getByRole('checkbox', { name: 'Seleccionar Póliza de Ferretería El Tornillo' })).toBeDisabled();
    expect(screen.getByText('Devuelta al tercero')).toBeInTheDocument();

    // Sin marcar nada solo está «Recordar a los observados»; al marcar la devuelta aparece «Recordar (1)».
    expect(screen.queryByRole('button', { name: 'Recordar (1)' })).not.toBeInTheDocument();
    await usuario.click(devuelta);
    await usuario.click(screen.getByRole('button', { name: 'Recordar (1)' }));

    // Nada se envía hasta confirmar.
    const dialogo = await screen.findByRole('dialog', { name: '¿Recordar las observaciones de 1 expediente?' });
    expect(peticiones.some((p) => p.metodo === 'POST')).toBe(false);
    expect(within(dialogo).getByText(/A quien ya se le recordó hoy no se le vuelve a enviar/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Enviar recordatorios' }));

    await waitFor(() => expect(peticiones.find((p) => p.metodo === 'POST')?.cuerpo).toEqual({ expedientes: ['e1'] }));
    expect(peticiones.find((p) => p.metodo === 'POST')?.url).toBe('/api/v1/expedientes/recordar-observaciones');
    await waitFor(() => expect(useAvisos.getState().actual).toMatchObject({ mensaje: 'Recordamos a 1 tercero.', severidad: 'success' }));
    // Terminado el envío, la selección se limpia.
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Recordar (1)' })).not.toBeInTheDocument());
  });

  it('marcar una aprobable no ofrece recordar: cada botón cuenta solo lo suyo', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.vinculosEscribir]);
    const usuario = userEvent.setup();
    montar();

    await usuario.click(await screen.findByRole('checkbox', { name: 'Seleccionar Cámara de comercio de Cementos Andes' }));
    expect(screen.getByRole('button', { name: 'Aprobar 1 sin cambios' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Recordar (1)' })).not.toBeInTheDocument();
  });

  it('sin vinculos:escribir no se ofrece recordar, y la devuelta no se puede marcar', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesRevisar]);
    montar();

    expect(await screen.findByRole('checkbox', { name: 'Seleccionar RUT de Aceros del Norte' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Recordar a los observados' })).not.toBeInTheDocument();
  });
});
