import { useEffect, useRef, useState } from 'react';
import { useAlTerminarSubida } from '../shared/api';
import {
  falloDeSubida,
  subirDocumento,
  validarArchivo,
  type DestinoCarga,
  type FalloSubida,
  type FaseSubida,
  type ProblemaArchivo,
  type ReglasArchivo,
  type ResultadoSubida,
} from './subida';

/** Reglas del tipo (para validar en el navegador) y campos de un `ArchivoYFormulario` (van en el paso 1). */
export interface ExtrasSubida {
  reglas?: ReglasArchivo;
  datos?: Record<string, string | boolean> | null;
  /** Para marcar en el formulario el campo de un 422 (`campo-requerido`, `valor-invalido`…). */
  alFallar?: (error: unknown) => void;
}

export type EstadoSubida =
  | { fase: 'inicio' }
  | { fase: 'problema'; problema: ProblemaArchivo }
  | { fase: 'en-curso'; archivo: File; etapa: FaseSubida; progreso: number }
  | { fase: 'resultado'; archivo: File; resultado: ResultadoSubida }
  | { fase: 'fallo'; archivo: File; fallo: FalloSubida; error: unknown };

/**
 * Estado de una subida: valida en el navegador, sube en dos pasos y refresca la bóveda al terminar. Solo la última subida manda
 * sobre la pantalla: al empezar otra (aunque el archivo nuevo no pase la validación), al cancelar o al desmontar, la anterior se
 * aborta y, si aun así termina, su resultado no se muestra.
 */
export function useSubida() {
  const [estado, setEstado] = useState<EstadoSubida>({ fase: 'inicio' });
  const control = useRef<AbortController | null>(null);
  const alTerminar = useAlTerminarSubida();

  // Al salir de la pantalla no queda una subida viva escribiendo en un estado que ya nadie ve.
  useEffect(
    () => () => {
      control.current?.abort();
      control.current = null;
    },
    [],
  );

  const subir = (archivo: File, destino: DestinoCarga, { reglas, datos, alFallar }: ExtrasSubida = {}) => {
    control.current?.abort();
    control.current = null;
    const problema = validarArchivo(archivo, reglas);
    if (problema) {
      setEstado({ fase: 'problema', problema });
      return;
    }
    const actual = new AbortController();
    control.current = actual;
    // Función y no la propiedad directa: TypeScript no estrecha su valor entre los `await` ni entre las llamadas.
    const reemplazada = () => actual.signal.aborted;
    setEstado({ fase: 'en-curso', archivo, etapa: 'preparando', progreso: 0 });
    subirDocumento({
      archivo,
      destino,
      datos,
      senal: actual.signal,
      alFase: (etapa) => {
        if (!reemplazada()) setEstado((e) => (e.fase === 'en-curso' ? { ...e, etapa } : e));
      },
      alProgreso: (progreso) => {
        if (!reemplazada()) setEstado((e) => (e.fase === 'en-curso' ? { ...e, progreso } : e));
      },
    }).then(
      (resultado) => {
        // La versión quedó creada en el servidor aunque la pantalla ya no la muestre: la bóveda se vuelve a pedir igual.
        if (!reemplazada()) setEstado({ fase: 'resultado', archivo, resultado });
        alTerminar();
      },
      (error: unknown) => {
        if (reemplazada()) return;
        setEstado({ fase: 'fallo', archivo, fallo: falloDeSubida(error), error });
        alFallar?.(error);
      },
    );
  };

  const cancelar = () => {
    control.current?.abort();
    control.current = null;
    setEstado({ fase: 'inicio' });
  };

  return { estado, subir, cancelar, reiniciar: () => setEstado({ fase: 'inicio' }) };
}
