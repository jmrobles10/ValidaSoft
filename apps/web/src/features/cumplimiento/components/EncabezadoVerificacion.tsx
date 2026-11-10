import { Box, Stack, Typography } from '@mui/material';
import type { VerificacionDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/** Encabezado común de una verificación: nombre, estado (con su vigencia en el tooltip), si bloquea la aprobación y sus acciones. */
export function EncabezadoVerificacion({ verificacion, acciones }: { verificacion: VerificacionDto; acciones?: ReactNode }) {
  const { t } = useTranslation();
  return (
    <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', mb: 1 }}>
      <Typography variant="h5" component="h3" sx={{ fontSize: 15 }}>
        {t(`cumplimiento.tipos.${verificacion.tipo}`)}
      </Typography>
      <ChipEstado estado={verificacion.estado} etiqueta={t(`cumplimiento.estados.${verificacion.estado}`)} fecha={verificacion.vigenteHasta ?? null} />
      <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
        {verificacion.bloqueante ? t('cumplimiento.panel.bloqueante') : t('cumplimiento.panel.noBloqueante')}
      </Typography>
      <Box sx={{ flex: 1 }} />
      {acciones}
    </Stack>
  );
}
