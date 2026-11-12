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

  it('en pre-vencimiento (por vencer con la renovación abierta): toca ratificar antes de la fecha', () => {
    const porVencer = { estado: 'PorVencer', invitacionAceptada: true, vigenteHasta: '2027-11-06' } as const;
    expect(avanceDelTercero(porVencer, { estado: 'Abierto', tipo: 'Renovacion', requisitosFaltantes: 1 })).toEqual({ tipo: 'preVencimiento', hasta: '2027-11-06' });
    expect(avanceDelTercero({ estado: 'PorVencer', invitacionAceptada: true }, { estado: 'Abierto', tipo: 'Renovacion' })).toEqual({ tipo: 'preVencimiento', hasta: null });
    // Ya la envió: la empresa revisa (o devolvió algo) y eso es lo que le toca saber.
    expect(avanceDelTercero(porVencer, { estado: 'EnRevision', tipo: 'Renovacion' })).toEqual({ tipo: 'enRevision' });
    expect(avanceDelTercero(porVencer, { estado: 'Observado', tipo: 'Renovacion' })).toEqual({ tipo: 'observado' });
  });

  it('por vencer sin renovación a la vista: lo avisa con la fecha; vencido con la renovación abierta: toca ratificar', () => {
    expect(avanceDelTercero({ estado: 'PorVencer', invitacionAceptada: true, vigenteHasta: '2027-11-06' }, { estado: 'Aprobado' })).toEqual({ tipo: 'porVencer', hasta: '2027-11-06' });
    expect(avanceDelTercero({ estado: 'PorVencer', invitacionAceptada: true }, null)).toEqual({ tipo: 'porVencer', hasta: null });
    // La renovación se cerró sin aprobarse: sigue por vencer, sin renovación abierta.
    expect(avanceDelTercero({ estado: 'PorVencer', invitacionAceptada: true }, { estado: 'Rechazado', tipo: 'Renovacion' })).toEqual({ tipo: 'porVencer', hasta: null });
    expect(avanceDelTercero({ estado: 'Vencido', invitacionAceptada: true }, { estado: 'Abierto', tipo: 'Renovacion', requisitosFaltantes: 1 })).toEqual({ tipo: 'ratificar' });
    // Vencido por un documento sin renovación a la vista (o una renovación ya en revisión) no cambia lo que se contaba.
    expect(avanceDelTercero({ estado: 'Vencido', invitacionAceptada: true }, null)).toBeNull();
    expect(avanceDelTercero({ estado: 'Vencido', invitacionAceptada: true }, { estado: 'EnRevision', tipo: 'Renovacion' })).toEqual({ tipo: 'enRevision' });
  });

  describe('con una actualización abierta (VR-1) el vínculo sigue Activo pero lo que le toca es lo del expediente, nunca «Aprobado»', () => {
    const activo = { estado: 'Activo', invitacionAceptada: true, enRevision: true } as const;

    it('en revisión, observada o con algo por subir', () => {
      expect(avanceDelTercero(activo, { estado: 'EnRevision', tipo: 'Actualizacion' })).toEqual({ tipo: 'enRevision' });
      expect(avanceDelTercero(activo, { estado: 'Observado', tipo: 'Actualizacion' })).toEqual({ tipo: 'observado' });
      expect(avanceDelTercero(activo, { estado: 'Abierto', tipo: 'Actualizacion', requisitosFaltantes: 2 })).toEqual({ tipo: 'faltan', documentos: 2 });
      expect(avanceDelTercero(activo, { estado: 'Abierto', tipo: 'Actualizacion', requisitosFaltantes: 0 })).toEqual({ tipo: 'enRevision' });
    });

    it('por vencer o vencido con la actualización abierta tampoco dicen «por vencer» ni quedan sin paso', () => {
      const porVencer = { estado: 'PorVencer', invitacionAceptada: true, vigenteHasta: '2027-11-06', enRevision: true } as const;
      expect(avanceDelTercero(porVencer, { estado: 'EnRevision', tipo: 'Actualizacion' })).toEqual({ tipo: 'enRevision' });
      expect(avanceDelTercero(porVencer, { estado: 'Observado', tipo: 'Actualizacion' })).toEqual({ tipo: 'observado' });
      expect(avanceDelTercero({ estado: 'Vencido', invitacionAceptada: true, enRevision: true }, { estado: 'EnRevision', tipo: 'Actualizacion' })).toEqual({ tipo: 'enRevision' });
    });

    it('con la marca del vínculo y sin el expediente todavía, tampoco dice «Aprobado»', () => {
      expect(avanceDelTercero(activo, null)).toEqual({ tipo: 'enRevision' });
      expect(avanceDelTercero({ estado: 'Activo', invitacionAceptada: true }, null)).toEqual({ tipo: 'aprobado' });
    });

    it('aprobada la actualización vuelve a «Aprobado»; rechazada, el vínculo conserva lo que tenía (RV-2)', () => {
      expect(avanceDelTercero({ estado: 'Activo', invitacionAceptada: true }, { estado: 'Aprobado', tipo: 'Actualizacion' })).toEqual({ tipo: 'aprobado' });
      expect(avanceDelTercero({ estado: 'Activo', invitacionAceptada: true }, { estado: 'Rechazado', tipo: 'Actualizacion' })).toEqual({ tipo: 'aprobado' });
    });

    it('una actualización abierta no cambia lo de un vínculo suspendido o terminado', () => {
      expect(avanceDelTercero({ estado: 'Bloqueado', invitacionAceptada: true }, { estado: 'EnRevision', tipo: 'Actualizacion' })).toBeNull();
      expect(avanceDelTercero({ estado: 'Desvinculado', invitacionAceptada: true }, { estado: 'EnRevision', tipo: 'Actualizacion' })).toBeNull();
    });

    it('un expediente inicial o una renovación en revisión se cuentan como siempre', () => {
      expect(avanceDelTercero({ estado: 'Activo', invitacionAceptada: true }, { estado: 'EnRevision', tipo: 'Renovacion' })).toEqual({ tipo: 'aprobado' });
      expect(avanceDelTercero(aceptado, { estado: 'EnRevision', tipo: 'Inicial' })).toEqual({ tipo: 'enRevision' });
    });
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
      vinculo('e', 'Energía Oeste', { estado: 'PorVencer', vigenteHasta: '2027-11-06' }),
    ];
    const expedientes: Record<string, Partial<ExpedientePublicoDto>> = {
      'exp-a': expediente('exp-a', 'Abierto', 3),
      'exp-b': expediente('exp-b', 'EnRevision', 0),
      'exp-c': expediente('exp-c', 'Observado', 0),
      'exp-d': expediente('exp-d', 'Aprobado', 0),
      'exp-e': { ...expediente('exp-e', 'Abierto', 1), tipo: 'Renovacion' },
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
    // Por vencer con la renovación abierta: pre-vencimiento con la fecha y la acción de ratificar, sin «Por vencer» suelto.
    expect(await tarjeta('Energía Oeste').findByText('En pre-vencimiento')).toBeInTheDocument();
    expect(tarjeta('Energía Oeste').getByText(/En pre-vencimiento: ratifica tu información con Energía Oeste antes del/)).toBeInTheDocument();
    expect(tarjeta('Energía Oeste').getByRole('link', { name: 'Ratificar mi información' })).toHaveAttribute('href', '/expediente/exp-e');
    expect(screen.queryByText('Por vencer')).not.toBeInTheDocument();
    expect(screen.queryByText('En validación')).not.toBeInTheDocument();
  });

  it('con una actualización abierta, la tarjeta de la empresa dice «En revisión» (o «Tienes observaciones»), no «Aprobado»; aprobada, vuelve a «Aprobado»', async () => {
    const vinculos = [
      vinculo('a', 'Aceros del Norte', { estado: 'Activo', enRevision: true }),
      vinculo('b', 'Bodegas del Sur', { estado: 'Activo', enRevision: true }),
      vinculo('c', 'Constructora Centro', { estado: 'Activo' }),
      vinculo('d', 'Distribuidora Este', { estado: 'PorVencer', vigenteHasta: '2027-11-06', enRevision: true }),
    ];
    const expedientes: Record<string, Partial<ExpedientePublicoDto>> = {
      'exp-a': { ...expediente('exp-a', 'EnRevision', 0), tipo: 'Actualizacion' },
      'exp-b': { ...expediente('exp-b', 'Observado', 0), tipo: 'Actualizacion' },
      'exp-c': { ...expediente('exp-c', 'Aprobado', 0), tipo: 'Actualizacion' },
      'exp-d': { ...expediente('exp-d', 'EnRevision', 0), tipo: 'Actualizacion' },
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
      const li = screen.getByRole('heading', { name: nombre }).closest('li');
      if (!li) throw new Error(`Sin tarjeta para ${nombre}`);
      return within(li);
    };

    await screen.findByRole('heading', { name: 'Aceros del Norte' });
    expect(await tarjeta('Aceros del Norte').findByText('En revisión')).toBeInTheDocument();
    expect(tarjeta('Aceros del Norte').getByText(/Aceros del Norte está revisando tu información/)).toBeInTheDocument();
    expect(tarjeta('Aceros del Norte').queryByText('Aprobado')).not.toBeInTheDocument();
    expect(tarjeta('Aceros del Norte').getByRole('link', { name: 'Ver mi revisión' })).toHaveAttribute('href', '/expediente/exp-a');

    expect(await tarjeta('Bodegas del Sur').findByText('Tienes observaciones')).toBeInTheDocument();
    expect(tarjeta('Bodegas del Sur').getByRole('link', { name: 'Ver observaciones' })).toHaveAttribute('href', '/expediente/exp-b');
    expect(tarjeta('Bodegas del Sur').queryByText('Aprobado')).not.toBeInTheDocument();

    // La actualización ya se aprobó: el vínculo vuelve a «Aprobado».
    expect(await tarjeta('Constructora Centro').findByText('Aprobado')).toBeInTheDocument();

    // Por vencer con una actualización abierta cuenta la actualización, no «Por vencer».
    expect(await tarjeta('Distribuidora Este').findByText('En revisión')).toBeInTheDocument();
    expect(tarjeta('Distribuidora Este').queryByText('Por vencer')).not.toBeInTheDocument();
  });
});
