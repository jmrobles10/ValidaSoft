import type {
  CampaniaDto,
  EstadoCampania,
  FilaCampaniaDto,
  FilaVistaPreviaCampaniaDto,
  FiltroDeCampaniaDto,
  TotalesCampaniaDto,
  VistaPreviaCampaniaDto,
} from '@validasoft/api-client';
import { formatearDiaCivil } from '@validasoft/ui';
import type { TFunction } from 'i18next';

/** Tono del chip de estado de la campaña (colores de MUI). */
export const TONO_CAMPANIA: Readonly<Record<EstadoCampania, 'info' | 'warning' | 'success' | 'error' | 'default'>> = {
  Procesando: 'info',
  Pausada: 'warning',
  Completada: 'success',
  CompletadaConErrores: 'error',
  Cancelada: 'default',
};

/** Estados de vínculo que admite una campaña (`FiltroDeCampania.Estados`). */
export const ESTADOS_DE_CAMPANIA = ['Activo', 'PorVencer', 'Vencido'] as const;
export type EstadoDeVinculoDeCampania = (typeof ESTADOS_DE_CAMPANIA)[number];

/** La API manda el estado del vínculo como texto: aquí se estrecha a los tres que una campaña admite. */
export function esEstadoDeCampania(valor: string): valor is EstadoDeVinculoDeCampania {
  return ESTADOS_DE_CAMPANIA.some((e) => e === valor);
}

/** Rango de «vencen en N días» (`FiltroDeCampania.MaximoDias`). */
export const MAXIMO_DIAS = 365;
export const DIAS_POR_DEFECTO = 30;

/** Las acciones de la vista previa y de las filas, tal como las devuelve la API. */
export type AccionDeVistaPrevia = 'Abrir' | 'Recordar' | 'Omitir';
export const ACCIONES_DE_VISTA_PREVIA: readonly AccionDeVistaPrevia[] = ['Abrir', 'Recordar', 'Omitir'];

/** Cuántas filas de la vista previa se ven por página. */
export const FILAS_POR_PAGINA = 10;

/** Lo que la persona elige en la pantalla antes de pedir la vista previa. */
export interface BorradorDeFiltro {
  dias: string;
  /** `''` = todos los tipos de vínculo. */
  tipoVinculoId: string;
  estados: readonly EstadoDeVinculoDeCampania[];
}

export const BORRADOR_INICIAL: BorradorDeFiltro = { dias: String(DIAS_POR_DEFECTO), tipoVinculoId: '', estados: ESTADOS_DE_CAMPANIA };

/** Un entero de 0 a 365 (sin signos ni decimales). */
export function diasValidos(texto: string): boolean {
  if (!/^\d{1,3}$/.test(texto.trim())) return false;
  return Number(texto) <= MAXIMO_DIAS;
}

/** ¿Se puede pedir la vista previa? Días válidos y al menos un estado. */
export function filtroValido(b: BorradorDeFiltro): boolean {
  return diasValidos(b.dias) && b.estados.length > 0;
}

/** Marca o desmarca un estado y deja siempre el orden canónico (Activo, Por vencer, Vencido). */
export function alternarEstado(estados: readonly EstadoDeVinculoDeCampania[], estado: EstadoDeVinculoDeCampania): EstadoDeVinculoDeCampania[] {
  const quedan = estados.includes(estado) ? estados.filter((e) => e !== estado) : [...estados, estado];
  return ESTADOS_DE_CAMPANIA.filter((e) => quedan.includes(e));
}

/** El filtro que se envía a la API. */
export function filtroDeBorrador(b: BorradorDeFiltro): FiltroDeCampaniaDto {
  return { diasParaVencer: Number(b.dias), tipoVinculoId: b.tipoVinculoId || null, estados: [...b.estados] };
}

/** La campaña todavía avanza: se refresca sola. */
export function enCurso(campania: Pick<CampaniaDto, 'estado'>): boolean {
  return campania.estado === 'Procesando';
}

