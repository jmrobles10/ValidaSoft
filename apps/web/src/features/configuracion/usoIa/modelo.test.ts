import { describe, expect, it } from 'vitest';
import { etiquetaMes, formatearUsd, leerMes, mesActual, nivelDeUso, porcentajeUsado, serieDelMes, ultimosMeses } from './modelo';

describe('modelo del uso de IA', () => {
  it('el mes actual es el de la zona del tenant', () => {
    // 1 de diciembre a las 02:00 UTC todavía es 30 de noviembre en Bogotá.
    expect(mesActual(new Date('2026-12-01T02:00:00Z'))).toBe('2026-11');
  });

  it('los últimos 12 meses cruzan el año', () => {
    const meses = ultimosMeses('2026-02');
    expect(meses).toHaveLength(12);
    expect(meses.slice(0, 3)).toEqual(['2026-02', '2026-01', '2025-12']);
    expect(meses.at(-1)).toBe('2025-03');
  });

  it('solo acepta de la URL un mes que esté en el selector', () => {
    const opciones = ultimosMeses('2026-11');
    expect(leerMes(new URLSearchParams('mes=2026-10'), opciones)).toBe('2026-10');
    expect(leerMes(new URLSearchParams('mes=2024-01'), opciones)).toBeNull();
    expect(leerMes(new URLSearchParams('mes=2026-13'), opciones)).toBeNull();
    expect(leerMes(new URLSearchParams(''), opciones)).toBeNull();
  });

  it('nombra el mes en español con mayúscula inicial', () => {
    expect(etiquetaMes('2026-03')).toBe('Marzo de 2026');
  });

  it('umbrales del 80 % y 100 %; sin incluidas cualquier consumo agota el cupo', () => {
    expect(nivelDeUso(porcentajeUsado(39, 50))).toBe('normal');
    expect(nivelDeUso(porcentajeUsado(40, 50))).toBe('alerta');
    expect(nivelDeUso(porcentajeUsado(50, 50))).toBe('agotado');
    expect(porcentajeUsado(0, 0)).toBe(0);
    expect(porcentajeUsado(1, 0)).toBe(100);
  });

  it('dólares con 4 decimales bajo 1 USD y 2 desde ahí', () => {
    expect(formatearUsd('0.1234')).toMatch(/^US\$\s0,1234$/);
    expect(formatearUsd(1234.5)).toMatch(/^US\$\s1\.234,50$/);
  });

  it('la serie llena todos los días del mes', () => {
    const serie = serieDelMes('2026-02', [{ fecha: '2026-02-03', incluidas: 4, adicionales: 1, analisisPagados: 0, tokensEntrada: 0, tokensSalida: 0, costoUsd: 0 }]);
    expect(serie).toHaveLength(28);
    expect(serie[2]).toEqual({ dia: 3, incluidas: 4, adicionales: 1 });
    expect(serie[0]).toEqual({ dia: 1, incluidas: 0, adicionales: 0 });
  });
});
