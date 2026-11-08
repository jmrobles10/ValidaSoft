import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { configurarCliente, reiniciarCliente, type ExpedientePublicoDto, type VinculoPublicoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { avanceDelTercero } from './avance';
import { PaginaEmpresas } from './PaginaEmpresas';

const aceptado = { estado: 'EnValidacion', invitacionAceptada: true } as const;

describe('avance del tercero: lo que le toca, no el estado interno', () => {
  it('expediente abierto con documentos pendientes: faltan N', () => {
    expect(avanceDelTercero(aceptado, { estado: 'Abierto', requisitosFaltantes: 3 })).toEqual({ tipo: 'faltan', documentos: 3 });
  });

  it('en revisión, observado y aprobado (por el expediente o por el vínculo activo)', () => {
    expect(avanceDelTercero(aceptado, { estado: 'EnRevision', requisitosFaltantes: 0 })).toEqual({ tipo: 'enRevision' });
    expect(avanceDelTercero(aceptado, { estado: 'Abierto', requisitosFaltantes: 0 })).toEqual({ tipo: 'enRevision' });
    expect(avanceDelTercero(aceptado, { estado: 'Observado' })).toEqual({ tipo: 'observado' });
    expect(avanceDelTercero(aceptado, { estado: 'Aprobado' })).toEqual({ tipo: 'aprobado' });
    expect(avanceDelTercero({ estado: 'Activo', invitacionAceptada: true }, null)).toEqual({ tipo: 'aprobado' });
  });

  it('sin paso propio que contar se deja el estado tal cual', () => {
    expect(avanceDelTercero({ estado: 'Invitado', invitacionAceptada: false }, { estado: 'Abierto', requisitosFaltantes: 3 })).toBeNull();
    expect(avanceDelTercero({ estado: 'Bloqueado', invitacionAceptada: true }, { estado: 'Abierto', requisitosFaltantes: 3 })).toBeNull();
    expect(avanceDelTercero(aceptado, { estado: 'Rechazado' })).toBeNull();
    expect(avanceDelTercero(aceptado, null)).toBeNull();
  });
});

const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

function vinculo(id: string, nombre: string, cambios: Partial<VinculoPublicoDto>): VinculoPublicoDto {
  return {
    id,
    empresa: { id: `emp-${id}`, nombre, logoUrl: null },
    tipoVinculo: { id: 'tp', codigo: 'CLIENTE', nombre: 'Cliente' },
    estado: 'EnValidacion',
    invitacionAceptada: true,
    invitacionAceptadaEn: '2026-11-08T15:00:00Z',
    expedienteId: `exp-${id}`,
    ...cambios,
  };
}

function expediente(id: string, estado: ExpedientePublicoDto['estado'], requisitosFaltantes: number): Partial<ExpedientePublicoDto> {
  return { id, folio: 'F-1', estado, requisitosFaltantes, observaciones: [], puedeReenviar: false, reenvios: 0, actualizadoEn: '2026-11-08T15:00:00Z' };
}

afterEach(() => reiniciarCliente());

describe('Mis empresas', () => {
  it('muestra «Faltan N documentos» con la acción para subirlos, nunca «En validación»', async () => {
    const vinculos = [
      vinculo('a', 'Aceros del Norte', {}),
      vinculo('b', 'Bodegas del Sur', {}),
      vinculo('c', 'Constructora Centro', {}),
      vinculo('d', 'Distribuidora Este', { estado: 'Activo' }),
    ];
    const expedientes: Record<string, Partial<ExpedientePublicoDto>> = {
      'exp-a': expediente('exp-a', 'Abierto', 3),
      'exp-b': expediente('exp-b', 'EnRevision', 0),
      'exp-c': expediente('exp-c', 'Observado', 0),
      'exp-d': expediente('exp-d', 'Aprobado', 0),
    };
    configurarCliente({
      fetch: vi.fn((url: RequestInfo | URL) => {
        const ruta = typeof url === 'string' ? url : url instanceof URL ? url.href : url.url;
        const id = ruta.split('/expedientes/')[1];
        return Promise.resolve(json(id ? expedientes[id] : vinculos));
      }),
    });
    render(
      <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
        <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
          <MemoryRouter>
            <PaginaEmpresas />
          </MemoryRouter>
        </QueryClientProvider>
      </ThemeProvider>,
    );

    const tarjeta = (nombre: string) => {
      const titulo = screen.getByRole('heading', { name: nombre });
      const li = titulo.closest('li');
      if (!li) throw new Error(`Sin tarjeta para ${nombre}`);
      return within(li);
    };

    expect(await screen.findByText('Faltan 3 documentos')).toBeInTheDocument();
    expect(tarjeta('Aceros del Norte').getByRole('link', { name: 'Subir documentos' })).toHaveAttribute('href', '/expediente/exp-a');
    expect(tarjeta('Aceros del Norte').getByText(/Te faltan 3 documentos/)).toBeInTheDocument();
    expect(await tarjeta('Bodegas del Sur').findByText('En revisión')).toBeInTheDocument();
    expect(await tarjeta('Constructora Centro').findByText('Tienes observaciones')).toBeInTheDocument();
    expect(tarjeta('Constructora Centro').getByRole('link', { name: 'Ver observaciones' })).toBeInTheDocument();
    expect(tarjeta('Distribuidora Este').getByText('Aprobado')).toBeInTheDocument();
    expect(screen.queryByText('En validación')).not.toBeInTheDocument();
  });
});
