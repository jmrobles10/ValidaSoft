import type { TipoDocumentoTenantDto } from '@validasoft/api-client';
import i18n from 'i18next';
import { describe, expect, it } from 'vitest';
import '../../../i18n';
import { sePuedePedir } from '../../flujos/editor';
import { borradorDe, borradorNuevo, campoNuevo, claveDesdeEtiqueta, cuerpoActualizar, cuerpoCrear, validarBorrador, type BorradorTipo } from './modelo';
import { enumerar, resumenTipo, resumenVigencia } from './resumen';

const t = i18n.t.bind(i18n);

export const TIPO: TipoDocumentoTenantDto = {
  id: 'td1',
  codigo: 'CAMARA_COMERCIO',
  nombre: 'Certificado de existencia y representación legal',
  tipoCanonico: 'CAMARA_COMERCIO',
  versionCanonico: 1,
  ambito: 'Global',
  clase: 'Archivo',
  textoAyuda: null,
  reglaFrescura: { tipo: 'MaxDiasDesdeExpedicion', dias: 30, regla: null },
  diasAviso: [30, 15, 5],
  diasGracia: 15,
  diasRevalidacion: 15,
  activo: true,
  creadoEn: '2026-11-08T20:41:38Z',
  formatosPermitidos: ['application/pdf'],
  tamanoMaximoMb: 5,
  paginasMaximas: null,
  modo: 'Archivo',
  campos: [],
  reglasEfectivas: { formatos: ['application/pdf'], tamanoMaximoBytes: 5 * 1024 * 1024, paginasMaximas: 50, descripcion: 'Certificado… admite solo PDF de hasta 5 MB' },
  plantilla: null,
  cargaSoportada: true,
};

const formulario = (cambios: Partial<BorradorTipo> = {}): BorradorTipo => ({
  ...borradorNuevo(),
  codigo: 'DECLARACION_ORIGEN',
  nombre: 'Declaración de origen de fondos',
  modo: 'Formulario',
  campos: [{ ...campoNuevo(), etiqueta: 'Fecha de expedición', clave: 'fecha_expedicion', tipo: 'Fecha', obligatorio: true }],
  ...cambios,
});

describe('borrador de un tipo de documento', () => {
  it('se arma desde la API con los formatos en nombre corto y los límites vacíos si son los globales', () => {
    const b = borradorDe(TIPO);
    expect(b.formatos).toEqual(['pdf']);
    expect(b.tamanoMb).toBe('5');
    expect(b.paginas).toBe('');
    expect(b.diasAviso).toBe('30, 15, 5');
    expect(b.frescura).toBe('MaxDiasDesdeExpedicion');
  });

  it('la clave se sugiere en snake_case a partir de la etiqueta', () => {
    expect(claveDesdeEtiqueta('Fecha de expedición')).toBe('fecha_de_expedicion');
    expect(claveDesdeEtiqueta('  ¿Año 2024? ')).toBe('ano_2024');
    expect(claveDesdeEtiqueta('3 socios')).toBe('c_3_socios');
  });

  it('valida como la API: código, formulario solo del vínculo, claves únicas, reservadas como Fecha y listas con opciones', () => {
    expect(validarBorrador(formulario(), true)).toEqual([]);
    expect(validarBorrador(formulario({ codigo: 'mal' }), true)).toContainEqual({ campo: 'codigo', codigo: 'codigo-invalido' });
    expect(validarBorrador(formulario({ ambito: 'Global' }), true)).toContainEqual({ campo: 'ambito', codigo: 'formulario-solo-por-vinculo' });
    expect(validarBorrador(formulario({ campos: [] }), true)).toContainEqual({ campo: 'campos', codigo: 'campos-requeridos' });

    const repetido = { ...campoNuevo(), etiqueta: 'Otra', clave: 'fecha_expedicion', tipo: 'Fecha' as const };
    expect(validarBorrador(formulario({ campos: [...formulario().campos, repetido] }), true)).toContainEqual({ campo: 'campo', indice: 1, codigo: 'campo-duplicado' });
    const reservada = { ...campoNuevo(), etiqueta: 'Vence', clave: 'fecha_vencimiento', tipo: 'Texto' as const };
    expect(validarBorrador(formulario({ campos: [reservada] }), true)).toContainEqual({ campo: 'campo', indice: 0, codigo: 'campo-reservado' });
    const lista = { ...campoNuevo(), etiqueta: 'Régimen', clave: 'regimen', tipo: 'Lista' as const };
    expect(validarBorrador(formulario({ campos: [lista] }), true)).toContainEqual({ campo: 'campo', indice: 0, codigo: 'opciones-requeridas' });

    expect(validarBorrador({ ...formulario(), frescura: 'MaxDiasDesdeExpedicion', diasFrescura: '' }, true)).toContainEqual({ campo: 'diasFrescura', codigo: 'dias-invalidos' });
    expect(validarBorrador({ ...formulario(), tamanoMb: '2000' }, true)).toContainEqual({ campo: 'tamanoMb', codigo: 'limite-invalido' });
  });

  it('al crear manda los formatos cortos y los campos; al actualizar, 0 vuelve al global y [] quita los campos', () => {
    const lista = { ...campoNuevo(), etiqueta: 'Régimen', clave: 'regimen', tipo: 'Lista' as const, opciones: 'Común\nSimplificado\n' };
    const crear = cuerpoCrear(formulario({ campos: [lista], formatos: ['pdf'] }));
    expect(crear).toMatchObject({ codigo: 'DECLARACION_ORIGEN', modo: 'Formulario', formatosPermitidos: ['pdf'], tipoCanonico: null, tamanoMaximoMb: null });
    expect(crear.campos).toEqual([{ clave: 'regimen', etiqueta: 'Régimen', tipo: 'Lista', obligatorio: false, opciones: ['Común', 'Simplificado'], ayuda: null }]);

    const actualizar = cuerpoActualizar({ ...borradorDe(TIPO), tamanoMb: '', modo: 'Archivo' });
    expect(actualizar).toMatchObject({ tamanoMaximoMb: 0, paginasMaximas: 0, campos: [], activo: true });
    expect(actualizar.reglaFrescura).toEqual({ tipo: 'MaxDiasDesdeExpedicion', dias: 30, regla: null });
  });
});

