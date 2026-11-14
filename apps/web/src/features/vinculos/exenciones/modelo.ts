import { esErrorApi, type EximirVinculoDto, type ExencionDeVinculoDto, type ObjetivoDeExencionDto, type VinculoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { formatearFechaDeclarada } from '../../documentos/formatoDatos';
import { mensajeDeError } from '../../../shared/errores';

/** Largo máximo de la justificación (`Vinculo.MaximoDeJustificacionDeExencion`). */
export const MAXIMO_JUSTIFICACION = 1000;

/** Estados desde los que se exime: los de un vínculo ya aprobado y que no está bloqueado ni terminado (la API vuelve a validarlo: `vinculo-sin-aprobacion`). */
const ESTADOS_QUE_ADMITEN: readonly VinculoDto['estado'][] = ['Activo', 'PorVencer', 'Vencido'];

export function admiteExencion(estado: VinculoDto['estado']): boolean {
  return ESTADOS_QUE_ADMITEN.includes(estado);
}

/** Las exenciones que rigen hoy (el estado ya viene resuelto a la fecha de la consulta). */
export function exencionesVigentes(exenciones: readonly ExencionDeVinculoDto[] | null | undefined): ExencionDeVinculoDto[] {
  return (exenciones ?? []).filter((e) => e.estado === 'Vigente');
}

/** Nombre legible de lo eximido: el que tenía al eximir (tipo de documento o sección). */
export function nombreDeObjetivo(objetivo: ObjetivoDeExencionDto): string {
  return objetivo.nombre ?? objetivo.codigo ?? objetivo.seccion ?? '—';
}

/** «Todos los requisitos y la vigencia del vínculo» o los nombres separados por coma. */
export function alcanceDe(exencion: Pick<ExencionDeVinculoDto, 'todos' | 'objetivos'>, t: TFunction): string {
  return exencion.todos ? t('exenciones.alcanceTodo') : exencion.objetivos.map(nombreDeObjetivo).join(', ');
}

/** «Hasta el 12/12/2026» o «Hasta que se revoque». */
export function vigenciaDe(exencion: Pick<ExencionDeVinculoDto, 'hasta'>, t: TFunction): string {
  return exencion.hasta ? t('exenciones.hasta', { fecha: formatearFechaDeclarada(exencion.hasta) }) : t('exenciones.sinFecha');
}

/** Lo que el vínculo trae para mostrar la insignia: nada si no hay exenciones vigentes. */
export function resumenDeInsignia(
  exenciones: readonly ExencionDeVinculoDto[] | null | undefined,
  t: TFunction,
): { etiqueta: string; ayuda: string } | null {
  const vigentes = exencionesVigentes(exenciones);
  const primera = vigentes[0];
  if (!primera) return null;
  const todo = vigentes.some((e) => e.todos);
  const alcance = todo ? t('exenciones.alcanceTodo') : alcanceDe(primera, t);
  const ayuda = vigentes.length > 1 && !todo ? t('exenciones.chipAyudaVarias', { alcance, vigencia: vigenciaDe(primera, t) }) : t('exenciones.chipAyuda', { alcance, vigencia: vigenciaDe(primera, t) });
  return { etiqueta: todo ? t('exenciones.chipTodo') : t('exenciones.chip'), ayuda };
}

/** Lo que la persona va llenando en el diálogo de eximir. */
export interface FormularioExencion {
  todos: boolean;
  /** Ids de los tipos de documento del grupo. */
  documentos: ReadonlySet<string>;
  /** Códigos de las secciones de la ficha. */
  secciones: ReadonlySet<string>;
  justificacion: string;
  /** Último día en ISO (`aaaa-mm-dd`) o vacío. */
  hasta: string;
}

export function formularioInicial(documentoInicial?: string | null): FormularioExencion {
  return { todos: false, documentos: new Set(documentoInicial ? [documentoInicial] : []), secciones: new Set(), justificacion: '', hasta: '' };
}

/** Qué falta para poder enviar (el botón queda deshabilitado con este texto a la vista); `null` = completo. */
export function faltaAlEximir(f: FormularioExencion, t: TFunction): string | null {
  if (!f.todos && f.documentos.size === 0 && f.secciones.size === 0) return t('exenciones.dialogo.faltaAlcance');
  if (!f.justificacion.trim()) return t('exenciones.dialogo.faltaJustificacion');
  return null;
}

/** La petición a la API: de todo, o de los documentos y secciones elegidos; sin fecha, hasta que se revoque. */
export function aPeticion(f: FormularioExencion): EximirVinculoDto {
  const objetivos: ObjetivoDeExencionDto[] = f.todos
    ? []
    : [
        ...[...f.documentos].map((id): ObjetivoDeExencionDto => ({ tipo: 'Documento', tipoDocumentoTenantId: id })),
        ...[...f.secciones].map((codigo): ObjetivoDeExencionDto => ({ tipo: 'Seccion', seccion: codigo })),
      ];
  return { todos: f.todos, objetivos: f.todos ? null : objetivos, justificacion: f.justificacion.trim(), hasta: f.hasta || null };
}

/** Los textos propios de los códigos de la exención; el resto, el mensaje común. */
export function errorDeExencion(error: unknown, t: TFunction): string {
  if (esErrorApi(error) && error.codigo in EXENCIONES_ERRORES) return t(`exenciones.errores.${error.codigo as keyof typeof EXENCIONES_ERRORES}`);
  return mensajeDeError(error, t);
}

const EXENCIONES_ERRORES = {
  'exencion-superpuesta': true,
  'exencion-sin-alcance': true,
  'exencion-alcance-invalido': true,
  'exencion-no-encontrada': true,
  'exencion-ya-revocada': true,
  'exencion-vencida': true,
  'objetivo-invalido': true,
  'tipo-documento-invalido': true,
  'seccion-invalida': true,
  'vinculo-sin-aprobacion': true,
  'vinculo-bloqueado': true,
  'estado-terminal': true,
  'justificacion-demasiado-larga': true,
  'fecha-invalida': true,
} as const;

/** Cómo terminó una exención, en una línea para la lista. */
export function cierreDe(exencion: ExencionDeVinculoDto, nombreDe: (id: string) => string, t: TFunction): string | null {
  if (exencion.estado === 'Revocada' && exencion.revocadaEn) {
    return t('exenciones.revocadaPor', {
      persona: nombreDe(exencion.revocadaPor ?? ''),
      fecha: formatearFecha(exencion.revocadaEn),
      justificacion: exencion.justificacionDeRevocacion ?? '',
    });
  }
  if (exencion.estado === 'Vencida') return t('exenciones.vencidaEl', { fecha: exencion.hasta ? formatearFechaDeclarada(exencion.hasta) : '—' });
  return null;
}
