import { Box, LinearProgress, Stack, Typography } from '@mui/material';
import type { MedicionDePlanDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { esMetrica, esNivel, formatearUso, porcentajeDeBarra } from './modelo';

/** Color de la barra por nivel: neutro hasta el 80 %, aviso del 80 % al 100 % y error al llegar al límite. */
const COLOR_DE_BARRA = { Normal: 'primary', PorAgotar: 'warning', Agotado: 'error' } as const;

/**
 * Una métrica del plan (OD-14): cuánto se usó, el límite y una barra. Sin límite no hay barra: solo lo usado. El estado no depende
 * del color: el texto dice «al 80 %» o «en el límite».
 */
export function MedidorDeUso({ medicion }: { medicion: MedicionDePlanDto }) {
  const { t } = useTranslation();
  if (!esMetrica(medicion.metrica)) return null;
  const nivel = esNivel(medicion.nivel) ? medicion.nivel : 'Normal';
  const usado = formatearUso(medicion.metrica, medicion.usado);
  const limite = medicion.limite === null ? null : formatearUso(medicion.metrica, medicion.limite);

  return (
    <Box component="li" sx={{ listStyle: 'none', minWidth: 0 }}>
      <Stack direction="row" sx={{ alignItems: 'baseline', justifyContent: 'space-between', gap: 1, mb: 0.5 }}>
        <Typography sx={{ fontWeight: 600, fontSize: 14, lineHeight: '20px' }}>{t(`plan.metricas.${medicion.metrica}.nombre`)}</Typography>
        <Typography sx={{ fontSize: 13, color: 'text.secondary', whiteSpace: 'nowrap' }}>
          {limite === null ? t('plan.usoSinLimite', { usado }) : t('plan.usoDeLimite', { usado, limite })}
        </Typography>
      </Stack>
      {limite !== null && (
        <LinearProgress
          variant="determinate"
          value={porcentajeDeBarra(medicion)}
          color={COLOR_DE_BARRA[nivel]}
          aria-label={t('plan.barra', { metrica: t(`plan.metricas.${medicion.metrica}.nombre`) })}
          sx={(th) => ({ height: 8, borderRadius: `${String(th.radio.sm)}px` })}
        />
      )}
      <Typography sx={{ mt: 0.5, fontSize: 12, lineHeight: '16px', color: nivel === 'Agotado' ? 'error.main' : 'text.secondary' }}>
        {limite === null ? t(`plan.metricas.${medicion.metrica}.ayuda`) : t(`plan.niveles.${nivel}`, { porcentaje: String(medicion.porcentaje ?? 0) })}
      </Typography>
    </Box>
  );
}

/** Las cinco métricas del plan en dos columnas (una en el celular). */
export function MedidoresDeUso({ mediciones }: { mediciones: readonly MedicionDePlanDto[] }) {
  const { t } = useTranslation();
  return (
    <Box component="ul" aria-label={t('plan.mediciones')} sx={{ m: 0, p: 0, display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: 'repeat(2, minmax(0, 1fr))' }, gap: 2.5, columnGap: 4 }}>
      {mediciones.map((m) => (
        <MedidorDeUso key={m.metrica} medicion={m} />
      ))}
    </Box>
  );
}
