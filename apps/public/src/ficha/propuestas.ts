import type { PropuestaDeSeccionDto, SoporteSeccionDto } from '@validasoft/api-client';

/**
 * Actualizar desde el documento (F5, FM-14): lo que la versión vigente de un documento (RUT, Cámara…) propone para una sección.
 * La API ya trae los campos distintos y la sección con esos cambios; aquí solo se ordena para mostrarlo. Nada se guarda solo.
 */

/** Cómo se nombra el documento en «Actualizar desde mi …»; los demás usan su nombre. */
const NOMBRES_CORTOS: Readonly<Record<string, string>> = {
  RUT: 'RUT',
  CAMARA_COMERCIO: 'Cámara de Comercio',
  ESTADOS_FINANCIEROS: 'estados financieros',
  CEDULA: 'cédula',
};

export function nombreCortoDelDocumento(propuesta: Pick<PropuestaDeSeccionDto, 'documento'>): string {
  return NOMBRES_CORTOS[propuesta.documento.tipoDocumento.toUpperCase()] ?? propuesta.documento.nombreDocumento;
}

/** Los datos de la sección ya con lo propuesto (snake_case), listos para prellenar el formulario; nulo si la propuesta no los trae. */
export function datosDePropuesta(propuesta: Pick<PropuestaDeSeccionDto, 'datos'>): Record<string, unknown> | null {
  const datos = propuesta.datos;
  return datos !== null && typeof datos === 'object' && !Array.isArray(datos) ? (datos as Record<string, unknown>) : null;
}

/** El documento que se leyó queda como soporte de la versión que el tercero guarde (la API pone el tipo). */
export function soporteDePropuesta(propuesta: Pick<PropuestaDeSeccionDto, 'documento'>): SoporteSeccionDto {
  return { documentoId: propuesta.documento.documentoId, version: propuesta.documento.version };
}

export const mismoSoporte = (a: Pick<SoporteSeccionDto, 'documentoId' | 'version'>, b: Pick<SoporteSeccionDto, 'documentoId' | 'version'>) =>
  a.documentoId === b.documentoId && a.version === b.version;

/** Clave estable de una propuesta (documento, versión, sección y periodo). */
export const clavePropuesta = (p: PropuestaDeSeccionDto) => `${p.documento.documentoId}-${String(p.documento.version)}-${p.seccion}-${String(p.periodo ?? '')}`;

/** Las propuestas de un mismo documento (y versión): un aviso por documento, con las secciones que afecta. */
export interface PropuestasDeDocumento {
  clave: string;
  /** Cómo se nombra el documento («RUT», «Cámara de Comercio»). */
  documento: string;
  version: number;
  propuestas: PropuestaDeSeccionDto[];
  /** Datos distintos en total (suma de las secciones que sí se pudieron leer). */
  datosDistintos: number;
  /** Alguna sección no pudo leerse automáticamente. */
  hayNoLeidas: boolean;
}

/** Agrupa por documento y versión conservando el orden de la API (la primera aparición manda). */
export function agruparPorDocumento(propuestas: readonly PropuestaDeSeccionDto[]): PropuestasDeDocumento[] {
  const grupos = new Map<string, PropuestasDeDocumento>();
  for (const propuesta of propuestas) {
    const clave = `${propuesta.documento.documentoId}-${String(propuesta.documento.version)}`;
    const grupo = grupos.get(clave) ?? { clave, documento: nombreCortoDelDocumento(propuesta), version: propuesta.documento.version, propuestas: [], datosDistintos: 0, hayNoLeidas: false };
    grupo.propuestas.push(propuesta);
    if (propuesta.disponible) grupo.datosDistintos += propuesta.campos.length;
    else grupo.hayNoLeidas = true;
    grupos.set(clave, grupo);
  }
  return [...grupos.values()];
}
