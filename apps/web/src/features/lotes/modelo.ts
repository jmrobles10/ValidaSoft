import type { EstadoLote, FilaVistaPreviaDto, LoteDto, TotalesLoteDto } from '@validasoft/api-client';

/** Tono del chip de estado del lote (colores de MUI). */
export const TONO_LOTE: Readonly<Record<EstadoLote, 'info' | 'warning' | 'success' | 'error' | 'default'>> = {
  Procesando: 'info',
  Pausado: 'warning',
  Completado: 'success',
  CompletadoConErrores: 'error',
  Cancelado: 'default',
};

/** El lote todavía avanza: se refresca solo. */
export function enCurso(lote: Pick<LoteDto, 'estado'>): boolean {
  return lote.estado === 'Procesando';
}

/** Filas que se procesan (las inválidas y repetidas se descartan al crear el lote). */
export function procesables(t: TotalesLoteDto): number {
  return t.filas - t.invalidas - t.duplicadas;
}

/** Porcentaje de avance sobre las filas procesables (0–100). */
export function porcentaje(t: TotalesLoteDto): number {
  const total = procesables(t);
  return total <= 0 ? 100 : Math.round((Math.min(t.procesadas, total) / total) * 100);
}

/** Lo que el usuario puede hacer con el lote en su estado. */
export function accionesDeLote(lote: Pick<LoteDto, 'estado' | 'totales'>): ('pausar' | 'reanudar' | 'cancelar' | 'reintentar')[] {
  switch (lote.estado) {
    case 'Procesando':
      return ['pausar', 'cancelar'];
    case 'Pausado':
      return ['reanudar', 'cancelar'];
    case 'CompletadoConErrores':
      return lote.totales.fallidas > 0 ? ['reintentar'] : [];
    default:
      return [];
  }
}

/** Las filas que no se van a invitar, para revisarlas primero. */
export function conProblemas(filas: readonly FilaVistaPreviaDto[]): FilaVistaPreviaDto[] {
  return filas.filter((f) => f.estado !== 'Valida');
}

/** El contenido del archivo en base64 (sin el prefijo `data:`), como lo recibe la API. */
export async function aBase64(archivo: Blob): Promise<string> {
  const bytes = new Uint8Array(await archivo.arrayBuffer());
  let binario = '';
  const bloque = 0x8000;
  for (let i = 0; i < bytes.length; i += bloque) binario += String.fromCharCode(...bytes.subarray(i, i + bloque));
  return btoa(binario);
}

/** Identificación legible de la fila de la vista previa («NIT 900123456»). */
export function identificacionDeFila(f: FilaVistaPreviaDto): string {
  return [f.datos.tipoIdentificacion, f.datos.numeroIdentificacion].filter(Boolean).join(' ') || '—';
}

/** `CO|NIT|900123456` → «NIT 900123456». */
export function identificacionLegible(clave: string): string {
  const partes = clave.split('|');
  return partes.length === 3 ? `${partes[1] ?? ''} ${partes[2] ?? ''}` : clave;
}
