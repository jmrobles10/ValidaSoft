import { Box, Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { CampoNumero } from '../../../shared/camposTransitorios';
import { ChipNivelRiesgo } from '../../cumplimiento/components/ChipNivelRiesgo';
import { tonoDeRiesgo } from '../../cumplimiento/modelo';
import { tramos } from './modelo';

export interface TarjetaLimitesProps {
  limiteBajo: string;
  limiteMedio: string;
  invalido: boolean;
  deshabilitado: boolean;
  onCambiar: (cambio: { limiteBajo?: string; limiteMedio?: string }) => void;
}

/** Los dos límites que reparten el puntaje (0–100) en Bajo, Medio y Alto, con una barra que muestra los tres tramos. */
export function TarjetaLimites({ limiteBajo, limiteMedio, invalido, deshabilitado, onCambiar }: TarjetaLimitesProps) {
  const { t } = useTranslation();
  const [bajo, medio, alto] = tramos(limiteBajo, limiteMedio);
  const textos = [
    t('matrizRiesgo.limites.tramoBajo', { hasta: String(bajo.hasta) }),
    t('matrizRiesgo.limites.tramoMedio', { desde: String(medio.desde), hasta: String(medio.hasta) }),
    t('matrizRiesgo.limites.tramoAlto', { desde: String(alto.desde) }),
  ];

  return (
    <Stack spacing={2}>
      <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('matrizRiesgo.limites.ayuda')}</Typography>
      <Box>
        <Box
          role="img"
          aria-label={t('matrizRiesgo.limites.barra', { bajo: String(bajo.hasta), medio: String(medio.hasta) })}
          sx={{ display: 'flex', height: 14, borderRadius: (th) => `${String(th.radio.sm)}px`, overflow: 'hidden', border: 1, borderColor: 'divider' }}
        >
          {[bajo, medio, alto].map((tramo) => (
            <Box key={tramo.nivel} data-tramo={tramo.nivel} sx={{ width: `${String(tramo.hasta - tramo.desde)}%`, bgcolor: `estado.${tonoDeRiesgo(tramo.nivel)}.solido`, '&:not(:last-of-type)': { borderRight: 2, borderColor: 'background.paper' } }} />
          ))}
        </Box>
        <Box aria-hidden sx={{ position: 'relative', height: 18, mt: 0.5, fontSize: 11, color: 'text.secondary' }}>
          {[0, bajo.hasta, medio.hasta, 100].map((p) => (
            <Box key={p} component="span" sx={{ position: 'absolute', left: `${String(p)}%`, transform: p === 0 ? 'none' : p === 100 ? 'translateX(-100%)' : 'translateX(-50%)' }}>
              {p}
            </Box>
          ))}
        </Box>
      </Box>
      <Stack spacing={0.75} component="ul" sx={{ listStyle: 'none', p: 0, m: 0 }}>
        {[bajo, medio, alto].map((tramo, i) => (
          <Stack key={tramo.nivel} component="li" direction="row" spacing={1} sx={{ alignItems: 'center' }}>
            <ChipNivelRiesgo nivel={tramo.nivel} />
            <Typography component="span" sx={{ fontSize: 13 }}>
              {textos[i]}
            </Typography>
          </Stack>
        ))}
      </Stack>
      <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(2, minmax(0, 1fr))', gap: 1.5 }}>
        <CampoNumero etiqueta={t('matrizRiesgo.limites.bajo')} valor={limiteBajo} onCambiar={(v) => onCambiar({ limiteBajo: v })} deshabilitado={deshabilitado} error={invalido} maxDigitos={3} fullWidth />
        <CampoNumero etiqueta={t('matrizRiesgo.limites.medio')} valor={limiteMedio} onCambiar={(v) => onCambiar({ limiteMedio: v })} deshabilitado={deshabilitado} error={invalido} maxDigitos={3} fullWidth />
      </Box>
      {invalido && (
        <Typography role="alert" sx={{ color: 'error.main', fontSize: 12, mt: '-8px' }}>
          {t('matrizRiesgo.validacion.limites')}
        </Typography>
      )}
    </Stack>
  );
}
