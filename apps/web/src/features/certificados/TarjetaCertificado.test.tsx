import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type CertificadoDelVinculoDto, type CertificadoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { TarjetaCertificado } from './TarjetaCertificado';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const ACTUAL = {
  id: 'c1',
  folio: 'NORTE-C-2026-000001-1',
  folioRaiz: 'NORTE-C-2026-000001',
  emision: 1,
  esLaActual: true,
  estado: 'Vigente',
  bloqueante: false,
  vinculoId: 'v1',
  expedienteId: 'x1',
  expedienteFolio: 'NORTE-E-2026-000001',
  emitidoEn: '2026-11-08T15:00:00Z',
  vigenteHasta: '2027-11-08',
  huella: 'AB12-CD34',
  hashContenido: 'abc',
  urlVerificacion: 'http://localhost:5181/verificar/NORTE-C-2026-000001-1?huella=AB12-CD34',
  pendientes: [],
  suspension: null,
  revocacion: null,
  pdf: null,
} as unknown as CertificadoDto;

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

function montar(datos: CertificadoDelVinculoDto, responder: (url: string, metodo: string, cuerpo: unknown) => Response | null = () => null) {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      if (metodo !== 'GET') peticiones.push({ url, metodo, cuerpo });
      return Promise.resolve(responder(url, metodo, cuerpo) ?? json(datos));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <TarjetaCertificado vinculo={{ id: 'v1' }} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
});

describe('Certificado del vínculo', () => {
  it('sin certificados:leer no se muestra', () => {
    montar({ vinculoId: 'v1', actual: ACTUAL, emisiones: [] });
    expect(screen.queryByText('Certificado')).not.toBeInTheDocument();
  });

  it('muestra estado, folio, vigencia sin correrse y el enlace de verificación; el PDF pendiente se explica', async () => {
    permisos.scopes = new Set([SCOPES.certificadosLeer]);
    const usuario = userEvent.setup();
    montar({ vinculoId: 'v1', actual: ACTUAL, emisiones: [] }, (url, metodo) => (metodo === 'POST' && url.endsWith('/descarga') ? json({ codigo: 'pdf-pendiente', detail: 'Se está generando.', status: 409 }, 409) : null));
    expect(await screen.findByText('NORTE-C-2026-000001-1')).toBeInTheDocument();
    expect(screen.getByText('08/11/2027')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Página de verificación/ })).toHaveAttribute('href', ACTUAL.urlVerificacion);
    expect(screen.queryByRole('button', { name: /Revocar/ })).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Descargar PDF' }));
    expect(await screen.findByText(/El PDF se está generando/)).toBeInTheDocument();
  });

  it('sin certificado dice cuándo se emite', async () => {
    permisos.scopes = new Set([SCOPES.certificadosLeer]);
    montar({ vinculoId: 'v1', actual: null, emisiones: [] });
    expect(await screen.findByText(/Se emite cuando se aprueba el expediente/)).toBeInTheDocument();
  });

  it('revocar pide justificación (botón deshabilitado sin ella) y la envía', async () => {
    permisos.scopes = new Set([SCOPES.certificadosLeer, SCOPES.expedientesAdministrar]);
    const usuario = userEvent.setup();
    const peticiones = montar({ vinculoId: 'v1', actual: ACTUAL, emisiones: [] }, (url) => (url.endsWith('/revocar') ? json({ ...ACTUAL, estado: 'Revocado' }) : null));
    await usuario.click(await screen.findByRole('button', { name: 'Revocar certificado' }));
    const dialogo = await screen.findByRole('dialog');
    const confirmar = within(dialogo).getByRole('button', { name: 'Revocar certificado' });
    expect(confirmar).toBeDisabled();
    await usuario.type(within(dialogo).getByRole('textbox', { name: /Justificación/ }), 'Se aprobó con un documento adulterado');
    await usuario.click(confirmar);
    await waitFor(() => expect(peticiones).toContainEqual({ url: '/api/v1/certificados/NORTE-C-2026-000001-1/revocar', metodo: 'POST', cuerpo: { justificacion: 'Se aprobó con un documento adulterado' } }));
  });
});
