import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, renderHook, waitFor } from '@testing-library/react';
import { configurarCliente, reiniciarCliente } from '@validasoft/api-client';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useDescargarCertificado } from '../features/certificados/api';
import { useAccionesPlantilla } from '../features/configuracion/tiposDocumento/api';
import { useDescargarAdjunto, useDescargarEvidencia, useDescargarEvidenciaMonitoreo } from '../features/cumplimiento/api';

const URL_PREFIRMADA = 'https://almacen.test/obj?firma=1';
const EXPIRA = '2026-11-14T12:05:00Z';

const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

const envoltorio = ({ children }: { children: ReactNode }) => (
  <QueryClientProvider client={new QueryClient({ defaultOptions: { mutations: { retry: false } } })}>{children}</QueryClientProvider>
);

/** Cada caso monta el hook de su pantalla y devuelve cómo disparar la descarga. */
const CASOS: { caso: string; respuesta: unknown; nombre: string; montar: () => () => Promise<unknown> }[] = [
  {
    caso: 'el PDF de un certificado',
    respuesta: { url: URL_PREFIRMADA, expira: EXPIRA, nombre: 'certificado.pdf', mime: 'application/pdf' },
    nombre: 'certificado.pdf',
    montar: () => {
      const { result } = renderHook(() => useDescargarCertificado('emp'), { wrapper: envoltorio });
      return () => result.current.mutateAsync('VERIF-C-1');
    },
  },
  {
    caso: 'la plantilla de un tipo de documento',
    respuesta: { url: URL_PREFIRMADA, expira: EXPIRA, nombre: 'formato.docx', mime: 'application/msword' },
    nombre: 'formato.docx',
    montar: () => {
      const { result } = renderHook(() => useAccionesPlantilla('tipo-1'), { wrapper: envoltorio });
      return () => result.current.descargar.mutateAsync(2);
    },
  },
  {
    caso: 'la evidencia de listas restrictivas',
    respuesta: { url: URL_PREFIRMADA, expira: EXPIRA, nombreArchivo: 'evidencia.pdf' },
    nombre: 'evidencia.pdf',
    montar: () => {
      const { result } = renderHook(() => useDescargarEvidencia('emp'), { wrapper: envoltorio });
      return () => result.current.mutateAsync({ expedienteId: 'exp-1' });
    },
  },
  {
    caso: 'la evidencia del monitoreo',
    respuesta: { url: URL_PREFIRMADA, expira: EXPIRA, nombreArchivo: 'monitoreo.pdf' },
    nombre: 'monitoreo.pdf',
    montar: () => {
      const { result } = renderHook(() => useDescargarEvidenciaMonitoreo('emp'), { wrapper: envoltorio });
      return () => result.current.mutateAsync('vin-1');
    },
  },
  {
    caso: 'un archivo del oficial de cumplimiento',
    respuesta: { url: URL_PREFIRMADA, expira: EXPIRA, nombreArchivo: 'concepto.pdf' },
    nombre: 'concepto.pdf',
    montar: () => {
      const { result } = renderHook(() => useDescargarAdjunto('emp', 'exp-1'), { wrapper: envoltorio });
      return () => result.current.mutateAsync('adj-1');
    },
  },
];

let enlaces: { href: string; download: string; target: string }[];
let abrirVentana: ReturnType<typeof vi.spyOn>;

beforeEach(() => {
  enlaces = [];
  abrirVentana = vi.spyOn(window, 'open').mockReturnValue(null);
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    enlaces.push({ href: this.href, download: this.download, target: this.target });
  });
});

afterEach(() => {
  reiniciarCliente();
  vi.restoreAllMocks();
});

/** Safari en iPhone bloquea sin error un `window.open` que ocurre después de un `await`: las descargas se siguen con un enlace. */
describe('descargas con URL prefirmada del web de empresa', () => {
  it.each(CASOS)('$caso se ofrece con un enlace de descarga y nunca con window.open', async ({ respuesta, nombre, montar }) => {
    configurarCliente({ fetch: vi.fn(() => Promise.resolve(json(respuesta))) });
    const descargar = montar();

    await act(async () => {
      await descargar();
    });

    await waitFor(() => expect(enlaces).toHaveLength(1));
    expect(enlaces[0]).toEqual({ href: URL_PREFIRMADA, download: nombre, target: '' });
    expect(abrirVentana).not.toHaveBeenCalled();
  });
});
