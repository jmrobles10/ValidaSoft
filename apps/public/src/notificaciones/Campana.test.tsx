import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type BandejaNotificacionesTerceroDto, type NotificacionTerceroDto, type PreferenciasNotificacionDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { crearCuerpoSse } from '../pruebas/sse';
import { useSesionTercero } from '../sesion/sesionTercero';
import { useAvisos } from '../shared/useAvisos';
import { Campana } from './Campana';

const json = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': estado >= 400 ? 'application/problem+json' : 'application/json' } });

const notificacion = (id: string, cambios: Partial<NotificacionTerceroDto> = {}): NotificacionTerceroDto => ({
  id,
  tipo: 'tercero.observado',
  titulo: 'Tienes observaciones por corregir',
  mensaje: 'Constructora Norte revisó tu validación NORTE-E-1 y te pidió corregir 1 punto.',
  empresa: { id: 'emp-norte', nombre: 'Constructora Norte', logoUrl: null, colorMarca: '#0a6' },
  expedienteId: 'exp-1',
  vinculoId: 'vin-1',
  folio: 'NORTE-E-1',
  enlace: '/expediente/exp-1',
  creadaEn: new Date(Date.now() - 5 * 60_000).toISOString(),
  leidaEn: null,
  ...cambios,
});

const SIN_LEER = notificacion('n1');
const DE_SUR = notificacion('n2', {
  tipo: 'tercero.documento_por_vencer',
  titulo: 'Documento por vencer',
  mensaje: 'Tu RUT vence el 8 de diciembre de 2026 para Ferretería Sur (28 días).',
  empresa: { id: 'emp-sur', nombre: 'Ferretería Sur', logoUrl: null },
  expedienteId: null,
  enlace: '/boveda',
  leidaEn: '2026-11-09T10:00:00Z',
});
const PREFERENCIAS: PreferenciasNotificacionDto = {
  correo: { 'tercero.observado': true, 'tercero.recordatorio': true, 'tercero.documento_por_vencer': false },
  tiposConCorreo: ['tercero.observado', 'tercero.recordatorio', 'tercero.documento_por_vencer'],
};

interface Peticion {
  metodo: string;
  ruta: string;
  cuerpo: unknown;
}

function Ubicacion() {
  const { pathname } = useLocation();
  return <p>{`Estás en ${pathname}`}</p>;
}

