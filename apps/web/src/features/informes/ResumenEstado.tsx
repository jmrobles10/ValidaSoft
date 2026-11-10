import { Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import type { ResumenEstadoExpedientesDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../shared/componentes';
import { Cifra, GrupoCifras } from '../inicio/Cifras';
import { formatearDias } from './modelo';

/** Totales del informe (sobre todas las filas, no solo la página) y los tiempos de cada área. */
export function ResumenEstado({ resumen }: { resumen: ResumenEstadoExpedientesDto }) {
  const { t } = useTranslation();
  const sinDato = t('informes.sinDato');
  const dias = (valor: number | string | null) => formatearDias(valor, sinDato);

  return (
    <Stack spacing={1.5} sx={{ mb: 2 }}>
      <GrupoCifras titulo={t('informes.estado.resumen.titulo')} nota={t('informes.estado.resumen.nota')}>
        <Cifra etiqueta={t('informes.estado.resumen.total')} valor={String(resumen.total)} />
        <Cifra etiqueta={t('informes.estado.resumen.aprobados')} valor={String(resumen.aprobados)} />
        <Cifra etiqueta={t('informes.estado.resumen.promedio')} valor={dias(resumen.tiempoPromedioDias)} />
        <Cifra etiqueta={t('informes.estado.resumen.mediana')} valor={dias(resumen.tiempoMedianaDias)} />
        <Cifra etiqueta={t('informes.estado.resumen.carga')} valor={dias(resumen.promedioEnCargaDias)} />
        <Cifra etiqueta={t('informes.estado.resumen.tercero')} valor={dias(resumen.promedioConTerceroDias)} />
        <Cifra etiqueta={t('informes.estado.resumen.revision')} valor={dias(resumen.promedioEnRevisionDias)} />
        <Cifra etiqueta={t('informes.estado.resumen.devoluciones')} valor={String(resumen.devoluciones)} />
        <Cifra etiqueta={t('informes.estado.resumen.sla')} valor={String(resumen.conSlaIncumplido)} />
      </GrupoCifras>

      <Tarjeta titulo={t('informes.estado.areas.titulo')} nivel="h3">
        {resumen.porArea.length === 0 ? (
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('informes.estado.areas.vacio')}
          </Typography>
        ) : (
          <Table size="small" aria-label={t('informes.estado.areas.titulo')}>
            <TableHead>
              <TableRow>
                <TableCell>{t('informes.estado.areas.area')}</TableCell>
                {(['expedientes', 'pendientes', 'promedio', 'mediana', 'maximo', 'sla'] as const).map((c) => (
                  <TableCell key={c} align="right">
                    {t(`informes.estado.areas.${c}`)}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {resumen.porArea.map((a) => (
                <TableRow key={a.areaId}>
                  <TableCell>{a.nombre}</TableCell>
                  <TableCell align="right">{a.expedientes}</TableCell>
                  <TableCell align="right">{a.pendientes}</TableCell>
                  <TableCell align="right">{dias(a.promedioDias)}</TableCell>
                  <TableCell align="right">{dias(a.medianaDias)}</TableCell>
                  <TableCell align="right">{dias(a.maximoDias)}</TableCell>
                  <TableCell align="right">{a.slaIncumplidos}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Tarjeta>
    </Stack>
  );
}
