import { Box, LinearProgress, Typography } from '@mui/material';
import type { UsoIaDto } from '@validasoft/api-client';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { Cifra, GrupoCifras } from '../../inicio/Cifras';
import { formatearConteo, formatearPorcentaje, formatearUsd, nivelDeUso, porcentajeUsado, type NivelUso } from './modelo';

const COLOR_BARRA: Record<NivelUso, string> = { normal: 'acento.main', alerta: 'estado.advertencia.solido', agotado: 'estado.error.solido' };

/** Una cifra principal con su nota debajo, en una tarjeta (patrón KPI de Inicio). */
function TarjetaKpi({ titulo, valor, nota, children }: { titulo: string; valor: string; nota?: ReactNode; children?: ReactNode }) {
  return (
    <Tarjeta titulo={titulo} etiqueta={titulo} nivel="h3">
      <Typography variant="kpi" component="p" sx={{ m: 0, display: 'block' }}>
        {valor}
      </Typography>
      {children}
      {nota && (
        <Typography variant="body2" component="div" sx={{ color: 'text.secondary', mt: 0.75 }}>
          {nota}
        </Typography>
      )}
    </Tarjeta>
  );
}

/** Incluidas usadas con barra (ámbar desde el 80 %, roja al 100 %), adicionales, análisis pagados, costo y tokens. */
export function TarjetasUso({ uso }: { uso: UsoIaDto }) {
  const { t } = useTranslation();
  const { plan } = uso;
  const incluidas = plan.validacionesIncluidasMes;
  const porcentaje = porcentajeUsado(uso.incluidasConsumidas, incluidas);
  const nivel = nivelDeUso(porcentaje);
  const textoPorcentaje = formatearPorcentaje(porcentaje);
  const notaIncluidas =
    nivel === 'agotado'
      ? t('ia.uso.agotadas')
      : nivel === 'alerta'
        ? t('ia.uso.alerta', { porcentaje: textoPorcentaje })
        : t('ia.uso.quedan', { count: uso.incluidasRestantes });

  return (
    <>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))', lg: 'repeat(4, minmax(0, 1fr))' }, gap: 1.5 }}>
        <TarjetaKpi
          titulo={t('ia.uso.incluidas')}
          valor={t('ia.uso.incluidasValor', { usadas: formatearConteo(uso.incluidasConsumidas), incluidas: formatearConteo(incluidas) })}
          nota={notaIncluidas}
        >
          <LinearProgress
            variant="determinate"
            value={Math.min(100, porcentaje)}
            aria-label={t('ia.uso.incluidasAccesible', {
              usadas: formatearConteo(uso.incluidasConsumidas),
              incluidas: formatearConteo(incluidas),
              porcentaje: textoPorcentaje,
            })}
            data-nivel={nivel}
            sx={{ mt: 1, height: 6, borderRadius: 3, '& .MuiLinearProgress-bar': { bgcolor: COLOR_BARRA[nivel], borderRadius: 3 } }}
          />
        </TarjetaKpi>
        <TarjetaKpi
          titulo={t('ia.uso.adicionales')}
          valor={plan.adicionalHabilitado ? formatearConteo(uso.adicionalesConsumidas) : t('ia.uso.sinAdicional')}
          nota={
            !plan.adicionalHabilitado
              ? t('ia.uso.sinAdicionalAyuda')
              : plan.topeAdicionalMes === null
                ? t('ia.uso.sinTope')
                : t('ia.uso.tope', { tope: formatearConteo(plan.topeAdicionalMes), restantes: formatearConteo(uso.adicionalesRestantes ?? 0) })
          }
        />
        <TarjetaKpi
          titulo={t('ia.uso.pagados')}
          valor={formatearConteo(uso.analisisPagados)}
          nota={
            <>
              {t('ia.uso.pagadosAyuda')}
              <Box component="span" sx={{ display: 'block' }}>
                {t('ia.uso.llave', { valor: plan.llaveConfigurada ? t('ia.uso.si') : t('ia.uso.no') })}
              </Box>
            </>
          }
        />
        <TarjetaKpi titulo={t('ia.uso.costo')} valor={formatearUsd(uso.costoUsd)} nota={t('ia.uso.costoAyuda')} />
      </Box>
      <GrupoCifras titulo={t('ia.uso.tokens')}>
        <Cifra etiqueta={t('ia.uso.tokensEntrada')} valor={formatearConteo(uso.tokensEntrada)} />
        <Cifra etiqueta={t('ia.uso.tokensCache')} valor={formatearConteo(uso.tokensEntradaEnCache)} />
        <Cifra etiqueta={t('ia.uso.tokensSalida')} valor={formatearConteo(uso.tokensSalida)} />
      </GrupoCifras>
    </>
  );
}
