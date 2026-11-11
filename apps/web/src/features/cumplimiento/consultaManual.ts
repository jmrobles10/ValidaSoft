import type { AdjuntoDeCumplimientoDto, RegistrarConsultaManualDto } from '@validasoft/api-client';

/**
 * Consulta de listas hecha por fuera (CU-16): el oficial consultó en otro lugar y registra el PDF, de dónde salió, cuándo y, si
 * las hubo, las coincidencias. Reglas de la API: fuente de 3 a 100 caracteres, fecha de los últimos 30 días y no futura,
 * vigencia de 1 a 60 meses y, por coincidencia, persona de la consulta, lista y nombre encontrado.
 */

export const DIAS_MAXIMOS_DE_CONSULTA = 30;
export const MESES_MAXIMOS_DE_VIGENCIA = 60;

export interface BorradorCoincidenciaManual {
  /** Identidad local (llave de React). */
  clave: string;
  /** `clave` de la persona (de `usePersonasDeConsulta`). */
  persona: string;
  lista: string;
  nombre: string;
  detalle: string;
  /** Similitud 0–100 (opcional). */
  puntaje: string;
}

export interface BorradorConsultaManual {
  /** El PDF ya subido con propósito `ConsultaManual`. */
  adjunto: AdjuntoDeCumplimientoDto | null;
  fuente: string;
  /** `aaaa-mm-dd`. */
  fecha: string;
  vigenciaMeses: string;
  coincidencias: BorradorCoincidenciaManual[];
  /** «¿Hubo coincidencias?»: sin esto las filas no cuentan aunque queden en el borrador. */
  hubo: boolean;
}

export type ErrorConsultaManual = 'archivo' | 'fuente' | 'fecha' | 'fecha-fuera-de-plazo' | 'vigencia' | 'coincidencias';

let secuencia = 0;

export function coincidenciaVacia(): BorradorCoincidenciaManual {
  return { clave: `m${String(++secuencia)}`, persona: '', lista: '', nombre: '', detalle: '', puntaje: '' };
}

export function consultaManualVacia(hoy: string): BorradorConsultaManual {
  return { adjunto: null, fuente: '', fecha: hoy, vigenciaMeses: '', coincidencias: [], hubo: false };
}

/** `aaaa-mm-dd` de una fecha en la zona del navegador. */
export function fechaIso(fecha: Date): string {
  return `${String(fecha.getFullYear())}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(fecha.getDate()).padStart(2, '0')}`;
}

/** La fecha de hace `dias` días a partir de `hoy` (`aaaa-mm-dd`). */
export function restarDias(hoy: string, dias: number): string {
  const [anio = 0, mes = 1, dia = 1] = hoy.split('-').map(Number);
  return fechaIso(new Date(anio, mes - 1, dia - dias));
}

function coincidenciaCompleta(c: BorradorCoincidenciaManual): boolean {
  return c.persona !== '' && c.lista.trim() !== '' && c.nombre.trim() !== '';
}

export function validarConsultaManual(b: BorradorConsultaManual, hoy: string): ErrorConsultaManual[] {
  const errores: ErrorConsultaManual[] = [];
  if (b.adjunto === null) errores.push('archivo');
  const fuente = b.fuente.trim();
  if (fuente.length < 3 || fuente.length > 100) errores.push('fuente');
  if (b.fecha === '') errores.push('fecha');
  else if (b.fecha > hoy || b.fecha < restarDias(hoy, DIAS_MAXIMOS_DE_CONSULTA)) errores.push('fecha-fuera-de-plazo');
  if (b.vigenciaMeses !== '' && !(Number(b.vigenciaMeses) >= 1 && Number(b.vigenciaMeses) <= MESES_MAXIMOS_DE_VIGENCIA)) errores.push('vigencia');
  if (b.hubo && (b.coincidencias.length === 0 || !b.coincidencias.every(coincidenciaCompleta))) errores.push('coincidencias');
  return errores;
}

/**
 * Cuerpo del registro. La fecha viaja con hora: hoy, la de ahora; un día anterior, el final de ese día en Colombia (así el
 * último día del plazo no se pasa por la hora y hoy no queda en el futuro).
 */
export function consultaManualADto(b: BorradorConsultaManual, hoy: string, ahora: Date = new Date()): RegistrarConsultaManualDto {
  return {
    adjuntoId: b.adjunto?.id ?? '',
    fuente: b.fuente.trim(),
    consultadaEn: b.fecha === hoy ? ahora.toISOString() : `${b.fecha}T23:59:00-05:00`,
    coincidencias: b.hubo
      ? b.coincidencias.map((c) => ({
          clavePersona: c.persona,
          lista: c.lista.trim(),
          nombreEncontrado: c.nombre.trim(),
          detalle: c.detalle.trim() || null,
          puntaje: c.puntaje === '' ? null : Math.min(100, Number(c.puntaje)),
        }))
      : [],
    mesesVigencia: b.vigenciaMeses === '' ? null : Number(b.vigenciaMeses),
  };
}

/** «Manual · fuente» → «fuente»; `null` si la consulta no se hizo por fuera. */
export function fuenteManual(proveedor: string | null | undefined): string | null {
  return proveedor?.startsWith('Manual · ') ? proveedor.slice('Manual · '.length) : null;
}
