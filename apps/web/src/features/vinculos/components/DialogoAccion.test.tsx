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

const tercero = {
  id: 't1',
  identificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456', digitoVerificacion: '8' },
  tipoPersona: 'Juridica',
  nombre: 'Aceros del Norte S.A.S.',
  nombreComercial: null,
  contactos: [],
  esExtranjero: false,
  creadoEn: '2026-11-01T00:00:00Z',
};

interface Peticion {
  url: string;
  cabeceras: Headers;
  cuerpo: unknown;
}

function preparar(accion: AccionVinculo, respuesta: () => Response, base: VinculoDto = vinculo) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((url: RequestInfo | URL, init?: RequestInit) => {
      const direccion = typeof url === 'string' ? url : url instanceof URL ? url.href : url.url;
      // El diálogo consulta el tercero para su subtítulo; esa lectura no cuenta como la acción.
      if (direccion.includes('/terceros/')) return Promise.resolve(json(200, tercero));
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
        <DialogoAccion definicion={definicion} vinculo={base} empresaId="e1" onCerrar={onCerrar} />
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

    await usuario.click(screen.getByRole('button', { name: 'Bloquear vínculo' }));

    expect(screen.getByText(/Escribe el motivo/)).toBeInTheDocument();
    expect(peticiones).toHaveLength(0);
  });

  it('bloquea con If-Match y la empresa activa, y cierra al terminar', async () => {
    const usuario = userEvent.setup();
    const { peticiones, onCerrar } = preparar('bloquear', () => json(200, { ...vinculo, estado: 'Bloqueado', version: 8 }));

    expect(await screen.findByText('Aceros del Norte S.A.S. · NIT 900123456-8')).toBeInTheDocument();
    await usuario.type(screen.getByLabelText(/Motivo/), 'Coincidencia en lista restrictiva');
    await usuario.click(screen.getByRole('button', { name: 'Bloquear vínculo' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    const [peticion] = peticiones;
    expect(peticion?.url).toBe('/api/v1/vinculos/v1/bloquear');
    expect(peticion?.cabeceras.get('If-Match')).toBe('"7"');
    expect(peticion?.cabeceras.get('X-Empresa')).toBe('e1');
    expect(peticion?.cuerpo).toEqual({ motivo: 'Coincidencia en lista restrictiva', hasta: null });
  });

  it('anular exige escribir la palabra de confirmación', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = preparar('anular', () => json(200, vinculo));

    await usuario.type(screen.getByLabelText(/Motivo/), 'Duplicado');
    await usuario.click(screen.getByRole('button', { name: 'Anular vínculo' }));

    expect(screen.getByText(/Escribe ANULAR tal cual/)).toBeInTheDocument();
    expect(peticiones).toHaveLength(0);
  });

  it('ante un 412 explica que el vínculo cambió, conserva lo escrito y ofrece recargar', async () => {
    const usuario = userEvent.setup();
    const { onCerrar } = preparar('anular', () => json(412, { codigo: 'version-desactualizada', detail: 'El recurso cambió.' }));

    await usuario.type(screen.getByLabelText(/Motivo/), 'Duplicado');
    await usuario.type(screen.getByLabelText(/Escribe ANULAR/), 'anular');
    await usuario.click(screen.getByRole('button', { name: 'Anular vínculo' }));

    expect(await screen.findByText('Alguien cambió este vínculo')).toBeInTheDocument();
    expect(screen.getByText(/Lo que escribiste sigue aquí/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Anular vínculo' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Recargar' })).toBeEnabled();
    expect(onCerrar).not.toHaveBeenCalled();
  });

  it('reenvía la invitación al correo del tercero por defecto', async () => {
    const usuario = userEvent.setup();
    const invitado: VinculoDto = { ...vinculo, estado: 'Invitado' };
    const { peticiones } = preparar('invitar', () => json(201, { correoDestino: 'contacto@aceros.co', expira: '2026-11-09T00:00:00Z', envios: 2 }), invitado);

    expect(screen.getByLabelText(/Correo destino/)).toHaveValue('contacto@aceros.co');
    await usuario.click(screen.getByRole('button', { name: 'Reenviar invitación' }));

    await waitFor(() => expect(peticiones).toHaveLength(1));
    expect(peticiones[0]?.cuerpo).toEqual({ correoDestino: 'contacto@aceros.co', vigenciaHoras: 72 });
  });

  it('si el vínculo ya no admite la acción lo dice y no deja enviarla', () => {
    preparar('invitar', () => json(200, vinculo));
    expect(screen.getByText(/esta acción ya no aplica/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reenviar invitación' })).not.toBeInTheDocument();
  });
});
