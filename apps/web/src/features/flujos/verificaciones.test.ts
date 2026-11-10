import type { FlujoVersionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { credencialesADto, borradorCredenciales } from '../configuracion/listasRestrictivas/modelo';
import { borradorDesdeVersion, firmaBorrador, validarVerificaciones, verificacionesADto, verificacionNueva } from './editor';

describe('Verificaciones del flujo (VE-6)', () => {
  it('valida vigencia y reconsulta como el dominio (vacías = las de la configuración)', () => {
    expect(validarVerificaciones([verificacionNueva('EstudioDeCumplimiento'), verificacionNueva('ListasRestrictivas')])).toEqual([]);
    expect(validarVerificaciones([{ ...verificacionNueva('EstudioDeCumplimiento'), vigenciaDias: 0 }])).toEqual([
      { codigo: 'vigencia-invalida', campo: 'verificacion.EstudioDeCumplimiento' },
    ]);
    expect(validarVerificaciones([{ ...verificacionNueva('ListasRestrictivas'), frecuenciaReconsultaMeses: 61 }])).toEqual([
      { codigo: 'reconsulta-invalida', campo: 'verificacion.ListasRestrictivas' },
    ]);
  });

  it('va a la API en orden fijo, sin reconsulta en el estudio, y vuelve igual al editor', () => {
    const dto = verificacionesADto([
      { tipo: 'EstudioDeCumplimiento', bloqueante: true, vigenciaDias: 45, frecuenciaReconsultaMeses: 6 },
      { tipo: 'ListasRestrictivas', bloqueante: false, vigenciaDias: null, frecuenciaReconsultaMeses: 6 },
    ]);
    expect(dto).toEqual([
      { tipo: 'ListasRestrictivas', bloqueante: false, vigenciaDias: null, frecuenciaReconsultaMeses: 6 },
      { tipo: 'EstudioDeCumplimiento', bloqueante: true, vigenciaDias: 45, frecuenciaReconsultaMeses: null },
    ]);

    const version = { politicaDevolucion: 'Inmediata', separacionDeFunciones: false, etapas: [], requisitos: [], verificaciones: dto } as unknown as FlujoVersionDto;
    const borrador = borradorDesdeVersion({ codigo: 'HAB', nombre: 'Habilitación', tipoVinculo: 'PROVEEDOR' }, version);
    expect(borrador.verificaciones.map((v) => v.tipo)).toEqual(['ListasRestrictivas', 'EstudioDeCumplimiento']);
    expect(firmaBorrador(borrador)).toContain('"verificaciones"');
  });
});

describe('Credenciales de listas de la empresa (VE-7)', () => {
  const proveedor = {
    nombre: 'Infolaft', real: true, principal: true, habilitadoEnInstalacion: false, configurado: true, usuario: 'sinco', ultimosCuatroClave: '9876',
    urlBase: null, plan: 'Empresarial', cuotaMensual: 500, modoBusqueda: 'PorNombre',
  } as const;

  it('la clave nunca vuelve: vacía conserva la actual y quitar envía cadena vacía', () => {
    const borrador = borradorCredenciales(proveedor);
    expect(borrador.clave).toBe('');
    expect(credencialesADto('Infolaft', borrador)).toEqual({
      proveedor: 'Infolaft', usuario: 'sinco', clave: null, urlBase: null, plan: 'Empresarial', cuotaMensual: 500, modoBusqueda: 'PorNombre',
    });
    expect(credencialesADto('Infolaft', { ...borrador, clave: 'nueva-clave', cuotaMensual: '' })).toMatchObject({ clave: 'nueva-clave', cuotaMensual: null });
    expect(credencialesADto('Infolaft', borrador, true)).toEqual({ proveedor: 'Infolaft', clave: '' });
  });
});
