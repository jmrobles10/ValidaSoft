import {
  Box,
  Button,
  IconButton,
  Paper,
  Skeleton,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from '@mui/material';
import { ChevronLeft, ChevronRight, Inbox } from 'lucide-react';
import type { ReactNode } from 'react';
import type { PaginacionCursor } from './usePaginacionCursor';

export interface ColumnaTabla<Fila> {
  id: string;
  encabezado: string;
  celda: (fila: Fila) => ReactNode;
  ancho?: number | string;
  alinear?: 'left' | 'right' | 'center';
}

export interface TablaConCursorProps<Fila> {
  columnas: readonly ColumnaTabla<Fila>[];
  filas: readonly Fila[] | undefined;
  claveFila: (fila: Fila) => string;
  cargando: boolean;
  /** Mensaje de error recuperable; con `onReintentar` muestra el botón. */
  error?: string | null;
  onReintentar?: () => void;
  paginacion: PaginacionCursor;
  /** `siguienteCursor` de la última respuesta; `null` = no hay más. */
  siguienteCursor: string | null | undefined;
  onFila?: (fila: Fila) => void;
  /** Texto accesible de la fila para lectores de pantalla ("Abrir vínculo de Aceros del Norte"). */
  etiquetaFila?: (fila: Fila) => string;
  vacio?: ReactNode;
  etiqueta: string;
  filasEsqueleto?: number;
}

/** Tabla de bandeja con paginación por cursor, esqueleto de carga, vacío con ícono y error con reintento (docs/06 §2.3). */
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
  filasEsqueleto = 5,
}: TablaConCursorProps<Fila>) {
  const sinFilas = !cargando && !error && (filas?.length ?? 0) === 0;

  return (
    <Paper variant="outlined" sx={{ borderRadius: 3, overflow: 'hidden' }}>
      <TableContainer>
        <Table size="small" aria-label={etiqueta} aria-busy={cargando}>
          <TableHead>
            <TableRow>
              {columnas.map((c) => (
                <TableCell key={c.id} align={c.alinear} sx={{ width: c.ancho }}>
                  {c.encabezado}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            {cargando &&
              Array.from({ length: filasEsqueleto }, (_, i) => (
                <TableRow key={`esqueleto-${String(i)}`}>
                  {columnas.map((c) => (
                    <TableCell key={c.id}>
                      <Skeleton variant="text" />
                    </TableCell>
                  ))}
                </TableRow>
              ))}
            {!cargando &&
              !error &&
              filas?.map((fila) => (
                <TableRow
                  key={claveFila(fila)}
                  hover={Boolean(onFila)}
                  tabIndex={onFila ? 0 : undefined}
                  aria-label={etiquetaFila?.(fila)}
                  onClick={onFila ? () => onFila(fila) : undefined}
                  onKeyDown={
                    onFila
                      ? (evento) => {
                          if (evento.key === 'Enter' || evento.key === ' ') {
                            evento.preventDefault();
                            onFila(fila);
                          }
                        }
                      : undefined
                  }
                  sx={{ cursor: onFila ? 'pointer' : 'default' }}
                >
                  {columnas.map((c) => (
                    <TableCell key={c.id} align={c.alinear}>
                      {c.celda(fila)}
                    </TableCell>
                  ))}
                </TableRow>
              ))}
          </TableBody>
        </Table>
      </TableContainer>

      {error && (
        <Stack direction="row" spacing={2} alignItems="center" sx={{ p: 2 }} role="alert">
          <Typography variant="body2" color="error">
            {error}
          </Typography>
          {onReintentar && (
            <Button size="small" onClick={onReintentar}>
              Reintentar
            </Button>
          )}
        </Stack>
      )}

      {sinFilas && (
        <Stack alignItems="center" spacing={1} sx={{ py: 5, color: 'text.secondary' }}>
          <Inbox size={28} strokeWidth={1.5} aria-hidden />
          <Typography variant="body2">{vacio}</Typography>
        </Stack>
      )}

      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: 1, px: 2, py: 1, borderTop: 1, borderColor: 'divider' }}>
        <Typography variant="caption" color="text.secondary">
          Página {paginacion.pagina}
        </Typography>
        <Tooltip title="Página anterior">
          <span>
            <IconButton size="small" aria-label="Página anterior" disabled={!paginacion.hayAnterior || cargando} onClick={paginacion.irAAnterior}>
              <ChevronLeft size={18} />
            </IconButton>
          </span>
        </Tooltip>
        <Tooltip title="Página siguiente">
          <span>
            <IconButton
              size="small"
              aria-label="Página siguiente"
              disabled={!siguienteCursor || cargando}
              onClick={() => {
                if (siguienteCursor) paginacion.irASiguiente(siguienteCursor);
              }}
            >
              <ChevronRight size={18} />
            </IconButton>
          </span>
        </Tooltip>
      </Box>
    </Paper>
  );
}
