import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type IndicadoresDto, type MisPendientesDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaInicio } from './PaginaInicio';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const AL_DIA: MisPendientesDto = {
  porArea: [],
  revalidaciones: 0,
  revalidacionesBloqueantes: 0,
  cuentasPorAprobar: 0,
  aperturasPendientes: 0,
  documentosPorVencer: 0,
  documentosVencidos: 0,
};

const PENDIENTES: MisPendientesDto = {
  porArea: [
    { areaId: 'a-compras', codigo: 'COMPRAS', nombre: 'Compras', cantidad: 3, masAntiguoDias: 5 },
    { areaId: 'a-juridica', codigo: 'JURIDICA', nombre: 'Jurídica', cantidad: 0, masAntiguoDias: 0 },
  ],
  revalidaciones: 4,
  revalidacionesBloqueantes: 1,
  cuentasPorAprobar: 2,
  aperturasPendientes: 0,
  documentosPorVencer: 0,
  documentosVencidos: 3,
};

const INDICADORES: IndicadoresDto = {
  dias: 30,
  vinculosActivos: 1234,
  vinculosEnValidacion: 8,
  vinculosInvitados: 5,
  vinculosVencidos: 2,
  vinculosBloqueados: 1,
  expedientesEnCurso: 9,
  expedientesObservados: 3,
  expedientesAprobados: 12,
  expedientesRechazados: 1,
  tiempoPromedioDias: 4.25,
  tiempoMedianaDias: null,
  certificadosVigentes: 40,
  certificadosConPendientes: 6,
  certificadosNoValidos: 2,
};

const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

function UbicacionActual() {
  const { pathname, search } = useLocation();
  return <p>{`Ubicación: ${pathname}${search}`}</p>;
}

