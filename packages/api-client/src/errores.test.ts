import { describe, expect, it } from 'vitest';
import { errorDesdeProblema } from './errores';

describe('errorDesdeProblema', () => {
  it('reúne los errores por campo de `errors` y de la extensión `errores[]`', () => {
    const error = errorDesdeProblema(
      422,
      {
        status: 422,
        codigo: 'valor-invalido',
        detail: 'El correo no es válido.',
        campo: 'correo',
        errors: { nombre: ['Es obligatorio.'] },
        errores: [
          { campo: 'correo', codigo: 'valor-invalido', mensaje: 'El correo no es válido.' },
          { campo: 'personas[0].pep', codigo: 'valor-invalido', mensaje: 'Indica si es PEP.' },
          { campo: 'sin-mensaje' },
        ],
      },
      null,
    );

    expect(error.errores).toEqual({
      nombre: ['Es obligatorio.'],
      correo: ['El correo no es válido.'],
      'personas[0].pep': ['Indica si es PEP.'],
    });
    expect(error.extensiones).toEqual({ campo: 'correo' });
  });
});
