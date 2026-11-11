import type { AreaDto, CrearDelegacionDto, DelegacionDto, EstadoDeDelegacion } from '@validasoft/api-client';
import { formatearFecha, type Estado } from '@validasoft/ui';

/** Igual que `DelegacionDeRevision.MaximoDias` y `LongitudMaximaMotivo` del dominio. */
export const MAXIMO_DIAS_DELEGACION = 366;
export const MAXIMO_MOTIVO = 300;

export const ESTADOS_DELEGACION: readonly EstadoDeDelegacion[] = ['Programada', 'Vigente', 'Vencida', 'Revocada'];

/**
 * Estado canónico con el que se dibuja cada estado de la delegación (el texto sale de `delegaciones.estados`): lo que terminó,
 * por vencerse o por revocarse, queda en neutro y no en rojo, porque no es un error.
 */
export const CHIP_DELEGACION: Readonly<Record<EstadoDeDelegacion, Estado>> = {
  Programada: 'Pendiente',
  Vigente: 'Vigente',
  Vencida: 'Superada',
  Revocada: 'Anulado',
};

/** Una delegación se puede revocar mientras no haya terminado ni se haya revocado. */
export function puedeRevocar(delegacion: Pick<DelegacionDto, 'estado'>): boolean {
  return delegacion.estado === 'Programada' || delegacion.estado === 'Vigente';
}

/** `2026-11-12` → «12/11/2026» sin pasar por zonas horarias (un día sin hora no se corre). */
export function formatearDia(iso: string): string {
  return formatearFecha(`${iso.slice(0, 10)}T12:00:00Z`);
}

/** Días de calendario entre dos fechas ISO, contando ambos extremos (`desde` = `hasta` es 1). */
export function diasDeLaDelegacion(desde: string, hasta: string): number {
  const dia = (iso: string) => {
    const [a, m, d] = iso.slice(0, 10).split('-').map(Number) as [number, number, number];
    return Date.UTC(a, m - 1, d);
  };
  return Math.round((dia(hasta) - dia(desde)) / 86_400_000) + 1;
}

export interface BorradorDelegacion {
  /** Persona en cuyo nombre se delega; vacío = quien está en sesión. */
  enNombreDe: string;
  aUsuarioId: string;
  desde: string;
  hasta: string;
  /** Áreas elegidas; vacío = todas las de quien delega. */
  areas: readonly string[];
  motivo: string;
}

export function borradorInicial(hoy: string): BorradorDelegacion {
  return { enNombreDe: '', aUsuarioId: '', desde: hoy, hasta: '', areas: [], motivo: '' };
}

export type FaltaDelegacion = 'persona' | 'desde' | 'hasta' | 'rango' | 'hoy' | 'largo' | 'areas';

/** Lo que impide crear la delegación (vacío = se puede). `areasDisponibles`: cuántas áreas activas tiene quien delega. */
export function faltasDeDelegacion(borrador: BorradorDelegacion, hoy: string, areasDisponibles: number): FaltaDelegacion[] {
  const faltas: FaltaDelegacion[] = [];
  if (!borrador.aUsuarioId) faltas.push('persona');
  if (!borrador.desde) faltas.push('desde');
  if (!borrador.hasta) faltas.push('hasta');
  if (borrador.desde && borrador.hasta) {
    if (borrador.hasta < borrador.desde) faltas.push('rango');
    else if (diasDeLaDelegacion(borrador.desde, borrador.hasta) > MAXIMO_DIAS_DELEGACION) faltas.push('largo');
  }
  if (borrador.hasta && borrador.hasta < hoy) faltas.push('hoy');
  if (areasDisponibles === 0) faltas.push('areas');
  return faltas;
}

/** Lo que viaja a la API: lo vacío no viaja y `deUsuarioId` solo cuando se delega por otra persona. */
export function aCrearDto(borrador: BorradorDelegacion): CrearDelegacionDto {
  const motivo = borrador.motivo.trim();
  return {
    aUsuarioId: borrador.aUsuarioId,
    desde: borrador.desde,
    hasta: borrador.hasta,
    ...(borrador.areas.length > 0 && { areas: [...borrador.areas] }),
    ...(borrador.enNombreDe && { deUsuarioId: borrador.enNombreDe }),
    ...(motivo && { motivo }),
  };
}

/** Áreas activas de las que la persona es miembro: lo único que puede delegar. */
export function areasDeLaPersona(areas: readonly AreaDto[], usuarioId: string): AreaDto[] {
  return areas.filter((a) => a.activa && a.miembros.includes(usuarioId));
}

const LISTA = new Intl.ListFormat('es-CO', { style: 'long', type: 'conjunction' });

/** Nombres de las áreas dichos en una frase («Compras, Jurídica y Cartera»). */
export function listaDeAreas(nombres: readonly string[]): string {
  return LISTA.format(nombres);
}