/** Porcentaje de avance sobre todas las filas (0–100). */
export function porcentaje(t: TotalesCampaniaDto): number {
  return t.filas <= 0 ? 100 : Math.round((Math.min(t.procesadas, t.filas) / t.filas) * 100);
}

export type AccionDeCampania = 'pausar' | 'reanudar' | 'cancelar' | 'reintentar';

/** Lo que la persona puede hacer con la campaña en su estado. */
export function accionesDeCampania(campania: Pick<CampaniaDto, 'estado' | 'totales'>): AccionDeCampania[] {
  switch (campania.estado) {
    case 'Procesando':
      return ['pausar', 'cancelar'];
    case 'Pausada':
      return ['reanudar', 'cancelar'];
    case 'CompletadaConErrores':
      return campania.totales.fallidas > 0 ? ['reintentar'] : [];
    default:
      return [];
  }
}

/** Cuántos vínculos toca cada acción con consecuencias (el número que dice el diálogo de confirmación). */
export function cuantosAfecta(accion: AccionDeCampania, totales: TotalesCampaniaDto): number {
  return accion === 'reintentar' ? totales.fallidas : totales.pendientes;
}

/** La campaña tiene algo que hacer: abrir o recordar alguna renovación. */
export function hayQueHacer(vista: Pick<VistaPreviaCampaniaDto, 'abririan' | 'recordarian'>): boolean {
  return vista.abririan + vista.recordarian > 0;
}

/** Filas de la vista previa con la acción elegida (`''` = todas). */
export function filasConAccion(filas: readonly FilaVistaPreviaCampaniaDto[], accion: string): FilaVistaPreviaCampaniaDto[] {
  return accion ? filas.filter((f) => f.accion === accion) : [...filas];
}

/** La página (desde 0) de una lista, y si hay una siguiente. */
export function paginar<T>(filas: readonly T[], pagina: number, tamano = FILAS_POR_PAGINA): { filas: T[]; haySiguiente: boolean } {
  const desde = pagina * tamano;
  return { filas: filas.slice(desde, desde + tamano), haySiguiente: filas.length > desde + tamano };
}

/** `2026-11-30` → «30/11/2026» sin pasar por la zona horaria (la API manda solo el día). */
export function formatearDia(dia: string | null | undefined): string {
  return dia ? formatearDiaCivil(dia) : '—';
}

/** `CO|NIT|900123456` → «NIT 900123456». */
export function identificacionLegible(clave: string): string {
  const partes = clave.split('|');
  return partes.length === 3 ? `${partes[1] ?? ''} ${partes[2] ?? ''}` : clave;
}

/** «Vencen en 30 días · Proveedor · Activo, Por vencer»: el filtro de la campaña en una línea. */
export function resumenDeFiltro(filtro: FiltroDeCampaniaDto, nombreDelTipo: string | null, t: TFunction): string {
  const dias = filtro.diasParaVencer ?? DIAS_POR_DEFECTO;
  const estados = filtro.estados ?? [];
  const todos = ESTADOS_DE_CAMPANIA.every((e) => estados.includes(e));
  return [
    dias === 0 ? t('campanias.filtro.venceHoy') : t('campanias.filtro.vencenEn', { count: dias }),
    filtro.tipoVinculoId ? (nombreDelTipo ?? t('campanias.filtro.unTipo')) : t('campanias.filtro.todosLosTipos'),
    todos || estados.length === 0
      ? t('campanias.filtro.todosLosEstados')
      : ESTADOS_DE_CAMPANIA.filter((e) => estados.includes(e))
          .map((e) => t(`campanias.estadosVinculo.${e}`))
          .join(', '),
  ].join(' · ');
}

/** La fila de una campaña es la de un vínculo con su expediente abierto: el enlace sale del folio. */
export function tieneExpediente(f: Pick<FilaCampaniaDto, 'expedienteId' | 'folio'>): boolean {
  return Boolean(f.expedienteId && f.folio);
}
