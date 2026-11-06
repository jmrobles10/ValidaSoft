import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ACCIONES_VINCULO, configurarCliente, reiniciarCliente, type AccionVinculo, type VinculoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DialogoAccion } from './DialogoAccion';

const vinculo: VinculoDto = {
  id: 'v1',
  empresaId: 'e1',
  terceroId: 't1',
  tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
  flujoId: null,
  estado: 'Activo',
  atributos: {},
  correoTercero: 'contacto@aceros.co',
  invitacion: null,
  invitacionAceptadaEn: null,
  requisitosCubribles: null,
  bloqueo: null,
  referencias: [],
  origen: 'Api',
  creadoEn: '2026-11-01T00:00:00Z',
  actualizadoEn: '2026-11-01T00:00:00Z',
  version: 7,
};

interface Peticion {
  url: string;
  cabeceras: Headers;
  cuerpo: unknown;
}

function preparar(accion: AccionVinculo, respuesta: () => Response) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((url: RequestInfo | URL, init?: RequestInit) => {
      const direccion = typeof url === 'string' ? url : url instanceof URL ? url.href : url.url;
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      peticiones.push({ url: direccion, cabeceras: new Headers(init?.headers), cuerpo });
      return Promise.resolve(respuesta());
    }),
  });
  const onCerrar = vi.fn();
  const definicion = ACCIONES_VINCULO.find((a) => a.accion === accion)!;
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient()}>
        <DialogoAccion definicion={definicion} vinculo={vinculo} empresaId="e1" onCerrar={onCerrar} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { peticiones, onCerrar };
}

const json = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

afterEach(() => reiniciarCliente());

describe('DialogoAccion', () => {
  it('exige el motivo antes de llamar a la API', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = preparar('bloquear', () => json(200, vinculo));

    await usuario.click(screen.getByRole('button', { name: 'Bloquear' }));

    expect(screen.getByText('Este campo es obligatorio.')).toBeInTheDocument();
    expect(peticiones).toHaveLength(0);
  });

  it('bloquea con If-Match y la empresa activa, y cierra al terminar', async () => {
    const usuario = userEvent.setup();
    const { peticiones, onCerrar } = preparar('bloquear', () => json(200, { ...vinculo, estado: 'Bloqueado', version: 8 }));

    await usuario.type(screen.getByLabelText(/Motivo/), 'Coincidencia en lista restrictiva');
    await usuario.click(screen.getByRole('button', { name: 'Bloquear' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    const [peticion] = peticiones;
    expect(peticion?.url).toBe('/api/v1/vinculos/v1/bloquear');
    expect(peticion?.cabeceras.get('If-Match')).toBe('"7"');
    expect(peticion?.cabeceras.get('X-Empresa')).toBe('e1');
    expect(peticion?.cuerpo).toEqual({ motivo: 'Coincidencia en lista restrictiva', hasta: null });
  });

  it('ante un 412 explica que el vínculo cambió y no deja reenviar', async () => {
    const usuario = userEvent.setup();
    const { onCerrar } = preparar('anular', () => json(412, { codigo: 'version-desactualizada', detail: 'El recurso cambió.' }));

    await usuario.type(screen.getByLabelText(/Motivo/), 'Duplicado');
    await usuario.click(screen.getByRole('button', { name: 'Anular' }));

    expect(await screen.findByText(/Alguien cambió este registro/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Anular' })).not.toBeInTheDocument();
    expect(onCerrar).not.toHaveBeenCalled();
  });

  it('reenvía la invitación al correo del tercero por defecto', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = preparar('invitar', () => json(201, { correoDestino: 'contacto@aceros.co', expira: '2026-11-09T00:00:00Z', envios: 2 }));

    expect(screen.getByLabelText(/Correo destino/)).toHaveValue('contacto@aceros.co');
    await usuario.click(screen.getByRole('button', { name: 'Reenviar invitación' }));

    await waitFor(() => expect(peticiones).toHaveLength(1));
    expect(peticiones[0]?.cuerpo).toEqual({ correoDestino: 'contacto@aceros.co', vigenciaHoras: 72 });
  });
});