function montar(bandeja: BandejaNotificacionesTerceroDto) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    obtenerToken: () => 'tok',
    fetch: vi.fn((url: RequestInfo | URL, init?: RequestInit) => {
      const direccion = new URL(url instanceof Request ? url.url : url.toString(), 'http://localhost');
      const metodo = init?.method ?? 'GET';
      peticiones.push({ metodo, ruta: direccion.pathname, cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : null });
      // El stream en tiempo real queda abierto y en silencio (lo prueba tiempoReal.test.tsx).
      if (direccion.pathname.endsWith('/stream')) return Promise.resolve(crearCuerpoSse(init?.signal).respuesta);
      if (direccion.pathname === '/public/v1/notificaciones' && metodo === 'GET') return Promise.resolve(json(200, bandeja));
      if (direccion.pathname === '/public/v1/notificaciones/leidas') return Promise.resolve(json(200, { marcadas: bandeja.noLeidas }));
      if (direccion.pathname.endsWith('/leida')) return Promise.resolve(json(200, { ...SIN_LEER, leidaEn: new Date().toISOString() }));
      if (direccion.pathname === '/public/v1/preferencias-notificacion' && metodo === 'GET') return Promise.resolve(json(200, PREFERENCIAS));
      if (direccion.pathname === '/public/v1/preferencias-notificacion') return Promise.resolve(json(200, { ...PREFERENCIAS, correo: { ...PREFERENCIAS.correo, 'tercero.observado': false } }));
      return Promise.resolve(json(404, { codigo: 'no-encontrado' }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={['/empresas']}>
          <Campana />
          <Routes>
            <Route path="*" element={<Ubicacion />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

beforeEach(() => {
  useSesionTercero.setState({ sesion: { accessToken: 'tok', terceroId: 't1' } as never, estado: 'activa' });
});

afterEach(() => {
  reiniciarCliente();
  useAvisos.setState({ actual: null });
  useSesionTercero.setState({ sesion: null, estado: 'sin-sesion' });
});

describe('campana del tercero', () => {
  it('muestra el contador de todas sus empresas y cada aviso con su empresa; sin leer primero marcado', async () => {
    const usuario = userEvent.setup();
    montar({ datos: [SIN_LEER, DE_SUR], siguienteCursor: null, noLeidas: 1 });

    await usuario.click(await screen.findByRole('button', { name: 'Notificaciones, 1 sin leer' }));
    const panel = await screen.findByRole('dialog', { name: 'Notificaciones' });
    const items = within(panel).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(within(items[0]!).getByText(/Sin leer/)).toBeInTheDocument();
    expect(within(items[0]!).getByText('Tienes observaciones por corregir')).toBeInTheDocument();
    expect(within(items[0]!).getByText(/Constructora Norte revisó tu validación/)).toBeInTheDocument();
    expect(within(items[0]!).getByText('hace 5 minutos')).toBeInTheDocument();
    expect(within(items[1]!).queryByText(/Sin leer/)).not.toBeInTheDocument();
    // La marca de cada empresa: su inicial cuando no tiene logo.
    expect(within(items[0]!).getByText('C')).toBeInTheDocument();
    expect(within(items[1]!).getByText('F')).toBeInTheDocument();
  });

  it('un clic marca el aviso como leído y lleva a lo que hay que hacer', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar({ datos: [SIN_LEER, DE_SUR], siguienteCursor: null, noLeidas: 1 });

    await usuario.click(await screen.findByRole('button', { name: 'Notificaciones, 1 sin leer' }));
    const panel = await screen.findByRole('dialog', { name: 'Notificaciones' });
    await usuario.click(within(within(panel).getAllByRole('listitem')[0]!).getByRole('button'));

    expect(await screen.findByText('Estás en /expediente/exp-1')).toBeInTheDocument();
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === '/public/v1/notificaciones/n1/leida')).toBe(true));
    expect(await screen.findByRole('button', { name: 'Notificaciones' })).toBeInTheDocument(); // contador apagado
  });

  it('«Marcar todas como leídas» llama a la API, apaga el contador y lo confirma', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar({ datos: [SIN_LEER, notificacion('n3')], siguienteCursor: null, noLeidas: 2 });

    await usuario.click(await screen.findByRole('button', { name: 'Notificaciones, 2 sin leer' }));
    await usuario.click(await screen.findByRole('button', { name: 'Marcar todas como leídas' }));

    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Marcamos 2 notificaciones como leídas.'));
    expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === '/public/v1/notificaciones/leidas')).toBe(true);
    expect(screen.queryByText(/Sin leer/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Marcar todas como leídas' })).toBeDisabled();
  });

  it('sin notificaciones muestra el estado vacío y no deja marcar todas', async () => {
    const usuario = userEvent.setup();
    montar({ datos: [], siguienteCursor: null, noLeidas: 0 });

    await usuario.click(await screen.findByRole('button', { name: 'Notificaciones' }));
    expect(await screen.findByText('No tienes notificaciones')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Marcar todas como leídas' })).toBeDisabled();
  });

  it('las preferencias de correo van por tipo, valen para todas las empresas y solo se guarda lo que cambió', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar({ datos: [SIN_LEER], siguienteCursor: null, noLeidas: 1 });

    await usuario.click(await screen.findByRole('button', { name: 'Notificaciones, 1 sin leer' }));
    await usuario.click(await screen.findByRole('button', { name: 'Preferencias de correo' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Preferencias de correo' });
    expect(await within(dialogo).findByRole('switch', { name: /Observaciones de una empresa/ })).toBeChecked();
    expect(within(dialogo).getByRole('switch', { name: /Documento por vencer/ })).not.toBeChecked();
    expect(within(dialogo).getByRole('button', { name: 'Guardar' })).toBeDisabled();

    await usuario.click(within(dialogo).getByRole('switch', { name: /Observaciones de una empresa/ }));
    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Guardamos tus preferencias de correo.'));
    const guardado = peticiones.find((p) => p.metodo === 'PUT');
    expect(guardado?.ruta).toBe('/public/v1/preferencias-notificacion');
    expect(guardado?.cuerpo).toEqual({ correo: { 'tercero.observado': false } });
  });
});
