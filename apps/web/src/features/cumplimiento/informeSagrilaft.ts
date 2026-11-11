import type { Estado } from '@validasoft/ui';
import type { FilaTerceroSagrilafDto, InformeSagrilaftDto, MesDeCumplimientoDto, PersonaInformeDto } from '@validasoft/api-client';

/** Lógica pura del informe SAGRILAFT completo: filtros de la tabla, distribución del riesgo, tendencia de 12 meses y estados. */

export const ESTADOS_DE_ESTUDIO = ['SinEstudio', 'Vigente', 'PorVencer', 'Vencida'] as const;
export type EstadoDeEstudio = (typeof ESTADOS_DE_ESTUDIO)[number];
export const ESTADOS_DE_LISTAS = ['SinConsulta', 'Vigente', 'PorVencer', 'Vencida'] as const;
export type EstadoDeListas = (typeof ESTADOS_DE_LISTAS)[number];
/** Terceros que se muestran de a tantos; «Mostrar más» agrega otro tanto. */
export const TAMANO_DE_PAGINA = 25;

export interface FiltrosSagrilaft {
  /** Estado del estudio (debida diligencia); `''` = todos. */
  estudio: '' | EstadoDeEstudio;
  /** Nivel de riesgo del estudio; `''` = todos. */
  riesgo: '' | '1' | '2' | '3';
}

export const SIN_FILTROS: FiltrosSagrilaft = { estudio: '', riesgo: '' };

export const hayFiltros = (f: FiltrosSagrilaft): boolean => f.estudio !== '' || f.riesgo !== '';

export function esEstadoDeEstudio(valor: string): valor is EstadoDeEstudio {
  return (ESTADOS_DE_ESTUDIO as readonly string[]).includes(valor);
}

/** El estado se normaliza porque el servidor lo envía como texto libre: lo desconocido se toma como «sin estudio». */
export function estadoDeEstudio(fila: Pick<FilaTerceroSagrilafDto, 'estadoDebidaDiligencia'>): EstadoDeEstudio {
  return esEstadoDeEstudio(fila.estadoDebidaDiligencia) ? fila.estadoDebidaDiligencia : 'SinEstudio';
}

export function estadoDeListas(fila: Pick<FilaTerceroSagrilafDto, 'estadoListas'>): EstadoDeListas {
  return (ESTADOS_DE_LISTAS as readonly string[]).includes(fila.estadoListas) ? (fila.estadoListas as EstadoDeListas) : 'SinConsulta';
}

/** El estado canónico del chip del sistema (`ChipEstado`) para el estado de una verificación del informe. */
export function estadoCanonico(estado: EstadoDeEstudio | EstadoDeListas): Estado {
  return estado === 'SinEstudio' || estado === 'SinConsulta' ? 'Pendiente' : estado;
}

export function filtrarFilas(filas: readonly FilaTerceroSagrilafDto[], filtros: FiltrosSagrilaft): FilaTerceroSagrilafDto[] {
  return filas.filter((f) => (filtros.estudio === '' || estadoDeEstudio(f) === filtros.estudio) && (filtros.riesgo === '' || String(f.nivelRiesgo ?? '') === filtros.riesgo));
}

/** Los tres niveles siempre (aunque no haya terceros en alguno) y cuántos terceros con estudio hay en cada uno. */
export function distribucionDeRiesgo(informe: Pick<InformeSagrilaftDto, 'riesgo'>): { nivel: 1 | 2 | 3; cantidad: number }[] {
  return ([1, 2, 3] as const).map((nivel) => ({ nivel, cantidad: informe.riesgo.filter((r) => r.nivel === nivel).reduce((suma, r) => suma + r.cantidad, 0) }));
}

/** Alto de una barra como porcentaje del máximo (con un mínimo visible cuando hay algo). */
export function porcentajeDe(valor: number, maximo: number): number {
  if (valor <= 0 || maximo <= 0) return 0;
  return Math.max(4, Math.round((valor / maximo) * 100));
}

export function maximoDeTendencia(meses: readonly MesDeCumplimientoDto[]): number {
  return Math.max(0, ...meses.flatMap((m) => [m.estudios, m.consultas]));
}

/** `2026-11` → «nov.»; en enero (o el primero de la serie) lleva el año: «ene. 2027». */
export function etiquetaDeMes(mes: string, conAnio: boolean): string {
  const [anio, numero] = mes.split('-').map(Number);
  if (!anio || !numero) return mes;
  const nombre = new Date(Date.UTC(anio, numero - 1, 15)).toLocaleDateString('es-CO', { month: 'short', timeZone: 'UTC' });
  return conAnio ? `${nombre} ${String(anio)}` : nombre;
}

export type RevisionDeTercero = { tipo: 'sin-fecha' } | { tipo: 'vencida'; dias: number } | { tipo: 'hoy' } | { tipo: 'vence'; dias: number };

/** Cuándo toca la próxima debida diligencia: días para (positivo) o desde (negativo) la fecha. */
export function revisionDe(fila: Pick<FilaTerceroSagrilafDto, 'proximaRevision' | 'diasParaRevision'>): RevisionDeTercero {
  if (!fila.proximaRevision || fila.diasParaRevision === null) return { tipo: 'sin-fecha' };
  if (fila.diasParaRevision < 0) return { tipo: 'vencida', dias: Math.abs(fila.diasParaRevision) };
  return fila.diasParaRevision === 0 ? { tipo: 'hoy' } : { tipo: 'vence', dias: fila.diasParaRevision };
}

/** «12,5» o `null` si no hay promedio. */
export function promedioDeDias(promedio: number | string | null): string | null {
  return promedio === null ? null : Number(promedio).toLocaleString('es-CO', { maximumFractionDigits: 1 });
}

/** Marcas de una persona del tercero para el detalle: PEP, vínculo con PEP y beneficiario final (desde 5 %). */
export function marcasDePersona(p: Pick<PersonaInformeDto, 'esPep' | 'vinculoPep' | 'beneficiarioFinal'>): ('pep' | 'vinculoPep' | 'beneficiarioFinal')[] {
  return [p.esPep ? 'pep' : null, p.vinculoPep ? 'vinculoPep' : null, p.beneficiarioFinal ? 'beneficiarioFinal' : null].filter((m): m is 'pep' | 'vinculoPep' | 'beneficiarioFinal' => m !== null);
}

export function participacionEnTexto(participacion: number | string | null): string | null {
  return participacion === null ? null : `${Number(participacion).toLocaleString('es-CO', { maximumFractionDigits: 2 })} %`;
}
