import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type EmpresaAfectadaPorRetiroDto, type ImpactoDeRetiroDto, type VersionDocumentoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../shared/useAvisos';
import { DialogoRetirar } from './DialogoRetirar';

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

const VERSION = { documentoId: 'd1', n: 8, estado: 'Actual' } as VersionDocumentoDto;

const norte = (cambios: Partial<EmpresaAfectadaPorRetiroDto> = {}): EmpresaAfectadaPorRetiroDto => ({
  vinculoId: 'v1',
  empresa: { id: 'e1', nombre: 'Constructora Norte S.A.S.', logoUrl: null },
  situacion: 'Aprobo',
  efecto: 'Revalidar',
  folio: null,
  ...cambios,
});

const impacto = (cambios: Partial<ImpactoDeRetiroDto> = {}): ImpactoDeRetiroDto => ({
  documentoId: 'd1',
  numero: 8,
  esLaVigente: true,
  versionQueQuedaVigente: 7,
  empresas: [norte()],
  secciones: [],
  ...cambios,
});

interface Llamada {
  metodo: string;
  ruta: string;
  cuerpo: unknown;
}

function montar(respuesta: () => Response | Promise<Response>, enlaceSubir: string | null = '/boveda/subir?documento=d1&tipo=RUT') {
  const llamadas: Llamada[] = [];
  configurarCliente({
    fetch: vi.fn((url: RequestInfo | URL, init?: RequestInit) => {
      const ruta = typeof url === 'string' ? url : url instanceof URL ? url.href : url.url;
      llamadas.push({ metodo: init?.method ?? 'GET', ruta, cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null });
      if (ruta.endsWith('/retirar')) return Promise.resolve(json({ documento: {}, binarioEliminado: false, compartidos: [] }));
      return Promise.resolve(respuesta());
    }),
  });
  const alCerrar = vi.fn();
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <DialogoRetirar documentoId="d1" tipo="Registro Único Tributario" version={VERSION} enlaceSubir={enlaceSubir} onCerrar={alCerrar} />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { llamadas, alCerrar };
}

afterEach(() => {
  reiniciarCliente();
  useAvisos.getState().cerrar();
});

