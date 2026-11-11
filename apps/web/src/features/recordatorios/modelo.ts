import type { FilaBandejaDto, RecordatoriosEnviadosDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';

/** Cuántos expedientes admite un recordatorio con lista (`RecordatoriosDeObservaciones.MaximoPorLote`). */
export const MAXIMO_POR_LOTE = 200;

/** Zona del calendario del negocio: «hoy» para el límite de un recordatorio por día (`CalculadoraDeVigencia.Fecha`). */
const ZONA_DEL_NEGOCIO = 'America/Bogota';

const DIA = new Intl.DateTimeFormat('en-CA', { timeZone: ZONA_DEL_NEGOCIO });

/** ¿El último recordatorio fue hoy (día de calendario del negocio)? Con eso el botón se deshabilita antes de que el servidor responda 409. */
export function recordadoHoy(ultimoEn: string | null | undefined, ahora: Date = new Date()): boolean {
  if (!ultimoEn) return false;
  return DIA.format(new Date(ultimoEn)) === DIA.format(ahora);
}

/** Una fila de la bandeja (un expediente × un área) cuyo expediente está devuelto al tercero: se le puede recordar. */
export const esObservado = (fila: FilaBandejaDto): boolean => fila.estadoExpediente === 'Observado';

/** Los expedientes observados de las filas, sin repetir (el mismo expediente sale una vez por cada área). */
export function observadosDe(filas: readonly FilaBandejaDto[]): string[] {
  return [...new Set(filas.filter(esObservado).map((f) => f.expedienteId))];
}

/** Cuántos terceros se recordaron y por qué se omitió el resto: «2 omitidos: 1 ya se les recordó hoy, 1 ya no están devueltos al tercero.» */
export function mensajeDelLote(resultado: RecordatoriosEnviadosDto, t: TFunction): { mensaje: string; severidad: 'success' | 'info' } {
  if (resultado.detalle.length === 0) return { mensaje: t('recordatorios.lote.ninguno'), severidad: 'info' };
  const porMotivo = new Map<string, number>();
  for (const d of resultado.detalle) {
    if (d.resultado === 'Enviado' || d.resultado === 'SoloCampana') continue;
    porMotivo.set(d.resultado, (porMotivo.get(d.resultado) ?? 0) + 1);
  }

  const motivos = [...porMotivo].map(([motivo, cuantos]) => `${cuantos} ${t(`recordatorios.lote.motivos.${motivo as 'YaRecordadoHoy'}`, { defaultValue: motivo })}`).join(', ');
  const enviados = resultado.enviados > 0 ? t('recordatorios.lote.enviados', { count: resultado.enviados }) : '';
  const omitidos = resultado.omitidos > 0 ? t('recordatorios.lote.omitidos', { count: resultado.omitidos, motivos }) : '';
  return { mensaje: [enviados, omitidos].filter(Boolean).join(' '), severidad: resultado.enviados > 0 ? 'success' : 'info' };
}
