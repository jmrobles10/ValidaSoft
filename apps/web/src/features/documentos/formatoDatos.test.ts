import { describe, expect, it } from 'vitest';
import { datosDe, esVersionSinArchivo, formatearDatoDeclarado, formatearFechaDeclarada } from './formatoDatos';

const TEXTOS = { si: 'Sí', no: 'No' };
const formato = (tipo: string, valor: string) => formatearDatoDeclarado({ tipo, valor }, TEXTOS);

describe('formato de los datos declarados según su tipo', () => {
  it('una fecha aaaa-mm-dd se lee como día calendario, sin correrla por la zona de Bogotá', () => {
    expect(formato('Fecha', '2026-11-01')).toBe('01/11/2026');
    expect(formato('Fecha', '2026-01-01')).toBe('01/01/2026');
  });

  it('una fecha ISO con hora usa la zona del tenant', () => {
    expect(formatearFechaDeclarada('2026-11-02T03:00:00Z')).toBe('01/11/2026');
  });

  it('true/false se leen Sí/No', () => {
    expect(formato('SiNo', 'true')).toBe('Sí');
    expect(formato('SiNo', 'false')).toBe('No');
  });

  it('los números llevan separadores es-CO', () => {
    expect(formato('Numero', '1234567.5')).toBe('1.234.567,5');
    expect(formato('Numero', '-0.25')).toBe('-0,25');
    expect(formato('Numero', '42')).toBe('42');
  });

  it('texto y lista se muestran tal cual; lo vacío, con raya', () => {
    expect(formato('Texto', 'Calle 10 # 5-20')).toBe('Calle 10 # 5-20');
    expect(formato('Lista', 'Común')).toBe('Común');
    expect(formato('Texto', '  ')).toBe('—');
  });

  it('un valor que no tiene el formato esperado no se inventa: se muestra tal cual', () => {
    expect(formato('Fecha', 'pronto')).toBe('pronto');
    expect(formato('Numero', '1,5')).toBe('1,5');
    expect(formato('SiNo', 'quizá')).toBe('quizá');
  });
});

describe('versiones sin archivo', () => {
  it('clase Formulario no tiene archivo; Archivo sí', () => {
    expect(esVersionSinArchivo({ clase: 'Formulario' })).toBe(true);
    expect(esVersionSinArchivo({ clase: 'Archivo' })).toBe(false);
  });

  it('sin clase en la versión decide el modo del tipo (ArchivoYFormulario sí trae archivo)', () => {
    expect(esVersionSinArchivo({}, 'Formulario')).toBe(true);
    expect(esVersionSinArchivo({ clase: null }, 'ArchivoYFormulario')).toBe(false);
    expect(esVersionSinArchivo({}, null)).toBe(false);
    // La clase de la versión manda sobre el modo actual del tipo (pudo cambiar después de la carga).
    expect(esVersionSinArchivo({ clase: 'Archivo' }, 'Formulario')).toBe(false);
  });

  it('datosDe devuelve vacío sin versión o sin datos', () => {
    expect(datosDe(null)).toEqual([]);
    expect(datosDe({ datosDeclarados: null })).toEqual([]);
  });
});
