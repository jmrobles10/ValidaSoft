import { Box, Stack, Typography } from '@mui/material';
import type { InformeSagrilaftDto } from '@validasoft/api-client';
import { Dato, RejillaDatos } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { distribucionDeRiesgo, porcentajeDe, promedioDeDias } from '../informeSagrilaft';
import { claveRiesgo, tonoDeRiesgo } from '../modelo';
import { ChipNivelRiesgo } from './ChipNivelRiesgo';

type Tono = 'exito' | 'advertencia' | 'error' | null;
const TARJETAS: readonly { clave: 'terceros' | 'conEstudioVigente' | 'estudiosPorVencer' | 'estudiosVencidos' | 'sinEstudio' | 'listasPorVencer' | 'listasVencidas'; tono: Tono }[] = [
  { clave: 'terceros', tono: null },
  { clave: 'conEstudioVigente', tono: 'exito' },
  { clave: 'estudiosPorVencer', tono: 'advertencia' },
  { clave: 'estudiosVencidos', tono: 'error' },
  { clave: 'sinEstudio', tono: 'advertencia' },
  { clave: 'listasPorVencer', tono: 'advertencia' },
  { clave: 'listasVencidas', tono: 'error' },
];

/** Los conteos de la situación de los terceros: con estudio vigente, por vencer, vencidos o sin estudio, y las listas por vencer o vencidas. */
export function TarjetasSagrilaft({ informe }: { informe: InformeSagrilaftDto }) {
  const { t } = useTranslation();
  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(148px, 1fr))', gap: 1.5 }}>
      {TARJETAS.map(({ clave, tono }) => {
        const valor = informe[clave];
        return (
          <Box
            key={clave}
            data-tarjeta={clave}
            sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.md)}px`, bgcolor: 'background.paper', px: 2, py: 1.5, minWidth: 0 }}
          >
            <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block' }}>
              {t(`cumplimiento.sagrilaft.tarjetas.${clave}`)}
            </Typography>
            <Typography sx={{ fontSize: 24, fontWeight: 600, lineHeight: '32px', color: tono && valor > 0 ? `estado.${tono}.texto` : 'text.primary' }}>{valor.toLocaleString('es-CO')}</Typography>
          </Box>
        );
      })}
    </Box>
  );
}

/** Cuántos terceros hay en cada nivel de riesgo (solo los que tienen estudio), con barras proporcionales. */
export function RiesgoSagrilaft({ informe }: { informe: InformeSagrilaftDto }) {
  const { t } = useTranslation();
  const distribucion = distribucionDeRiesgo(informe);
  const maximo = Math.max(0, ...distribucion.map((d) => d.cantidad));
  return (
    <Tarjeta titulo={t('cumplimiento.sagrilaft.riesgo.titulo')} etiqueta={t('cumplimiento.sagrilaft.riesgo.titulo')} nivel="h3">
      {maximo === 0 ? (
        <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.sagrilaft.riesgo.sinDatos')}</Typography>
      ) : (
        <Stack component="ul" spacing={1.25} sx={{ listStyle: 'none', p: 0, m: 0 }}>
          {distribucion.map(({ nivel, cantidad }) => (
            <Stack key={nivel} component="li" direction="row" spacing={1.5} sx={{ alignItems: 'center' }} aria-label={t('cumplimiento.sagrilaft.riesgo.barra', { nivel: t(`cumplimiento.riesgos.${claveRiesgo(nivel)}`), cantidad: String(cantidad) })}>
              <Box sx={{ width: 104, flexShrink: 0 }}>
                <ChipNivelRiesgo nivel={nivel} />
              </Box>
              <Box aria-hidden sx={{ flex: 1, height: 12, borderRadius: '6px', bgcolor: 'superficie.nivel2', overflow: 'hidden' }}>
                <Box data-barra={nivel} sx={{ height: '100%', width: `${String(porcentajeDe(cantidad, maximo))}%`, bgcolor: `estado.${tonoDeRiesgo(nivel)}.solido`, borderRadius: '6px' }} />
              </Box>
              <Typography component="span" sx={{ width: 28, textAlign: 'right', fontSize: 14, fontWeight: 500 }}>
                {cantidad}
              </Typography>
            </Stack>
          ))}
        </Stack>
      )}
    </Tarjeta>
  );
}

/** Tiempos del oficial: cuánto tarda en resolver una verificación y cuántos expedientes siguen esperándolo. */
export function TiemposSagrilaft({ informe }: { informe: InformeSagrilaftDto }) {
  const { t } = useTranslation();
  const { tiempos } = informe;
  const promedio = promedioDeDias(tiempos.promedioDiasEnResolver);
  return (
    <Tarjeta titulo={t('cumplimiento.sagrilaft.tiempos.titulo')} etiqueta={t('cumplimiento.sagrilaft.tiempos.titulo')} nivel="h3">
      <RejillaDatos columnas={2}>
        <Dato etiqueta={t('cumplimiento.sagrilaft.tiempos.promedio')}>{promedio === null ? t('cumplimiento.sagrilaft.tiempos.sinPromedio') : t('cumplimiento.sagrilaft.tiempos.dias', { dias: promedio })}</Dato>
        <Dato etiqueta={t('cumplimiento.sagrilaft.tiempos.resueltas')}>{tiempos.resueltas}</Dato>
        <Dato etiqueta={t('cumplimiento.sagrilaft.tiempos.abiertas')}>{tiempos.abiertas}</Dato>
        <Dato etiqueta={t('cumplimiento.sagrilaft.tiempos.abiertasMasDe7Dias')}>{tiempos.abiertasMasDe7Dias}</Dato>
      </RejillaDatos>
      <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 1.5 }}>{t('cumplimiento.sagrilaft.tiempos.ayuda')}</Typography>
    </Tarjeta>
  );
}
