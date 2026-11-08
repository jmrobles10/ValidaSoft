import { configurarCliente, reiniciarCliente } from '@validasoft/api-client';
import { renderHook, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { cargarOrigenesPermitidos, origenesPermitidos, RUTA_ORIGENES_PERMITIDOS } from './puenteEmbed';
import { usePuenteEmbed } from './usePuenteEmbed';

// La página de pruebas no está en un iframe: se simula que sí para probar el puente completo.
vi.mock('./puenteEmbed', async (importarOriginal) => ({
  ...(await importarOriginal<typeof import('./puenteEmbed')>()),
  estaEmbebida: () => true,
}));

const SRM = 'https://srm.cliente.co';

const respuestaJson = (cuerpo: unknown, status = 200) =>
  new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  reiniciarCliente();
});

describe('orígenes permitidos en tiempo de ejecución (SE-2)', () => {
  it('usa la lista de la API y deja VITE_ORIGENES_CONTENEDOR solo como respaldo', () => {
    const configurados = 'https://viejo.cliente.co';
    expect(origenesPermitidos(configurados, 'https://validasoft.co', [SRM])).toEqual(['https://validasoft.co', SRM]);
    expect(origenesPermitidos(configurados, 'https://validasoft.co', [SRM], true)).toEqual([
      'https://validasoft.co',
      SRM,
      'https://viejo.cliente.co',
    ]);
    // Sin lista de la API (aún no llega o falló): el propio y los configurados.
    expect(origenesPermitidos(configurados, 'https://validasoft.co', null)).toEqual(['https://validasoft.co', 'https://viejo.cliente.co']);
    expect(origenesPermitidos(undefined, 'https://validasoft.co', [])).toEqual(['https://validasoft.co']);
  });

  it('lee la lista del endpoint público y descarta lo que no es un origen', async () => {
    const fetchFalso = vi.fn<typeof fetch>(() =>
      Promise.resolve(respuestaJson({ origenes: [SRM, 'https://srm.cliente.co/ruta', 42, 'http://localhost:5173'], vigenciaSegundos: 60 })),
    );
    await expect(cargarOrigenesPermitidos('https://api.validasoft.co', fetchFalso)).resolves.toEqual([SRM, 'http://localhost:5173']);
    expect(fetchFalso).toHaveBeenCalledWith(`https://api.validasoft.co${RUTA_ORIGENES_PERMITIDOS}`, expect.anything());
  });

  it('devuelve null si la API falla o responde otra cosa', async () => {
    await expect(cargarOrigenesPermitidos('', () => Promise.resolve(respuestaJson({}, 500)))).resolves.toBeNull();
    await expect(cargarOrigenesPermitidos('', () => Promise.resolve(respuestaJson({ origenes: 'https://srm.cliente.co' })))).resolves.toBeNull();
    await expect(cargarOrigenesPermitidos('', () => Promise.reject(new TypeError('sin red')))).resolves.toBeNull();
  });
});

describe('usePuenteEmbed con orígenes de la API', () => {
  it('responde al contenedor solo cuando la API confirma su origen', async () => {
    let entregar: (r: Response) => void = () => undefined;
    const fetchFalso = vi.fn<typeof fetch>(() => new Promise<Response>((resolver) => (entregar = resolver)));
    vi.stubGlobal('fetch', fetchFalso);
    configurarCliente({ baseUrl: 'https://api.validasoft.co' });
    vi.spyOn(document, 'referrer', 'get').mockReturnValue(`${SRM}/compras/proveedor/7`);
    const postMessage = vi.spyOn(window.parent, 'postMessage').mockImplementation(() => undefined);

    const { result } = renderHook(() => usePuenteEmbed());

    // Antes de la respuesta, el SRM no está en el respaldo: no se le envía nada.
    expect(result.current.origenContenedor).toBeNull();
    expect(postMessage).not.toHaveBeenCalled();
    expect(fetchFalso).toHaveBeenCalledWith(`https://api.validasoft.co${RUTA_ORIGENES_PERMITIDOS}`, expect.anything());

    entregar(respuestaJson({ origenes: [SRM], vigenciaSegundos: 60 }));
    await waitFor(() => expect(result.current.origenContenedor).toBe(SRM));
    expect(postMessage).toHaveBeenCalledWith({ tipo: 'validasoft:ready', version: 1 }, SRM);
  });

  it('no responde a un contenedor que la API no publica', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn<typeof fetch>(() => Promise.resolve(respuestaJson({ origenes: ['https://otro.cliente.co'], vigenciaSegundos: 60 }))),
    );
    vi.spyOn(document, 'referrer', 'get').mockReturnValue(`${SRM}/`);
    const postMessage = vi.spyOn(window.parent, 'postMessage').mockImplementation(() => undefined);

    const { result } = renderHook(() => usePuenteEmbed());
    await waitFor(() => expect(vi.mocked(globalThis.fetch)).toHaveBeenCalled());
    await new Promise((r) => setTimeout(r, 0));

    expect(result.current.origenContenedor).toBeNull();
    expect(postMessage).not.toHaveBeenCalled();
  });
});