describe('resumen de reglas (lista y editor de flujos)', () => {
  it('dice formato, peso, páginas, formulario y plantilla en corto', () => {
    expect(enumerar(['PDF', 'JPG', 'PNG'])).toBe('PDF, JPG o PNG');
    expect(resumenTipo(TIPO, t)).toBe('PDF · hasta 5 MB · 50 págs.');
    const conTodo = {
      ...TIPO,
      modo: 'ArchivoYFormulario',
      campos: [{ clave: 'a', etiqueta: 'A', tipo: 'Texto' }, { clave: 'b', etiqueta: 'B', tipo: 'Texto' }],
      plantilla: { version: 1, nombreOriginal: 'f.docx', mime: 'x', tamanoBytes: 1, sha256: 'x', cargadaEn: '2026-11-08', cargadaPor: 'u', vigente: true, retiradaEn: null },
    };
    expect(resumenTipo(conTodo, t)).toBe('PDF · hasta 5 MB · 50 págs. · Formulario (2 campos) · con plantilla');
    expect(resumenTipo({ ...TIPO, modo: 'Formulario', campos: [{ clave: 'a', etiqueta: 'A', tipo: 'Texto' }] }, t)).toBe('Formulario (1 campo)');
  });

  it('explica la vigencia en palabras', () => {
    expect(resumenVigencia(TIPO, t)).toBe('Vale 30 días desde su expedición');
    expect(resumenVigencia({ reglaFrescura: { tipo: 'Ninguna' } }, t)).toBe('No vence');
    expect(resumenVigencia({ reglaFrescura: { tipo: 'MaxDiasDesdeAprobacion', dias: 1 } }, t)).toBe('Vale 1 día desde la entrega');
  });

  it('en un flujo se puede pedir cualquier tipo activo que el proveedor pueda entregar (también formularios)', () => {
    expect(sePuedePedir(TIPO)).toBe(true);
    expect(sePuedePedir({ activo: true, clase: 'Formulario', cargaSoportada: true })).toBe(true);
    expect(sePuedePedir({ activo: true, clase: 'Formulario', cargaSoportada: false })).toBe(false);
    expect(sePuedePedir({ activo: false, clase: 'Archivo', cargaSoportada: true })).toBe(false);
    expect(sePuedePedir({ activo: true, clase: 'Archivo' })).toBe(true);
  });
});
