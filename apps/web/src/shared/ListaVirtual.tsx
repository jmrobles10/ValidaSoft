import { Box, Button, Typography } from '@mui/material';
import { useVirtualizer, useWindowVirtualizer, type VirtualItem } from '@tanstack/react-virtual';
import { EsqueletoTarjeta } from '@validasoft/ui';
import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode, type RefObject } from 'react';
import { contenedorConScroll, useAlturaFija } from '../sesion/alturaFija';

/** Filas de más que se dibujan arriba y abajo de lo visible, para que al desplazar no se vea el hueco. */
const FILAS_DE_MARGEN = 4;
/** Cuántas filas antes del final se pide la siguiente página. */
const FILAS_ANTES_DEL_FINAL = 3;

export interface ListaVirtualProps<T> {
  filas: readonly T[];
  /** Clave única de la fila. Debe tener identidad estable (una función del módulo o con `useCallback`). */
  claveFila: (fila: T) => string;
  /** Alto aproximado de una fila (px) mientras no se mide; después cada fila se mide sola y puede cambiar de alto. */
  altoEstimado: number;
  /** Espacio entre filas (px). */
  separacion?: number;
  /** Nombre accesible de la lista. */
  etiqueta: string;
  renderFila: (fila: T) => ReactNode;
  /** Hay otra página en el servidor: al acercarse al final se pide sola con `alCargarMas`. */
  hayMas: boolean;
  cargandoMas: boolean;
  /** La última página pedida falló: no se insiste sola, se ofrece reintentar. */
  errorAlCargarMas?: boolean;
  alCargarMas: () => void;
  /** Nombre accesible del pie mientras llega la siguiente página (no se ve: lo que se ve es el esqueleto). */
  textoCargandoMas: string;
  /** Esqueleto de la fila que viene (por omisión, una tarjeta genérica). */
  esqueletoCargandoMas?: ReactNode;
  textoErrorMas: string;
  textoReintentar: string;
}

/** Lo que sale de un virtualizador, sea de la ventana o de un contenedor con scroll propio. */
interface Ventana {
  items: VirtualItem[];
  tamanoTotal: number;
  medir: (nodo: Element | null) => void;
}

type CuerpoProps<T> = ListaVirtualProps<T> & { cantidad: number; claveDe: (indice: number) => string };

/**
 * Lista larga que solo dibuja las filas visibles (más unas pocas de margen) y pide la siguiente página del servidor al
 * acercarse al final: ni un paginador ni un botón «Cargar más». Cada fila se mide sola, así que pueden tener alto
 * variable (una tarjeta con tabla, que se expande). Desplaza la ventana o, embebida con altura fija (ver
 * `sesion/alturaFija.ts`), el `main` de la app.
 */
export function ListaVirtual<T>(propiedades: ListaVirtualProps<T>) {
  const { filas, claveFila, hayMas } = propiedades;
  const marco = useRef<HTMLDivElement>(null);
  const alturaFija = useAlturaFija((s) => s.altura);
  const [raiz, fijarRaiz] = useState<HTMLElement | null>(null);
  useLayoutEffect(() => {
    // `alturaFija` es lo que decide si desplaza la ventana o `main`: al cambiar, se vuelve a buscar el contenedor.
    fijarRaiz(alturaFija === null ? null : contenedorConScroll(marco.current));
  }, [alturaFija]);

  // Estable mientras no cambien las filas: el virtualizador recalcula todas las medidas cuando cambia la función de claves.
  const claveDe = useCallback(
    (indice: number) => {
      const fila = filas[indice];
      return fila === undefined ? '__pie' : claveFila(fila);
    },
    [filas, claveFila],
  );
  // Una fila más al final, mientras haya otra página, para el aviso de carga (y para que ver el final la pida).
  const cuerpo: CuerpoProps<T> = { ...propiedades, cantidad: filas.length + (hayMas ? 1 : 0), claveDe };
  return (
    <Box ref={marco}>
      {raiz ? <ListaEnContenedor raiz={raiz} cuerpo={cuerpo} /> : <ListaEnVentana cuerpo={cuerpo} />}
    </Box>
  );
}

function ListaEnVentana<T>({ cuerpo }: { cuerpo: CuerpoProps<T> }) {
  const lista = useRef<HTMLDivElement>(null);
  const margen = useMargenSuperior(lista, null);
  const virtualizador = useWindowVirtualizer({
    count: cuerpo.cantidad,
    estimateSize: () => cuerpo.altoEstimado,
    gap: cuerpo.separacion,
    overscan: FILAS_DE_MARGEN,
    scrollMargin: margen,
    getItemKey: cuerpo.claveDe,
  });
  return (
    <CuerpoDeLista
      cuerpo={cuerpo}
      lista={lista}
      margen={margen}
      ventana={{ items: virtualizador.getVirtualItems(), tamanoTotal: virtualizador.getTotalSize(), medir: virtualizador.measureElement }}
    />
  );
}

