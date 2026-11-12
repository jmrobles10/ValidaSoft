import type { ActualizarPlanDelGrupoDto, MedicionDePlanDto, PlanDelGrupoDto } from '@validasoft/api-client';

/** Lo que el plan del grupo mide, en el orden en que se muestra (OD-14). */
export const METRICAS = ['Usuarios', 'Empresas', 'VinculosActivos', 'Almacenamiento', 'LlamadasApi'] as const;
export type Metrica = (typeof METRICAS)[number];

export type NivelDeUso = 'Normal' | 'PorAgotar' | 'Agotado';

export function esMetrica(valor: string): valor is Metrica {
  return (METRICAS as readonly string[]).includes(valor);
}

export function esNivel(valor: string): valor is NivelDeUso {
  return valor === 'Normal' || valor === 'PorAgotar' || valor === 'Agotado';
}

/** Lo medido en sus unidades: el almacenamiento llega en bytes y se escribe en MB o GB; lo demás, con separador de miles. */
export function formatearUso(metrica: Metrica, valor: number): string {
  if (metrica !== 'Almacenamiento') return valor.toLocaleString('es-CO');
  if (valor >= 1024 ** 3) return `${(valor / 1024 ** 3).toLocaleString('es-CO', { maximumFractionDigits: 1 })} GB`;
  if (valor >= 1024 ** 2) return `${(valor / 1024 ** 2).toLocaleString('es-CO', { maximumFractionDigits: 1 })} MB`;
  return `${(valor / 1024).toLocaleString('es-CO', { maximumFractionDigits: 1 })} KB`;
}

/** Lo que llena la barra (0 a 100); sin límite, nada. */
export function porcentajeDeBarra(m: Pick<MedicionDePlanDto, 'porcentaje'>): number {
  return Math.min(100, Math.max(0, m.porcentaje ?? 0));
}

/** Metas de la métrica con su aviso: las que están al 80 % o más, para el resumen. */
export function enAlerta(mediciones: readonly MedicionDePlanDto[]): MedicionDePlanDto[] {
  return mediciones.filter((m) => m.nivel !== 'Normal');
}

// ---------- el plan que fija Plataforma ----------

export interface BorradorPlan {
  codigo: string;
  usuarios: string;
  empresas: string;
  vinculos: string;
  almacenamientoMb: string;
  llamadas: string;
  bloqueoSuave: boolean;
}

export type ErrorPlan = 'codigo-invalido';

export const LONGITUD_MINIMA_CODIGO = 2;
export const LONGITUD_MAXIMA_CODIGO = 40;

const comoTexto = (n: number | null | undefined): string => (n === null || n === undefined ? '' : String(n));

export function borradorDesdePlan(plan: PlanDelGrupoDto): BorradorPlan {
  return {
    codigo: plan.codigo,
    usuarios: comoTexto(plan.limites.usuarios),
    empresas: comoTexto(plan.limites.empresas),
    vinculos: comoTexto(plan.limites.vinculosActivos),
    almacenamientoMb: comoTexto(plan.limites.almacenamientoMb),
    llamadas: comoTexto(plan.limites.llamadasApiMes),
    bloqueoSuave: plan.bloqueoSuave,
  };
}

export function validarPlan(b: BorradorPlan): ErrorPlan[] {
  const largo = b.codigo.trim().length;
  return largo < LONGITUD_MINIMA_CODIGO || largo > LONGITUD_MAXIMA_CODIGO ? ['codigo-invalido'] : [];
}

/** Vacío = sin límite; `0` es un límite de verdad (no se permite nada). */
const limiteDe = (texto: string): number | null => (texto.trim() === '' ? null : Number(texto));

export function cuerpoPlan(b: BorradorPlan): ActualizarPlanDelGrupoDto {
  return {
    codigo: b.codigo.trim(),
    limites: {
      usuarios: limiteDe(b.usuarios),
      empresas: limiteDe(b.empresas),
      vinculosActivos: limiteDe(b.vinculos),
      almacenamientoMb: limiteDe(b.almacenamientoMb),
      llamadasApiMes: limiteDe(b.llamadas),
    },
    bloqueoSuave: b.bloqueoSuave,
  };
}

/** ¿El borrador difiere de lo guardado? (el botón solo se habilita con cambios). */
export function hayCambiosDePlan(b: BorradorPlan, plan: PlanDelGrupoDto): boolean {
  const base = borradorDesdePlan(plan);
  return (Object.keys(base) as (keyof BorradorPlan)[]).some((k) => b[k] !== base[k]);
}
