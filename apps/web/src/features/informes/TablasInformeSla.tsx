import { Box, LinearProgress, Link, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import type { InformeSlaDto } from '@validasoft/api-client';
import { EstadoVacio, formatearFechaHora, MarcoTabla } from '@validasoft/ui';
import { CircleCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { Tarjeta } from '../../shared/componentes';
import { formatearDias } from './modelo';
import { cumplimientoParaBarra, desgloseDeRetraso, formatearCumplimiento, tonoDeCumplimiento } from './modeloSla';

/** Cuántos vencidos se listan en pantalla; el Excel trae todos. */
export const MAXIMO_VENCIDOS_VISIBLES = 100;

/** Cumplimiento de cada área: plazo, revisiones a tiempo y fuera de plazo, barra de cumplimiento, promedio y vencidos hoy. */
export function CumplimientoPorArea({ areas }: { areas: InformeSlaDto['porArea'] }) {
  const { t } = useTranslation();
  const sinDato = t('informes.sinDato');
  return (
    <Tarjeta titulo={t('informes.sla.areas.titulo')} nivel="h3">
      {areas.length === 0 ? (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('informes.sla.areas.vacio')}
        </Typography>
      ) : (
        <Box sx={{ overflowX: 'auto' }}>
          <Table size="small" aria-label={t('informes.sla.areas.titulo')}>
            <TableHead>
              <TableRow>
                <TableCell>{t('informes.sla.areas.area')}</TableCell>
                {(['plazo', 'rondas', 'aTiempo', 'fuera'] as const).map((c) => (
                  <TableCell key={c} align="right">
                    {t(`informes.sla.areas.${c}`)}
                  </TableCell>
                ))}
                <TableCell sx={{ minWidth: 170 }}>{t('informes.sla.areas.cumplimiento')}</TableCell>
                <TableCell align="right">{t('informes.sla.areas.promedio')}</TableCell>
                <TableCell align="right">{t('informes.sla.areas.vencidos')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {areas.map((a) => {
                const texto = formatearCumplimiento(a.cumplimiento, sinDato);
                return (
                  <TableRow key={a.areaId}>
                    <TableCell sx={{ fontWeight: 500 }}>{a.nombre}</TableCell>
                    <TableCell align="right">{a.slaHoras === null ? t('informes.sla.areas.sinPlazo') : t('informes.sla.areas.plazoHoras', { horas: a.slaHoras })}</TableCell>
                    <TableCell align="right">{a.rondas}</TableCell>
                    <TableCell align="right">{a.aDentroDelPlazo}</TableCell>
                    <TableCell align="right">{a.fueraDelPlazo}</TableCell>
                    <TableCell>
                      <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
                        <LinearProgress
                          variant="determinate"
                          value={cumplimientoParaBarra(a.cumplimiento)}
                          color={tonoDeCumplimiento(a.cumplimiento)}
                          aria-label={t('informes.sla.areas.cumplimientoDe', { area: a.nombre, valor: texto })}
                          sx={{ width: 90, flex: 'none', height: 6, borderRadius: 3 }}
                        />
                        <Typography variant="monoTabla" component="span" sx={{ whiteSpace: 'nowrap' }}>
                          {texto}
                        </Typography>
                      </Stack>
                    </TableCell>
                    <TableCell align="right">{formatearDias(a.promedioDias, sinDato)}</TableCell>
                    <TableCell align="right">{a.vencidosAhora}</TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </Box>
      )}
    </Tarjeta>
  );
}

/** Revisiones que hoy están vencidas, de la más atrasada a la menos, con enlace al expediente. */
export function VencidosAhora({ empresaId, vencidos }: { empresaId: string; vencidos: InformeSlaDto['vencidos'] }) {
  const { t } = useTranslation();
  const visibles = vencidos.slice(0, MAXIMO_VENCIDOS_VISIBLES);
  return (
    <Tarjeta titulo={t('informes.sla.vencidos.titulo')} nivel="h3">
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
        {t('informes.sla.vencidos.ayuda')}
      </Typography>
      {vencidos.length === 0 ? (
        <EstadoVacio compacto icono={CircleCheck} titulo={t('informes.sla.vencidos.vacio')} />
      ) : (
        <MarcoTabla>
          <Box sx={{ overflowX: 'auto' }}>
            <Table size="small" aria-label={t('informes.sla.vencidos.titulo')}>
              <TableHead>
                <TableRow>
                  {(['folio', 'tercero', 'area', 'vencia'] as const).map((c) => (
                    <TableCell key={c}>{t(`informes.sla.vencidos.${c}`)}</TableCell>
                  ))}
                  <TableCell align="right">{t('informes.sla.vencidos.retraso')}</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {visibles.map((v) => {
                  const { dias, horas } = desgloseDeRetraso(v.horasDeRetraso);
                  return (
                    <TableRow key={`${v.expedienteId}|${v.areaId}`}>
                      <TableCell>
                        <Link
                          component={RouterLink}
                          to={`/e/${empresaId}/expedientes/${v.expedienteId}`}
                          aria-label={t('informes.sla.vencidos.abrir', { folio: v.folio, tercero: v.tercero })}
                          sx={{ fontFamily: (th) => th.typography.monoTabla.fontFamily }}
                        >
                          {v.folio}
                        </Link>
                      </TableCell>
                      <TableCell>{v.tercero}</TableCell>
                      <TableCell>{v.areaNombre}</TableCell>
                      <TableCell>
                        <Typography variant="monoTabla">{formatearFechaHora(v.vence)}</Typography>
                      </TableCell>
                      <TableCell align="right">{dias === 0 ? t('informes.sla.vencidos.retrasoHoras', { count: horas }) : t('informes.sla.vencidos.retrasoDias', { dias, horas })}</TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </Box>
        </MarcoTabla>
      )}
      {vencidos.length > MAXIMO_VENCIDOS_VISIBLES && (
        <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', mt: 1 }}>
          {t('informes.sla.vencidos.hayMas', { maximo: MAXIMO_VENCIDOS_VISIBLES })}
        </Typography>
      )}
    </Tarjeta>
  );
}
