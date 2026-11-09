import type { DescargaDto } from '@validasoft/api-client';
import { useEffect, useState } from 'react';
import { tipoDeVisor, usePedirDescarga } from './api';

export type ContenidoDelVisor = { estado: 'cargando' } | { estado: 'listo'; url: string } | { estado: 'error'; error: unknown };

/**
 * Archivo de una versión para el visor. La URL prefirmada trae `Content-Disposition: attachment`: para verlo en línea
 * se lee el archivo y se muestra desde un `blob:` con su tipo. Con `habilitado: false` (versión de solo formulario) no
 * pide nada: su descarga respondería 409 `version-sin-archivo`.
 */
export function useArchivoDelVisor(empresaId: string, vinculoId: string, documentoId: string, numero: number, habilitado: boolean) {
  const pedir = usePedirDescarga(empresaId, vinculoId);
  const [descarga, setDescarga] = useState<DescargaDto | null>(null);
  const [contenido, setContenido] = useState<ContenidoDelVisor>({ estado: 'cargando' });
  const { mutateAsync } = pedir;

  useEffect(() => {
    if (!habilitado) return;
    // Al desmontar se cancela la lectura del archivo y se libera el `blob:`.
    const control = new AbortController();
    let objeto: string | null = null;
    // Función y no la propiedad directa: TypeScript no estrecha su valor entre los `await`.
    const abortado = () => control.signal.aborted;
    const cargar = async () => {
      const d = await mutateAsync({ documentoId, numero });
      if (abortado()) return;
      setDescarga(d);
      if (tipoDeVisor(d.mime) === 'otro') return;
      const respuesta = await fetch(d.url, { signal: control.signal });
      if (!respuesta.ok) throw new Error(`HTTP ${String(respuesta.status)}`);
      const blob = new Blob([await respuesta.arrayBuffer()], { type: d.mime });
      if (abortado()) return;
      objeto = URL.createObjectURL(blob);
      setContenido({ estado: 'listo', url: objeto });
    };
    cargar().catch((error: unknown) => {
      if (!abortado()) setContenido({ estado: 'error', error });
    });
    return () => {
      control.abort();
      if (objeto) URL.revokeObjectURL(objeto);
    };
  }, [mutateAsync, documentoId, numero, habilitado]);

  return { descarga, contenido, errorAlPedir: pedir.isError };
}
