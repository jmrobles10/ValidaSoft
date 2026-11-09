import { Link } from '@mui/material';
import type { ReactNode } from 'react';
import { Link as RouterLink, useParams } from 'react-router';

/**
 * Lleva a la ficha del tercero (Tercero 360) en la empresa de la ruta. Dentro de una fila clicable de tabla no dispara
 * el clic de la fila (que abre el vínculo); con el teclado la fila ya ignora lo que no le llega directamente.
 */
export function EnlaceFichaTercero({ terceroId, etiqueta, children }: { terceroId: string; etiqueta?: string; children: ReactNode }) {
  const { empresaId = '' } = useParams();
  return (
    <Link
      component={RouterLink}
      to={`/e/${empresaId}/terceros/${terceroId}`}
      underline="hover"
      aria-label={etiqueta}
      onClick={(e) => e.stopPropagation()}
      sx={{ color: 'inherit', minWidth: 0, maxWidth: '100%', display: 'inline-flex' }}
    >
      {children}
    </Link>
  );
}
