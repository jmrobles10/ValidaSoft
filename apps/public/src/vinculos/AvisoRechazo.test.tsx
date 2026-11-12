import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type ReintentoPublicoDto, type VinculoPublicoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaEmpresas } from './PaginaEmpresas';

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

const reintento = (cambios: Partial<ReintentoPublicoDto> = {}): ReintentoPublicoDto => ({
  rechazadoEn: '2026-11-01T15:00:00Z',
  motivos: [{ codigo: 'no-corresponde-al-tercero', texto: 'El documento no corresponde a la persona o empresa que se está validando.' }],
  permitido: true,
  disponible: true,
  desde: '2026-11-01T15:00:00Z',
  ...cambios,
});

const vinculo = (cambios: Partial<VinculoPublicoDto> = {}): VinculoPublicoDto => ({
  id: 'v1',
  empresa: { id: 'emp', nombre: 'Aceros del Norte', logoUrl: null },
  tipoVinculo: { id: 'tp', codigo: 'CLIENTE', nombre: 'Cliente' },
  estado: 'Invitado',
  invitacionAceptada: true,
  invitacionAceptadaEn: '2026-10-20T15:00:00Z',
  expedienteId: 'exp-rechazado',
  reintento: reintento(),
  ...cambios,
});

function mostrar(vinculos: VinculoPublicoDto[], reintentar?: () => Response | Promise<Response>, lista: () => VinculoPublicoDto[] = () => vinculos) {
  const llamadas: string[] = [];
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  configurarCliente({
    fetch: vi.fn((url: RequestInfo | URL, init?: RequestInit) => {
      const ruta = typeof url === 'string' ? url : url instanceof URL ? url.href : url.url;
      llamadas.push(`${init?.method ?? 'GET'} ${ruta}`);
      return Promise.resolve(ruta.endsWith('/reintentar') && reintentar ? reintentar() : json(lista()));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={cliente}>
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route path="/" element={<PaginaEmpresas />} />
            <Route path="/expediente/:id" element={<p>Expediente nuevo</p>} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return Object.assign(llamadas, { cliente });
}

afterEach(() => reiniciarCliente());

describe('rechazo y «Volver a postularme» (DS-4)', () => {
  it('muestra el rechazo con los textos de los motivos y deja volver a postularse', async () => {
    const llamadas = mostrar([vinculo()], () => json({ id: 'exp-nuevo', estado: 'EnRevision' }));

    expect(await screen.findByText(/cerró tu revisión sin aprobarla/)).toBeInTheDocument();
    expect(screen.getByText('El documento no corresponde a la persona o empresa que se está validando.')).toBeInTheDocument();
    // Ya no ofrece «Ver mi revisión» del expediente rechazado: el aviso dice cómo seguir.
    expect(screen.queryByRole('link', { name: 'Ver mi revisión' })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Volver a postularme' }));

    await waitFor(() => expect(screen.getByText('Expediente nuevo')).toBeInTheDocument());
    expect(llamadas).toContain('POST /public/v1/vinculos/v1/reintentar');
  });

  it('abre la revisión nueva aunque el aviso en tiempo real refresque la lista (y el aviso desaparezca) antes de que llegue la respuesta', async () => {
    // El POST queda en el aire hasta que la lista ya cambió: así llega el tiempo real en una red lenta.
    let responder: (r: Response) => void = () => undefined;
    const respuesta = new Promise<Response>((resolver) => (responder = resolver));
    let rechazado = true;
    const llamadas = mostrar([vinculo()], () => respuesta, () => [vinculo(rechazado ? {} : { reintento: null, estado: 'EnValidacion', expedienteId: 'exp-nuevo' })]);

    await userEvent.click(await screen.findByRole('button', { name: 'Volver a postularme' }));
    await waitFor(() => expect(llamadas).toContain('POST /public/v1/vinculos/v1/reintentar'));
    rechazado = false;
    await llamadas.cliente.invalidateQueries();
    await waitFor(() => expect(screen.queryByRole('button', { name: 'Volver a postularme' })).not.toBeInTheDocument());
    expect(screen.queryByText('Expediente nuevo')).not.toBeInTheDocument();

    responder(json({ id: 'exp-nuevo', estado: 'EnRevision' }));
    await waitFor(() => expect(screen.getByText('Expediente nuevo')).toBeInTheDocument());
  });

  it('con espera dice desde cuándo puede y no deja postularse todavía', async () => {
    mostrar([vinculo({ reintento: reintento({ disponible: false, desde: '2026-12-01T15:00:00Z' }) })]);

    expect(await screen.findByText(/Podrás volver a postularte desde el/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Volver a postularme' })).not.toBeInTheDocument();
  });

  it('si la empresa no lo permite, dice a quién escribirle', async () => {
    mostrar([vinculo({ reintento: reintento({ permitido: false, disponible: false, desde: null }) })]);

    expect(await screen.findByText(/escríbele a Aceros del Norte/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Volver a postularme' })).not.toBeInTheDocument();
  });

  it('explica el error si ya no se puede reintentar y conserva el aviso', async () => {
    mostrar([vinculo()], () => json({ type: 'x', title: 'x', status: 409, codigo: 'reintento-no-disponible', detail: 'No hay un rechazo del que volver a postularte.' }, 409));

    await userEvent.click(await screen.findByRole('button', { name: 'Volver a postularme' }));

    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByText(/cerró tu revisión sin aprobarla/)).toBeInTheDocument();
  });
});
