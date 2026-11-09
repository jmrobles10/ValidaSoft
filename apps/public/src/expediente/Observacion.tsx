import { Box, Stack, Typography } from '@mui/material';
import type { ObservacionPublicaDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';

/** Una observación: los textos de sus motivos y, si la hay, la observación escrita (nunca quién ni qué área). */
export function Observacion({ observacion }: { observacion: ObservacionPublicaDto }) {
  const { t } = useTranslation();
  return (
    <Box sx={{ mt: 1.5, p: 1.5, borderRadius: 1, bgcolor: 'superficie.nivel2' }}>
      <Typography color="text.secondary" sx={{ fontSize: 13, mb: 0.5 }}>
        {t('expediente.observacionDel', { fecha: formatearFecha(observacion.fecha) })}
      </Typography>
      <Stack component="ul" spacing={0.5} sx={{ p: 0, m: 0, listStyle: 'none' }}>
        {observacion.motivos.map((m) => (
          <Typography component="li" key={m.codigo} sx={{ fontSize: 15, lineHeight: '22px' }}>
            {m.texto}
          </Typography>
        ))}
      </Stack>
      {observacion.observacion && <Typography sx={{ mt: 1, fontSize: 15, lineHeight: '22px', whiteSpace: 'pre-wrap' }}>“{observacion.observacion}”</Typography>}
    </Box>
  );
}
