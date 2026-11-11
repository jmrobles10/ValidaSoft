import type { TFunction } from 'i18next';
import { etiquetaCampo } from './formato';

/**
 * Secciones canónicas de la ficha maestra (docs/14 §4) tal como las pide un flujo (F2, FM-25): código, si van por
 * periodo (solo la financiera), si no aplican a persona natural (Legal y Financiera) y los campos de primer nivel que
 * una empresa puede señalar al observarla (rutas snake_case; lo que fija el registro del tercero no se corrige ahí).
 */

export type CodigoSeccionFicha = 'FICHA_GENERAL' | 'FICHA_TRIBUTARIA' | 'FICHA_FINANCIERA' | 'FICHA_LEGAL' | 'FICHA_SST' | 'FICHA_ESPECIALIDADES';

export interface DefinicionSeccionFicha {
  codigo: CodigoSeccionFicha;
  porPeriodo: boolean;
  /** A la persona natural no se le pide (no entra a su expediente). */
  soloJuridica: boolean;
  campos: readonly string[];
}

/** Hasta cuántos cierres financieros se pueden pedir (`RequisitosDeSeccion.MaximoPeriodos`). */
export const MAXIMO_PERIODOS = 5;

/** Ruta que señala el soporte de la versión (el selector de soporte del wizard). */
export const CAMPO_SOPORTES = 'soportes';

export const SECCIONES_FICHA: readonly DefinicionSeccionFicha[] = [
  {
    codigo: 'FICHA_GENERAL',
    porPeriodo: false,
    soloJuridica: false,
    campos: ['razon_social', 'nombres', 'apellidos', 'correo', 'telefono', 'pais', 'ciudad', 'direccion', 'direccion_estructurada', 'ciiu_principal', 'ciiu_secundarias', 'pagina_web', 'certificado_iso', 'anio_inicio'],
  },
  {
    codigo: 'FICHA_TRIBUTARIA',
    porPeriodo: false,
    soloJuridica: false,
    campos: ['responsabilidades', 'resolucion_gran_contribuyente', 'resolucion_autorretenedor', 'responsable_ica', 'autorretenedor_ica', 'exento_rete_cree', 'ica'],
  },
  {
    codigo: 'FICHA_FINANCIERA',
    porPeriodo: true,
    soloJuridica: true,
    campos: ['fecha_corte', 'activos', 'pasivos', 'patrimonio', 'ingresos_brutos', 'ingresos_netos', 'gastos_operacionales', 'gastos_no_operacionales', 'otros_ingresos', 'moneda_extranjera'],
  },
  { codigo: 'FICHA_LEGAL', porPeriodo: false, soloJuridica: true, campos: ['personas', 'revisor_fiscal_firma'] },
  {
    codigo: 'FICHA_SST',
    porPeriodo: false,
    soloJuridica: false,
    campos: ['programa_salud_ocupacional', 'identifica_factores_de_riesgo', 'comite_paritario', 'programa_higiene_y_seguridad', 'gestion_ambiental', 'reglamento_interno', 'politica_hseq', 'licencias_y_permisos'],
  },
  { codigo: 'FICHA_ESPECIALIDADES', porPeriodo: false, soloJuridica: false, campos: ['especialidades'] },
];

export function seccionFicha(codigo: string | null | undefined): DefinicionSeccionFicha | null {
  const normalizado = codigo?.trim().toUpperCase();
  return SECCIONES_FICHA.find((s) => s.codigo === normalizado) ?? null;
}

/** Nombre legible de una sección del catálogo. */
export function nombreSeccion(codigo: CodigoSeccionFicha, t: TFunction): string {
  return t(`fichaEmpresa.secciones.${codigo}`);
}

/** Campos que se pueden señalar al observar la sección (los de primer nivel y el soporte). */
export function camposSenalables(codigo: string | null | undefined): string[] {
  const seccion = seccionFicha(codigo);
  return seccion ? [...seccion.campos, CAMPO_SOPORTES] : [];
}

/**
 * Etiqueta legible de una ruta de campo: `razon_social` ⇒ «Razón social»; `personas[1].porcentaje` ⇒
 * «Personas relacionadas 2 › Participación»; `soportes` ⇒ «Soporte».
 */
export function etiquetaDeRuta(ruta: string, t: TFunction): string {
  if (ruta === CAMPO_SOPORTES) return t('fichaEmpresa.campos.soporte');
  return ruta
    .split('.')
    .map((parte) => {
      const [, clave = parte, indice] = /^([^[]+)(?:\[(\d+)\])?$/.exec(parte) ?? [];
      const etiqueta = etiquetaCampo(clave, t);
      return indice === undefined ? etiqueta : `${etiqueta} ${String(Number(indice) + 1)}`;
    })
    .join(' › ');
}
