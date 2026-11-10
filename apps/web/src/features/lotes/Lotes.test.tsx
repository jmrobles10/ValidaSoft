import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, SCOPES, type FilaLoteDto, type LoteDto, type VistaPreviaLoteDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { describe, expect, it, vi } from 'vitest';
import { DetalleLote } from './DetalleLote';
import { PaginaNuevoLote } from './PaginaNuevoLote';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>(['vinculos:leer', 'vinculos:escribir']) }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

const VISTA: VistaPreviaLoteDto = {
  archivo: 'proveedores.csv',
  total: 3,
  validas: 2,
  invalidas: 1,
  duplicadas: 0,
  filas: [
    { n: 1, estado: 'Valida', datos: { tipoIdentificacion: 'NIT', numeroIdentificacion: '900123456', nombre: 'Aceros', correo: 'a@x.co', tipoVinculo: 'PROVEEDOR' }, errores: [], duplicadaDe: null, accionPrevista: 'Invitado' },
    { n: 2, estado: 'Invalida', datos: { tipoIdentificacion: 'NIT', numeroIdentificacion: '800', nombre: 'Malo', correo: 'malo', tipoVinculo: 'PROVEEDOR' }, errores: [{ campo: 'correo', codigo: 'correo-invalido', mensaje: '«malo» no es un correo válido.' }], duplicadaDe: null, accionPrevista: null },
    { n: 3, estado: 'Valida', datos: { tipoIdentificacion: 'CC', numeroIdentificacion: '1020', nombre: 'Ana', correo: 'ana@x.co', tipoVinculo: 'PROVEEDOR' }, errores: [], duplicadaDe: null, accionPrevista: 'Reinvitado' },
  ],
};

function lote(cambios: Partial<LoteDto> = {}): LoteDto {
  return {
    id: 'l1', nombre: 'Proveedores', archivo: 'proveedores.csv', estado: 'Procesando', vigenciaHoras: 168, creadoPor: 'Usuario:u1',
    creadoEn: '2026-11-10T12:00:00Z', actualizadoEn: '2026-11-10T12:00:00Z', terminadoEn: null, reintentos: 0, version: 3,
    totales: { filas: 3, procesadas: 1, pendientes: 1, exitosas: 1, fallidas: 0, duplicadas: 0, invalidas: 1 },
    ...cambios,
  };
}

const FILA: FilaLoteDto = {
  n: 1, identificacion: 'CO|NIT|900123456', nombre: 'Aceros', correo: 'a@x.co', tipoVinculo: 'PROVEEDOR', estado: 'Exitosa', accion: 'Invitado', detalle: null,
  errores: [], duplicadaDe: null, terceroId: 't1', vinculoId: 'v1', terceroCreado: true, intentos: 1, procesadaEn: '2026-11-10T12:00:01Z',
};

function montar(ruta: string, responder: (url: string, metodo: string) => Response) {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = init?.method ?? 'GET';
      peticiones.push({ url, metodo, cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : null });
      return Promise.resolve(responder(url, metodo));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/e/:empresaId/invitaciones/lotes/nuevo" element={<PaginaNuevoLote />} />
            <Route path="/e/:empresaId/invitaciones/lotes/:loteId" element={<DetalleLote />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

describe('invitación masiva por lote', () => {
  it('sube el archivo, muestra los errores por fila y crea el lote solo con las válidas', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar('/e/emp/invitaciones/lotes/nuevo', (url, metodo) => {
      if (url.includes('/vista-previa')) return json(VISTA);
      if (metodo === 'POST') return json(lote(), 202);
      if (url.endsWith('/filas') || url.includes('/filas?')) return json({ datos: [FILA], siguienteCursor: null });
      return json(lote());
    });

    const archivo = new File(['Tipo de identificación;Número de identificación\n'], 'proveedores.csv', { type: 'text/csv' });
    const entrada = document.querySelector<HTMLInputElement>('input[type="file"]');
    if (!entrada) throw new Error('Sin selector de archivo');
    await usuario.upload(entrada, archivo);

    expect(await screen.findByText('3 filas: 2 válidas, 1 con errores, 0 repetidas.')).toBeInTheDocument();
    // Con errores, la tabla abre filtrada a lo que no se invitará.
    const tabla = screen.getByRole('table');
    expect(within(tabla).getByText('«malo» no es un correo válido.')).toBeInTheDocument();
    expect(within(tabla).queryByText('Aceros')).not.toBeInTheDocument();
    await usuario.click(screen.getByLabelText('Ver solo filas con errores'));
    expect(within(screen.getByRole('table')).getByText('Se reenvía la invitación')).toBeInTheDocument();

    await usuario.type(screen.getByLabelText('Nombre del lote'), 'Noviembre');
    await usuario.click(screen.getByRole('button', { name: 'Invitar a 2 terceros' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.url === '/api/v1/invitaciones/lotes')).toBe(true));
    const creacion = peticiones.find((p) => p.metodo === 'POST' && p.url === '/api/v1/invitaciones/lotes');
    expect(creacion?.cuerpo).toMatchObject({ nombre: 'Noviembre', vigenciaHoras: 168, archivo: { nombre: 'proveedores.csv' } });
    // Va al detalle del lote creado.
    expect(await screen.findByRole('button', { name: 'Pausar' })).toBeInTheDocument();
  });

  it('el detalle muestra el avance y las filas, y pausa el lote en proceso', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar('/e/emp/invitaciones/lotes/l1', (url, metodo) => {
      if (metodo === 'POST') return json(lote({ estado: 'Pausado' }));
      if (url.includes('/filas')) return json({ datos: [FILA], siguienteCursor: null });
      return json(lote());
    });

    expect(await screen.findByText('1 de 2')).toBeInTheDocument();
    expect(await screen.findByText('NIT 900123456')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Invitado' })).toHaveAttribute('href', '/e/emp/vinculos/v1');

    await usuario.click(screen.getByRole('button', { name: 'Pausar' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.url === '/api/v1/invitaciones/lotes/l1/pausar')).toBe(true));
    expect(SCOPES.vinculosEscribir).toBe('vinculos:escribir');
  });
});
