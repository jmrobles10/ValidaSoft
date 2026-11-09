import { Box, Typography, useTheme, type Breakpoint, type Theme } from '@mui/material';
import type { SubmitEvent, ReactNode } from 'react';
import { DENSIDAD } from './tokens';

/**
 * Medidas de un campo con etiqueta fija encima (`MuiInputLabel` estático del tema): la etiqueta ocupa su línea más
 * 4 px de separación, y debajo va el control (32 px en web, 44 px en public).
 */
export function medidasDeCampo(app: Theme['app']): { etiqueta: number; control: number } {
  return app === 'web' ? { etiqueta: 18 + 4, control: DENSIDAD.controlCompacto } : { etiqueta: 20 + 4, control: DENSIDAD.controlComodo };
}

/**
 * Coloca switches, casillas y botones de icono de una fila de formulario a la altura de los **campos**, no de sus
 * etiquetas: deja arriba el espacio de la etiqueta y centra el contenido en el alto del control. El texto de ayuda
 * bajo un campo (p. ej. «1 miembro») no los desplaza porque la fila se alinea arriba. `desde`: el punto de quiebre en
 * que la fila deja de apilarse (antes no hace falta el espacio de la etiqueta).
 */
export function AlineadoConCampo({
  children,
  desde = 'xs',
  columna,
  conEtiqueta = true,
}: {
  children: ReactNode;
  desde?: Breakpoint;
  /** `false` en filas cuyos campos no llevan etiqueta (p. ej. las filas siguientes de una lista): solo centra en el control. */
  conEtiqueta?: boolean;
  /** `grid-column` de la celda (p. ej. `{ xs: '1 / -1', lg: 'auto' }`). */
  columna?: string | Partial<Record<Breakpoint, string>>;
}) {
  const { app } = useTheme();
  const { etiqueta, control } = medidasDeCampo(app);
  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        gap: 1,
        minHeight: control,
        boxSizing: 'content-box',
        pt: !conEtiqueta ? 0 : desde === 'xs' ? `${String(etiqueta)}px` : { xs: 0, [desde]: `${String(etiqueta)}px` },
        gridColumn: columna,
      }}
    >
      {children}
    </Box>
  );
}

/**
 * Formulario dentro de un `Dialog`: deja que solo `DialogContent` desplace y que el título y las acciones (el botón de
 * enviar) queden siempre a la vista, aunque el contenido crezca.
 */
export function FormularioDeDialogo({ onSubmit, children }: { onSubmit: (evento: SubmitEvent<HTMLFormElement>) => void; children: ReactNode }) {
  return (
    <Box component="form" onSubmit={onSubmit} noValidate sx={{ display: 'flex', flexDirection: 'column', minHeight: 0 }}>
      {children}
    </Box>
  );
}

/**
 * Junto al botón de envío deshabilitado, qué falta para poder enviar («Marca al menos un documento»). Va a la izquierda
 * de las acciones de un diálogo o formulario y se anuncia al cambiar (`role="status"`).
 */
export function FaltaParaEnviar({ children }: { children: ReactNode }) {
  return (
    <Typography role="status" variant="caption" sx={{ color: 'text.secondary', mr: 'auto', alignSelf: 'center' }}>
      {children}
    </Typography>
  );
}
