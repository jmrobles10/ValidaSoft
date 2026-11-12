import { useMutation } from '@tanstack/react-query';
import { descargarArchivo, type ArchivoDescargado } from '@validasoft/api-client';

/** Qué declaración PEP se descarga: el tercero, la sección (`FICHA_GENERAL` o `FICHA_LEGAL`), la versión y, en la legal, la posición de la persona. */
export interface DescargaDeclaracionPep {
  empresaId: string;
  terceroId: string;
  seccion: string;
  /** Versión de la sección que se está viendo; sin ella, la vigente. */
  version: number | null;
}

/** Ruta del PDF de la declaración PEP de una persona del tercero (permiso `terceros:leer` y vínculo con la empresa). */
export const rutaDeclaracionPep = (terceroId: string) => `/api/v1/terceros/${terceroId}/declaraciones-pep/pdf`;

/** Parámetros de la consulta: la sección, la persona (solo en la legal) y la versión (si se conoce). */
export function parametrosDeclaracionPep(descarga: DescargaDeclaracionPep, persona: number | null): Record<string, string | number> {
  const params: Record<string, string | number> = { seccion: descarga.seccion };
  if (persona !== null) params.persona = persona;
  if (descarga.version !== null) params.version = descarga.version;
  return params;
}

/** Pide el PDF con la sesión y `X-Empresa` (el cliente generado tipa las descargas como JSON: se usa la descarga del cliente). */
export function useDescargarDeclaracionPep(empresaId: string) {
  return useMutation({
    mutationFn: ({ descarga, persona }: { descarga: DescargaDeclaracionPep; persona: number | null }): Promise<ArchivoDescargado> =>
      descargarArchivo({ url: rutaDeclaracionPep(descarga.terceroId), params: parametrosDeclaracionPep(descarga, persona) }, { empresa: empresaId }),
  });
}
