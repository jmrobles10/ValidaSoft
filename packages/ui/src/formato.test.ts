import { describe, expect, it } from 'vitest';
import { formatearDiaCivil, formatearFecha, ZONA_HORARIA_POR_DEFECTO } from './formato';

describe('formatearDiaCivil', () => {
  it('escribe un día de calendario tal cual, sin correrlo al día anterior por la zona horaria', () => {
    // Como `Date`, la medianoche UTC de este día es el 25 de noviembre en Bogotá: un plazo de ley no puede mostrarse un día antes.
    expect(formatearFecha('2026-11-26')).toBe('25/11/2026');
    expect(formatearDiaCivil('2026-11-26')).toBe('26/11/2026');
    expect(formatearDiaCivil('2027-01-01')).toBe('01/01/2027');
  });

  it('una fecha con hora se muestra en la zona del tenant, como formatearFecha', () => {
    expect(formatearDiaCivil('2026-11-26T03:00:00Z')).toBe(formatearFecha('2026-11-26T03:00:00Z'));
    expect(formatearDiaCivil('2026-11-26T03:00:00Z')).toBe('25/11/2026');
    expect(formatearDiaCivil('2026-11-26T17:00:00Z')).toBe('26/11/2026');
  });
});

/**
 * Un día de calendario (`DateOnly` del backend: vigencias, plazos de ley, vencimientos) no tiene hora ni zona: pasarlo por
 * `formatearFecha` lo toma como la medianoche UTC, que en America/Bogota (UTC−5, sin horario de verano) es el día anterior. Estas fechas
 * incluyen cambios de mes y de año y un 29 de febrero: el día mostrado es siempre el recibido.
 */
describe('días de calendario en America/Bogota', () => {
  const DIAS: [string, string, string][] = [
    ['2026-11-26', '26/11/2026', '25/11/2026'],
    ['2026-12-31', '31/12/2026', '30/12/2026'],
    ['2027-01-01', '01/01/2027', '31/12/2026'],
    ['2026-03-01', '01/03/2026', '28/02/2026'],
    ['2028-03-01', '01/03/2028', '29/02/2028'],
    ['2028-02-29', '29/02/2028', '28/02/2028'],
  ];

  it('la zona del negocio por omisión es America/Bogota', () => {
    expect(ZONA_HORARIA_POR_DEFECTO).toBe('America/Bogota');
  });

  it.each(DIAS)('%s se muestra %s y no el día anterior', (dia, esperado, corridoPorLaZona) => {
    expect(formatearDiaCivil(dia, 'America/Bogota')).toBe(esperado);
    expect(formatearDiaCivil(dia)).toBe(esperado);
    // Lo que pasaba al usar formatearFecha con un día sin hora: justo el defecto que formatearDiaCivil evita.
    expect(formatearFecha(dia, 'America/Bogota')).toBe(corridoPorLaZona);
  });

  it('un instante sí se muestra en la zona del negocio: a las 8 p. m. de Bogotá ya es el día siguiente en UTC pero sigue siendo el mismo día', () => {
    expect(formatearFecha('2026-11-27T01:00:00Z', 'America/Bogota')).toBe('26/11/2026');
    expect(formatearDiaCivil('2026-11-27T01:00:00Z', 'America/Bogota')).toBe('26/11/2026');
  });
});
