import type { FlujoVersionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { borradorDesdeVersion, borradorVacio, firmaBorrador, reintentoADto } from './editor';

const base = { codigo: 'PROV', nombre: 'Proveedores', tipoVinculo: 'PROVEEDOR' };
const version = (reintento?: { permitido: boolean; diasEspera?: number }): FlujoVersionDto => ({
  id: 'v',
  version: 1,
  estado: 'Borrador',
  etapas: [],
  politicaDevolucion: 'Inmediata',
  separacionDeFunciones: false,
  creadaEn: '2026-11-10T00:00:00Z',
  publicadaEn: null,
  retiradaEn: null,
  ...(reintento ? { reintento } : {}),
});

describe('reintento tras un rechazo en el editor de flujos (DS-4)', () => {
  it('por omisión no se permite y no hay espera', () => {
    const borrador = borradorVacio();

    expect(borrador.reintentoPermitido).toBe(false);
    expect(reintentoADto(borrador)).toEqual({ permitido: false, diasEspera: 0 });
    // Una versión guardada antes (sin la política) también vale «no permitido».
    expect(borradorDesdeVersion(base, version()).reintentoPermitido).toBe(false);
  });

  it('lleva la política de la versión al editor y de vuelta a la API', () => {
    const borrador = borradorDesdeVersion(base, version({ permitido: true, diasEspera: 15 }));

    expect(borrador.reintentoPermitido).toBe(true);
    expect(borrador.reintentoDias).toBe('15');
    expect(reintentoADto(borrador)).toEqual({ permitido: true, diasEspera: 15 });
  });

  it('sin permiso no se manda espera aunque haya días escritos; lo que no es número vale 0', () => {
    expect(reintentoADto({ reintentoPermitido: false, reintentoDias: '30' })).toEqual({ permitido: false, diasEspera: 0 });
    expect(reintentoADto({ reintentoPermitido: true, reintentoDias: '' })).toEqual({ permitido: true, diasEspera: 0 });
    expect(reintentoADto({ reintentoPermitido: true, reintentoDias: 'abc' })).toEqual({ permitido: true, diasEspera: 0 });
  });

  it('cambiar la política cuenta como cambio sin guardar', () => {
    const cargado = borradorDesdeVersion(base, version({ permitido: true, diasEspera: 15 }));

    expect(firmaBorrador({ ...cargado, reintentoDias: '30' })).not.toBe(firmaBorrador(cargado));
    expect(firmaBorrador({ ...cargado, reintentoPermitido: false })).not.toBe(firmaBorrador(cargado));
  });
});
