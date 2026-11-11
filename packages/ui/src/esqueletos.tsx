import { Box, Skeleton, Stack, Table, TableBody, TableCell, TableHead, TableRow, useTheme, type SxProps, type Theme } from '@mui/material';
import type { ReactNode } from 'react';

/**
 * Esqueletos de carga con la forma real del contenido (UI-1): mientras algo carga, la pantalla muestra bloques grises con
 * la silueta de lo que va a llegar —filas de tabla, tarjetas, campos de un formulario— y nunca el texto «Cargando…» ni un
 * spinner de página o de sección (un spinner pequeño solo cabe dentro de un botón que está guardando). Todos son una región
 * `status` ocupada con un nombre accesible (no visible), para que un lector de pantalla sepa qué pasa.
 */

const ETIQUETA = 'Cargando';
const radioMarco = (t: Theme) => `${String(t.radio.md)}px`;

interface Comun {
  /** Nombre accesible de la región (no se ve). Por omisión «Cargando». */
  etiqueta?: string;
  sx?: SxProps<Theme>;
}

/** Región ocupada que envuelve un esqueleto. */
function Region({ etiqueta = ETIQUETA, sx, children }: Comun & { children: ReactNode }) {
  return (
    <Box role="status" aria-busy="true" aria-label={etiqueta} sx={sx}>
      {children}
    </Box>
  );
}

/** Ancho de una línea de texto: varía con la posición para que no parezca una rejilla, sin azar (estable entre dibujos). */
const anchoDeLinea = (indice: number, base = 40, paso = 14) => `${String(base + ((indice * 7 + 3) % 5) * paso)}%`;

/** Líneas de texto de un párrafo. La última es más corta. */
export function LineasEsqueleto({ lineas = 3, alto = 12 }: { lineas?: number; alto?: number }) {
  return (
    <Stack spacing={1}>
      {Array.from({ length: lineas }, (_, i) => (
        <Skeleton key={i} variant="rounded" height={alto} width={i === lineas - 1 && lineas > 1 ? '55%' : '100%'} />
      ))}
    </Stack>
  );
}

/** Cabecera de una página: título y subtítulo. */
export function CabeceraEsqueleto() {
  return (
    <Stack spacing={1} sx={{ mb: 3 }}>
      <Skeleton variant="rounded" width={280} height={26} sx={{ maxWidth: '70%' }} />
      <Skeleton variant="rounded" width={420} height={14} sx={{ maxWidth: '90%' }} />
    </Stack>
  );
}

/** Una tarjeta: borde y radio de las tarjetas reales, un título y varias líneas (`alto` fija el alto mínimo). */
export function EsqueletoTarjeta({ lineas = 3, alto }: { lineas?: number; alto?: number }) {
  return (
    <Box sx={{ py: 1.5, px: 2, border: 1, borderColor: 'divider', borderRadius: radioMarco, bgcolor: 'background.paper', minHeight: alto }}>
      <Skeleton variant="rounded" width={180} height={16} sx={{ mb: 1.5, maxWidth: '60%' }} />
      <Stack spacing={1.25}>
        {Array.from({ length: lineas }, (_, i) => (
          <Skeleton key={i} variant="rounded" height={12} width={anchoDeLinea(i, 50, 12)} />
        ))}
      </Stack>
    </Box>
  );
}

/** Tarjetas apiladas (listas de documentos, empresas, requisitos…). */
export function EsqueletoTarjetas({ cantidad = 2, lineas = 2, alto, etiqueta, sx }: Comun & { cantidad?: number; lineas?: number; alto?: number }) {
  return (
    <Region etiqueta={etiqueta} sx={sx}>
      <Stack spacing={1.5}>
        {Array.from({ length: cantidad }, (_, i) => (
          <EsqueletoTarjeta key={i} lineas={lineas} alto={alto} />
        ))}
      </Stack>
    </Region>
  );
}

/**
 * Los campos de un formulario: etiqueta corta encima y control de 32 px (web) o 40 px (tercero), en una o dos columnas.
 * Con `conTarjeta` va dentro de un marco como el de una tarjeta de configuración.
 */
