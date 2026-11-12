import type { AreaDto, DelegacionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import {
  aCrearDto,
  areasDeLaPersona,
  borradorInicial,
  CHIP_DELEGACION,
  diasDeLaDelegacion,
  ESTADOS_DELEGACION,
  faltasDeDelegacion,
  formatearDia,
  listaDeAreas,
  puedeRevocar,
  type BorradorDelegacion,
} from './modelo';

const HOY = '2026-11-12';
const completo = (cambios: Partial<BorradorDelegacion> = {}): BorradorDelegacion => ({ ...borradorInicial(HOY), aUsuarioId: 'ana', hasta: '2026-11-20', ...cambios });

describe('delegación de revisión: modelo', () => {
  it('formatea un día sin hora sin correrlo por la zona horaria', () => {
    expect(formatearDia('2026-11-12')).toBe('12/11/2026');
    expect(formatearDia('2026-01-01')).toBe('01/01/2026');
    expect(formatearDia('2026-11-12T00:00:00')).toBe('12/11/2026');
  });

  it('cuenta los días de calendario contando ambos extremos', () => {
    expect(diasDeLaDelegacion('2026-11-12', '2026-11-12')).toBe(1);
    expect(diasDeLaDelegacion('2026-11-12', '2026-11-20')).toBe(9);
    expect(diasDeLaDelegacion('2026-01-01', '2026-12-31')).toBe(365);
    expect(diasDeLaDelegacion('2027-01-01', '2028-01-01')).toBe(366);
  });

  it('una delegación completa no tiene faltas; empieza hoy y sin fecha final', () => {
    expect(borradorInicial(HOY)).toMatchObject({ desde: HOY, hasta: '', areas: [], enNombreDe: '' });
    expect(faltasDeDelegacion(completo(), HOY, 2)).toEqual([]);
    expect(faltasDeDelegacion(borradorInicial(HOY), HOY, 2)).toEqual(['persona', 'hasta']);
  });

  it('valida el rango (final antes que inicial, antes de hoy, más de 366 días) y que haya áreas que delegar', () => {
    expect(faltasDeDelegacion(completo({ desde: '2026-11-15', hasta: '2026-11-14' }), HOY, 2)).toEqual(['rango']);
    expect(faltasDeDelegacion(completo({ desde: '2026-11-01', hasta: '2026-11-10' }), HOY, 2)).toEqual(['hoy']);
    expect(faltasDeDelegacion(completo({ hasta: '2027-11-13' }), HOY, 2)).toEqual(['largo']);
    expect(faltasDeDelegacion(completo({ hasta: '2027-11-12' }), HOY, 2)).toEqual([]);
    expect(faltasDeDelegacion(completo(), HOY, 0)).toEqual(['areas']);
  });

  it('arma lo que viaja a la API: lo vacío no viaja y «en nombre de» solo si se delega por otra persona', () => {
    expect(aCrearDto(completo())).toEqual({ aUsuarioId: 'ana', desde: HOY, hasta: '2026-11-20' });
    expect(aCrearDto(completo({ areas: ['a1', 'a2'], enNombreDe: 'bea', motivo: '  Vacaciones  ' }))).toEqual({
      aUsuarioId: 'ana',
      desde: HOY,
      hasta: '2026-11-20',
      areas: ['a1', 'a2'],
      deUsuarioId: 'bea',
      motivo: 'Vacaciones',
    });
    expect(aCrearDto(completo({ motivo: '   ' }))).not.toHaveProperty('motivo');
  });

  it('solo se delegan las áreas activas de las que la persona es miembro', () => {
    const area = (id: string, miembros: string[], activa = true): AreaDto => ({ id, empresaId: 'emp', codigo: id.toUpperCase(), nombre: `Área ${id}`, miembros, activa });
    const areas = [area('a1', ['yo']), area('a2', ['yo', 'ana']), area('a3', ['ana']), area('a4', ['yo'], false)];
    expect(areasDeLaPersona(areas, 'yo').map((a) => a.id)).toEqual(['a1', 'a2']);
    expect(areasDeLaPersona(areas, 'otra')).toEqual([]);
  });

  it('solo se revoca lo programado o vigente', () => {
    const estados: DelegacionDto['estado'][] = ['Programada', 'Vigente', 'Vencida', 'Revocada'];
    expect(estados.map((estado) => puedeRevocar({ estado }))).toEqual([true, true, false, false]);
  });

  it('cada estado tiene su chip canónico distinto, sin rojo para lo que simplemente terminó', () => {
    expect(ESTADOS_DELEGACION.map((e) => CHIP_DELEGACION[e])).toEqual(['Pendiente', 'Vigente', 'Superada', 'Anulado']);
    expect(new Set(Object.values(CHIP_DELEGACION)).size).toBe(4);
  });

  it('dice las áreas en una frase', () => {
    expect(listaDeAreas(['Compras'])).toBe('Compras');
    expect(listaDeAreas(['Compras', 'Jurídica'])).toBe('Compras y Jurídica');
    expect(listaDeAreas(['Compras', 'Jurídica', 'Cartera'])).toBe('Compras, Jurídica y Cartera');
  });
});
