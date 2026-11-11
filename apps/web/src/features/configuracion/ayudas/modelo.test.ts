import { describe, expect, it } from 'vitest';
import { cambioDeAyuda, MAXIMO_AYUDA, origenDeAyuda, resumirAyuda, textoParaGuardar } from './modelo';

describe('ayuda de documentos: modelo', () => {
  it('el origen es la empresa si escribió la suya, si no el grupo, y si no hay ninguna', () => {
    expect(origenDeAyuda({ ayudaDeLaEmpresa: 'Propia', ayudaDelGrupo: 'Del grupo' })).toBe('propia');
    expect(origenDeAyuda({ ayudaDeLaEmpresa: null, ayudaDelGrupo: 'Del grupo' })).toBe('grupo');
    expect(origenDeAyuda({ ayudaDeLaEmpresa: '   ', ayudaDelGrupo: 'Del grupo' })).toBe('grupo');
    expect(origenDeAyuda({ ayudaDeLaEmpresa: null, ayudaDelGrupo: null })).toBe('ninguna');
    expect(origenDeAyuda({ ayudaDeLaEmpresa: null, ayudaDelGrupo: ' ' })).toBe('ninguna');
  });

  it('el límite es el del backend: 2000 caracteres', () => {
    expect(MAXIMO_AYUDA).toBe(2000);
  });

  it('el texto vacío o de solo espacios se guarda como null: la empresa vuelve a usar la del grupo', () => {
    expect(textoParaGuardar('  Trae el RUT actualizado.  ')).toBe('Trae el RUT actualizado.');
    expect(textoParaGuardar('')).toBeNull();
    expect(textoParaGuardar('  \n ')).toBeNull();
  });

  it('solo hay cambio si lo escrito difiere de lo guardado (ignorando espacios de los bordes)', () => {
    expect(cambioDeAyuda(null, '')).toBe(false);
    expect(cambioDeAyuda(null, '   ')).toBe(false);
    expect(cambioDeAyuda('Trae el RUT', 'Trae el RUT ')).toBe(false);
    expect(cambioDeAyuda('Trae el RUT', 'Trae el RUT y la cámara')).toBe(true);
    expect(cambioDeAyuda('Trae el RUT', '')).toBe(true);
    expect(cambioDeAyuda(null, 'Algo')).toBe(true);
  });

  it('resume la ayuda en una línea corta, sin saltos', () => {
    expect(resumirAyuda(null)).toBe('');
    expect(resumirAyuda('Línea uno\n\nLínea   dos')).toBe('Línea uno Línea dos');
    const larga = 'a'.repeat(300);
    expect(resumirAyuda(larga, 50)).toHaveLength(50);
    expect(resumirAyuda(larga, 50).endsWith('…')).toBe(true);
  });
});
