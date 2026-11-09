import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type UsoIaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PaginaUsoIa } from './PaginaUsoIa';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const USO: UsoIaDto = {
  mes: '2026-11',
  plan: {
    configurado: true,
    validacionesIncluidasMes: 50,
    adicionalHabilitado: true,
    topeAdicionalMes: 20,
    llaveConfigurada: true,
    modeloPorDefecto: 'gpt-6-luna',
    modeloEscalamiento: 'gpt-6.1-terra',
    umbralConfianza: 0.85,
    activo: true,
  },
  incluidasConsumidas: 42,
  adicionalesConsumidas: 3,
  incluidasRestantes: 8,
  adicionalesRestantes: 17,
  analisisPagados: 30,
  tokensEntrada: 1_234_567,
  tokensEntradaEnCache: 2_000,
  tokensSalida: 45_000,
  costoUsd: '0.1234',
  porDia: [
    { fecha: '2026-11-03', incluidas: 4, adicionales: 0, analisisPagados: 3, tokensEntrada: 1000, tokensSalida: 100, costoUsd: 0.01 },
    { fecha: '2026-11-10', incluidas: 2, adicionales: 3, analisisPagados: 2, tokensEntrada: 800, tokensSalida: 90, costoUsd: 0.02 },
  ],
};

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

function montar(uso: (url: URL) => UsoIaDto = () => USO, ruta = '/e/emp/uso-ia') {
  const urls: URL[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      const url = new URL(typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url, 'http://localhost');
      urls.push(url);
      return Promise.resolve(json(uso(url)));
    }),
  });
  const router = createMemoryRouter([{ path: '/e/:empresaId/uso-ia', element: <PaginaUsoIa /> }], { initialEntries: [ruta] });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { urls, router };
}

beforeEach(() => {
  // Solo el reloj de Date: el mes actual es noviembre de 2026 (user-event sigue con temporizadores reales).
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-11-15T15:00:00Z'));
});

afterEach(() => {
  vi.useRealTimers();
  reiniciarCliente();
  permisos.scopes = new Set();
});

describe('Plan y uso de IA', () => {
  it('sin configuracion:leer dice que no hay acceso y no pide nada', () => {
    const { urls } = montar();
    expect(screen.getByRole('alert')).toHaveTextContent('Plan y uso de IA');
    expect(urls).toHaveLength(0);
  });

  it('con plan muestra el uso del mes actual, la barra ámbar desde el 80 % y la serie por día', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    const { urls } = montar();

    expect(await screen.findByText('42 de 50')).toBeInTheDocument();
    expect(urls[0]?.pathname).toBe('/api/v1/ia/uso');
    expect(urls[0]?.searchParams.get('mes')).toBe('2026-11');
    expect(screen.getByRole('combobox', { name: 'Mes' })).toHaveTextContent('Noviembre de 2026');

    const barra = screen.getByRole('progressbar', { name: /42 de 50 validaciones incluidas usadas/ });
    expect(barra).toHaveAttribute('data-nivel', 'alerta');
    expect(barra).toHaveAttribute('aria-valuenow', '84');
    expect(screen.getByText(/Usaste el 84\s?% de las incluidas/)).toBeInTheDocument();

    const adicionales = within(screen.getByRole('region', { name: 'Validaciones adicionales' }));
    expect(adicionales.getByText('3')).toBeInTheDocument();
    expect(adicionales.getByText('Tope de 20 al mes · quedan 17')).toBeInTheDocument();
    expect(screen.getByText('Llave de OpenAI configurada: sí')).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Costo real' })).getByText(/US\$\s0,1234/)).toBeInTheDocument();
    expect(screen.getByText('1.234.567')).toBeInTheDocument();

    const tabla = screen.getByRole('table', { name: 'Validaciones por día' });
    const filas = within(tabla).getAllByRole('row').slice(1);
    expect(filas.map((f) => within(f).getAllByRole('cell').map((c) => c.textContent))).toEqual([
      ['3', '4', '0'],
      ['10', '2', '3'],
    ]);
    expect(screen.getByText(/Si otra empresa ya pagó el análisis/)).toBeInTheDocument();
  });

  it('al agotar las incluidas la barra queda roja; sin adicional lo dice', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    montar(() => ({ ...USO, incluidasConsumidas: 50, incluidasRestantes: 0, plan: { ...USO.plan, adicionalHabilitado: false, topeAdicionalMes: null } }));

    const barra = await screen.findByRole('progressbar', { name: /50 de 50/ });
    expect(barra).toHaveAttribute('data-nivel', 'agotado');
    expect(screen.getByText('Agotaste las incluidas de este mes.')).toBeInTheDocument();
    expect(screen.getByText('Sin adicional')).toBeInTheDocument();
  });

  it('sin plan activo explica que la revisión automática no está contratada', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    montar(() => ({ ...USO, plan: { ...USO.plan, configurado: true, activo: false } }));

    expect(await screen.findByText('La revisión automática no está contratada')).toBeInTheDocument();
    expect(screen.getByText(/lo configura Validasoft desde Plataforma/)).toBeInTheDocument();
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('cambiar el mes lo escribe en la URL y pide ese mes', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    const { urls, router } = montar((url) => ({ ...USO, mes: url.searchParams.get('mes') ?? USO.mes, porDia: [] }));
    const usuario = userEvent.setup();
    await screen.findByText('42 de 50');

    await usuario.click(screen.getByRole('combobox', { name: 'Mes' }));
    const opciones = screen.getAllByRole('option');
    expect(opciones).toHaveLength(12);
    expect(opciones.at(-1)).toHaveTextContent('Diciembre de 2025');
    await usuario.click(screen.getByRole('option', { name: 'Octubre de 2026' }));

    await waitFor(() => expect(router.state.location.search).toBe('?mes=2026-10'));
    await waitFor(() => expect(urls.at(-1)?.searchParams.get('mes')).toBe('2026-10'));
    expect(await screen.findByText('Sin validaciones en este mes.')).toBeInTheDocument();
  });

  it('un mes válido en la URL se respeta al abrir', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    const { urls } = montar(undefined, '/e/emp/uso-ia?mes=2026-09');
    await screen.findByText('42 de 50');
    expect(urls[0]?.searchParams.get('mes')).toBe('2026-09');
  });
});
