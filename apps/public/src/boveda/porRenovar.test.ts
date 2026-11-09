import type { ComparticionPublicaDto, DocumentoBovedaDto, VigenciaDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import { lineasPorRenovar } from './porRenovar';

const t = i18n.t.bind(i18n);

const vigencia = (estado: string, vigenteHasta: string | null, cambios: Partial<VigenciaDto> = {}): VigenciaDto => ({
  vigenteHasta,
  base: 'FechaExpedicion',
  estado,
  diasAviso: [30, 7],
  diasGracia: 15,
  finDeGracia: null,
  vencidaEn: null,
  ...cambios,
});

const empresa = (nombre: string, v: VigenciaDto | null, cambios: Partial<ComparticionPublicaDto> = {}): ComparticionPublicaDto => ({
  vinculoId: `v-${nombre}`,
  empresa: { id: `e-${nombre}`, nombre, logoUrl: null },
  compartido: true,
  version: 1,
  versionActualCompartida: true,
  compartidaEn: '2026-10-01T12:00:00Z',
  vigencia: v,
  ...cambios,
});

const documento = (id: string, nombre: string, porEmpresa: ComparticionPublicaDto[], cambios: Partial<DocumentoBovedaDto> = {}): DocumentoBovedaDto => ({
  id,
  tipo: { codigo: id.toUpperCase(), nombre, tipoDocumentoTenantId: null },
  ambito: 'Global',
  clase: 'Archivo',
  vinculoId: null,
  versionActual: null,
  versiones: 1,
  porEmpresa,
  ...cambios,
});

const linea = (d: DocumentoBovedaDto) => lineasPorRenovar([d], t).map((l) => `${l.documento}: ${l.texto}`);

describe('aviso consolidado de la bóveda', () => {
  it('con una empresa dice cuándo vence y enlaza la subida de la versión nueva', () => {
    const lineas = lineasPorRenovar([documento('rut', 'Registro Único Tributario', [empresa('Constructora Norte', vigencia('PorVencer', '2026-11-30'))])], t);
    expect(lineas).toHaveLength(1);
    expect(lineas[0]?.texto).toBe('vence para Constructora Norte el 30/11/2026.');
    expect(lineas[0]?.subir).toBe('/boveda/subir?documento=rut&tipo=RUT');
  });

  it('con dos empresas y fechas distintas dice antes de cuándo renovar para quedar al día con las dos', () => {
    const camara = documento('camara', 'Cámara de comercio', [
      empresa('Constructora Sur', vigencia('Vigente', '2026-12-30')),
      empresa('Constructora Norte', vigencia('PorVencer', '2026-10-31')),
    ]);
    expect(linea(camara)).toEqual([
      'Cámara de comercio: vence para Constructora Norte el 31/10/2026 y para Constructora Sur el 30/12/2026. Renueva antes del 31/10 y quedas al día con las dos.',
    ]);
  });

  it('con tres empresas dice «las tres» y con más, «todas»', () => {
    const tres = documento('camara', 'Cámara de comercio', [
      empresa('A', vigencia('PorVencer', '2026-11-20')),
      empresa('B', vigencia('Vigente', '2027-01-10')),
      empresa('C', vigencia('Vigente', '2027-02-10')),
    ]);
    expect(lineasPorRenovar([tres], t)[0]?.texto).toBe(
      'vence para A el 20/11/2026, para B el 10/01/2027 y para C el 10/02/2027. Renueva antes del 20/11 y quedas al día con las tres.',
    );
    const cuatro = { ...tres, porEmpresa: [...tres.porEmpresa, empresa('D', vigencia('Vigente', '2027-03-10'))] };
    expect(lineasPorRenovar([cuatro], t)[0]?.texto).toMatch(/quedas al día con todas\.$/);
  });

  it('en gracia dice hasta cuándo se acepta y ese es el plazo para quedar al día', () => {
    const rut = documento('rut', 'Registro Único Tributario', [
      empresa('Constructora Norte', vigencia('EnGracia', '2026-11-01', { finDeGracia: '2026-11-16' })),
      empresa('Constructora Sur', vigencia('PorVencer', '2026-11-30')),
    ]);
    expect(linea(rut)).toEqual([
      'Registro Único Tributario: está en el periodo de gracia con Constructora Norte hasta el 16/11/2026 y vence para Constructora Sur el 30/11/2026. Renueva antes del 16/11 y quedas al día con las dos.',
    ]);
  });

  it('vencido dice cuándo venció y, con otra empresa, que lo renueve cuanto antes', () => {
    const solo = documento('rut', 'Registro Único Tributario', [empresa('Constructora Norte', vigencia('Vencida', '2026-11-01'))]);
    expect(linea(solo)).toEqual(['Registro Único Tributario: venció para Constructora Norte el 01/11/2026.']);
    const conOtra = { ...solo, porEmpresa: [...solo.porEmpresa, empresa('Constructora Sur', vigencia('Vigente', '2027-01-15'))] };
    const lineas = lineasPorRenovar([conOtra], t);
    expect(lineas[0]?.texto).toBe('venció para Constructora Norte el 01/11/2026 y vence para Constructora Sur el 15/01/2027. Renueva cuanto antes y quedas al día con las dos.');
    expect(lineas[0]?.vigencia.estado).toBe('Vencida');
  });

  it('no avisa si todo está vigente, no vence o no se comparte', () => {
    const documentos = [
      documento('rut', 'Registro Único Tributario', [empresa('Constructora Norte', vigencia('Vigente', '2027-05-01'))]),
      documento('cedula', 'Cédula', [empresa('Constructora Norte', vigencia('SinVencimiento', null))]),
      documento('camara', 'Cámara de comercio', [empresa('Constructora Norte', vigencia('Vencida', '2026-10-01'), { compartido: false, version: null })]),
      documento('otro', 'Otro', [empresa('Constructora Norte', vigencia('Renovando', '2026-10-01'))]),
      documento('vacio', 'Sin empresas', []),
    ];
    expect(lineasPorRenovar(documentos, t)).toEqual([]);
  });

  it('ordena por la fecha más próxima y un formulario no lleva enlace de subida', () => {
    const documentos = [
      documento('camara', 'Cámara de comercio', [empresa('Constructora Norte', vigencia('PorVencer', '2026-12-01'))]),
      documento('form', 'Formulario de vinculación', [empresa('Constructora Norte', vigencia('PorVencer', '2026-11-20'))], { clase: 'Formulario' }),
      documento('rut', 'Registro Único Tributario', [empresa('Constructora Norte', vigencia('Vencida', '2026-11-01'))]),
    ];
    const lineas = lineasPorRenovar(documentos, t);
    expect(lineas.map((l) => l.documentoId)).toEqual(['rut', 'form', 'camara']);
    expect(lineas[1]?.subir).toBeNull();
  });
});