function ListaEnContenedor<T>({ raiz, cuerpo }: { raiz: HTMLElement; cuerpo: CuerpoProps<T> }) {
  const lista = useRef<HTMLDivElement>(null);
  const margen = useMargenSuperior(lista, raiz);
  const virtualizador = useVirtualizer<HTMLElement, Element>({
    count: cuerpo.cantidad,
    getScrollElement: () => raiz,
    estimateSize: () => cuerpo.altoEstimado,
    gap: cuerpo.separacion,
    overscan: FILAS_DE_MARGEN,
    scrollMargin: margen,
    getItemKey: cuerpo.claveDe,
  });
  return (
    <CuerpoDeLista
      cuerpo={cuerpo}
      lista={lista}
      margen={margen}
      ventana={{ items: virtualizador.getVirtualItems(), tamanoTotal: virtualizador.getTotalSize(), medir: virtualizador.measureElement }}
    />
  );
}

/**
 * Distancia (px) del borde superior de la lista al inicio del área que desplaza (la página o `raiz`): lo que hay encima
 * —encabezado, buscador— que el virtualizador debe descontar. Se vuelve a medir en cada dibujo y al cambiar la ventana.
 */
function useMargenSuperior(lista: RefObject<HTMLElement | null>, raiz: HTMLElement | null): number {
  const [margen, fijarMargen] = useState(0);
  const medir = useCallback(() => {
    const caja = lista.current?.getBoundingClientRect().top;
    if (caja === undefined) return;
    fijarMargen(Math.round(raiz ? caja - raiz.getBoundingClientRect().top + raiz.scrollTop : caja + window.scrollY));
  }, [lista, raiz]);
  useLayoutEffect(medir);
  useEffect(() => {
    window.addEventListener('resize', medir);
    return () => window.removeEventListener('resize', medir);
  }, [medir]);
  return margen;
}

function CuerpoDeLista<T>({ cuerpo, lista, margen, ventana }: { cuerpo: CuerpoProps<T>; lista: RefObject<HTMLDivElement | null>; margen: number; ventana: Ventana }) {
  const { etiqueta, filas, renderFila, hayMas, cargandoMas, errorAlCargarMas = false, alCargarMas, textoCargandoMas, esqueletoCargandoMas, textoErrorMas, textoReintentar } = cuerpo;
  const ultimaVisible = ventana.items.at(-1)?.index ?? -1;
  useEffect(() => {
    if (hayMas && !cargandoMas && !errorAlCargarMas && ultimaVisible >= filas.length - FILAS_ANTES_DEL_FINAL) alCargarMas();
  }, [hayMas, cargandoMas, errorAlCargarMas, ultimaVisible, filas.length, alCargarMas]);

  return (
    <Box ref={lista} role="list" aria-label={etiqueta} style={{ height: ventana.tamanoTotal }} sx={{ position: 'relative', width: '100%' }}>
      {ventana.items.map((item) => {
        const fila = filas[item.index];
        return (
          <Box
            key={item.key}
            ref={ventana.medir}
            data-index={item.index}
            role="listitem"
            aria-setsize={hayMas ? -1 : filas.length}
            aria-posinset={fila === undefined ? undefined : item.index + 1}
            style={{ transform: `translateY(${String(item.start - margen)}px)` }}
            sx={{ position: 'absolute', top: 0, left: 0, width: '100%' }}
          >
            {fila === undefined ? (
              <PieDeCarga error={errorAlCargarMas} texto={textoCargandoMas} esqueleto={esqueletoCargandoMas} textoError={textoErrorMas} textoReintentar={textoReintentar} onReintentar={alCargarMas} />
            ) : (
              renderFila(fila)
            )}
          </Box>
        );
      })}
    </Box>
  );
}

interface PropsPie {
  error: boolean;
  texto: string;
  esqueleto?: ReactNode;
  textoError: string;
  textoReintentar: string;
  onReintentar: () => void;
}

/** Pie de la lista: el esqueleto de la fila que viene mientras carga (nunca un spinner con texto) o el error con su reintento. */
function PieDeCarga({ error, texto, esqueleto, textoError, textoReintentar, onReintentar }: PropsPie) {
  if (error) {
    return (
      <Box role="alert" sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: 1.5, py: 2, color: 'text.secondary' }}>
        <Typography variant="body2">{textoError}</Typography>
        <Button size="small" variant="outlined" onClick={onReintentar}>
          {textoReintentar}
        </Button>
      </Box>
    );
  }
  return (
    <Box role="status" aria-busy="true" aria-label={texto}>
      {esqueleto ?? <EsqueletoTarjeta lineas={2} />}
    </Box>
  );
}
