import { Box, Link, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { Link as RouterLink } from 'react-router';
import { Tarjeta } from '../../shared/componentes';

/** Grupo de cifras con su título (Vínculos, Expedientes, Certificados): una lista de definiciones en rejilla. */
export function GrupoCifras({ titulo, nota, children }: { titulo: string; nota?: string; children: ReactNode }) {
  return (
    <Tarjeta titulo={titulo} etiqueta={titulo} nivel="h3">
      <Box
        component="dl"
        sx={{ m: 0, display: 'grid', gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', sm: 'repeat(auto-fill, minmax(150px, 1fr))' }, columnGap: 2, rowGap: 1.5 }}
      >
        {children}
      </Box>
      {nota && (
        <Typography variant="body2" sx={{ color: 'text.secondary', mt: 1.5 }}>
          {nota}
        </Typography>
      )}
    </Tarjeta>
  );
}

/**
 * Una cifra: el número grande (tipografía `kpi`) y debajo su etiqueta, que enlaza a la lista filtrada cuando la hay.
 * En el DOM la etiqueta (`dt`) va antes que el número (`dd`); se invierte solo al dibujar.
 */
export function Cifra({ etiqueta, valor, a, nota }: { etiqueta: string; valor: string; a?: string | null; nota?: string }) {
  return (
    <Box sx={{ display: 'flex', flexDirection: 'column-reverse', justifyContent: 'flex-end', minWidth: 0 }}>
      <Box component="dt" sx={{ minWidth: 0 }}>
        {a ? (
          <Link component={RouterLink} to={a} variant="body2">
            {etiqueta}
          </Link>
        ) : (
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {etiqueta}
          </Typography>
        )}
        {nota && (
          <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block' }}>
            {nota}
          </Typography>
        )}
      </Box>
      <Typography variant="kpi" component="dd" sx={{ m: 0, display: 'block' }}>
        {valor}
      </Typography>
    </Box>
  );
}
