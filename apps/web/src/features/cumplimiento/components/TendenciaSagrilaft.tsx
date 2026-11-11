import { Box, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import type { MesDeCumplimientoDto } from '@validasoft/api-client';
import { visualmenteOculto } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { etiquetaDeMes, maximoDeTendencia, porcentajeDe } from '../informeSagrilaft';

const ALTO = 112;
const COLOR_ESTUDIOS = 'acento.main';
const COLOR_CONSULTAS = 'estado.info.solido';

function Leyenda({ color, texto }: { color: string; texto: string }) {
  return (
    <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
      <Box aria-hidden sx={{ width: 10, height: 10, borderRadius: '2px', bgcolor: color }} />
      <Typography variant="body2" component="span" sx={{ color: 'text.secondary' }}>
        {texto}
      </Typography>
    </Stack>
  );
}

/**
 * Estudios cerrados y consultas de listas de los últimos 12 meses como barras en pares. Las barras son decorativas: el dato
 * accesible va en una tabla visualmente oculta con una fila por mes.
 */
export function TendenciaSagrilaft({ meses }: { meses: readonly MesDeCumplimientoDto[] }) {
  const { t } = useTranslation();
  const maximo = maximoDeTendencia(meses);
  return (
    <Tarjeta titulo={t('cumplimiento.sagrilaft.tendencia.titulo')} etiqueta={t('cumplimiento.sagrilaft.tendencia.titulo')} nivel="h3">
      {maximo === 0 ? (
        <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('cumplimiento.sagrilaft.tendencia.sinDatos')}</Typography>
      ) : (
        <>
          <Stack direction="row" spacing={2} sx={{ mb: 1 }}>
            <Leyenda color={COLOR_ESTUDIOS} texto={t('cumplimiento.sagrilaft.tendencia.estudios')} />
            <Leyenda color={COLOR_CONSULTAS} texto={t('cumplimiento.sagrilaft.tendencia.consultas')} />
          </Stack>
          <Box aria-hidden sx={{ display: 'grid', gridTemplateColumns: `repeat(${String(meses.length)}, minmax(0, 1fr))`, gap: { xs: '4px', sm: '10px' }, alignItems: 'end' }}>
            {meses.map((m, i) => (
              <Box key={m.mes} title={t('cumplimiento.sagrilaft.tendencia.barra', { mes: etiquetaDeMes(m.mes, true), estudios: String(m.estudios), consultas: String(m.consultas) })}>
                <Box sx={{ height: ALTO, display: 'flex', alignItems: 'flex-end', justifyContent: 'center', gap: '2px', borderBottom: 1, borderColor: 'divider' }}>
                  <Box data-barra="estudios" sx={{ flex: 1, maxWidth: 14, height: `${String(porcentajeDe(m.estudios, maximo))}%`, bgcolor: COLOR_ESTUDIOS, borderRadius: '3px 3px 0 0' }} />
                  <Box data-barra="consultas" sx={{ flex: 1, maxWidth: 14, height: `${String(porcentajeDe(m.consultas, maximo))}%`, bgcolor: COLOR_CONSULTAS, borderRadius: '3px 3px 0 0' }} />
                </Box>
                <Typography component="span" sx={{ display: 'block', textAlign: 'center', fontSize: 10, lineHeight: '16px', color: 'text.secondary', mt: 0.25, whiteSpace: 'nowrap' }}>
                  {etiquetaDeMes(m.mes, i === 0 || m.mes.endsWith('-01'))}
                </Typography>
              </Box>
            ))}
          </Box>
          <Table size="small" aria-label={t('cumplimiento.sagrilaft.tendencia.titulo')} sx={visualmenteOculto}>
            <TableHead>
              <TableRow>
                <TableCell>{t('cumplimiento.sagrilaft.tendencia.titulo')}</TableCell>
                <TableCell>{t('cumplimiento.sagrilaft.tendencia.estudios')}</TableCell>
                <TableCell>{t('cumplimiento.sagrilaft.tendencia.consultas')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {meses.map((m) => (
                <TableRow key={m.mes}>
                  <TableCell>{etiquetaDeMes(m.mes, true)}</TableCell>
                  <TableCell>{m.estudios}</TableCell>
                  <TableCell>{m.consultas}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </>
      )}
    </Tarjeta>
  );
}
