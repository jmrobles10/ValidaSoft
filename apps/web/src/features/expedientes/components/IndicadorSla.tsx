import { Box, Typography } from '@mui/material';
import type { EstadoDeSla } from '@validasoft/api-client';
import { ChipEstado, formatearFecha, formatearFechaHora } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { ESTADO_CANONICO_SLA } from '../sla';

/**
 * Plazo de revisión de una fila de la bandeja (OP-12): chip «A tiempo», «Por vencer» o «Vencido» y, al lado, cuándo vence o
 * venció. Un área sin plazo (o ya decidida) no tiene indicador: solo un guion.
 */
export function IndicadorSla({ sla, vence }: { sla?: EstadoDeSla | null; vence?: string | null }) {
  const { t } = useTranslation();
  if (!sla) {
    return (
      <Typography variant="monoTabla" sx={{ color: 'text.secondary' }} aria-label={t('sla.indicador.sinPlazo')}>
        —
      </Typography>
    );
  }
  return (
    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 1, whiteSpace: 'nowrap' }}>
      <ChipEstado estado={ESTADO_CANONICO_SLA[sla]} etiqueta={t(`sla.indicador.estados.${sla}`)} />
      {vence && (
        <Typography component="span" title={formatearFechaHora(vence)} sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
          {t(sla === 'Vencido' ? 'sla.indicador.vencio' : 'sla.indicador.vence', { fecha: formatearFecha(vence) })}
        </Typography>
      )}
    </Box>
  );
}
