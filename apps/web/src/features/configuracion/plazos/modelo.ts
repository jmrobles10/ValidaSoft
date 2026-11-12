import { esErrorApi, type ActualizarConfiguracionSlaDto, type ConfiguracionSlaDto } from '@validasoft/api-client';

/** Igual que `Expediente.PorcentajeDeAvisoMinimo`/`Maximo` y `ConfiguracionDeSla.MaximoHoras` (90 días) del backend. */
export const PORCENTAJE_MINIMO = 50;
export const PORCENTAJE_MAXIMO = 95;
export const MAXIMO_HORAS = 24 * 90;

/** Lo que se está editando: textos de los campos (vacío = sin plazo en las horas) y el interruptor. */
export interface BorradorSla {
  porcentaje: string;
  avisarAdministradores: boolean;
  /** Horas por área (`areaId` → texto con dígitos). */
  horas: Readonly<Record<string, string>>;
}

export function borradorDeSla(configuracion: ConfiguracionSlaDto): BorradorSla {
  return {
    porcentaje: String(configuracion.porcentajeAviso),
    avisarAdministradores: configuracion.avisarAdministradores,
    horas: Object.fromEntries(configuracion.areas.map((a) => [a.areaId, a.horas === null ? '' : String(a.horas)])),
  };
}

export function errorDePorcentaje(texto: string): 'requerido' | 'rango' | null {
  if (texto === '') return 'requerido';
  const numero = Number(texto);
  return numero < PORCENTAJE_MINIMO || numero > PORCENTAJE_MAXIMO ? 'rango' : null;
}

/** El plazo de un área puede quedar vacío (sin plazo); si se escribe, va de 1 hora a 90 días. */
export function errorDeHoras(texto: string): 'rango' | null {
  if (texto === '') return null;
  const numero = Number(texto);
  return numero < 1 || numero > MAXIMO_HORAS ? 'rango' : null;
}

export function esValido(borrador: BorradorSla): boolean {
  return errorDePorcentaje(borrador.porcentaje) === null && Object.values(borrador.horas).every((h) => errorDeHoras(h) === null);
}

export function hayCambios(borrador: BorradorSla, original: BorradorSla): boolean {
  if (borrador.porcentaje !== original.porcentaje || borrador.avisarAdministradores !== original.avisarAdministradores) return true;
  return Object.keys({ ...original.horas, ...borrador.horas }).some((id) => (borrador.horas[id] ?? '') !== (original.horas[id] ?? ''));
}

/** Lo que viaja a la API: todas las áreas de la lista, con su plazo o `null` (sin plazo por defecto). */
export function aActualizarDto(borrador: BorradorSla): ActualizarConfiguracionSlaDto {
  return {
    porcentajeAviso: Number(borrador.porcentaje),
    avisarAdministradores: borrador.avisarAdministradores,
    areas: Object.entries(borrador.horas).map(([areaId, texto]) => ({ areaId, horas: texto === '' ? null : Number(texto) })),
  };
}

/** `porcentaje-invalido` es del campo del porcentaje; el resto de los errores se muestra arriba, en general. */
export function errorDelPorcentaje(error: unknown): boolean {
  return esErrorApi(error) && error.codigo === 'porcentaje-invalido';
}
