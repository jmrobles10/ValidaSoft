import type { VencimientoDto } from '@validasoft/api-client';
import i18n from 'i18next';
import { describe, expect, it } from 'vitest';
import { baseVencimiento, claveVencimiento, estadoVencimiento, leerFiltros, lote, mensajeRecordatorio, textoDias, tonoVencimiento } from './modelo';

const t = i18n.t.bind(i18n);

function vencimiento(parcial: Partial<VencimientoDto> = {}): VencimientoDto {
  return {
    vinculoId: 'v1',
    documentoId: 'd1',
    tercero: { id: 't1', nombre: 'Ferretería El Tornillo', identificacion: 'NIT 900123456' },
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    documento: 'RUT',
    version: 2,
    vigenteHasta: '2026-11-19',
    diasRestantes: 10,
    estado: 'PorVencer',
    base: 'FechaExpedicion',
    finDeGracia: null,
    permiteRatificar: false,
    ultimoRecordatorio: null,
    ...parcial,
  };
}

describe('vencimientos', () => {
  it('lee los filtros de la URL: 30 días por defecto, 0 = todos y lo inválido vuelve al defecto', () => {
    expect(leerFiltros(new URLSearchParams())).toEqual({ dias: 30, estado: '' });
    expect(leerFiltros(new URLSearchParams('dias=0&estado=EnGracia'))).toEqual({ dias: 0, estado: 'EnGracia' });
    expect(leerFiltros(new URLSearchParams('dias=7'))).toEqual({ dias: 7, estado: '' });
    expect(leerFiltros(new URLSearchParams('dias=45&estado=SinVencimiento'))).toEqual({ dias: 30, estado: '' });
    expect(leerFiltros(new URLSearchParams('dias=abc'))).toEqual({ dias: 30, estado: '' });
  });

  it('dice los días en palabras: hoy, en N días o hace N días', () => {
    expect(textoDias(0, t)).toBe('Vence hoy');
    expect(textoDias(1, t)).toBe('Vence en 1 día');
    expect(textoDias(10, t)).toBe('Vence en 10 días');
    expect(textoDias(-3, t)).toBe('Venció hace 3 días');
    expect(textoDias(-1, t)).toBe('Venció hace 1 día');
  });

  it('el tono sigue al estado: vencida es error; por vencer y en gracia, advertencia', () => {
    expect(tonoVencimiento({ estado: 'Vigente' })).toBe('normal');
    expect(tonoVencimiento({ estado: 'PorVencer' })).toBe('advertencia');
    expect(tonoVencimiento({ estado: 'EnGracia' })).toBe('advertencia');
    expect(tonoVencimiento({ estado: 'Vencida' })).toBe('error');
    expect(tonoVencimiento({ estado: 'Otro' })).toBe('normal');
  });

  it('estrecha el estado y la base; lo desconocido queda en null', () => {
    expect(estadoVencimiento({ estado: 'EnGracia' })).toBe('EnGracia');
    expect(estadoVencimiento({ estado: 'SinVencimiento' })).toBeNull();
    expect(baseVencimiento({ base: 'Ratificacion' })).toBe('Ratificacion');
    expect(baseVencimiento({ base: 'Inventada' })).toBeNull();
  });

  it('el lote lleva solo lo seleccionado que sigue en la lista, sin repetir', () => {
    const a = vencimiento();
    const b = vencimiento({ documentoId: 'd2' });
    const c = vencimiento({ vinculoId: 'v2' });
    const seleccion = new Set([claveVencimiento(a), claveVencimiento(c), 'v9|d9']);
    expect(lote([a, b, c, a], seleccion)).toEqual([
      { vinculoId: 'v1', documentoId: 'd1' },
      { vinculoId: 'v2', documentoId: 'd1' },
    ]);
  });

  it('el aviso cuenta recordatorios y terceros, y explica los omitidos', () => {
    expect(mensajeRecordatorio({ correos: 2, documentos: 3, omitidos: 0 }, t)).toEqual({ mensaje: 'Enviaste 3 recordatorios a 2 terceros.', severidad: 'success' });
    expect(mensajeRecordatorio({ correos: 1, documentos: 1, omitidos: 2 }, t)).toEqual({
      mensaje: 'Enviaste 1 recordatorio a 1 tercero. 2 se omitieron (ya se recordaron hoy o el vínculo no tiene correo).',
      severidad: 'success',
    });
    expect(mensajeRecordatorio({ correos: 0, documentos: 0, omitidos: 1 }, t)).toEqual({
      mensaje: 'No se envió ningún recordatorio. 1 se omitió (ya se recordó hoy o el vínculo no tiene correo).',
      severidad: 'warning',
    });
  });
});
