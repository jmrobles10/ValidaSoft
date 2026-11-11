import { Box, Stack } from '@mui/material';
import type { ReactNode } from 'react';

/**
 * Página de configuración a ancho completo (UI-5): una columna en pantallas angostas y **dos columnas iguales** desde `lg`,
 * para que un formulario no quede volcado a la izquierda con media pantalla vacía. Cada columna apila sus tarjetas; el alto
 * de una no estira el de la otra. Se reparten a mano las tarjetas para que ambas queden parejas (lo más largo, a la izquierda).
 */
export function DosColumnas({ izquierda, derecha }: { izquierda: ReactNode; derecha: ReactNode }) {
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', lg: 'repeat(2, minmax(0, 1fr))' }, gap: 2, alignItems: 'start' }}>
      <Stack spacing={2} sx={{ minWidth: 0 }}>
        {izquierda}
      </Stack>
      <Stack spacing={2} sx={{ minWidth: 0 }}>
        {derecha}
      </Stack>
    </Box>
  );
}
