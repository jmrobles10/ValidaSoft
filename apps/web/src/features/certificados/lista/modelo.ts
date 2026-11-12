import type { FilaCertificadoDto, ListarCertificadosParams } from '@validasoft/api-client';

/**
 * Estados por los que filtra la pantalla (`GET /certificados`). `Bloqueante` no es un estado: son los certificados que la
 * consulta de certificación responde bloqueados (suspendidos, vencidos, revocados o con pendientes que bloquean). `EnVerificacion` (VR-11)
 * tampoco: son los vigentes que la empresa revisa de nuevo por una actualización (`enVerificacion=true`).
 */
export const ESTADOS_FILTRO_CERTIFICADOS = ['Vigente', 'EnVerificacion', 'ConPendientes', 'Suspendido', 'Vencido', 'Revocado', 'Bloqueante'] as const;
export type EstadoFiltroCertificados = (typeof ESTADOS_FILTRO_CERTIFICADOS)[number];

/** Espera de la búsqueda antes de escribir `q` en la URL y pedir la lista. */
export const ESPERA_BUSQUEDA_MS = 300;

export interface FiltrosCertificados {
  estado: EstadoFiltroCertificados | '';
  q: string;
}

const esEstadoFiltro = (valor: string): valor is EstadoFiltroCertificados => ESTADOS_FILTRO_CERTIFICADOS.some((e) => e === valor);

/** Filtros de la URL (`?estado=&q=`): un estado que no existe se ignora (la API respondería 422 `estado-invalido`). */
export function leerFiltros(params: URLSearchParams): FiltrosCertificados {
  const estado = params.get('estado') ?? '';
  return { estado: esEstadoFiltro(estado) ? estado : '', q: (params.get('q') ?? '').trim() };
}

/** Parámetros de la consulta: lo vacío no viaja. */
export function parametrosConsulta(filtros: FiltrosCertificados, limite: number, cursor: string | null): ListarCertificadosParams {
  return { ...parametrosDeEstado(filtros.estado), q: filtros.q || undefined, limite, cursor: cursor ?? undefined };
}

/**
 * «En verificación» es `enVerificacion=true`; cualquier otro estado excluye a los que se ven «En verificación» (`enVerificacion=false`), así «Con
 * pendientes» no repite lo que la tabla muestra «En verificación».
 */
export function parametrosDeEstado(estado: EstadoFiltroCertificados | ''): Pick<ListarCertificadosParams, 'estado' | 'enVerificacion'> {
  if (!estado) return {};
  return estado === 'EnVerificacion' ? { enVerificacion: true } : { estado, enVerificacion: false };
}

/**
 * Fecha que acompaña al chip de estado: desde cuándo está así. En un vigente no se muestra (su chip diría «Vigente
 * hasta el…» y esa fecha ya tiene su columna).
 */
export function fechaDelEstado(fila: Pick<FilaCertificadoDto, 'estado' | 'estadoDesde'>): string | null {
  return fila.estado === 'Vigente' ? null : fila.estadoDesde;
}
