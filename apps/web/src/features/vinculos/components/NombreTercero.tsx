import { Box, Skeleton, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { useTercero } from '../api';
import { identificacionDe } from '../tercero';

/**
 * Nombre del tercero con su NIT en la misma línea (fila de 36 px). `VinculoDto` solo trae `terceroId`, así que cada
 * fila lo consulta (con caché de 5 minutos); pendiente pedir al backend un resumen del tercero en el listado.
 */
export function NombreTercero({ terceroId }: { terceroId: string }) {
  const { t } = useTranslation();
  const { data, isPending, isError } = useTercero(terceroId);

  if (isPending) return <Skeleton variant="rounded" height={12} width={200} />;
  if (isError) {
    return (
      <Typography component="span" color="text.secondary">
        {t('vinculos.terceroDesconocido')}
      </Typography>
    );
  }

  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'baseline', gap: 1, minWidth: 0, maxWidth: '100%' }}>
      <Typography component="span" noWrap sx={{ fontWeight: 500 }}>
        {data.nombre}
      </Typography>
      <Typography component="span" color="text.secondary" sx={{ fontSize: 12, lineHeight: '16px', whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>
        {identificacionDe(data)}
      </Typography>
    </Box>
  );
}
