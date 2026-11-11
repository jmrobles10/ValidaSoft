import { Alert, Box, LinearProgress, Typography } from '@mui/material';
import type { UsoRevisionAutomaticaDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { Cifra } from '../../inicio/Cifras';
import { etiquetaMes, formatearConteo, formatearPorcentaje, nivelDeUso, porcentajeUsado, type NivelUso } from '../usoIa/modelo';

const COLOR_BARRA: Record<NivelUso, string> = { normal: 'acento.main', alerta: 'estado.advertencia.solido', agotado: 'estado.error.solido' };

/**
 * Lo que la revisión automática hizo en el mes: aprobadas por el sistema, las que quedan para confirmar por muestreo, las
 * excepciones y las sugerencias; con el tope mensual como barra (ámbar desde el 80 %, roja al llegar).
 */
export function TarjetaUso({ uso }: { uso: UsoRevisionAutomaticaDto }) {
  const { t } = useTranslation();
  const titulo = t('revisionAutomatica.config.uso.titulo', { mes: etiquetaMes(uso.mes) });
  const sinActividad = uso.aprobadas + uso.muestreadas + uso.excepciones + uso.sugerencias === 0;
  const porcentaje = uso.tope === null ? 0 : porcentajeUsado(uso.aprobadas, uso.tope);
  const nivel = nivelDeUso(porcentaje);

  return (
    <Tarjeta titulo={titulo} etiqueta={titulo}>
      <Box component="dl" sx={{ m: 0, display: 'grid', gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', md: 'repeat(4, minmax(0, 1fr))' }, columnGap: 2, rowGap: 1.5 }}>
        <Cifra etiqueta={t('revisionAutomatica.config.uso.aprobadas')} valor={formatearConteo(uso.aprobadas)} />
        <Cifra etiqueta={t('revisionAutomatica.config.uso.muestreadas')} valor={formatearConteo(uso.muestreadas)} />
        <Cifra etiqueta={t('revisionAutomatica.config.uso.excepciones')} valor={formatearConteo(uso.excepciones)} />
        <Cifra etiqueta={t('revisionAutomatica.config.uso.sugerencias')} valor={formatearConteo(uso.sugerencias)} />
      </Box>
      {uso.tope !== null ? (
        <Box sx={{ mt: 2 }}>
          <Typography variant="body2" sx={{ mb: 0.75 }}>
            {t('revisionAutomatica.config.uso.barra', { aprobadas: formatearConteo(uso.aprobadas), tope: formatearConteo(uso.tope) })}
          </Typography>
          <LinearProgress
            variant="determinate"
            value={Math.min(100, porcentaje)}
            data-nivel={nivel}
            aria-label={t('revisionAutomatica.config.uso.barraAccesible', { aprobadas: formatearConteo(uso.aprobadas), tope: formatearConteo(uso.tope), porcentaje: formatearPorcentaje(porcentaje) })}
            sx={{ height: 6, borderRadius: 3, '& .MuiLinearProgress-bar': { bgcolor: COLOR_BARRA[nivel], borderRadius: 3 } }}
          />
          {nivel !== 'normal' && (
            <Alert severity={nivel === 'agotado' ? 'error' : 'warning'} sx={{ mt: 1.5 }}>
              {nivel === 'agotado' ? t('revisionAutomatica.config.uso.agotado') : t('revisionAutomatica.config.uso.alerta', { porcentaje: formatearPorcentaje(porcentaje) })}
            </Alert>
          )}
        </Box>
      ) : (
        <Typography variant="body2" sx={{ color: 'text.secondary', mt: 1.5 }}>
          {sinActividad ? t('revisionAutomatica.config.uso.vacio') : t('revisionAutomatica.config.uso.sinTope')}
        </Typography>
      )}
    </Tarjeta>
  );
}
