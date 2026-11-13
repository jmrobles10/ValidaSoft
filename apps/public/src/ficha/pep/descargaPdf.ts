import { useMutation } from '@tanstack/react-query';
import { descargarArchivo, type ArchivoDescargado } from '@validasoft/api-client';
import { createContext } from 'react';

/**
 * ¿Lo que hay en pantalla es lo que está guardado? El PDF de la declaración sale de la versión guardada de la sección (la vigente), así que
 * solo se ofrece cuando no hay cambios sin guardar ni valores prellenados por un documento: si no, el PDF diría otra cosa que la pantalla.
 * Lo provee `EditorSeccion`.
 */
export const DescargaDeDeclaracionDisponible = createContext(false);

/** Ruta del PDF de la propia declaración PEP del tercero (`FICHA_GENERAL`, o `FICHA_LEGAL` con la posición de la persona). */
export const rutaPdfDeDeclaracion = (seccion: string) => `/public/v1/ficha/${seccion}/declaracion-pep/pdf`;

/** Pide el PDF con la sesión del tercero (el cliente generado tipa las descargas como JSON: se usa la descarga del cliente). */
export function useDescargarDeclaracionPropia(seccion: string, persona: number | null) {
  return useMutation({
    mutationFn: (): Promise<ArchivoDescargado> => descargarArchivo({ url: rutaPdfDeDeclaracion(seccion), params: persona === null ? undefined : { persona } }),
  });
}
