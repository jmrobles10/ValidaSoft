import { Box, Stack, Typography } from '@mui/material';
import type { ObservacionPublicaDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { etiquetaDeCampo } from '../ficha/etiquetas';

/**
 * Una observación: los textos de sus motivos, si la hay la observación escrita y, en una sección de «Mi información», los
 * datos que la empresa señaló (nunca quién ni qué área).
 */
export function Observacion({ observacion }: { observacion: ObservacionPublicaDto }) {
  const { t } = useTranslation();
  return (
    <Box sx={(th) => ({ mt: 1, py: 1, px: 1.5, borderRadius: `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2' })}>
      <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px', mb: 0.25 }}>
        {t('expediente.observacionDel', { fecha: formatearFecha(observacion.fecha) })}
      </Typography>
      <Stack component="ul" spacing={0.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
        {observacion.motivos.map((m) => (
          <Typography component="li" key={m.codigo} sx={{ fontSize: 14, lineHeight: '20px' }}>
            {m.texto}
          </Typography>
        ))}
      </Stack>
      {observacion.observacion && <Typography sx={{ mt: 0.5, fontSize: 14, lineHeight: '20px', whiteSpace: 'pre-wrap' }}>“{observacion.observacion}”</Typography>}
      {observacion.campos && observacion.campos.length > 0 && (
        <Typography sx={{ mt: 0.5, fontSize: 13, lineHeight: '18px' }}>
          {t('expediente.camposSenalados', { campos: observacion.campos.map((c) => etiquetaDeCampo(c, t)).join(', ') })}
        </Typography>
      )}
    </Box>
  );
}
