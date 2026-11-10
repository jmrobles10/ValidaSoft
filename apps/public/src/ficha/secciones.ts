import type { DocumentoBovedaDto, EstadoFichaDto, EstadoSeccionFichaDto, FaltanteSeccionDto } from '@validasoft/api-client';

/** Canónico con el que el tercero sube un soporte ad hoc de cualquier sección (FM-20). */
export const SOPORTE_FICHA = 'SOPORTE_FICHA';

/** Regla del faltante «Falta el soporte del cambio» (FM-20): se muestra junto al selector de soporte. */
export const REGLA_SOPORTE = 'soporte-del-cambio';

export type CodigoSeccion = 'FICHA_GENERAL' | 'FICHA_TRIBUTARIA' | 'FICHA_LEGAL' | 'FICHA_FINANCIERA' | 'FICHA_SST' | 'FICHA_ESPECIALIDADES';

export interface DefinicionSeccion {
  codigo: CodigoSeccion;
  /** Segmento de la ruta del wizard (`/ficha/general`). */
  ruta: string;
  porPeriodo: boolean;
  /** Tipos de documento que la respaldan (mapa de docs/14 §5.1); `null` = cualquier documento global (opcional). */
  soportes: readonly string[] | null;
}

/** Las seis secciones en el orden del wizard (FM-17); el orden que se muestra es el que devuelve la API. */
export const SECCIONES: readonly DefinicionSeccion[] = [
  { codigo: 'FICHA_GENERAL', ruta: 'general', porPeriodo: false, soportes: ['RUT', 'CAMARA_COMERCIO', 'CEDULA', SOPORTE_FICHA] },
  { codigo: 'FICHA_TRIBUTARIA', ruta: 'tributaria', porPeriodo: false, soportes: ['RUT', SOPORTE_FICHA] },
  { codigo: 'FICHA_LEGAL', ruta: 'legal', porPeriodo: false, soportes: ['CAMARA_COMERCIO', 'RUT', 'CEDULA', SOPORTE_FICHA] },
  { codigo: 'FICHA_FINANCIERA', ruta: 'financiera', porPeriodo: true, soportes: ['ESTADOS_FINANCIEROS', SOPORTE_FICHA] },
  { codigo: 'FICHA_SST', ruta: 'sst', porPeriodo: false, soportes: null },
  { codigo: 'FICHA_ESPECIALIDADES', ruta: 'especialidades', porPeriodo: false, soportes: null },
];

export const seccionPorCodigo = (codigo: string | null | undefined) => SECCIONES.find((s) => s.codigo === codigo?.toUpperCase()) ?? null;

export const seccionPorRuta = (ruta: string | undefined) => SECCIONES.find((s) => s.ruta === ruta) ?? null;

/** Ruta del formulario de una sección; la financiera va con su periodo si se conoce. */
export function enlaceSeccion(codigo: string, periodo?: number | null): string {
  const seccion = seccionPorCodigo(codigo);
  if (!seccion) return '/ficha';
  return seccion.porPeriodo && periodo ? `/ficha/${seccion.ruta}?periodo=${String(periodo)}` : `/ficha/${seccion.ruta}`;
}

/** Filas del resumen: solo las secciones del orden de la API (la persona natural no ve Legal ni Financiera). */
export function filasDelResumen(estado: EstadoFichaDto): EstadoSeccionFichaDto[] {
  return estado.orden.flatMap((codigo) => estado.secciones.filter((s) => s.seccion === codigo));
}

export type EstadoDeSeccion = 'completa' | 'incompleta' | 'sin-diligenciar';

export const estadoDeSeccion = (fila: Pick<EstadoSeccionFichaDto, 'version' | 'completa'>): EstadoDeSeccion =>
  fila.version === null ? 'sin-diligenciar' : fila.completa ? 'completa' : 'incompleta';

/**
 * Periodo de la financiera al abrirla sin `?periodo=`: el más reciente diligenciado o, si no hay, el último cierre
 * (el año anterior).
 */
export function periodoPorOmision(estado: EstadoFichaDto | undefined, hoy = new Date()): number {
  const periodos = (estado?.secciones ?? []).filter((s) => s.seccion === 'FICHA_FINANCIERA' && s.periodo !== null).map((s) => s.periodo ?? 0);
  return periodos.length > 0 ? Math.max(...periodos) : hoy.getFullYear() - 1;
}

/** Años que se pueden elegir en la financiera: del actual hacia atrás (la API admite desde 1990). */
export function periodosElegibles(actual: number, hoy = new Date()): number[] {
  const anio = hoy.getFullYear();
  const anios = Array.from({ length: 10 }, (_, i) => anio - i);
  return actual >= 1990 && !anios.includes(actual) ? [...anios, actual] : anios;
}

/** Documentos de la bóveda que pueden respaldar la sección: globales, del tipo que admite y con versión vigente. */
export function documentosDeSoporte(documentos: readonly DocumentoBovedaDto[], seccion: DefinicionSeccion): DocumentoBovedaDto[] {
  return documentos.filter(
    (d) =>
      d.ambito === 'Global' &&
      d.versionActual !== null &&
      (d.versionActual.estado === 'Actual' || d.versionActual.estado === 'Superada') &&
      (seccion.soportes === null || seccion.soportes.includes(d.tipo.codigo)),
  );
}

/** Parte de primer nivel de una ruta de campo: `personas[0].pep` ⇒ `personas`. */
export const claveDeRuta = (ruta: string) => ruta.split(/[.[]/)[0] ?? ruta;

export const esFaltanteDeSoporte = (f: FaltanteSeccionDto) => f.regla === REGLA_SOPORTE;
