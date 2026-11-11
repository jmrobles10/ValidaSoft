import { Box, Typography } from '@mui/material';
import type { VistaPreviaCampaniaDto } from '@validasoft/api-client';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/** Una tarjeta del resumen: el número grande y, debajo, qué significa. */
function TarjetaTotal({ valor, etiqueta, nota }: { valor: number; etiqueta: string; nota?: ReactNode }) {
  return (
    <Box
      sx={{
        minWidth: 0,
        px: 2,
        py: 1.5,
        border: 1,
        borderColor: 'divider',
        borderRadius: (t) => `${String(t.radio.md)}px`,
        bgcolor: 'background.paper',
        display: 'flex',
        flexDirection: 'column-reverse',
        justifyContent: 'flex-end',
      }}
    >
      <Box component="dt" sx={{ minWidth: 0 }}>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {etiqueta}
        </Typography>
        {nota && (
          <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block' }}>
            {nota}
          </Typography>
        )}
      </Box>
      <Typography variant="kpi" component="dd" sx={{ m: 0, display: 'block' }}>
        {valor.toLocaleString('es-CO')}
      </Typography>
    </Box>
  );
}

/** Totales de la vista previa: a quién se le abre la renovación, a quién se le recuerda, cuántos se omiten y cuántos reciben correo. */
export function ResumenVistaPrevia({ vista }: { vista: VistaPreviaCampaniaDto }) {
  const { t } = useTranslation();
  return (
    <Box
      component="dl"
      sx={{ m: 0, display: 'grid', gap: 1.5, gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', md: 'repeat(4, minmax(0, 1fr))' } }}
    >
      <TarjetaTotal valor={vista.abririan} etiqueta={t('campanias.nuevaCampania.totales.abririan')} />
      <TarjetaTotal valor={vista.recordarian} etiqueta={t('campanias.nuevaCampania.totales.recordarian')} />
      <TarjetaTotal valor={vista.omitidos} etiqueta={t('campanias.nuevaCampania.totales.omitidos')} />
      <TarjetaTotal valor={vista.conCorreo} etiqueta={t('campanias.nuevaCampania.totales.conCorreo')} nota={t('campanias.nuevaCampania.totales.conCorreoNota')} />
    </Box>
  );
}
