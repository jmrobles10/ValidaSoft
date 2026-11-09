import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type FilaCertificadoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaCertificados } from './PaginaCertificados';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

function fila(parcial: Partial<FilaCertificadoDto>): FilaCertificadoDto {
  return {
    id: 'c1',
    folio: 'VS-2026-000001',
    emision: 1,
    vinculoId: 'v1',
    tercero: { id: 't1', nombre: 'Ferretería El Tornillo', identificacion: 'NIT 900123456' },
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    estado: 'Vigente',
    bloqueante: false,
    emitidoEn: '2026-10-01T15:00:00Z',
    vigenteHasta: '2026-11-30',
    estadoDesde: '2026-10-01T15:00:00Z',
    ...parcial,
  };
}

const FILAS: FilaCertificadoDto[] = [
  fila({}),
  fila({
    id: 'c2',
    folio: 'VS-2026-000002',
    emision: 3,
    vinculoId: 'v2',
    tercero: { id: 't2', nombre: 'Aceros del Norte', identificacion: 'NIT 800555111' },
    estado: 'ConPendientes',
    bloqueante: true,
    vigenteHasta: null,
    estadoDesde: '2026-11-02T15:00:00Z',
  }),
  fila({ id: 'c3', folio: 'VS-2026-000003', vinculoId: 'v3', tercero: { id: 't3', nombre: 'Pinturas Andinas', identificacion: 'NIT 811222333' }, estado: 'ConPendientes' }),
];

const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

function UbicacionActual() {
  return <p>{`Ubicación: ${useLocation().pathname}`}</p>;
}

/** API falsa: devuelve `filas` en una sola página y guarda la URL de cada petición. */
function montar(filas: FilaCertificadoDto[], ruta = '/e/emp/certificados') {
  const urls: string[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      urls.push(typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url);
      return Promise.resolve(json({ datos: filas, siguienteCursor: null }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/e/:empresaId/certificados" element={<PaginaCertificados />} />
            <Route path="/e/:empresaId/vinculos/:vinculoId" element={<UbicacionActual />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return urls;
}

async function filaDe(texto: string) {
  const celda = (await screen.findByText(texto)).closest('tr');
  if (!celda) throw new Error(`sin fila para ${texto}`);
  return celda;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  vi.restoreAllMocks();
});

describe('Certificados', () => {
  it('sin certificados:leer no muestra la tabla ni pide la lista', () => {
    const urls = montar(FILAS);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
    expect(urls).toHaveLength(0);
  });

  it('muestra cada certificado con su estado y marca los que bloquean', async () => {
    permisos.scopes = new Set([SCOPES.certificadosLeer]);
    const urls = montar(FILAS);

    const vigente = await filaDe('VS-2026-000001');
    expect(within(vigente).getByText('Ferretería El Tornillo')).toBeInTheDocument();
    expect(within(vigente).getByText('Proveedor')).toBeInTheDocument();
    expect(within(vigente).getByText('Vigente')).toBeInTheDocument();
    expect(within(vigente).getByText('30/11/2026')).toBeInTheDocument();
    expect(within(vigente).getByText('01/10/2026')).toBeInTheDocument();

    const bloqueante = await filaDe('VS-2026-000002');
    expect(within(bloqueante).getByText('3')).toBeInTheDocument();
    expect(within(bloqueante).getByText('—')).toBeInTheDocument();
    expect(within(bloqueante).getByText(/Desde el 02\/11\/2026 · Bloqueante/)).toBeInTheDocument();

    const informativo = await filaDe('VS-2026-000003');
    expect(within(informativo).getByText('Con pendientes')).toBeInTheDocument();
    expect(within(informativo).queryByText(/Bloqueante/)).not.toBeInTheDocument();
    expect(urls[0]).toBe('/api/v1/certificados?limite=25');
  });

  it('el filtro «Bloquean la certificación» pide estado=Bloqueante', async () => {
    permisos.scopes = new Set([SCOPES.certificadosLeer]);
    const usuario = userEvent.setup();
    const urls = montar(FILAS);
    await filaDe('VS-2026-000001');

    await usuario.click(screen.getByRole('button', { name: /Estado/ }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Bloquean la certificación' }));

    await waitFor(() => expect(urls).toContain('/api/v1/certificados?estado=Bloqueante&limite=25'));
  });

  it('la búsqueda espera a que se deje de escribir y viaja como q', async () => {
    permisos.scopes = new Set([SCOPES.certificadosLeer]);
    const usuario = userEvent.setup();
    const urls = montar(FILAS);
    await filaDe('VS-2026-000001');

    await usuario.type(screen.getByRole('searchbox', { name: 'Buscar por tercero, identificación o folio' }), 'Norte');

    await waitFor(() => expect(urls).toContain('/api/v1/certificados?q=Norte&limite=25'));
    expect(urls.filter((u) => u.includes('q='))).toEqual(['/api/v1/certificados?q=Norte&limite=25']);
  });

  it('al hacer clic en una fila va al vínculo', async () => {
    permisos.scopes = new Set([SCOPES.certificadosLeer]);
    const usuario = userEvent.setup();
    montar(FILAS);
    await usuario.click(await screen.findByText('Aceros del Norte'));
    expect(await screen.findByText('Ubicación: /e/emp/vinculos/v2')).toBeInTheDocument();
  });

  it('vacío con filtros: lo dice y limpiar vuelve a la lista completa', async () => {
    permisos.scopes = new Set([SCOPES.certificadosLeer]);
    const usuario = userEvent.setup();
    const urls = montar([], '/e/emp/certificados?estado=Revocado&q=Tornillo');

    expect(await screen.findByText('Ningún certificado coincide con los filtros.')).toBeInTheDocument();
    expect(urls[0]).toBe('/api/v1/certificados?estado=Revocado&q=Tornillo&limite=25');
    expect(screen.getByRole('searchbox')).toHaveValue('Tornillo');

    // Está en la barra de filtros y en el estado vacío; cualquiera limpia la URL.
    const [limpiar] = screen.getAllByRole('button', { name: 'Limpiar filtros' });
    if (!limpiar) throw new Error('sin botón para limpiar');
    await usuario.click(limpiar);

    expect(await screen.findByText('Constructora Norte aún no tiene certificados.')).toBeInTheDocument();
    expect(screen.getByRole('searchbox')).toHaveValue('');
    expect(urls).toContain('/api/v1/certificados?limite=25');
  });
});
