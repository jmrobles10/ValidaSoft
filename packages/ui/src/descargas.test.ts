import { afterEach, describe, expect, it, vi } from 'vitest';
import { abrirDescarga, guardarArchivo } from './descargas';

afterEach(() => vi.restoreAllMocks());

describe('guardarArchivo', () => {
  it('ofrece los bytes con el nombre del servidor, o el de reserva, y libera el blob después del clic', () => {
    vi.useFakeTimers();
    const liberar = vi.fn();
    Object.assign(URL, { createObjectURL: vi.fn(() => 'blob:prueba'), revokeObjectURL: liberar });
    const nombres: string[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      nombres.push(this.download);
    });

    guardarArchivo({ blob: new Blob(['x']), nombre: 'informe.xlsx' }, 'reserva.xlsx');
    guardarArchivo({ blob: new Blob(['x']), nombre: null }, 'reserva.xlsx');

    expect(nombres).toEqual(['informe.xlsx', 'reserva.xlsx']);
    expect(liberar).not.toHaveBeenCalled();
    vi.runAllTimers();
    expect(liberar).toHaveBeenCalledTimes(2);
    vi.useRealTimers();
  });
});

describe('abrirDescarga', () => {
  it('sigue un enlace de descarga con la URL y el nombre, sin abrir otra ventana, y lo retira del documento', () => {
    const abrir = vi.spyOn(window, 'open').mockReturnValue(null);
    const enlaces: HTMLAnchorElement[] = [];
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
      enlaces.push(this);
      expect(document.body.contains(this)).toBe(true);
    });

    const descarga = { url: 'https://almacen.test/obj?firma=1', nombre: 'certificado.pdf', mime: 'application/pdf' };
    expect(abrirDescarga(descarga)).toBe(descarga);

    expect(enlaces).toHaveLength(1);
    expect(enlaces[0]?.href).toBe('https://almacen.test/obj?firma=1');
    expect(enlaces[0]?.download).toBe('certificado.pdf');
    expect(enlaces[0]?.rel).toBe('noopener');
    expect(enlaces[0]?.target).toBe('');
    expect(document.body.contains(enlaces[0] ?? null)).toBe(false);
    expect(abrir).not.toHaveBeenCalled();
  });
});
