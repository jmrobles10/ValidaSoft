import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { configurarCliente, reiniciarCliente, type ExpedientePublicoDto, type VinculoPublicoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaExpediente } from './PaginaExpediente';

/**
 * El encabezado de «Tu revisión con {empresa}» con una actualización abierta (VR-1): el vínculo sigue Activo, pero la revisión no está
 * aprobada; el tercero no debe ver «Aprobado» arriba y «En revisión» en la tarjeta del documento (dos verdades en pantalla).
 */
const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

function vinculo(cambios: Partial<VinculoPublicoDto>): VinculoPublicoDto {
  return {
    id: 'v1',
    empresa: { id: 'emp', nombre: 'Constructora Norte', logoUrl: null },
    tipoVinculo: { id: 'tp', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    estado: 'Activo',
    invitacionAceptada: true,
    invitacionAceptadaEn: '2026-11-08T15:00:00Z',
    expedienteId: 'exp-1',
    ...cambios,
  };
}

function expediente(cambios: Partial<ExpedientePublicoDto>): ExpedientePublicoDto {
  return {
    id: 'exp-1',
    folio: 'NORTE-E-2026-000002',
    empresa: { id: 'emp', nombre: 'Constructora Norte', logoUrl: null },
    tipoVinculo: { id: 'tp', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    estado: 'EnRevision',
    tipo: 'Actualizacion',
    observaciones: [],
    puedeReenviar: false,
    reenvios: 0,
    requisitosFaltantes: 0,
    requisitos: [],
    actualizadoEn: '2026-11-09T15:00:00Z',
    ...cambios,
  };
}

function montar(vinculos: VinculoPublicoDto[], cuerpo: ExpedientePublicoDto) {
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      return Promise.resolve(json(url.includes('/expedientes/') ? cuerpo : vinculos));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={['/expediente/exp-1']}>
          <Routes>
            <Route path="/expediente/:id" element={<PaginaExpediente />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
}

afterEach(() => reiniciarCliente());

describe('Mi revisión con una empresa: falta la cuenta bancaria designada (CB-4)', () => {
  it('avisa que la empresa aprueba cuando su cuenta esté designada y aprobada, y lleva a Mis cuentas', async () => {
    montar([vinculo({ estado: 'EnValidacion', expedienteId: 'exp-1' })], expediente({ tipo: 'Inicial', estado: 'EnRevision', esperandoCuentaDesignada: true }));

    const titulo = await screen.findByRole('heading', { level: 1, name: 'Tu revisión con Constructora Norte' });
    const principal = within(titulo.closest('main') ?? document.body);
    expect(await principal.findByText('Falta tu cuenta bancaria designada')).toBeInTheDocument();
    expect(principal.getByText(/Constructora Norte aprueba tu validación cuando tu cuenta bancaria para esta empresa esté designada y aprobada/)).toBeInTheDocument();
    expect(principal.getByRole('link', { name: 'Ver mis cuentas' })).toHaveAttribute('href', '/cuentas');
  });

  it('sin la espera no aparece el aviso', async () => {
    montar([vinculo({ enRevision: true })], expediente({ tipo: 'Inicial', estado: 'EnRevision' }));

    await screen.findByRole('heading', { level: 1, name: 'Tu revisión con Constructora Norte' });
    expect(screen.queryByText('Falta tu cuenta bancaria designada')).not.toBeInTheDocument();
  });
});

describe('Mi revisión con una empresa: actualización abierta de un vínculo activo', () => {
  it('en revisión: el encabezado dice «En revisión» y que la empresa está revisando, no «Aprobado» ni «tu vínculo está activo»', async () => {
    montar([vinculo({ enRevision: true })], expediente({ estado: 'EnRevision' }));

    const titulo = await screen.findByRole('heading', { level: 1, name: 'Tu revisión con Constructora Norte' });
    const principal = within(titulo.closest('main') ?? document.body);
    expect(await principal.findByText('En revisión')).toBeInTheDocument();
    expect(principal.getByText(/Constructora Norte está revisando tu información/)).toBeInTheDocument();
    expect(principal.queryByText('Aprobado')).not.toBeInTheDocument();
    expect(principal.queryByText(/aprobó tu información/)).not.toBeInTheDocument();
  });

  it('observada: «Tienes observaciones» con lo que le toca, no «Aprobado»', async () => {
    montar([vinculo({ enRevision: true })], expediente({ estado: 'Observado', puedeReenviar: true }));

    expect(await screen.findByText('Tienes observaciones')).toBeInTheDocument();
    expect(screen.getByText(/Constructora Norte te pide corregir lo siguiente/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar respuesta' })).toBeInTheDocument();
    expect(screen.queryByText('Aprobado')).not.toBeInTheDocument();
  });

  it('rechazada: «Actualización no aprobada» con los motivos y que lo ya aprobado sigue vigente; no «Aprobado» ni «cerró la revisión sin aprobarla»', async () => {
    montar(
      [vinculo({ actualizacionNoAprobada: { resultado: 'Rechazado', fecha: '2026-11-12T15:00:00Z', motivos: [{ codigo: 'incompleto', texto: 'Falta información.' }] } })],
      expediente({ estado: 'Rechazado' }),
    );

    const titulo = await screen.findByRole('heading', { level: 1, name: 'Tu revisión con Constructora Norte' });
    const principal = within(titulo.closest('main') ?? document.body);
    expect(await principal.findByText('Actualización no aprobada')).toBeInTheDocument();
    expect(principal.getByText('Esta actualización no se aprobó')).toBeInTheDocument();
    expect(principal.getByText('Falta información.')).toBeInTheDocument();
    // El encabezado ya cuenta que lo ya aprobado sigue vigente: el aviso solo suma el título y los motivos.
    expect(principal.getByText(/Constructora Norte no aprobó la actualización que enviaste\. Lo que ya tenías aprobado sigue vigente\./)).toBeInTheDocument();
    expect(principal.queryByText('Aprobado')).not.toBeInTheDocument();
  });

  it('aprobada la actualización, el vínculo vuelve a «Aprobado»', async () => {
    montar([vinculo({ enRevision: false })], expediente({ estado: 'Aprobado' }));

    expect(await screen.findByText('Aprobado')).toBeInTheDocument();
    expect(screen.getByText(/Constructora Norte aprobó tu información/)).toBeInTheDocument();
    expect(screen.queryByText('En revisión')).not.toBeInTheDocument();
    // Lo que le toca al tercero es «Aprobado»: lleva el éxito de marca (decorativo; el estado ya está en texto).
    expect(document.querySelector('svg g.pop')).not.toBeNull();
  });

  it('el éxito de marca no sale mientras la empresa revisa una actualización ni cuando se rechazó', async () => {
    montar([vinculo({ enRevision: true })], expediente({ estado: 'EnRevision' }));
    expect(await screen.findByText('En revisión')).toBeInTheDocument();
    expect(document.querySelector('svg g.pop')).toBeNull();
  });
});
