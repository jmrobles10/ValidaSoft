import { useMutation, useQuery } from '@tanstack/react-query';
import {
  descargarDocumentoVinculo,
  getListarDocumentosVinculoQueryOptions,
  type DocumentoVistaEmpresaDto,
  type HechosDto,
  type VersionDocumentoDto,
} from '@validasoft/api-client';
import { conEmpresa } from '../../shared/consultas';

/** Documentos compartidos con el vínculo (solo tras la aceptación del tercero, B-1; antes la lista es vacía). */
export function useDocumentosVinculo(empresaId: string, vinculoId: string, habilitado: boolean) {
  const opciones = getListarDocumentosVinculoQueryOptions(vinculoId, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado && Boolean(vinculoId) });
}

/** Cada apertura pide una URL prefirmada nueva (5 min) y queda auditada como `DocumentoAccedido` (BO-7). */
export function usePedirDescarga(empresaId: string, vinculoId: string) {
  return useMutation({
    mutationFn: ({ documentoId, numero }: { documentoId: string; numero: number }) => descargarDocumentoVinculo(vinculoId, documentoId, numero, { empresa: empresaId }),
  });
}

export type TipoVisor = 'pdf' | 'imagen' | 'otro';

/** El visor abre PDF e imágenes; lo demás solo se descarga. */
export function tipoDeVisor(mime: string): TipoVisor {
  if (mime === 'application/pdf') return 'pdf';
  if (mime.startsWith('image/')) return 'imagen';
  return 'otro';
}

/** La versión vigente para esta empresa, o la más reciente que le compartieron. */
export function versionVisible(documento: DocumentoVistaEmpresaDto): VersionDocumentoDto | null {
  const ordenadas = [...documento.versionesCompartidas].sort((a, b) => b.n - a.n);
  return ordenadas.find((v) => v.n === documento.versionActual) ?? ordenadas[0] ?? null;
}

/** Clave de una versión de un documento (`{documentoId}:{n}`). */
export function claveVersion(documentoId: string, n: number): string {
  return `${documentoId}:${String(n)}`;
}

/** Los hechos de cada versión compartida con el vínculo, por `claveVersion`: el expediente no los trae (IA-11). */
export function hechosPorVersion(documentos: readonly DocumentoVistaEmpresaDto[]): ReadonlyMap<string, HechosDto> {
  const mapa = new Map<string, HechosDto>();
  for (const documento of documentos) {
    for (const version of documento.versionesCompartidas) {
      if (version.hechos) mapa.set(claveVersion(documento.documentoId, version.n), version.hechos);
    }
  }
  return mapa;
}

/** Hash corto para la tabla (los 12 primeros caracteres); el completo va en el título y al copiar. */
export function hashCorto(sha256: string): string {
  return sha256.length > 12 ? `${sha256.slice(0, 12)}…` : sha256;
}
