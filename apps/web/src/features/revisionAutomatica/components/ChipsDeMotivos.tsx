import { Box, Chip, Tooltip } from '@mui/material';
import type { MotivoDeRevisionDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { etiquetaDeMotivo } from '../modelo';

/** Los motivos por los que el sistema no aprueba solo: un chip corto por motivo y, en su tooltip, el texto completo del servidor. */
export function ChipsDeMotivos({ motivos, etiqueta }: { motivos: readonly MotivoDeRevisionDto[]; etiqueta: string }) {
  const { t } = useTranslation();
  if (motivos.length === 0) return null;
  return (
    <Box component="ul" aria-label={etiqueta} sx={{ listStyle: 'none', m: 0, p: 0, display: 'flex', flexWrap: 'wrap', gap: 0.5 }}>
      {motivos.map((motivo) => (
        <Box component="li" key={motivo.codigo} sx={{ display: 'inline-flex', minWidth: 0, maxWidth: '100%' }}>
          <Tooltip title={motivo.texto} describeChild>
            <Chip
              size="small"
              variant="outlined"
              label={etiquetaDeMotivo(motivo, t)}
              sx={{ maxWidth: '100%', color: 'text.primary', borderColor: 'borde.sutil', borderRadius: (th) => `${String(th.radio.xs)}px` }}
            />
          </Tooltip>
        </Box>
      ))}
    </Box>
  );
}
