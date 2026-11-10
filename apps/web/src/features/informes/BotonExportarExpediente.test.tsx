import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { BotonExportarExpediente } from './BotonExportarExpediente';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

function montar() {
  const peticiones: { url: string; empresa: string | null }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      peticiones.push({ url, empresa: new Headers(init?.headers).get('X-Empresa') });
      return Promise.resolve(new Response(new Uint8Array([80, 75]), { headers: { 'Content-Disposition': 'attachment; filename=expediente-N-1.zip' } }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient()}>
        <BotonExportarExpediente empresaId="emp" expedienteId="x1" />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
});

describe('BotonExportarExpediente', () => {
  it('pide expedientes:exportar y documentos:descargar', () => {
    permisos.scopes = new Set([SCOPES.expedientesExportar]);
    montar();
    expect(screen.queryByRole('button', { name: 'Exportar expediente' })).not.toBeInTheDocument();
  });

  it('descarga el ZIP del expediente con la empresa activa', async () => {
    permisos.scopes = new Set([SCOPES.expedientesExportar, SCOPES.documentosDescargar]);
    const peticiones = montar();
    await userEvent.click(screen.getByRole('button', { name: 'Exportar expediente' }));
    await waitFor(() => expect(peticiones).toEqual([{ url: '/api/v1/expedientes/x1/exportar', empresa: 'emp' }]));
  });
});
