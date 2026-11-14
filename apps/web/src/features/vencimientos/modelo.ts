import type { RecordatorioEnviadoDto, VencimientoARecordarDto, VencimientoDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';

/** Horizontes del filtro, en días; `0` = todos los que tienen fecha. */
export const HORIZONTES = [7, 15, 30, 60, 90, 0] as const;
export type Horizonte = (typeof HORIZONTES)[number];
export const HORIZONTE_POR_DEFECTO: Horizonte = 30;

/** Estados por los que filtra la API (`SinVencimiento` no aparece: esos documentos no tienen fecha). */
export const ESTADOS_VENCIMIENTO = ['Vigente', 'PorVencer', 'EnGracia', 'Vencida'] as const;
export type EstadoVencimiento = (typeof ESTADOS_VENCIMIENTO)[number];

/** De dónde sale la fecha de vencimiento (`BaseVigencia` del dominio). */
export const BASES_VENCIMIENTO = ['FechaExpedicion', 'FechaCarga', 'VencimientoPropio', 'Ratificacion', 'SinVencimiento', 'PeriodoNoCalculado'] as const;
export type BaseVencimiento = (typeof BASES_VENCIMIENTO)[number];

/** Máximo de documentos por recordatorio (`VencimientosEndpoints.MaximoPorRecordatorio`). */
export const MAXIMO_POR_RECORDATORIO = 200;

/** El cliente generado tipa `estado` y `base` como texto: se estrechan aquí y lo desconocido se ignora. */
function esUnoDe<T extends string | number>(valores: readonly T[], valor: unknown): valor is T {
  return (valores as readonly unknown[]).includes(valor);
}

export function estadoVencimiento(v: Pick<VencimientoDto, 'estado'>): EstadoVencimiento | null {
  return esUnoDe(ESTADOS_VENCIMIENTO, v.estado) ? v.estado : null;
}

export function baseVencimiento(v: Pick<VencimientoDto, 'base'>): BaseVencimiento | null {
  return esUnoDe(BASES_VENCIMIENTO, v.base) ? v.base : null;
}

/** Exención (EX-6): todos los vencimientos, solo los que el vínculo tiene eximidos (`true`) o solo los que no (`false`). */
export type FiltroDeExencion = '' | 'true' | 'false';

export interface FiltrosVencimientos {
  dias: Horizonte;
  estado: EstadoVencimiento | '';
  exento: FiltroDeExencion;
}

/** Filtros de la URL (`?dias=&estado=&exento=`); lo que no sea una opción válida vuelve al valor por defecto. */
export function leerFiltros(params: URLSearchParams): FiltrosVencimientos {
  const textoDias = params.get('dias');
  const dias = textoDias === null ? HORIZONTE_POR_DEFECTO : Number(textoDias);
  const estado = params.get('estado') ?? '';
  return {
    dias: esUnoDe(HORIZONTES, dias) ? dias : HORIZONTE_POR_DEFECTO,
    estado: esUnoDe(ESTADOS_VENCIMIENTO, estado) ? estado : '',
    exento: params.get('exento') === 'true' ? 'true' : params.get('exento') === 'false' ? 'false' : '',
  };
}

/** El filtro de exención como lo pide la API (`exento`); sin filtro, no se manda. */
export function exentoParaApi(filtro: FiltroDeExencion): boolean | undefined {
  return filtro === '' ? undefined : filtro === 'true';
}

/** Un vencimiento es un documento de un vínculo: la clave combina los dos. */
export function claveVencimiento(v: Pick<VencimientoDto, 'vinculoId' | 'documentoId'>): string {
  return `${v.vinculoId}|${v.documentoId}`;
}

/** Lo que se envía al recordar: solo lo seleccionado que sigue en la lista, sin repetir. */
export function lote(filas: readonly VencimientoDto[], seleccion: ReadonlySet<string>): VencimientoARecordarDto[] {
  const vistos = new Set<string>();
  const resultado: VencimientoARecordarDto[] = [];
  for (const f of filas) {
    const clave = claveVencimiento(f);
    if (!seleccion.has(clave) || vistos.has(clave)) continue;
    vistos.add(clave);
    resultado.push({ vinculoId: f.vinculoId, documentoId: f.documentoId });
  }
  return resultado;
}

/** A cuántos terceros les llega correo con lo que se va a recordar: uno por vínculo, con todos sus documentos (lo dice el diálogo antes de enviar). */
export function tercerosDelLote(lote: readonly VencimientoARecordarDto[]): number {
  return new Set(lote.map((v) => v.vinculoId)).size;
}

/** «Vence hoy», «Vence en 10 días» o «Venció hace 3 días» (los días negativos ya vencieron). */
export function textoDias(diasRestantes: number, t: TFunction): string {
  if (diasRestantes === 0) return t('vencimientos.dias.venceHoy');
  return diasRestantes > 0 ? t('vencimientos.dias.venceEn', { count: diasRestantes }) : t('vencimientos.dias.vencioHace', { count: -diasRestantes });
}

export type TonoVencimiento = 'normal' | 'advertencia' | 'error';

/** Tono del texto de días: vencida es error; por vencer y en gracia, advertencia. */
export function tonoVencimiento(v: Pick<VencimientoDto, 'estado'>): TonoVencimiento {
  const estado = estadoVencimiento(v);
  if (estado === 'Vencida') return 'error';
  return estado === 'PorVencer' || estado === 'EnGracia' ? 'advertencia' : 'normal';
}

export const COLOR_TONO: Readonly<Record<TonoVencimiento, string>> = { normal: 'text.primary', advertencia: 'warning.main', error: 'error.main' };

/** Aviso tras recordar: cuántos se enviaron a cuántos terceros y, si los hubo, cuántos se omitieron. */
export function mensajeRecordatorio(r: RecordatorioEnviadoDto, t: TFunction): { mensaje: string; severidad: 'success' | 'warning' } {
  const enviados =
    r.documentos > 0 ? t('vencimientos.enviados', { count: r.documentos, terceros: t('vencimientos.terceros', { count: r.correos }) }) : t('vencimientos.ningunoEnviado');
  const omitidos = r.omitidos > 0 ? ` ${t('vencimientos.omitidos', { count: r.omitidos })}` : '';
  return { mensaje: `${enviados}${omitidos}`, severidad: r.documentos > 0 ? 'success' : 'warning' };
}
