import { Box, Chip, Stack, Typography } from '@mui/material';
import { CircleAlert } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

interface Props {
  /** Cómo se nombra el documento («RUT», «Cámara de Comercio»). */
  documento: string;
  version: number;
  /** Datos distintos que propone (nada = no se pudo leer, y se dice con un chip). */
  datosDistintos: number;
  noLeido: boolean;
  /** Las secciones que afecta (chips), si el aviso es el de Mi información. */
  secciones?: ReactNode;
  /** La acción, a la derecha y pequeña. */
  acciones: ReactNode;
  /** Lo que se despliega debajo (la tabla de cambios). */
  children?: ReactNode;
}

/**
 * Un documento más nuevo que la información (UI-9), como un bloque propio dentro del aviso: nombre y versión con su estado en
 * chips discretos («1 dato distinto», «No pudimos leerlo»), las secciones que afecta como chips y la acción a la derecha. Cada
 * documento tiene su borde, así no se confunde con el de al lado.
 */
export function TarjetaDeDocumento({ documento, version, datosDistintos, noLeido, secciones, acciones, children }: Props) {
  const { t } = useTranslation();
  return (
    <Box
      component="li"
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'minmax(0, 1fr) auto' },
        columnGap: 2,
        rowGap: 1,
        alignItems: 'center',
        px: 1.5,
        py: 1.25,
        border: 1,
        borderColor: 'divider',
        borderRadius: (th) => `${String(th.radio.sm)}px`,
        bgcolor: 'background.paper',
      }}
    >
      <Box sx={{ minWidth: 0 }}>
        <Stack direction="row" useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', gap: 0.75 }}>
          <Typography component="span" sx={{ fontSize: 14, lineHeight: '20px' }}>
            <Box component="span" sx={{ fontWeight: 600 }}>
              {t('ficha.propuesta.nombreDelDocumento', { documento })}
            </Box>
            <Box component="span" sx={{ color: 'text.secondary' }}>{` · ${t('ficha.propuesta.version', { n: String(version) })}`}</Box>
          </Typography>
          {datosDistintos > 0 && <Chip size="small" variant="outlined" label={t('ficha.propuesta.datosDistintos', { count: datosDistintos })} />}
          {noLeido && (
            <Chip size="small" variant="outlined" icon={<CircleAlert size={14} strokeWidth={1.75} aria-hidden />} label={t('ficha.propuesta.noLeido')} sx={{ color: 'text.secondary' }} />
          )}
        </Stack>
        {secciones}
      </Box>
      <Stack direction="row" useFlexGap sx={{ alignItems: 'center', justifyContent: { xs: 'flex-start', sm: 'flex-end' }, flexWrap: 'wrap', gap: 0.5 }}>
        {acciones}
      </Stack>
      {children && <Box sx={{ gridColumn: '1 / -1', minWidth: 0 }}>{children}</Box>}
    </Box>
  );
}
