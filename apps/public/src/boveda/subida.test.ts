import { configurarCliente, ErrorApi, ErrorDeRed, reiniciarCliente, type VersionDocumentoDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ErrorSubida, falloDeSubida, formatearTamano, sha256Hex, subirDocumento, TAMANO_MAXIMO_BYTES, validarArchivo, type EnviarArchivo } from './subida';

const json = (estado: number, cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

function version(cambios: Partial<VersionDocumentoDto> = {}): VersionDocumentoDto {
  return {
    documentoId: 'd1',
    n: 2,
    estado: 'Actual',
    cargadaEn: '2026-11-06T12:00:00Z',
    cargadaPor: 'Tercero',
    clase: 'Archivo',
    nombreOriginal: 'rut.pdf',
    mime: 'application/pdf',
    tamanoBytes: 3,
    paginas: 1,
    sha256: 'x',
    motivoDevolucion: null,
    detalleDevolucion: null,
    hechos: null,
    ...cambios,
  };
}

const archivo = () => new File(['abc'], 'rut.pdf', { type: 'application/pdf' });
const CARGA = { metodo: 'PUT', url: 'http://minio/cargas/1', cabeceras: { 'Content-Type': 'application/pdf' }, expira: '2026-11-06T12:15:00Z' };

/** La API simulada: paso 1 y paso 2 por URL; registra los cuerpos. */
function api(paso1: () => Response, paso2?: () => Response) {
  const cuerpos: unknown[] = [];
  configurarCliente({
    fetch: vi.fn((url: RequestInfo | URL, init?: RequestInit) => {
      const ruta = typeof url === 'string' ? url : url instanceof URL ? url.href : url.url;
      cuerpos.push(typeof init?.body === 'string' ? JSON.parse(init.body) : null);
      if (ruta.endsWith('/confirmar')) return Promise.resolve(paso2?.() ?? json(500, {}));
      return Promise.resolve(paso1());
    }),
  });
  return cuerpos;
}

afterEach(() => reiniciarCliente());

describe('validación en el navegador', () => {
  it('rechaza vacío, más de 20 MB y formatos que la bóveda no acepta', () => {
    expect(validarArchivo({ size: 0, type: 'application/pdf' })).toEqual({ tipo: 'vacio' });
    expect(validarArchivo({ size: TAMANO_MAXIMO_BYTES + 1, type: 'application/pdf' })).toEqual({ tipo: 'demasiado-grande', tamano: TAMANO_MAXIMO_BYTES + 1 });
    expect(validarArchivo({ size: 10, type: 'image/heic' })).toEqual({ tipo: 'formato', mime: 'image/heic' });
    expect(validarArchivo({ size: 10, type: 'image/jpeg' })).toBeNull();
  });

  it('calcula el SHA-256 del archivo en hexadecimal', async () => {
    await expect(sha256Hex(new Blob(['abc']))).resolves.toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
  });

  it('muestra el tamaño en MB o KB', () => {
    expect(formatearTamano(2.4 * 1024 * 1024)).toBe('2,4 MB');
    expect(formatearTamano(820 * 1024)).toBe('820 KB');
  });
});

describe('subida en dos pasos', () => {
  it('inicia con nombre, tamaño, tipo y hash; sube a la URL prefirmada y confirma: queda lista y compartida', async () => {
    const compartidos = [{ documentoId: 'd1', tipo: 'RUT', version: 2, vinculoId: 'v1', empresaId: 'e1' }];
    const cuerpos = api(() => json(201, { documentoId: 'd1', version: 2, duplicado: false, carga: CARGA }), () => json(200, { duplicado: false, version: version(), compartidos }));
    const enviar = vi.fn<EnviarArchivo>((_carga, _archivo, alProgreso) => {
      alProgreso(1);
      return Promise.resolve();
    });
    const fases: string[] = [];

    const resultado = await subirDocumento({ archivo: archivo(), destino: { tipoCanonico: 'RUT' }, enviar, alFase: (f) => fases.push(f) });

    expect(resultado).toEqual({ tipo: 'lista', version: version(), compartidos });
    expect(fases).toEqual(['preparando', 'subiendo', 'confirmando']);
    expect(enviar).toHaveBeenCalledWith(CARGA, expect.any(File), expect.any(Function), undefined);
    expect(cuerpos[0]).toEqual({
      nombreArchivo: 'rut.pdf',
      tamanoBytes: 3,
      tipoMime: 'application/pdf',
      tipoCanonico: 'RUT',
      sha256: 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad',
    });
  });

  it('si el archivo ya es una versión, el paso 1 lo dice y no se sube nada', async () => {
    api(() => json(200, { documentoId: 'd1', version: 1, duplicado: true, carga: null }));
    const enviar = vi.fn<EnviarArchivo>();
    await expect(subirDocumento({ archivo: archivo(), destino: { tipoCanonico: 'RUT' }, enviar })).resolves.toEqual({ tipo: 'duplicado', numero: 1 });
    expect(enviar).not.toHaveBeenCalled();
  });

  it('una versión devuelta (PDF con contenido activo) llega con su motivo', async () => {
    const devuelta = version({ estado: 'Devuelta', motivoDevolucion: 'pdf-con-contenido-activo' });
    api(() => json(201, { documentoId: 'd1', version: 2, duplicado: false, carga: CARGA }), () => json(200, { duplicado: false, version: devuelta, compartidos: [] }));
    await expect(subirDocumento({ archivo: archivo(), destino: { tipoCanonico: 'RUT' }, enviar: () => Promise.resolve() })).resolves.toEqual({ tipo: 'devuelta', version: devuelta });
  });

  it('clasifica 413, 415, sin red y la URL vencida', async () => {
    api(() => json(413, { codigo: 'archivo-demasiado-grande', detail: 'Supera 20 MB' }));
    const error: unknown = await subirDocumento({ archivo: archivo(), destino: { tipoCanonico: 'RUT' } }).catch((e: unknown) => e);
    expect(falloDeSubida(error)).toBe('demasiado-grande');
    expect(falloDeSubida(new ErrorApi(415, 'tipo-archivo-no-soportado', '', null, null))).toBe('formato');
    expect(falloDeSubida(new ErrorDeRed('sin red', null))).toBe('red');
    expect(falloDeSubida(new ErrorSubida(0))).toBe('red');
    expect(falloDeSubida(new ErrorSubida(403))).toBe('vencida');
    expect(falloDeSubida(new ErrorApi(422, 'hash-no-coincide', '', null, null))).toBe('otro');
  });
});
