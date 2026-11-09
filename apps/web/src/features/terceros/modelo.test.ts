import { describe, expect, it } from 'vitest';
import { esEstadoVinculo, nombreEmpresaVisible } from './modelo';

describe('modelo de la ficha del tercero', () => {
  const empresas = [{ id: 'a1b2', nombre: 'Constructora Sur' }];

  it('nombra la otra empresa solo si está entre las de la sesión (sin importar mayúsculas del id)', () => {
    expect(nombreEmpresaVisible('A1B2', empresas)).toBe('Constructora Sur');
    expect(nombreEmpresaVisible('otra', empresas)).toBeNull();
  });

  it('reconoce los estados de vínculo y descarta los desconocidos', () => {
    expect(esEstadoVinculo('Bloqueado')).toBe(true);
    expect(esEstadoVinculo('Inventado')).toBe(false);
  });
});