describe('retirar una versión: lo que pasará, antes del motivo', () => {
  it('cuenta qué versión queda vigente y qué le toca a cada empresa, y ofrece subir una versión corregida', async () => {
    const usuario = userEvent.setup();
    const { llamadas, alCerrar } = montar(() => json(impacto()));

    // Mientras se calcula, un esqueleto (nunca «Cargando…») y sin motivo ni confirmar.
    expect(screen.getByRole('dialog', { name: 'Retirar la versión 8 de Registro Único Tributario' })).toBeInTheDocument();
    expect(screen.queryByText(/Cargando/)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/Motivo/)).not.toBeInTheDocument();

    expect(await screen.findByText('Tu Registro Único Tributario vigente pasará a ser la versión 7.')).toBeInTheDocument();
    expect(screen.getByText('Constructora Norte S.A.S. la aprobó: tendrá que revisar de nuevo tu Registro Único Tributario versión 7.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Subir una versión corregida' })).toHaveAttribute('href', '/boveda/subir?documento=d1&tipo=RUT');
    expect(screen.getByText(/Les avisamos con tu motivo/)).toBeInTheDocument();

    // El motivo se pide después del impacto; sin él no se puede confirmar.
    const confirmar = screen.getByRole('button', { name: 'Retirar versión' });
    expect(confirmar).toBeDisabled();
    await usuario.type(screen.getByLabelText(/Motivo/), 'Subí el RUT de otra empresa');
    expect(confirmar).toBeEnabled();
    await usuario.click(confirmar);

    await vi.waitFor(() => expect(alCerrar).toHaveBeenCalled());
    expect(llamadas.find((l) => l.ruta.endsWith('/retirar'))).toMatchObject({ metodo: 'POST', cuerpo: { motivo: 'Subí el RUT de otra empresa' } });
    expect(llamadas[0]?.ruta).toContain('/documentos/d1/versiones/8/retiro/impacto');
    expect(useAvisos.getState().actual?.mensaje).toBe('Retiraste la versión 8.');
  });

  it('si nadie la recibió lo dice y es simple: sin alternativa ni avisos', async () => {
    montar(() => json(impacto({ empresas: [], versionQueQuedaVigente: null })));

    expect(await screen.findByText('No tienes otra versión: te quedarás sin Registro Único Tributario vigente hasta que subas uno nuevo.')).toBeInTheDocument();
    expect(screen.getByText('Ninguna empresa la ha recibido: este cambio no afecta a nadie.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Subir una versión corregida' })).not.toBeInTheDocument();
    expect(screen.queryByText(/Les avisamos/)).not.toBeInTheDocument();
    expect(await screen.findByLabelText(/Motivo/)).toBeInTheDocument();
  });

  it('la empresa que revisa dice su folio, y las secciones de la ficha que la usan de soporte se nombran', async () => {
    montar(() =>
      json(
        impacto({
          empresas: [
            norte({ vinculoId: 'v2', empresa: { id: 'e2', nombre: 'Constructora Sur', logoUrl: null }, situacion: 'Recibio', efecto: 'VuelveALaAnterior' }),
            norte({ situacion: 'Revisa', efecto: 'VolverARevisar', folio: 'NORTE-E-2026-000012' }),
          ],
          secciones: [
            { seccion: 'FICHA_GENERAL', nombre: 'Información general', periodo: null },
            { seccion: 'FICHA_FINANCIERA', nombre: 'Información financiera', periodo: 2025 },
          ],
        }),
      ),
    );

    const empresas = await screen.findByRole('heading', { name: 'Qué pasa con tus empresas' });
    const lista = within(empresas.parentElement!).getAllByRole('listitem');
    // A quien le pesa va primero, aunque su nombre no sea el primero.
    expect(lista[0]).toHaveTextContent('Constructora Norte S.A.S. la está revisando (NORTE-E-2026-000012): su revisión pasa a la versión 7 y tendrá que volver a revisarla.');
    expect(lista[1]).toHaveTextContent('Constructora Sur la recibió: volverá a tener la versión 7.');
    expect(screen.getByText('Información general usa esta versión como soporte: tendrás que elegir otro soporte. Tus datos no se borran.')).toBeInTheDocument();
    expect(screen.getByText('Información financiera · 2025 usa esta versión como soporte: tendrás que elegir otro soporte. Tus datos no se borran.')).toBeInTheDocument();
  });

  it('retirar una versión que no es la vigente dice que lo que ven las empresas no cambia', async () => {
    montar(() => json(impacto({ esLaVigente: false, versionQueQuedaVigente: 9, empresas: [norte({ situacion: 'Recibio', efecto: 'SinCambios' })] })));

    expect(await screen.findByText('No es tu versión vigente: la vigente sigue siendo la 9.')).toBeInTheDocument();
    expect(screen.getByText('Tus empresas la recibieron, pero no les cambia nada.')).toBeInTheDocument();
    expect(screen.getByText('Constructora Norte S.A.S. la recibió: no le cambia nada.')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Subir una versión corregida' })).not.toBeInTheDocument();
  });

  it('si no se pudo calcular el impacto no deja confirmar a ciegas y permite reintentar', async () => {
    const usuario = userEvent.setup();
    let intento = 0;
    montar(() => (++intento === 1 ? json({ codigo: 'estado-invalido', detalle: 'No se puede retirar una versión Retirada.', status: 409 }, 409) : json(impacto())));

    expect(await screen.findByText(/No pudimos calcular qué pasará con tus empresas/)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Motivo/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Retirar versión' })).toBeDisabled();

    await usuario.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(await screen.findByLabelText(/Motivo/)).toBeInTheDocument();
  });
});
