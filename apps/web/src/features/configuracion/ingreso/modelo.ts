import type { ConfiguracionDeIngresoDto } from '@validasoft/api-client';

/** Duraciones máximas de la sesión directa que se ofrecen (la API admite de 1 a 24 horas). */
export const HORAS_DE_SESION: readonly number[] = [1, 2, 4, 8, 12, 24];

/** Cierres por inactividad que se ofrecen (la API admite de 5 a 480 minutos). */
const MINUTOS_DE_INACTIVIDAD: readonly number[] = [5, 10, 15, 30, 60, 120, 240, 480];

/** Los cierres por inactividad que caben en una sesión de `horas` horas (no puede superar su duración). */
export function minutosDeInactividad(horas: number): readonly number[] {
  const validos = MINUTOS_DE_INACTIVIDAD.filter((m) => m <= horas * 60);
  // Un valor del servidor fuera de la lista (otro cliente de la API) no desaparece del selector.
  return validos.length > 0 ? validos : [Math.min(5, horas * 60)];
}

/** La política es la misma (los modos en cualquier orden). */
export function sinCambios(a: ConfiguracionDeIngresoDto, b: ConfiguracionDeIngresoDto): boolean {
  return (
    [...a.modos].sort().join('|') === [...b.modos].sort().join('|') &&
    a.sesionHoras === b.sesionHoras &&
    a.inactividadMinutos === b.inactividadMinutos &&
    a.passkeyParaTodos === b.passkeyParaTodos
  );
}
