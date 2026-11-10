import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type FilaEstadoExpedienteDto, type InformeEstadoExpedientesDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaEstadoExpedientes } from './PaginaEstadoExpedientes';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const FILA: FilaEstadoExpedienteDto = {
  id: 'x1',
  folio: 'NORTE-E-2026-000007',
  tipo: 'Inicial',
  estado: 'Aprobado',
  vinculoId: 'v1',
  tercero: { id: 't1', nombre: 'Ferretería El Tornillo', identificacion: 'CO|NIT|900123456' },
  tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
  flujo: null,
  abiertoEn: '2026-11-02T13:00:00Z',
  cerradoEn: '2026-11-05T13:00:00Z',
  requisitos: 2,
  requisitosAprobados: 2,
  devoluciones: 1,
  diasTotales: 3,
  diasEnCarga: 0.5,
  diasConTercero: 1,
  diasEnRevision: 1.5,
  diasHastaAprobacion: 3,
  slaIncumplido: true,
  areas: [{ areaId: 'a1', codigo: 'COMPRAS', nombre: 'Compras', etapa: 1, estado: 'Aprobada', dias: 1.5, rondas: 2, slaHoras: 24, slaIncumplido: true }],
};

const INFORME: InformeEstadoExpedientesDto = {
  resumen: {
    total: 1,
    porEstado: [{ clave: 'Aprobado', cantidad: 1 }],
    porTipo: [{ clave: 'Inicial', cantidad: 1 }],
    aprobados: 1,
    tiempoPromedioDias: 3,
    tiempoMedianaDias: 3,
    promedioEnCargaDias: 0.5,
    promedioConTerceroDias: 1,
    promedioEnRevisionDias: 1.5,
    devoluciones: 1,
    conSlaIncumplido: 1,
    porArea: [{ areaId: 'a1', codigo: 'COMPRAS', nombre: 'Compras', expedientes: 1, pendientes: 0, promedioDias: 1.5, medianaDias: 1.5, maximoDias: 1.5, slaIncumplidos: 1 }],
  },
  datos: [FILA],
  siguienteCursor: null,
};

const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

function UbicacionActual() {
  return <p>{`Ubicación: ${useLocation().pathname}`}</p>;
}

function montar(ruta = '/e/emp/informes/estado-expedientes') {
  const peticiones: string[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      peticiones.push(url);
      if (url.startsWith('/api/v1/informes/estado-expedientes/')) {
        return Promise.resolve(new Response(new Uint8Array([1, 2, 3]), { headers: { 'Content-Disposition': 'attachment; filename=estado.xlsx' } }));
      }
      if (url.startsWith('/api/v1/informes/estado-expedientes')) return Promise.resolve(json(INFORME));
      if (url.startsWith('/api/v1/areas')) return Promise.resolve(json([{ id: 'a1', codigo: 'COMPRAS', nombre: 'Compras', activa: true, miembros: [] }]));
      return Promise.resolve(json([]));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/e/:empresaId/informes/estado-expedientes" element={<PaginaEstadoExpedientes />} />
            <Route path="/e/:empresaId/expedientes/:expedienteId" element={<UbicacionActual />} />
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

describe('PaginaEstadoExpedientes', () => {
  it('sin expedientes:leer no muestra el informe', () => {
    montar();
    expect(screen.queryByRole('heading', { level: 1, name: 'Informes' })).not.toBeInTheDocument();
  });

  it('muestra el resumen, los tiempos por área y cada expediente con su reparto de días; la fila abre el expediente', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.configuracionLeer]);
    montar();
    expect(await screen.findByRole('heading', { level: 1, name: 'Informes' })).toBeInTheDocument();
    const fila = await screen.findByRole('row', { name: /NORTE-E-2026-000007/ });
    expect(fila).toHaveTextContent('Ferretería El Tornillo');
    expect(fila).toHaveTextContent('0,5 · 1,0 · 1,5');
    expect(fila).toHaveTextContent('2 de 2');
    expect(fila).toHaveTextContent('Incumplido');
    const areas = screen.getByRole('table', { name: 'Tiempos por área' });
    expect(within(areas).getByRole('row', { name: /Compras/ })).toHaveTextContent('1,5');
    expect(screen.getByText('Días hasta aprobar (promedio)')).toBeInTheDocument();

    await userEvent.click(fila);
    expect(await screen.findByText('Ubicación: /e/emp/expedientes/x1')).toBeInTheDocument();
  });

  it('los filtros de la URL van a la consulta y a las descargas de Excel y PDF, sin la paginación', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    const peticiones = montar('/e/emp/informes/estado-expedientes?estado=Aprobado&fecha=Cierre&desde=2026-11-01');
    await screen.findByRole('row', { name: /NORTE-E-2026-000007/ });
    expect(peticiones.find((p) => p.startsWith('/api/v1/informes/estado-expedientes?'))).toMatch(/estado=Aprobado.*desde=2026-11-01.*fecha=Cierre.*limite=50/);

    await userEvent.click(screen.getByRole('button', { name: 'Descargar Excel' }));
    await waitFor(() => expect(peticiones.some((p) => p.startsWith('/api/v1/informes/estado-expedientes/excel?'))).toBe(true));
    const excel = peticiones.find((p) => p.startsWith('/api/v1/informes/estado-expedientes/excel?')) ?? '';
    expect(excel).toContain('estado=Aprobado');
    expect(excel).toContain('fecha=Cierre');
    expect(excel).not.toContain('limite');

    await userEvent.click(screen.getByRole('button', { name: 'Descargar PDF' }));
    await waitFor(() => expect(peticiones.some((p) => p.startsWith('/api/v1/informes/estado-expedientes/pdf?estado=Aprobado'))).toBe(true));
  });

  it('un rango al revés no consulta y lo avisa', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    const peticiones = montar('/e/emp/informes/estado-expedientes?desde=2026-11-05&hasta=2026-11-01');
    expect((await screen.findAllByText('La fecha inicial es posterior a la final.')).length).toBeGreaterThan(0);
    expect(peticiones.some((p) => p.startsWith('/api/v1/informes/estado-expedientes'))).toBe(false);
  });
});
