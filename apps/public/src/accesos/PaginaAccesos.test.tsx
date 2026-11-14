import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type AccesoSolicitadoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../shared/useAvisos';
import { PaginaAccesos } from './PaginaAccesos';

const json = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': estado >= 400 ? 'application/problem+json' : 'application/json' } });

function solicitud(cambios: Partial<AccesoSolicitadoDto> = {}): AccesoSolicitadoDto {
  return {
    id: 'a1',
    correo: 'compras@empresa-a.co',
    estado: 'Pendiente',
    desde: '2026-11-12T15:00:00Z',
    empresas: [{ empresa: 'Empresa A S.A.S.', fecha: '2026-11-12T15:00:00Z' }],
    decididoPor: null,
    decididoEn: null,
    motivo: null,
    ...cambios,
  };
}

interface Peticion {
  metodo: string;
  url: URL;
  cuerpo: unknown;
}

function montar(solicitudes: AccesoSolicitadoDto[], alMutar: (p: Peticion) => Response = (p) => json(200, solicitud({ id: p.url.pathname.split('/')[4] ?? 'a1', estado: 'Aprobada' }))) {
  const mutaciones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost');
      const metodo = init?.method ?? 'GET';
      if (metodo === 'GET') return Promise.resolve(json(200, solicitudes));
      const peticion: Peticion = { metodo, url, cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null };
      mutaciones.push(peticion);
      return Promise.resolve(alMutar(peticion));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <PaginaAccesos />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return mutaciones;
}

afterEach(() => {
  reiniciarCliente();
  useAvisos.setState({ actual: null });
});

describe('Accesos por aprobar', () => {
  it('sin solicitudes explica cuándo se avisará', async () => {
    montar([]);
    expect(await screen.findByText('No hay accesos por decidir')).toBeInTheDocument();
    expect(screen.getByText(/te avisamos aquí, en tus notificaciones y por correo/)).toBeInTheDocument();
  });

  it('muestra cada correo con la empresa que lo invitó, lo pendiente primero y lo decidido aparte', async () => {
    montar([
      solicitud({ id: 'r', correo: 'viejo@empresa-b.co', estado: 'Rechazada', decididoPor: 'gerente@norte.co', decididoEn: '2026-11-10T10:00:00Z', motivo: 'No lo conozco' }),
      solicitud({ empresas: [{ empresa: 'Empresa A S.A.S.', fecha: '2026-11-12T15:00:00Z' }, { empresa: 'Empresa C', fecha: '2026-11-13T09:00:00Z' }] }),
    ]);

    const [pendiente, decidida] = await screen.findAllByRole('listitem');
    expect(within(pendiente!).getByRole('heading', { name: 'compras@empresa-a.co' })).toBeInTheDocument();
    expect(within(pendiente!).getByText('Invitado por Empresa A S.A.S. y Empresa C')).toBeInTheDocument();
    expect(within(pendiente!).getByText('Por decidir')).toBeInTheDocument();
    expect(within(pendiente!).getByRole('button', { name: 'Aprobar el acceso de compras@empresa-a.co' })).toBeInTheDocument();
    expect(within(pendiente!).getByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Decididos' })).toBeInTheDocument();
    expect(within(decidida!).getByText('Rechazado')).toBeInTheDocument();
    expect(within(decidida!).getByText(/Lo decidió gerente@norte\.co el 10\/11\/2026/)).toBeInTheDocument();
    expect(within(decidida!).getByText(/Motivo: No lo conozco/)).toBeInTheDocument();
    // Un rechazo se puede reconsiderar (aprobarlo de todos modos); ya no se vuelve a rechazar.
    expect(within(decidida!).getByRole('button', { name: 'Aprobar el acceso de viejo@empresa-b.co' })).toHaveTextContent('Aprobarlo de todos modos');
    expect(within(decidida!).queryByRole('button', { name: /Rechazar/ })).not.toBeInTheDocument();
  });

  it('antes de aprobar explica que el correo pasa a ser contacto y lo subido pasa a la bóveda, y recién entonces manda la aprobación', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([solicitud()]);

    await usuario.click(await screen.findByRole('button', { name: 'Aprobar el acceso de compras@empresa-a.co' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Aprobar el acceso de compras@empresa-a.co' });
    expect(within(dialogo).getByText(/pasará a ser contacto de tu empresa: verá tu bóveda y tu información/)).toBeInTheDocument();
    expect(within(dialogo).getByText(/Lo que subió para Empresa A S\.A\.S\. pasará a tu bóveda/)).toBeInTheDocument();
    expect(mutaciones).toHaveLength(0);

    await usuario.click(within(dialogo).getByRole('button', { name: 'Sí, aprobar' }));
    await waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.metodo).toBe('POST');
    expect(mutaciones[0]?.url.pathname).toBe('/public/v1/accesos-solicitados/a1/aprobar');
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Aprobaste el acceso de compras@empresa-a.co.'));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('al rechazar explica que se cortan sus sesiones, deja anotar el motivo y lo manda', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([solicitud()], () => json(200, solicitud({ estado: 'Rechazada' })));

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Rechazar el acceso de compras@empresa-a.co' });
    expect(within(dialogo).getByText(/Sus sesiones se cierran al instante y no vuelve a entrar/)).toBeInTheDocument();
    await usuario.type(within(dialogo).getByLabelText('Motivo (opcional)'), '  No conozco a esa persona ');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Sí, rechazar' }));

    await waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.url.pathname).toBe('/public/v1/accesos-solicitados/a1/rechazar');
    expect(mutaciones[0]?.cuerpo).toEqual({ motivo: 'No conozco a esa persona' });
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Rechazaste el acceso de compras@empresa-a.co.'));
  });

  it('rechazar sin motivo manda el motivo vacío como nulo y cancelar no manda nada', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([solicitud()], () => json(200, solicitud({ estado: 'Rechazada' })));

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' }));
    await usuario.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(mutaciones).toHaveLength(0);

    await usuario.click(screen.getByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' }));
    await usuario.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Sí, rechazar' }));
    await waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.cuerpo).toEqual({ motivo: null });
  });

  it('si el servidor no deja decidir (ya está aprobado), lo dice dentro del diálogo y no lo cierra', async () => {
    const usuario = userEvent.setup();
    montar([solicitud()], () => json(409, { type: 'x', title: 'solicitud-ya-aprobada', status: 409, detail: 'ya', codigo: 'solicitud-ya-aprobada' }));

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' }));
    const dialogo = await screen.findByRole('dialog');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Sí, rechazar' }));

    expect(await within(dialogo).findByText('Ese correo ya es contacto de tu empresa: no se puede rechazar.')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
