import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type SesionActivaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useSesionTercero } from '../sesion/sesionTercero';
import { useAvisos } from '../shared/useAvisos';
import { SeccionSesiones } from './SeccionSesiones';

const json = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': estado >= 400 ? 'application/problem+json' : 'application/json' } });

const CHROME = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36';
const IPHONE = 'Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Version/17.0 Mobile/15E148 Safari/604.1';

function sesion(cambios: Partial<SesionActivaDto> = {}): SesionActivaDto {
  return {
    id: 's1',
    correo: 'ana@aceros.co',
    creadaEn: '2026-11-14T08:00:00Z',
    expiraEn: '2026-11-14T16:00:00Z',
    ultimoUsoEn: '2026-11-14T09:30:00Z',
    dispositivo: CHROME,
    ip: '10.0.0.7',
    esActual: false,
    ...cambios,
  };
}

interface Peticion {
  metodo: string;
  url: URL;
}

function montar(sesiones: SesionActivaDto[], alCerrar: () => Response = () => new Response(null, { status: 204 })) {
  const mutaciones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost');
      const metodo = init?.method ?? 'GET';
      if (metodo === 'GET') return Promise.resolve(json(200, sesiones));
      mutaciones.push({ metodo, url });
      return Promise.resolve(alCerrar());
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <SeccionSesiones />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return mutaciones;
}

afterEach(() => {
  reiniciarCliente();
  useAvisos.setState({ actual: null });
  useSesionTercero.setState({ estado: 'sin-sesion', sesion: null });
});

describe('Tus sesiones abiertas', () => {
  it('lista cada sesión con su navegador, su correo, su dirección y cuándo se abrió y se usó; la de este navegador va primero y marcada', async () => {
    montar([sesion({ id: 'otra', dispositivo: IPHONE, correo: 'luis@aceros.co', ip: null }), sesion({ id: 'esta', esActual: true })]);

    const [primera, segunda] = await screen.findAllByRole('listitem');
    expect(within(primera!).getByRole('heading', { name: /Chrome en Windows/ })).toBeInTheDocument();
    expect(within(primera!).getByText('Esta sesión')).toBeInTheDocument();
    expect(within(primera!).getByText(/ana@aceros\.co · 10\.0\.0\.7/)).toBeInTheDocument();
    expect(within(primera!).getByText(/Abierta el 14\/11\/2026/)).toBeInTheDocument();
    expect(within(primera!).getByText(/Último uso 14\/11\/2026/)).toBeInTheDocument();
    expect(within(segunda!).getByRole('heading', { name: /Safari en iOS/ })).toBeInTheDocument();
    expect(within(segunda!).queryByText('Esta sesión')).not.toBeInTheDocument();
    expect(within(segunda!).getByText('luis@aceros.co')).toBeInTheDocument();
  });

  it('cerrar la sesión de otro navegador pide cerrarla en el servidor y lo avisa; la lista no pide confirmación', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([sesion({ id: 'esta', esActual: true }), sesion({ id: 'otra', dispositivo: IPHONE, correo: 'luis@aceros.co' })]);

    await usuario.click(await screen.findByRole('button', { name: 'Cerrar la sesión de Safari en iOS (luis@aceros.co)' }));

    await waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.metodo).toBe('POST');
    expect(mutaciones[0]?.url.pathname).toBe('/public/v1/sesiones/otra/cerrar');
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Cerraste la sesión.'));
  });

  it('cerrar la de este navegador es «Salir»: borra la sesión de la pantalla en vez de llamar a «cerrar una sesión»', async () => {
    const usuario = userEvent.setup();
    const salir = vi.fn(() => Promise.resolve());
    useSesionTercero.setState({ salir });
    const mutaciones = montar([sesion({ id: 'esta', esActual: true })]);

    await usuario.click(await screen.findByRole('button', { name: 'Cerrar esta sesión' }));

    expect(salir).toHaveBeenCalledTimes(1);
    expect(mutaciones).toHaveLength(0);
  });

  it('si no se pudo cerrar, lo dice y la sesión sigue en la lista', async () => {
    const usuario = userEvent.setup();
    montar([sesion({ id: 'otra' })], () => json(404, { type: 'x', title: 'sesion-no-encontrada', status: 404, detail: 'no', codigo: 'sesion-no-encontrada' }));

    await usuario.click(await screen.findByRole('button', { name: /Cerrar la sesión de Chrome en Windows/ }));

    await waitFor(() => expect(useAvisos.getState().actual?.severidad).toBe('error'));
    expect(useAvisos.getState().actual?.mensaje).toContain('No pudimos cerrar la sesión.');
    expect(screen.getByRole('listitem')).toBeInTheDocument();
  });

  it('sin sesiones lo dice', async () => {
    montar([]);
    expect(await screen.findByText('No hay sesiones abiertas')).toBeInTheDocument();
  });
});
