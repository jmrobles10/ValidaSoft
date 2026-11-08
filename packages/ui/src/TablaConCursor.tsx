import { Box, Skeleton, Table, TableBody, TableCell, TableContainer, TableHead, TableRow } from '@mui/material';
import type { ReactNode } from 'react';
import { visualmenteOculto } from './accesibilidad';
import { PaginacionCursor } from './PaginacionCursor';
import { ErrorEnLinea, EstadoVacio } from './patrones';
import type { EstadoPaginacionCursor as EstadoPaginacion } from './usePaginacionCursor';

export interface ColumnaTabla<Fila> {
  id: string;
  encabezado: string;
  celda: (fila: Fila) => ReactNode;
  ancho?: number | string;
  alinear?: 'left' | 'right' | 'center';
  /** El encabezado solo para lectores de pantalla (columna de acciones). */
  encabezadoOculto?: boolean;
}

export interface TablaConCursorProps<Fila> {
  columnas: readonly ColumnaTabla<Fila>[];
  filas: readonly Fila[] | undefined;
  claveFila: (fila: Fila) => string;
  cargando: boolean;
  /** Mensaje de error recuperable; con `onReintentar` muestra el botón. */
  error?: string | null;
  onReintentar?: () => void;
  paginacion: EstadoPaginacion;
  /** `siguienteCursor` de la última respuesta; `null` = no hay más. */
  siguienteCursor: string | null | undefined;
  onFila?: (fila: Fila) => void;
  /** Texto accesible de la fila ("Abrir vínculo de Aceros del Norte"). */
  etiquetaFila?: (fila: Fila) => string;
  /** Vacío: un texto se muestra con `EstadoVacio`; un nodo se pinta tal cual. */
  vacio?: ReactNode;
  etiqueta: string;
  filasEsqueleto?: number;
  /** Alto de fila: 36 px (compacta) o 48 px cuando la celda lleva dos líneas. */
  filasAltas?: boolean;
}

/** Marco de tabla densa (borde sutil, radio 12, sin bordes verticales). Úsalo también en tablas sin paginación. */
export function MarcoTabla({ children }: { children: ReactNode }) {
  return (
    <Box sx={{ position: 'relative', overflow: 'hidden', border: 1, borderColor: 'divider', borderRadius: (t) => `${String(t.radio.md)}px`, bgcolor: 'background.paper' }}>
      {children}
    </Box>
  );
}

/** Tabla de bandeja con paginación por cursor, esqueleto con la forma de las filas, vacío y error con reintento. */
export function TablaConCursor<Fila>({
  columnas,
  filas,
  claveFila,
  cargando,
  error,
  onReintentar,
  paginacion,
  siguienteCursor,
  onFila,
  etiquetaFila,
  vacio = 'No hay resultados.',
  etiqueta,
  filasEsqueleto = 6,
  filasAltas = false,
}: TablaConCursorProps<Fila>) {
  const sinFilas = !cargando && !error && (filas?.length ?? 0) === 0;
  const alto = filasAltas ? 48 : undefined;

  return (
    <>
      {error && (
        <Box sx={{ mb: 1.5 }} role="alert">
          <ErrorEnLinea mensaje={error} onReintentar={onReintentar} />
        </Box>
      )}
      <MarcoTabla>
        <TableContainer>
          <Table size="small" aria-label={etiqueta} aria-busy={cargando}>
            <TableHead>
              <TableRow>
                {columnas.map((c) => (
                  <TableCell key={c.id} align={c.alinear} sx={{ width: c.ancho, whiteSpace: 'nowrap' }}>
                    {c.encabezadoOculto ? <Box component="span" sx={visualmenteOculto}>{c.encabezado}</Box> : c.encabezado}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {cargando &&
                Array.from({ length: filasEsqueleto }, (_, i) => (
                  <TableRow key={`esqueleto-${String(i)}`} sx={{ '&:hover': { bgcolor: 'transparent' } }}>
                    {columnas.map((c, j) => (
                      <TableCell key={c.id} sx={{ height: alto }}>
                        <Skeleton variant="rounded" height={12} width={c.encabezadoOculto ? 20 : 40 + ((i * 7 + j * 13) % 5) * 14} />
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              {!cargando &&
                !error &&
                filas?.map((fila) => (
                  <TableRow
                    key={claveFila(fila)}
                    tabIndex={onFila ? 0 : undefined}
                    aria-label={etiquetaFila?.(fila)}
                    onClick={onFila ? () => onFila(fila) : undefined}
                    onKeyDown={
                      onFila
                        ? (evento) => {
                            if (evento.target === evento.currentTarget && (evento.key === 'Enter' || evento.key === ' ')) {
                              evento.preventDefault();
                              onFila(fila);
                            }
                          }
                        : undefined
                    }
                    sx={{ cursor: onFila ? 'pointer' : 'default', '&:focus-visible': { outlineOffset: -2 } }}
                  >
                    {columnas.map((c) => (
                      <TableCell key={c.id} align={c.alinear} sx={{ height: alto }}>
                        {c.celda(fila)}
                      </TableCell>
                    ))}
                  </TableRow>
                ))}
              {sinFilas && (
                <TableRow sx={{ '&:hover': { bgcolor: 'transparent' } }}>
                  <TableCell colSpan={columnas.length} sx={{ borderBottom: 0, height: 'auto' }}>
                    {typeof vacio === 'string' ? <EstadoVacio compacto titulo={vacio} /> : vacio}
                  </TableCell>
                </TableRow>
              )}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>
      {(paginacion.hayAnterior || Boolean(siguienteCursor)) && (
        <PaginacionCursor
          pagina={paginacion.pagina}
          hayAnterior={paginacion.hayAnterior}
          haySiguiente={Boolean(siguienteCursor)}
          cargando={cargando}
          onAnterior={paginacion.irAAnterior}
          onSiguiente={() => {
            if (siguienteCursor) paginacion.irASiguiente(siguienteCursor);
          }}
        />
      )}
    </>
  );
}

/** Filas de esqueleto con la forma de la tabla (para tablas sin paginación). */
export function FilasEsqueleto({ columnas, filas = 4, alto }: { columnas: number; filas?: number; alto?: number }) {
  return (
    <>
      {Array.from({ length: filas }, (_, i) => (
        <TableRow key={`esqueleto-${String(i)}`} sx={{ '&:hover': { bgcolor: 'transparent' } }}>
          {Array.from({ length: columnas }, (_, j) => (
            <TableCell key={j} sx={{ height: alto }}>
              <Skeleton variant="rounded" height={12} width={j === columnas - 1 ? 20 : 40 + ((i * 7 + j * 13) % 5) * 14} />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}