/** API falsa: `/mis-pendientes` devuelve `pendientes`; `/indicadores`, los indicadores con la ventana pedida. */
function montar(pendientes: MisPendientesDto, indicadores: IndicadoresDto = INDICADORES, ruta = '/e/emp/inicio') {
  const urls: string[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      urls.push(url);
      if (url.startsWith('/api/v1/mis-pendientes')) return Promise.resolve(json(pendientes));
      const dias = Number(new URL(url, 'http://local').searchParams.get('dias'));
      return Promise.resolve(json({ ...indicadores, dias }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/e/:empresaId/inicio" element={<PaginaInicio />} />
            <Route path="*" element={<UbicacionActual />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return urls;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  vi.restoreAllMocks();
});

describe('Inicio', () => {
  it('sin expedientes:leer no consulta nada', () => {
    const urls = montar(PENDIENTES);
    expect(screen.queryByRole('region', { name: 'Lo que te toca' })).not.toBeInTheDocument();
    expect(urls).toEqual([]);
  });

  it('lista las áreas con pendientes (enlace a la bandeja filtrada) y las que están al día, y los demás pendientes', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.vinculosLeer]);
    const usuario = userEvent.setup();
    montar(PENDIENTES);

    const toca = await screen.findByRole('region', { name: 'Lo que te toca' });
    const compras = await within(toca).findByRole('link', { name: 'Compras: 3 expedientes por revisar · el más antiguo hace 5 días' });
    expect(within(toca).getByText('Jurídica: Al día')).toBeInTheDocument();
    expect(within(toca).queryByRole('link', { name: /Jurídica/ })).not.toBeInTheDocument();
    expect(within(toca).getByRole('link', { name: '4 revalidaciones por revisar' })).toHaveAttribute('href', '/e/emp/revalidaciones');
    expect(within(toca).getByText('1 ya bloquea el certificado')).toBeInTheDocument();
    // Las cuentas no tienen pantalla propia: texto sin enlace.
    expect(within(toca).getByText('2 cuentas bancarias por aprobar')).toBeInTheDocument();
    expect(within(toca).queryByRole('link', { name: /cuentas bancarias/ })).not.toBeInTheDocument();
    expect(within(toca).getByRole('link', { name: '3 documentos vencidos' })).toHaveAttribute('href', '/e/emp/vencimientos?estado=Vencida');
    // Lo que está en cero no se muestra.
    expect(within(toca).queryByText(/aperturas? pendientes?/)).not.toBeInTheDocument();
    expect(within(toca).queryByText(/vencen en los próximos/)).not.toBeInTheDocument();

    await usuario.click(compras);
    expect(await screen.findByText('Ubicación: /e/emp/bandeja?area=a-compras')).toBeInTheDocument();
  });

  it('si todo está en cero dice «Estás al día»', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    montar({ ...AL_DIA, porArea: [{ areaId: 'a1', codigo: 'C', nombre: 'Compras', cantidad: 0, masAntiguoDias: 0 }] });
    const toca = await screen.findByRole('region', { name: 'Lo que te toca' });
    expect(await within(toca).findByText('Estás al día')).toBeInTheDocument();
    expect(within(toca).queryByText('Compras: Al día')).not.toBeInTheDocument();
  });

  it('muestra los indicadores con «—» donde no hay dato y enlaza los vínculos a su lista filtrada', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.vinculosLeer]);
    const urls = montar(AL_DIA);

    const vinculos = await screen.findByRole('region', { name: 'Vínculos' });
    expect(within(vinculos).getByText('1.234')).toBeInTheDocument();
    expect(within(vinculos).getByRole('link', { name: 'Vencidos' })).toHaveAttribute('href', '/e/emp/vinculos?estado=Vencido');
    expect(within(vinculos).getByRole('link', { name: 'Bloqueados' })).toHaveAttribute('href', '/e/emp/vinculos?estado=Bloqueado');
    // Activos suma los que están por vencer: sin enlace a un estado único.
    expect(within(vinculos).queryByRole('link', { name: 'Activos' })).not.toBeInTheDocument();

    const expedientes = screen.getByRole('region', { name: 'Expedientes' });
    expect(within(expedientes).getByText('4,3')).toBeInTheDocument();
    expect(within(expedientes).getByText('—')).toBeInTheDocument();
    expect(within(expedientes).getByText('Sin expedientes aprobados en la ventana')).toBeInTheDocument();
    expect(within(expedientes).getByText(/los últimos 30 días/)).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Certificados' })).getByText('40')).toBeInTheDocument();
    expect(urls).toContain('/api/v1/indicadores?dias=30');
  });

  it('muestra cuántos vínculos están por vencer (vigencia periódica) y enlaza a su lista; en cero no hay tarjeta', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.vinculosLeer]);
    montar(AL_DIA, { ...INDICADORES, vinculosPorVencer: 7 });
    const vinculos = await screen.findByRole('region', { name: 'Vínculos' });
    expect(within(vinculos).getByText('7')).toBeInTheDocument();
    expect(within(vinculos).getByRole('link', { name: 'Por vencer' })).toHaveAttribute('href', '/e/emp/vinculos?estado=PorVencer');
  });

  it('sin vínculos por vencer no aparece la tarjeta «Por vencer»', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.vinculosLeer]);
    montar(AL_DIA, { ...INDICADORES, vinculosPorVencer: 0 });
    const vinculos = await screen.findByRole('region', { name: 'Vínculos' });
    expect(within(vinculos).queryByText('Por vencer')).not.toBeInTheDocument();
  });

  it('sin vinculos:leer los indicadores de vínculos no enlazan', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    montar(AL_DIA);
    const vinculos = await screen.findByRole('region', { name: 'Vínculos' });
    expect(within(vinculos).queryByRole('link')).not.toBeInTheDocument();
  });

  it('al cambiar la ventana pide los indicadores de 7 días', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    const usuario = userEvent.setup();
    const urls = montar(AL_DIA);
    await screen.findByRole('region', { name: 'Expedientes' });

    const ventana = screen.getByRole('group', { name: 'Ventana de los indicadores' });
    expect(within(ventana).getByRole('button', { name: '30 días' })).toHaveAttribute('aria-pressed', 'true');
    await usuario.click(within(ventana).getByRole('button', { name: '7 días' }));

    await waitFor(() => expect(urls).toContain('/api/v1/indicadores?dias=7'));
    expect(await screen.findByText(/los últimos 7 días/)).toBeInTheDocument();
    expect(within(ventana).getByRole('button', { name: '7 días' })).toHaveAttribute('aria-pressed', 'true');
  });
});
