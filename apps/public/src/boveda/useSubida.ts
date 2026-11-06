import { useRef, useState } from 'react';
import { useAlTerminarSubida } from '../shared/api';
import { falloDeSubida, subirDocumento, validarArchivo, type DestinoCarga, type FalloSubida, type FaseSubida, type ProblemaArchivo, type ResultadoSubida } from './subida';

export type EstadoSubida =
  | { fase: 'inicio' }
  | { fase: 'problema'; problema: ProblemaArchivo }
  | { fase: 'en-curso'; archivo: File; etapa: FaseSubida; progreso: number }
  | { fase: 'resultado'; archivo: File; resultado: ResultadoSubida }
  | { fase: 'fallo'; archivo: File; fallo: FalloSubida; error: unknown };

/** Estado de una subida: valida en el navegador, sube en dos pasos y refresca la bóveda al terminar. */
export function useSubida() {
  const [estado, setEstado] = useState<EstadoSubida>({ fase: 'inicio' });
  const control = useRef<AbortController | null>(null);
  const alTerminar = useAlTerminarSubida();

  const subir = (archivo: File, destino: DestinoCarga) => {
    const problema = validarArchivo(archivo);
    if (problema) {
      setEstado({ fase: 'problema', problema });
      return;
    }
    control.current?.abort();
    const actual = new AbortController();
    control.current = actual;
    setEstado({ fase: 'en-curso', archivo, etapa: 'preparando', progreso: 0 });
    subirDocumento({
      archivo,
      destino,
      senal: actual.signal,
      alFase: (etapa) => setEstado((e) => (e.fase === 'en-curso' ? { ...e, etapa } : e)),
      alProgreso: (progreso) => setEstado((e) => (e.fase === 'en-curso' ? { ...e, progreso } : e)),
    }).then(
      (resultado) => {
        if (actual.signal.aborted) return;
        setEstado({ fase: 'resultado', archivo, resultado });
        alTerminar();
      },
      (error: unknown) => {
        if (actual.signal.aborted) return;
        setEstado({ fase: 'fallo', archivo, fallo: falloDeSubida(error), error });
      },
    );
  };

  const cancelar = () => {
    control.current?.abort();
    setEstado({ fase: 'inicio' });
  };

  return { estado, subir, cancelar, reiniciar: () => setEstado({ fase: 'inicio' }) };
}
