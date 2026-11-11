import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type InformeSlaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaInformeSla } from './PaginaInformeSla';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const INFORME: InformeSlaDto = {
  resumen: { expedientes: 14, rondas: 20, aDentroDelPlazo: 17, fueraDelPlazo: 3, cumplimiento: 85, vencidosAhora: 2 },
  porArea: [
    { areaId: 'a1', codigo: 'COMPRAS', nombre: 'Compras', slaHoras: 24, rondas: 12, aDentroDelPlazo: 11, fueraDelPlazo: 1, cumplimiento: 91.7, promedioDias: 0.8, vencidosAhora: 1 },
    { areaId: 'a2', codigo: 'JURIDICA', nombre: 'Jurídica', slaHoras: null, rondas: 8, aDentroDelPlazo: 6, fueraDelPlazo: 2, cumplimiento: '75', promedioDias: '1.5', vencidosAhora: 1 },
  ],
  vencidos: [
    { expedienteId: 'x1', folio: 'NORTE-E-2026-000007', areaId: 'a1', areaNombre: 'Compras', tercero: 'Ferretería El Tornillo', vence: '2026-11-09T15:00:00Z', horasDeRetraso: '75.2' },
    { expedienteId: 'x2', folio: 'NORTE-E-2026-000009', areaId: 'a2', areaNombre: 'Jurídica', tercero: 'Aceros del Norte', vence: '2026-11-11T20:00:00Z', horasDeRetraso: 5.4 },
  ],
};

const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

function montar(ruta = '/e/emp/informes/sla', informe: InformeSlaDto = INFORME) {
  const peticiones: string[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      peticiones.push(url);
      if (url.startsWith('/api/v1/informes/sla/excel')) {
        return Promise.resolve(new Response(new Uint8Array([1, 2, 3]), { headers: { 'Content-Disposition': 'attachment; filename=sla.xlsx' } }));
      }
      if (url.startsWith('/api/v1/informes/sla')) return Promise.resolve(json(informe));
      if (url.startsWith('/api/v1/areas')) return Promise.resolve(json([{ id: 'a1', codigo: 'COMPRAS', nombre: 'Compras', activa: true, miembros: [] }]));
      return Promise.resolve(json([]));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/e/:empresaId/informes/sla" element={<PaginaInformeSla />} />
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
});

describe('Informe de cumplimiento del plazo de revisión', () => {
  it('sin expedientes:leer no muestra el informe ni consulta', () => {
    const peticiones = montar();
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(peticiones).toHaveLength(0);
  });

  it('es una pestaña de Informes y muestra el resumen, el cumplimiento de cada área y los vencidos con enlace al expediente', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.configuracionLeer]);
    montar();

    expect(await screen.findByRole('tab', { name: 'Plazo de revisión', selected: true })).toHaveAttribute('href', '/e/emp/informes/sla');
    const resumen = await screen.findByRole('region', { name: 'Cumplimiento del plazo de revisión' });
    expect(within(resumen).getByText('85 %')).toBeInTheDocument();
    expect(within(resumen).getByText('Vencidos ahora').closest('div')).toHaveTextContent('2');
    expect(within(resumen).getByText('Revisiones con plazo').closest('div')).toHaveTextContent('20');

    const areas = screen.getByRole('table', { name: 'Cumplimiento por área' });
    const compras = within(areas).getByRole('row', { name: /Compras/ });
    expect(compras).toHaveTextContent('24 h');
    expect(compras).toHaveTextContent('91,7 %');
    expect(within(compras).getByRole('progressbar', { name: 'Cumplimiento de Compras: 91,7 %' })).toHaveAttribute('aria-valuenow', '91.7');
    const juridica = within(areas).getByRole('row', { name: /Jurídica/ });
    expect(juridica).toHaveTextContent('Sin plazo');
    expect(juridica).toHaveTextContent('75 %');
    expect(juridica).toHaveTextContent('1,5');

    const vencidos = screen.getByRole('table', { name: 'Vencidos ahora' });
    const primero = within(vencidos).getByRole('link', { name: 'Abrir el expediente NORTE-E-2026-000007 de Ferretería El Tornillo' });
    expect(primero).toHaveAttribute('href', '/e/emp/expedientes/x1');
    expect(within(primero.closest('tr') as HTMLElement).getByText('3 d 3 h')).toBeInTheDocument();
    expect(within(within(vencidos).getByRole('link', { name: /000009/ }).closest('tr') as HTMLElement).getByText('5 horas')).toBeInTheDocument();
  });

  it('los filtros de la URL van a la consulta y a la descarga de Excel', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    const peticiones = montar('/e/emp/informes/sla?areaId=a1&desde=2026-11-01');
    await screen.findByRole('table', { name: 'Cumplimiento por área' });
    expect(peticiones.find((p) => p.startsWith('/api/v1/informes/sla?'))).toMatch(/areaId=a1.*desde=2026-11-01/);

    await userEvent.click(screen.getByRole('button', { name: 'Descargar Excel' }));
    await waitFor(() => expect(peticiones.some((p) => p.startsWith('/api/v1/informes/sla/excel?'))).toBe(true));
    const excel = peticiones.find((p) => p.startsWith('/api/v1/informes/sla/excel?')) ?? '';
    expect(excel).toContain('areaId=a1');
    expect(excel).toContain('desde=2026-11-01');
  });

  it('sin revisiones con plazo lo dice y no deja descargar un Excel vacío', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    montar('/e/emp/informes/sla', {
      resumen: { expedientes: 0, rondas: 0, aDentroDelPlazo: 0, fueraDelPlazo: 0, cumplimiento: null, vencidosAhora: 0 },
      porArea: [],
      vencidos: [],
    });
    expect(await screen.findByText('Ninguna área tuvo revisiones con plazo en este periodo.')).toBeInTheDocument();
    expect(screen.getByText('Ninguna revisión está vencida ahora.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Descargar Excel' })).toBeDisabled();
    const resumen = screen.getByRole('region', { name: 'Cumplimiento del plazo de revisión' });
    expect(within(resumen).getByText('Cumplimiento').closest('div')).toHaveTextContent('—');
  });

  it('un rango al revés no consulta y lo avisa', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    const peticiones = montar('/e/emp/informes/sla?desde=2026-11-05&hasta=2026-11-01');
    expect((await screen.findAllByText('La fecha inicial es posterior a la final.')).length).toBeGreaterThan(0);
    expect(peticiones.some((p) => p.startsWith('/api/v1/informes/sla'))).toBe(false);
  });
});
