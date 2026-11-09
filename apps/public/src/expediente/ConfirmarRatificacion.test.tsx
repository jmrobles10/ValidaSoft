import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ConfirmarRatificacion } from './ConfirmarRatificacion';

const json = (estado: number, cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/problem+json' } });

function montar(respuesta: () => Response) {
  const urls: string[] = [];
  configurarCliente({
    fetch: vi.fn((url: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'POST') urls.push(url instanceof Request ? url.url : url.toString());
      return Promise.resolve(respuesta());
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <ConfirmarRatificacion expedienteId="e1" requisitoId="r1" documento="Registro Único Tributario" empresa="Constructora Norte" />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return urls;
}

afterEach(() => reiniciarCliente());

describe('ratificar un documento vencido', () => {
  it('explica qué se declara, pide confirmación y ratifica el requisito de la renovación', async () => {
    const usuario = userEvent.setup();
    const urls = montar(() => json(200, {}));

    await usuario.click(screen.getByRole('button', { name: 'No ha cambiado' }));
    const dialogo = screen.getByRole('dialog', { name: '¿Tu Registro Único Tributario sigue igual?' });
    expect(dialogo).toHaveTextContent('Constructora Norte lo da por renovado sin volver a revisarlo');
    expect(dialogo).toHaveTextContent('Si cambió algo, cierra y sube la versión nueva.');

    await usuario.click(screen.getByRole('button', { name: 'Sí, sigue igual' }));
    await vi.waitFor(() => expect(urls).toHaveLength(1));
    expect(urls[0]).toContain('/public/v1/expedientes/e1/requisitos/r1/ratificar');
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('si la empresa no lo admite, lo dice sin cerrar el diálogo', async () => {
    const usuario = userEvent.setup();
    montar(() => json(422, { codigo: 'ratificacion-no-permitida', detail: 'Este documento no se puede ratificar: sube una versión nueva.' }));

    await usuario.click(screen.getByRole('button', { name: 'No ha cambiado' }));
    await usuario.click(screen.getByRole('button', { name: 'Sí, sigue igual' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
