import type { RevalidacionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { claveRevalidacion, lote, seleccionables, tonoPlazo } from './modelo';

function fila(parcial: Partial<RevalidacionDto>): RevalidacionDto {
  return {
    expedienteId: 'e1',
    folio: 'NOR-2026-00001',
    vinculoId: 'v1',
    requisitoId: 'r1',
    tercero: { id: 't1', nombre: 'Acme', identificacion: 'NIT 900' },
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    tipoDocumentoCodigo: 'RUT',
    tipoDocumento: 'RUT',
    versionAceptada: { numero: 1, fecha: '2026-11-01T00:00:00Z' },
    versionActual: { numero: 2, fecha: '2026-11-08T00:00:00Z' },
    comparacion: 'Distinta',
    aprobableEnLote: false,
    estadoRequisito: 'EnRevision',
    areas: [],
    desde: '2026-11-08T00:00:00Z',
    plazoHasta: '2026-11-23T00:00:00Z',
    diasRestantes: 15,
    bloqueante: false,
    ...parcial,
  };
}

describe('revalidaciones', () => {
  it('solo se aprueban en lote las que no cambiaron, sobre la versión que se vio', () => {
    const sinCambios = fila({ requisitoId: 'r2', comparacion: 'SinCambios', aprobableEnLote: true });
    const distinta = fila({ requisitoId: 'r3' });
    const filas = [sinCambios, distinta];
    expect(seleccionables(filas)).toEqual([sinCambios]);

    const seleccion = new Set([claveRevalidacion(sinCambios), claveRevalidacion(distinta), 'otra|r9']);
    expect(lote(filas, seleccion)).toEqual([{ expedienteId: 'e1', requisitoId: 'r2', sobreVersion: 2 }]);
  });

  it('el plazo pasa a urgente en los últimos tres días y a bloqueante al vencer', () => {
    expect(tonoPlazo({ bloqueante: false, diasRestantes: 10 })).toBe('normal');
    expect(tonoPlazo({ bloqueante: false, diasRestantes: 3 })).toBe('urgente');
    expect(tonoPlazo({ bloqueante: true, diasRestantes: 0 })).toBe('bloqueante');
  });
});
