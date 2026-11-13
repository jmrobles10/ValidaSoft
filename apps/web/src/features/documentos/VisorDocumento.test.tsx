import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { VisorDocumento, type DocumentoEnVisor } from './VisorDocumento';

const DOCUMENTO: DocumentoEnVisor = { documentoId: 'd1', tipo: 'Registro Único Tributario', n: 2, cargadaEn: '2026-11-01T10:00:00Z' };

function montar(documento: DocumentoEnVisor = DOCUMENTO) {
  let entregarArchivo: () => void = () => undefined;
  const archivo = new Promise<Response>((resolver) => {
    entregarArchivo = () => resolver(new Response(new Uint8Array([37, 80, 68, 70]), { status: 200, headers: { 'Content-Type': 'application/pdf' } }));
  });
  // El archivo se lee de la URL prefirmada con el `fetch` global; la URL misma, con el del cliente de la API.
  vi.stubGlobal('fetch', vi.fn(() => archivo));
  configurarCliente({
    fetch: vi.fn(() =>
      Promise.resolve(
        new Response(JSON.stringify({ url: 'https://almacen.test/rut.pdf', expira: '2026-11-13T12:00:00Z', nombre: 'rut.pdf', mime: 'application/pdf' }), {
          status: 200,
          headers: { 'Content-Type': 'application/json' },
        }),
      ),
    ),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <VisorDocumento empresaId="e1" vinculoId="v1" documento={documento} onCerrar={vi.fn()} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { entregarArchivo };
}

describe('VisorDocumento: el botón «Descargar»', () => {
  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => 'blob:visor');
    URL.revokeObjectURL = vi.fn();
  });
  afterEach(() => {
    reiniciarCliente();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it('conserva su rol de botón mientras llega la URL y después: empieza deshabilitado y se habilita sin volverse enlace', async () => {
    const { entregarArchivo } = montar();

    // Mientras se pide la descarga: botón deshabilitado (no hay enlace todavía).
    const pendiente = screen.getByRole('button', { name: 'Descargar' });
    expect(pendiente).toBeDisabled();
    expect(screen.queryByRole('link', { name: 'Descargar' })).not.toBeInTheDocument();

    // Llegó la URL: el mismo botón, ya habilitado; sigue sin haber un enlace con ese nombre.
    await waitFor(() => expect(screen.getByRole('button', { name: 'Descargar' })).toBeEnabled());
    expect(screen.queryByRole('link', { name: 'Descargar' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Descargar' })).toBe(pendiente);

    entregarArchivo();
    await waitFor(() => expect(screen.getByTitle(/Registro Único Tributario/)).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Descargar' })).toBeEnabled();
  });

  it('al pulsarlo ofrece el archivo con su nombre', async () => {
    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const crear = vi.spyOn(document, 'createElement');
    montar();
    await waitFor(() => expect(screen.getByRole('button', { name: 'Descargar' })).toBeEnabled());

    await userEvent.click(screen.getByRole('button', { name: 'Descargar' }));

    expect(clic).toHaveBeenCalledTimes(1);
    const enlace = crear.mock.results.map((r) => r.value as unknown).find((e): e is HTMLAnchorElement => e instanceof HTMLAnchorElement);
    expect(enlace?.getAttribute('href')).toBe('https://almacen.test/rut.pdf');
    expect(enlace?.download).toBe('rut.pdf');
  });

  it('una versión de solo formulario no tiene archivo que descargar', () => {
    montar({ ...DOCUMENTO, sinArchivo: true });
    expect(screen.queryByRole('button', { name: 'Descargar' })).not.toBeInTheDocument();
  });
});
