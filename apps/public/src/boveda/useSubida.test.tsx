import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { VersionDocumentoDto } from '@validasoft/api-client';
import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { OpcionesSubida, ResultadoSubida } from './subida';
import { useSubida, type EstadoSubida } from './useSubida';

const subirDocumentoSimulado = vi.hoisted(() => vi.fn<(opciones: OpcionesSubida) => Promise<ResultadoSubida>>());

vi.mock('./subida', async (importarOriginal) => ({ ...(await importarOriginal<typeof import('./subida')>()), subirDocumento: subirDocumentoSimulado }));

const DESTINO = { tipoCanonico: 'RUT' };

const archivo = (nombre: string, tipo = 'application/pdf', contenido = 'abc') => new File([contenido], nombre, { type: tipo });

const version = { documentoId: 'd1', n: 1, estado: 'Actual' } as VersionDocumentoDto;
const lista = (): ResultadoSubida => ({ tipo: 'lista', version, compartidos: [] });

/** Una subida simulada que se resuelve o rechaza cuando el test lo decide; guarda lo que `useSubida` le pasó. */
function pendiente() {
  let resolver: (resultado: ResultadoSubida) => void = () => undefined;
  let rechazar: (error: unknown) => void = () => undefined;
  const promesa = new Promise<ResultadoSubida>((si, no) => {
    resolver = si;
    rechazar = no;
  });
  return { promesa, resolver, rechazar };
}

function montar() {
  const cliente = new QueryClient();
  const invalidar = vi.spyOn(cliente, 'invalidateQueries');
  const envoltorio = ({ children }: { children: ReactNode }) => <QueryClientProvider client={cliente}>{children}</QueryClientProvider>;
  return { ...renderHook(() => useSubida(), { wrapper: envoltorio }), invalidar };
}

/** La fase y el nombre del archivo que la pantalla está mostrando. */
const enPantalla = (estado: EstadoSubida) => ({ fase: estado.fase, archivo: 'archivo' in estado ? estado.archivo.name : null });

/** Lo que `useSubida` le pasó a la n-ésima llamada de `subirDocumento`. */
const llamada = (n: number): OpcionesSubida => {
  const opciones = subirDocumentoSimulado.mock.calls[n]?.[0];
  if (!opciones) throw new Error(`No hubo llamada ${String(n)}`);
  return opciones;
};

beforeEach(() => {
  subirDocumentoSimulado.mockReset();
});

describe('useSubida: solo la última subida manda sobre la pantalla', () => {
  it('al empezar otra aborta la anterior y, si la vieja termina después, no pisa a la nueva', async () => {
    const a = pendiente();
    const b = pendiente();
    subirDocumentoSimulado.mockReturnValueOnce(a.promesa).mockReturnValueOnce(b.promesa);
    const { result, invalidar } = montar();

    act(() => result.current.subir(archivo('a.pdf'), DESTINO));
    act(() => result.current.subir(archivo('b.pdf'), DESTINO));

    expect(llamada(0).senal?.aborted).toBe(true);
    expect(llamada(1).senal?.aborted).toBe(false);

    await act(async () => {
      a.resolver(lista());
      await a.promesa;
    });
    expect(enPantalla(result.current.estado)).toEqual({ fase: 'en-curso', archivo: 'b.pdf' });
    // La versión de la vieja sí quedó en el servidor: la bóveda se vuelve a pedir, pero la pantalla sigue con la nueva.
    expect(invalidar).toHaveBeenCalledTimes(1);

    await act(async () => {
      b.resolver(lista());
      await b.promesa;
    });
    expect(enPantalla(result.current.estado)).toEqual({ fase: 'resultado', archivo: 'b.pdf' });
  });

  it('el fallo de la anterior no se muestra ni marca el formulario de la nueva', async () => {
    const a = pendiente();
    const b = pendiente();
    subirDocumentoSimulado.mockReturnValueOnce(a.promesa).mockReturnValueOnce(b.promesa);
    const alFallarVieja = vi.fn();
    const { result } = montar();

    act(() => result.current.subir(archivo('a.pdf'), DESTINO, { alFallar: alFallarVieja }));
    act(() => result.current.subir(archivo('b.pdf'), DESTINO));
    await act(async () => {
      a.rechazar(new DOMException('Subida cancelada', 'AbortError'));
      await a.promesa.catch(() => undefined);
    });

    expect(alFallarVieja).not.toHaveBeenCalled();
    expect(enPantalla(result.current.estado)).toEqual({ fase: 'en-curso', archivo: 'b.pdf' });
  });

  it('las etapas y el progreso de una subida abortada no tocan a la nueva', () => {
    subirDocumentoSimulado.mockReturnValue(new Promise<ResultadoSubida>(() => undefined));
    const { result } = montar();

    act(() => result.current.subir(archivo('a.pdf'), DESTINO));
    act(() => result.current.subir(archivo('b.pdf'), DESTINO));
    act(() => {
      llamada(0).alFase?.('subiendo');
      llamada(0).alProgreso?.(0.9);
    });
    expect(result.current.estado).toMatchObject({ fase: 'en-curso', etapa: 'preparando', progreso: 0 });

    act(() => {
      llamada(1).alFase?.('subiendo');
      llamada(1).alProgreso?.(0.4);
    });
    expect(result.current.estado).toMatchObject({ fase: 'en-curso', etapa: 'subiendo', progreso: 0.4 });
  });

  it('un archivo nuevo que no pasa la validación también cancela la subida que seguía en curso', async () => {
    const a = pendiente();
    subirDocumentoSimulado.mockReturnValueOnce(a.promesa);
    const { result } = montar();

    act(() => result.current.subir(archivo('a.pdf'), DESTINO));
    act(() => result.current.subir(archivo('foto.heic', 'image/heic'), DESTINO));
    expect(llamada(0).senal?.aborted).toBe(true);
    expect(result.current.estado.fase).toBe('problema');

    await act(async () => {
      a.resolver(lista());
      await a.promesa;
    });
    expect(result.current.estado.fase).toBe('problema');
  });

  it('al desmontar se aborta la subida en curso y su desenlace ya no escribe en la pantalla', async () => {
    const a = pendiente();
    subirDocumentoSimulado.mockReturnValueOnce(a.promesa);
    const alFallar = vi.fn();
    const { result, unmount } = montar();

    act(() => result.current.subir(archivo('a.pdf'), DESTINO, { alFallar }));
    unmount();
    expect(llamada(0).senal?.aborted).toBe(true);

    await act(async () => {
      a.rechazar(new Error('sin red'));
      await a.promesa.catch(() => undefined);
    });
    expect(alFallar).not.toHaveBeenCalled();
  });

  it('cancelar aborta la subida y vuelve al inicio', () => {
    subirDocumentoSimulado.mockReturnValue(new Promise<ResultadoSubida>(() => undefined));
    const { result } = montar();

    act(() => result.current.subir(archivo('a.pdf'), DESTINO));
    act(() => result.current.cancelar());

    expect(llamada(0).senal?.aborted).toBe(true);
    expect(result.current.estado).toEqual({ fase: 'inicio' });
  });
});
