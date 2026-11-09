import { ErrorApi } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { campoDelError, cuerpoDe, insertarVariable } from './modelo';

const error422 = (codigo: string, extensiones: Record<string, unknown> = {}) => new ErrorApi(422, codigo, 'detalle', null, null, {}, extensiones);

describe('modelo de plantillas', () => {
  it('los textos vacíos o en blanco van nulos', () => {
    expect(cuerpoDe({ asunto: '  ', cabecera: 'Hola\n', pie: '' })).toEqual({ asunto: null, cabecera: 'Hola\n', pie: null });
  });

  it('inserta la variable en la selección o al final', () => {
    expect(insertarVariable('Hola !', 'empresa', 5, 5)).toEqual({ texto: 'Hola {{empresa}}!', cursor: 16 });
    expect(insertarVariable('Hola XX', 'folio', 5, 7)).toEqual({ texto: 'Hola {{folio}}', cursor: 14 });
    expect(insertarVariable('Fin', 'folio', null, null)).toEqual({ texto: 'Fin{{folio}}', cursor: 12 });
  });

  it('ubica el 422 en su campo por la extensión o por el código del asunto', () => {
    expect(campoDelError(error422('variable-no-permitida', { campo: 'pie' }))).toBe('pie');
    expect(campoDelError(error422('texto-demasiado-largo', { campo: 'cabecera' }))).toBe('cabecera');
    expect(campoDelError(error422('asunto-demasiado-largo'))).toBe('asunto');
    expect(campoDelError(error422('canal-invalido'))).toBeNull();
    expect(campoDelError(new ErrorApi(404, 'plantilla-no-encontrada', 'x', null, null))).toBeNull();
    expect(campoDelError(new Error('red'))).toBeNull();
  });
});
