import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type ExportacionBovedaDto, type SolicitudHabeasDto, type VinculoPublicoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../shared/useAvisos';
import { PaginaPrivacidad } from './PaginaPrivacidad';

const json = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': estado >= 400 ? 'application/problem+json' : 'application/json' } });

const VINCULO: VinculoPublicoDto = {
  id: 'v1',
  empresa: { id: 'emp1', nombre: 'Constructora Norte', logoUrl: null },
  tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
  estado: 'Activo',
  invitacionAceptada: true,
  invitacionAceptadaEn: '2026-10-01T10:00:00Z',
};

function solicitud(cambios: Partial<SolicitudHabeasDto> = {}): SolicitudHabeasDto {
  return {
    id: 's1',
    radicado: 'HD-2026-000001',
    tipo: 'Consulta',
    alcance: null,
    estado: 'Recibida',
    resultado: null,
    destino: { empresaId: null, nombre: 'Validasoft' },
    recibidaEn: '2026-11-02T15:00:00Z',
    venceEl: '2026-11-16',
    diasHabiles: 10,
    diasHabilesRestantes: 3,
    plazo: 'EnPlazo',
    dentroDelPlazo: null,
    resueltaEn: null,
    detalle: null,
    respuesta: null,
    contenidoSuprimido: false,
    constanciaHash: null,
    titular: null,
    ...cambios,
  };
}

function exportacion(cambios: Partial<ExportacionBovedaDto> = {}): ExportacionBovedaDto {
  return {
    id: 'x1',
    estado: 'Lista',
    solicitadaEn: '2026-11-10T15:00:00Z',
    terminadaEn: '2026-11-10T15:01:00Z',
    expiraEn: '2026-11-17T15:01:00Z',
    archivos: 12,
    bytes: 4_404_019,
    sha256: 'a'.repeat(64),
    hashManifiesto: 'b'.repeat(64),
    kid: 'k1',
    codigoDeError: null,
    descargas: 0,
    ...cambios,
  };
}

interface Peticion {
  metodo: string;
  url: URL;
  cuerpo: unknown;
}

function montar(solicitudes: SolicitudHabeasDto[], exportaciones: ExportacionBovedaDto[] = [], alMutar: (p: Peticion) => Response = () => json(201, solicitud())) {
  const mutaciones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost');
      const metodo = init?.method ?? 'GET';
      if (metodo === 'GET') {
        if (url.pathname.includes('/solicitudes-habeas')) return Promise.resolve(json(200, solicitudes));
        if (url.pathname.includes('/exportaciones')) return Promise.resolve(json(200, exportaciones));
        return Promise.resolve(json(200, [VINCULO]));
      }
      const peticion: Peticion = { metodo, url, cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null };
      mutaciones.push(peticion);
      return Promise.resolve(alMutar(peticion));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <PaginaPrivacidad />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return mutaciones;
}

afterEach(() => reiniciarCliente());

describe('Privacidad y mis datos', () => {
  it('sin solicitudes explica qué se puede pedir y ofrece la exportación de la bóveda', async () => {
    montar([]);
    expect(await screen.findByText('Aún no has hecho solicitudes')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Exportar mi bóveda' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Pedir exportación' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Nueva solicitud' })).toBeInTheDocument();
  });

  it('muestra cada solicitud con su radicado y cuántos días hábiles quedan, las abiertas primero', async () => {
    montar([
      solicitud({ id: 's2', radicado: 'HD-2026-000002', estado: 'Resuelta', resultado: 'Atendida', plazo: 'Resuelta', respuesta: 'Listo, te enviamos tus datos.', recibidaEn: '2026-10-20T10:00:00Z' }),
      solicitud({ tipo: 'Supresion', alcance: 'Documentos', destino: { empresaId: 'emp1', nombre: 'Constructora Norte' } }),
    ]);

    const tarjetas = await screen.findAllByRole('article');
    expect(tarjetas).toHaveLength(2);
    expect(within(tarjetas[0]!).getByText('HD-2026-000001')).toBeInTheDocument();
    expect(within(tarjetas[0]!).getByText(/Solo mis documentos/)).toBeInTheDocument();
    expect(within(tarjetas[0]!).getByText(/3 días hábiles/)).toBeInTheDocument();
    expect(within(tarjetas[1]!).getByText('Listo, te enviamos tus datos.')).toBeInTheDocument();
    expect(within(tarjetas[1]!).getByRole('button', { name: 'Ver constancia' })).toBeInTheDocument();
  });

  it('una solicitud vencida lo dice con la fecha, sin esconderlo', async () => {
    montar([solicitud({ plazo: 'Vencida', diasHabilesRestantes: -1 })]);
    expect(await screen.findByText(/venció el/i)).toBeInTheDocument();
  });

  it('antes de enviar una supresión avisa qué se conserva por ley y manda la solicitud con su alcance', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([]);

    await usuario.click(await screen.findByRole('button', { name: 'Nueva solicitud' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Nueva solicitud sobre mis datos' });
    await usuario.click(within(dialogo).getByRole('button', { name: 'Enviar solicitud' }));
    expect(within(dialogo).getAllByText('Elige qué quieres hacer.').length).toBeGreaterThan(0);
    expect(mutaciones).toHaveLength(0);

    await usuario.click(within(dialogo).getByRole('radio', { name: /Suprimir mis datos/ }));
    expect(within(dialogo).getByText(/la ley nos obliga a guardarlo/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('radio', { name: 'Solo mis documentos' }));
    expect(within(dialogo).getByText(/Validasoft \(mi bóveda\) recibe tu solicitud ahora y te responde en máximo 15 días hábiles/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Enviar solicitud' }));

    await waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.url.pathname).toBe('/public/v1/solicitudes-habeas');
    expect(mutaciones[0]?.cuerpo).toEqual({ tipo: 'Supresion', alcance: 'Documentos', empresaId: null, detalle: null });
  }, 20_000);

  it('la corrección pide decir qué dato está mal y no envía sin eso', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([]);

    await usuario.click(await screen.findByRole('button', { name: 'Nueva solicitud' }));
    const dialogo = await screen.findByRole('dialog');
    await usuario.click(within(dialogo).getByRole('radio', { name: /Corregir un dato/ }));
    await usuario.click(within(dialogo).getByRole('button', { name: 'Enviar solicitud' }));

    expect(within(dialogo).getAllByText(/Cuéntanos qué dato quieres corregir/).length).toBeGreaterThan(0);
    expect(mutaciones).toHaveLength(0);
  }, 20_000);

  it('una exportación lista dice cuántos archivos trae, cuánto pesa y hasta cuándo se puede bajar', async () => {
    montar([], [exportacion()]);
    expect(await screen.findByText(/12 archivos · 4,2 MB · disponible hasta/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Descargar' })).toBeInTheDocument();
  });

  it('pedir la exportación avisa que llega un correo cuando esté lista', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([], [], () => json(202, exportacion({ estado: 'Pendiente', archivos: 0, bytes: null })));

    await usuario.click(await screen.findByRole('button', { name: 'Pedir exportación' }));
    await waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.url.pathname).toBe('/public/v1/boveda/exportar');
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toMatch(/Te avisamos por correo cuando esté lista/));
  }, 20_000);
});
