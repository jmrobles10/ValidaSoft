import type { FlujoVersionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { borradorDesdeVersion, borradorVacio, firmaBorrador, validarFlujo } from './editor';
import { recordatorioADto, recordatorioDesdeVersion, validarRecordatorio } from './recordatorio';

const base = { codigo: 'PROV', nombre: 'Proveedores', tipoVinculo: 'PROVEEDOR' };
const version = (recordatorioObservaciones?: FlujoVersionDto['recordatorioObservaciones']): FlujoVersionDto => ({
  id: 'v',
  version: 1,
  estado: 'Borrador',
  etapas: [],
  politicaDevolucion: 'Inmediata',
  separacionDeFunciones: false,
  creadaEn: '2026-11-10T00:00:00Z',
  publicadaEn: null,
  retiradaEn: null,
  ...(recordatorioObservaciones === undefined ? {} : { recordatorioObservaciones }),
});

describe('recordatorio automático por flujo en el editor (OP-19)', () => {
  it('por omisión el flujo sigue a la empresa y no manda días', () => {
    const borrador = borradorVacio();

    expect(borrador.recordatorioModo).toBe('Empresa');
    expect(recordatorioADto(borrador)).toEqual({ modo: 'Empresa', cadaDias: null });
    // Una versión guardada antes de esta opción (sin el campo) también sigue a la empresa.
    expect(borradorDesdeVersion(base, version()).recordatorioModo).toBe('Empresa');
    expect(recordatorioDesdeVersion(null)).toEqual({ recordatorioModo: 'Empresa', recordatorioDias: '3' });
  });

  it('lleva «cada N días» y «apagado» de la versión al editor y de vuelta a la API', () => {
    const cada = borradorDesdeVersion(base, version({ modo: 'CadaDias', cadaDias: 5 }));
    expect(cada.recordatorioModo).toBe('CadaDias');
    expect(cada.recordatorioDias).toBe('5');
    expect(recordatorioADto(cada)).toEqual({ modo: 'CadaDias', cadaDias: 5 });

    const apagado = borradorDesdeVersion(base, version({ modo: 'Apagado' }));
    expect(apagado.recordatorioModo).toBe('Apagado');
    expect(recordatorioADto({ ...apagado, recordatorioDias: '9' })).toEqual({ modo: 'Apagado', cadaDias: null });
  });

  it('un modo desconocido vuelve a seguir a la empresa y lo que no es número queda en 0 para que la API lo rechace', () => {
    expect(recordatorioDesdeVersion({ modo: 'Otro' }).recordatorioModo).toBe('Empresa');
    expect(recordatorioADto({ recordatorioModo: 'CadaDias', recordatorioDias: 'abc' })).toEqual({ modo: 'CadaDias', cadaDias: 0 });
  });

  it('solo valida los días cuando el modo es «cada N días» (1 a 60)', () => {
    expect(validarRecordatorio({ recordatorioModo: 'CadaDias', recordatorioDias: '7' })).toEqual([]);
    for (const dias of ['', '0', '61', 'x']) {
      expect(validarRecordatorio({ recordatorioModo: 'CadaDias', recordatorioDias: dias })).toEqual([{ codigo: 'recordatorio-invalido', campo: 'recordatorio.dias' }]);
    }
    expect(validarRecordatorio({ recordatorioModo: 'Empresa', recordatorioDias: '0' })).toEqual([]);
    expect(validarRecordatorio({ recordatorioModo: 'Apagado', recordatorioDias: '' })).toEqual([]);
    // El error llega al editor con el resto de las validaciones del flujo.
    const errores = validarFlujo({ ...borradorVacio(), nombre: 'X', recordatorioModo: 'CadaDias', recordatorioDias: '0' }, [], false);
    expect(errores.some((e) => e.codigo === 'recordatorio-invalido')).toBe(true);
  });

  it('cambiar el recordatorio cuenta como cambio sin guardar', () => {
    const cargado = borradorDesdeVersion(base, version({ modo: 'CadaDias', cadaDias: 5 }));

    expect(firmaBorrador({ ...cargado, recordatorioDias: '6' })).not.toBe(firmaBorrador(cargado));
    expect(firmaBorrador({ ...cargado, recordatorioModo: 'Apagado' })).not.toBe(firmaBorrador(cargado));
    // Con la empresa, los días escritos no cuentan: no se mandan.
    const empresa = { ...cargado, recordatorioModo: 'Empresa' as const };
    expect(firmaBorrador({ ...empresa, recordatorioDias: '9' })).toBe(firmaBorrador(empresa));
  });
});
