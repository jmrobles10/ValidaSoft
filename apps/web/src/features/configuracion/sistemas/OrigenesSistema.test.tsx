import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type SistemaIntegradoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { OrigenesSistema } from './OrigenesSistema';

const SISTEMA: SistemaIntegradoDto = {
  id: 's1',
  codigo: 'SRM',
  nombre: 'SRM de Sinco',
  clavePublicaPem: '-----BEGIN PUBLIC KEY-----',
  activo: true,
  creadoEn: '2026-11-08T15:00:00Z',
  origenesPermitidos: ['https://srm.cliente.co'],
};

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

function montar(sistema: SistemaIntegradoDto, respuesta: (cuerpo: unknown) => Response) {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((url: RequestInfo | URL, init?: RequestInit) => {
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      peticiones.push({ url: typeof url === 'string' ? url : url instanceof URL ? url.href : url.url, metodo: init?.method ?? 'GET', cuerpo });
      return Promise.resolve(respuesta(cuerpo));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient()}>
        <OrigenesSistema sistema={sistema} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => reiniciarCliente());

describe('Sitios que pueden incrustar Validasoft', () => {
  it('explica para qué sirve, normaliza lo escrito y guarda la lista completa con PUT', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar(SISTEMA, (cuerpo) => json({ ...SISTEMA, origenesPermitidos: (cuerpo as { origenes: string[] }).origenes }));

    expect(screen.getByText(/solo se deja abrir dentro de un iframe en estos sitios/)).toBeInTheDocument();
    const guardar = screen.getByRole('button', { name: 'Guardar sitios' });
    expect(guardar).toBeDisabled();

    await usuario.type(screen.getByRole('textbox', { name: 'Agregar sitio' }), 'https://Marco.Cliente.co/');
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }));
    expect(screen.getByText('https://marco.cliente.co')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Hay cambios sin guardar.');

    await usuario.click(screen.getByRole('button', { name: 'Quitar https://srm.cliente.co' }));
    await usuario.click(guardar);

    await waitFor(() => expect(peticiones).toHaveLength(1));
    expect(peticiones[0]).toMatchObject({ url: '/api/v1/sistemas/s1/origenes', metodo: 'PUT', cuerpo: { origenes: ['https://marco.cliente.co'] } });
  });

  it('dice qué está mal antes de agregar (http fuera de localhost, rutas, repetidos)', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar(SISTEMA, () => json(SISTEMA));
    const campo = screen.getByRole('textbox', { name: 'Agregar sitio' });

    await usuario.type(campo, 'http://srm.cliente.co');
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }));
    expect(screen.getByText(/Usa https\. http solo se admite hacia localhost/)).toBeInTheDocument();

    await usuario.clear(campo);
    await usuario.type(campo, 'https://srm.cliente.co/compras');
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }));
    expect(screen.getByText(/sin ruta, consulta, comodines ni usuario/)).toBeInTheDocument();

    await usuario.clear(campo);
    await usuario.type(campo, 'https://SRM.cliente.co');
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }));
    expect(screen.getByText('Ese sitio ya está en la lista.')).toBeInTheDocument();
    expect(peticiones).toHaveLength(0);
  });

  it('muestra el 422 de la API con su detalle', async () => {
    const usuario = userEvent.setup();
    montar(SISTEMA, () => json({ codigo: 'origen-invalido', detail: 'El origen «http://localhost:9000» debe usar https.', status: 422 }, 422));
    await usuario.type(screen.getByRole('textbox', { name: 'Agregar sitio' }), 'http://localhost:9000');
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar sitios' }));
    expect(await screen.findByText(/debe usar https/)).toBeInTheDocument();
  });

  it('con el sistema desactivado la lista es solo de lectura', () => {
    montar({ ...SISTEMA, activo: false }, () => json(SISTEMA));
    expect(screen.queryByRole('textbox', { name: 'Agregar sitio' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Quitar/ })).not.toBeInTheDocument();
    expect(screen.getByText(/está desactivado/)).toBeInTheDocument();
  });
});