export function EsqueletoFormulario({
  campos = 4,
  columnas = 1,
  conTarjeta = false,
  etiqueta,
  sx,
}: Comun & { campos?: number; columnas?: 1 | 2; conTarjeta?: boolean }) {
  const { app } = useTheme();
  const cuerpo = (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: `repeat(${String(columnas)}, minmax(0, 1fr))` }, gap: 2 }}>
      {Array.from({ length: campos }, (_, i) => (
        <Stack key={i} spacing={0.75}>
          <Skeleton variant="rounded" width={`${String(26 + ((i * 5) % 4) * 8)}%`} height={12} />
          <Skeleton variant="rounded" height={app === 'web' ? 32 : 40} sx={{ borderRadius: (t) => `${String(t.radio.sm)}px` }} />
        </Stack>
      ))}
    </Box>
  );
  return (
    <Region etiqueta={etiqueta} sx={sx}>
      {conTarjeta ? (
        <Box sx={{ py: 2, px: 2, border: 1, borderColor: 'divider', borderRadius: radioMarco, bgcolor: 'background.paper' }}>
          <Skeleton variant="rounded" width={160} height={16} sx={{ mb: 2 }} />
          {cuerpo}
        </Box>
      ) : (
        cuerpo
      )}
    </Region>
  );
}

/** La silueta de una tabla (marco, encabezado y filas) sin región propia, para componerla dentro de otro esqueleto. */
export function TablaEsqueleto({ columnas = 4, filas = 5, alto }: { columnas?: number; filas?: number; alto?: number }) {
  return (
    <Box sx={{ overflow: 'hidden', border: 1, borderColor: 'divider', borderRadius: radioMarco, bgcolor: 'background.paper' }}>
      <Table size="small">
        <TableHead>
          <TableRow>
            {Array.from({ length: columnas }, (_, j) => (
              <TableCell key={j}>
                <Skeleton variant="rounded" height={10} width={`${String(36 + ((j * 3) % 4) * 12)}%`} />
              </TableCell>
            ))}
          </TableRow>
        </TableHead>
        <TableBody>
          {Array.from({ length: filas }, (_, i) => (
            <TableRow key={i} sx={{ '&:hover': { bgcolor: 'transparent' } }}>
              {Array.from({ length: columnas }, (_, j) => (
                <TableCell key={j} sx={{ height: alto }}>
                  <Skeleton variant="rounded" height={12} width={j === columnas - 1 ? 24 : `${String(40 + ((i * 7 + j * 13) % 5) * 12)}%`} />
                </TableCell>
              ))}
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </Box>
  );
}

/** Una tabla completa para cuando todavía no hay tabla en pantalla. */
export function EsqueletoTabla({ etiqueta, sx, ...tabla }: Comun & { columnas?: number; filas?: number; alto?: number }) {
  return (
    <Region etiqueta={etiqueta} sx={sx}>
      <TablaEsqueleto {...tabla} />
    </Region>
  );
}

/** Una página de detalle: cabecera y dos bloques (el principal y uno de apoyo). */
export function EsqueletoDetalle({ etiqueta, sx }: Comun) {
  return (
    <Region etiqueta={etiqueta} sx={sx}>
      <CabeceraEsqueleto />
      <Stack spacing={2}>
        <EsqueletoTarjeta lineas={5} />
        <EsqueletoTarjeta lineas={3} />
      </Stack>
    </Region>
  );
}

/** Una línea de tiempo: punto y dos líneas por evento. */
export function EsqueletoHistorial({ eventos = 3, etiqueta, sx }: Comun & { eventos?: number }) {
  return (
    <Region etiqueta={etiqueta} sx={sx}>
      <Stack spacing={2}>
        {Array.from({ length: eventos }, (_, i) => (
          <Stack key={i} direction="row" spacing={1.5}>
            <Skeleton variant="circular" width={28} height={28} />
            <Box sx={{ flex: 1 }}>
              <Skeleton variant="rounded" height={14} width="60%" sx={{ mb: 1 }} />
              <Skeleton variant="rounded" height={12} width="40%" />
            </Box>
          </Stack>
        ))}
      </Stack>
    </Region>
  );
}

/** Lo que muestra un autocompletar mientras busca opciones: tres filas de lista. */
export function OpcionesEnCarga() {
  return (
    <Region etiqueta="Buscando opciones" sx={{ py: 0.5 }}>
      <Stack spacing={1.25}>
        {[72, 58, 66].map((ancho) => (
          <Skeleton key={ancho} variant="rounded" height={12} width={`${String(ancho)}%`} />
        ))}
      </Stack>
    </Region>
  );
}
