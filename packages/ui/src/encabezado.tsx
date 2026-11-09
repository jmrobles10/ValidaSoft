import { Box, Stack, Typography } from '@mui/material';
import type { ReactNode } from 'react';

/**
 * Variable CSS con la distancia desde el borde superior del contenedor de scroll a la que se pega el encabezado. La
 * fija la app: bajo su barra superior cuando desplaza la ventana (p. ej. 48 px) o `0px` cuando la página tiene un
 * contenedor de scroll propio (embebida con altura fija).
 */
export const VARIABLE_TOPE = '--vs-tope';

export interface EncabezadoFijoProps {
  titulo: ReactNode;
  /** Texto o fila de datos bajo el título (código, NIT, estado…). */
  subtitulo?: ReactNode;
  /** Acciones principales de la página («Guardar borrador», «Publicar»…). */
  acciones?: ReactNode;
  /** Va encima del título dentro de la franja, p. ej. el enlace «‹ Flujos». */
  antes?: ReactNode;
  /** Nombre accesible del grupo de acciones. */
  etiquetaAcciones?: string;
  /** Texto completo del título para el `title` nativo (el título se corta con puntos suspensivos). */
  tituloCompleto?: string;
  /** Sangría lateral del contenedor (unidades del tema): la franja la cubre para ir de borde a borde. */
  sangria?: number;
}

/**
 * Encabezado de página «título + subtítulo + acciones» que queda visible mientras se desplaza el contenido: se pega
 * arriba del contenedor de scroll (`position: sticky`, distancia {@link VARIABLE_TOPE}) con fondo opaco y un borde
 * inferior, y el contenido pasa por debajo. En pantallas angostas las acciones bajan a una segunda línea en vez de
 * tapar el título, que se corta con puntos suspensivos; en el celular el subtítulo ocupa una sola línea.
 */
export function EncabezadoFijo({ titulo, subtitulo, acciones, antes, etiquetaAcciones, tituloCompleto, sangria = 3 }: EncabezadoFijoProps) {
  return (
    <Box
      data-encabezado-fijo=""
      sx={{
        position: 'sticky',
        top: `var(${VARIABLE_TOPE}, 0px)`,
        zIndex: (t) => t.zIndex.appBar - 1,
        bgcolor: 'background.default',
        mx: -sangria,
        px: sangria,
        mt: -1.5,
        pt: 1.5,
        pb: 1.5,
        mb: 2,
        borderBottom: 1,
        borderColor: 'divider',
      }}
    >
      {antes}
      <Box sx={{ display: 'flex', flexWrap: 'wrap', alignItems: 'flex-end', columnGap: 2, rowGap: 1 }}>
        {/* En el celular el título cede antes (150 px) para que una acción quepa a su lado; si no, bajan a otra línea. */}
        <Box sx={{ flex: { xs: '1 1 150px', sm: '1 1 320px' }, minWidth: 0 }}>
          <Typography variant="h2" component="h1" noWrap title={tituloCompleto}>
            {titulo}
          </Typography>
          {subtitulo && (
            <Typography
              component="div"
              variant="body2"
              // En pantallas angostas, una sola línea: la franja fija no debe comerse la pantalla.
              sx={{ color: 'text.secondary', mt: 0.25, minWidth: 0, whiteSpace: { xs: 'nowrap', sm: 'normal' }, overflow: { xs: 'hidden', sm: 'visible' }, textOverflow: 'ellipsis' }}
            >
              {subtitulo}
            </Typography>
          )}
        </Box>
        {acciones && (
          <Stack
            direction="row"
            spacing={1}
            useFlexGap
            role="group"
            aria-label={etiquetaAcciones}
            sx={{ flexWrap: 'wrap', flex: '0 1 auto', ml: { xs: 0, sm: 'auto' }, justifyContent: { sm: 'flex-end' } }}
          >
            {acciones}
          </Stack>
        )}
      </Box>
    </Box>
  );
}
