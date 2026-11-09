import { ErrorApi, type CampoFormularioDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import { campoDelError, cuerpoFormulario, datosParaEnviar, errorDeCampo, erroresDelFormulario, faltaParaEnviar, tipoDeCampo, valoresIniciales } from './formulario';

const t = i18n.t.bind(i18n);
const HOY = '2026-11-08';

const campo = (clave: string, tipo: string, cambios: Partial<CampoFormularioDto> = {}): CampoFormularioDto => ({ clave, etiqueta: clave, tipo, obligatorio: false, ...cambios });

const CAMPOS: CampoFormularioDto[] = [
  campo('razon', 'Texto', { etiqueta: 'Razón social', obligatorio: true }),
  campo('monto', 'Numero', { etiqueta: 'Monto' }),
  campo('fecha_expedicion', 'Fecha', { etiqueta: 'Fecha de expedición', obligatorio: true }),
  campo('fecha_vencimiento', 'Fecha', { etiqueta: 'Fecha de vencimiento' }),
  campo('origen', 'Lista', { etiqueta: 'Origen de fondos', opciones: ['Propios', 'Crédito'], obligatorio: true }),
  campo('pep', 'SiNo', { etiqueta: '¿Es PEP?', obligatorio: true }),
];

describe('validación del formulario configurable', () => {
  it('los obligatorios vacíos faltan; los opcionales vacíos no', () => {
    expect(errorDeCampo(CAMPOS[0]!, {}, HOY)).toBe('requerido');
    expect(errorDeCampo(CAMPOS[0]!, { razon: '   ' }, HOY)).toBe('requerido');
    expect(errorDeCampo(CAMPOS[1]!, {}, HOY)).toBeNull();
  });

  it('revisa número, fecha, opción y sí/no como la API', () => {
    expect(errorDeCampo(CAMPOS[1]!, { monto: '1.500.000' }, HOY)).toBe('numero');
    expect(errorDeCampo(CAMPOS[1]!, { monto: '1500000,5' }, HOY)).toBeNull();
    expect(errorDeCampo(CAMPOS[2]!, { fecha_expedicion: '2026-02-30' }, HOY)).toBe('fecha');
    expect(errorDeCampo(CAMPOS[2]!, { fecha_expedicion: '2026-11-09' }, HOY)).toBe('expedicion-futura');
    expect(errorDeCampo(CAMPOS[3]!, { fecha_expedicion: '2026-10-01', fecha_vencimiento: '2026-09-30' }, HOY)).toBe('vencimiento-antes');
    expect(errorDeCampo(CAMPOS[4]!, { origen: 'Otro' }, HOY)).toBe('opcion');
    expect(errorDeCampo(CAMPOS[5]!, { pep: 'false' }, HOY)).toBeNull();
    expect(errorDeCampo(campo('largo', 'Texto'), { largo: 'x'.repeat(2001) }, HOY)).toBe('largo');
    expect(tipoDeCampo({ tipo: 'Desconocido' })).toBe('Texto');
  });

  it('dice qué falta: primero lo vacío, luego lo inválido; nada si está completo', () => {
    expect(faltaParaEnviar(CAMPOS, erroresDelFormulario(CAMPOS, {}, HOY), t)).toBe('Para enviar, llena: Razón social, Fecha de expedición, Origen de fondos, ¿Es PEP?.');
    const casi = { razon: 'ACME', fecha_expedicion: '2026-10-01', origen: 'Propios', pep: 'true', monto: 'mil' };
    expect(faltaParaEnviar(CAMPOS, erroresDelFormulario(CAMPOS, casi, HOY), t)).toBe('Para enviar, corrige: Monto.');
    expect(faltaParaEnviar(CAMPOS, erroresDelFormulario(CAMPOS, casi, HOY), t, true)).toBe('Para subir el archivo, corrige: Monto.');
    expect(faltaParaEnviar(CAMPOS, erroresDelFormulario(CAMPOS, { ...casi, monto: '' }, HOY), t)).toBeNull();
  });

  it('arma `datos`: sin opcionales vacíos, número con punto y sí/no booleano', () => {
    const valores = { razon: ' ACME ', monto: '12,5', fecha_expedicion: '2026-10-01', fecha_vencimiento: '', origen: 'Crédito', pep: 'false' };
    expect(datosParaEnviar(CAMPOS, valores)).toEqual({ razon: 'ACME', monto: '12.5', fecha_expedicion: '2026-10-01', origen: 'Crédito', pep: false });
  });

  it('el cuerpo del envío lleva el vínculo y el tipo de la empresa o el canónico', () => {
    expect(cuerpoFormulario({ tipoDocumentoTenantId: 't1', vinculoId: 'v1' }, { a: 'x' })).toEqual({ vinculoId: 'v1', tipoDocumentoTenantId: 't1', datos: { a: 'x' } });
    expect(cuerpoFormulario({ tipoCanonico: 'BANCARIO', vinculoId: 'v1' }, {})).toEqual({ vinculoId: 'v1', tipoCanonico: 'BANCARIO', datos: {} });
    expect(cuerpoFormulario({ tipoCanonico: 'BANCARIO' }, {})).toBeNull();
  });

  it('al actualizar parte de lo ya declarado, solo de los campos que el tipo sigue pidiendo', () => {
    const declarados = [
      { clave: 'pep', valor: 'false' },
      { clave: 'monto', valor: '1500000' },
      { clave: 'campo_retirado', valor: 'x' },
    ];
    expect(valoresIniciales(CAMPOS, declarados)).toEqual({ pep: 'false', monto: '1500000' });
    expect(valoresIniciales(CAMPOS, null)).toEqual({});
  });

  it('lee el campo que señala un 422 de la API', () => {
    expect(campoDelError(new ErrorApi(422, 'valor-invalido', '«Monto» debe ser un número.', null, null, {}, { campo: 'monto' }))).toBe('monto');
    expect(campoDelError(new ErrorApi(422, 'archivo-requerido', 'x', null, null))).toBeNull();
    expect(campoDelError(new Error('x'))).toBeNull();
  });
});
