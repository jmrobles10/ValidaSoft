import { describe, expect, it } from 'vitest';
import { borradorDe, cuerpoActualizar, errorDeLogo, hayCambios, TAMANO_MAXIMO_LOGO, validarMarca } from './modelo';

describe('Datos y marca (CF-1)', () => {
  const original = borradorDe({ replyTo: 'proveedores@norte.example', colorMarca: '#E07A1F', logoUrl: null });

  it('valida correo y color como el dominio; vacío quita el valor', () => {
    expect(validarMarca({ ...original, contactoSoporte: 'no-es-correo' })).toEqual(['contacto-soporte-invalido']);
    expect(validarMarca({ ...original, colorMarca: '#FFF' })).toEqual(['color-marca-invalido']);
    expect(validarMarca({ contactoSoporte: '', colorMarca: '', logoUrl: '' })).toEqual([]);
  });

  it('solo envía lo que cambió, con el color en mayúsculas', () => {
    expect(hayCambios(original, { ...original, colorMarca: '#e07a1f' })).toBe(false);
    expect(cuerpoActualizar(original, { ...original, colorMarca: '#1f6feb', logoUrl: 'data:image/png;base64,AA==' })).toEqual({
      contactoSoporte: null,
      colorMarca: '#1F6FEB',
      logoUrl: 'data:image/png;base64,AA==',
    });
    expect(cuerpoActualizar(original, { ...original, contactoSoporte: '' }).contactoSoporte).toBe('');
  });

  it('el logo es SVG o PNG de hasta 200 KB', () => {
    expect(errorDeLogo({ type: 'image/jpeg', size: 10 })).toBe('logo-tipo');
    expect(errorDeLogo({ type: 'image/png', size: TAMANO_MAXIMO_LOGO + 1 })).toBe('logo-tamano');
    expect(errorDeLogo({ type: 'image/svg+xml', size: 1024 })).toBeNull();
  });
});
