import { Box, Typography } from '@mui/material';
import { AlineadoConCampo, CampoNumero } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { UMBRAL_MINIMO } from './modelo';

export interface TarjetaConfianzaProps {
  valor: string;
  onCambiar: (valor: string) => void;
  error: boolean;
  /** Lo que exige el plan de IA (porcentaje): el de la empresa nunca queda por debajo. */
  umbralDelPlan: number;
  deshabilitado: boolean;
}

/** La confianza mínima del análisis, que vale para sugerir (Asistido) y para aprobar (Automático). */
export function TarjetaConfianza({ valor, onCambiar, error, umbralDelPlan, deshabilitado }: TarjetaConfianzaProps) {
  const { t } = useTranslation();
  return (
    <Tarjeta titulo={t('revisionAutomatica.config.confianza.titulo')}>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: '240px minmax(0, 1fr)' }, columnGap: 3, rowGap: 1, alignItems: 'start' }}>
        <CampoNumero
          fullWidth
          etiqueta={t('revisionAutomatica.config.confianza.umbral')}
          valor={valor}
          onCambiar={onCambiar}
          maxDigitos={3}
          error={error}
          ayuda={error ? t('revisionAutomatica.config.confianza.rango', { minimo: String(UMBRAL_MINIMO) }) : undefined}
          deshabilitado={deshabilitado}
        />
        <AlineadoConCampo desde="md">
          <Box>
            <Typography variant="body2">{t('revisionAutomatica.config.confianza.ayuda')}</Typography>
            {umbralDelPlan > UMBRAL_MINIMO && (
              <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.5 }}>
                {t('revisionAutomatica.config.confianza.delPlan', { valor: String(umbralDelPlan) })}
              </Typography>
            )}
          </Box>
        </AlineadoConCampo>
      </Box>
    </Tarjeta>
  );
}
