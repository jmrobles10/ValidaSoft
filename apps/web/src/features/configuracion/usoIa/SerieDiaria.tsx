import { Box, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import type { UsoIaDiaDto } from '@validasoft/api-client';
import { visualmenteOculto } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { formatearConteo, serieDelMes } from './modelo';

const ALTO = 120;
const COLOR_INCLUIDAS = 'acento.main';
const COLOR_ADICIONALES = 'estado.advertencia.solido';

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
 * Validaciones por día del mes (incluidas y adicionales apiladas) como barras simples. Las barras son decorativas: el
 * dato accesible va en una tabla visualmente oculta con una fila por día con uso.
 */
export function SerieDiaria({ mes, porDia }: { mes: string; porDia: readonly UsoIaDiaDto[] }) {
  const { t } = useTranslation();
  const serie = serieDelMes(mes, porDia);
  const maximo = Math.max(1, ...serie.map((d) => d.incluidas + d.adicionales));
  const conUso = serie.filter((d) => d.incluidas + d.adicionales > 0);

  return (
    <Tarjeta titulo={t('ia.uso.porDia')} etiqueta={t('ia.uso.porDia')} nivel="h3">
      {conUso.length === 0 ? (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('ia.uso.porDiaVacio')}
        </Typography>
      ) : (
        <>
          <Stack direction="row" spacing={2} sx={{ mb: 1 }}>
            <Leyenda color={COLOR_INCLUIDAS} texto={t('ia.uso.leyendaIncluidas')} />
            <Leyenda color={COLOR_ADICIONALES} texto={t('ia.uso.leyendaAdicionales')} />
          </Stack>
          <Box aria-hidden sx={{ display: 'grid', gridTemplateColumns: `repeat(${String(serie.length)}, minmax(0, 1fr))`, gap: '2px', alignItems: 'end' }}>
            {serie.map((d) => (
              <Box key={d.dia} title={t('ia.uso.barra', { dia: String(d.dia), incluidas: String(d.incluidas), adicionales: String(d.adicionales) })}>
                <Box sx={{ height: ALTO, display: 'flex', flexDirection: 'column', justifyContent: 'flex-end', bgcolor: 'superficie.nivel2', borderRadius: '2px', overflow: 'hidden' }}>
                  <Box data-barra="adicionales" sx={{ height: `${String((d.adicionales / maximo) * 100)}%`, bgcolor: COLOR_ADICIONALES }} />
                  <Box data-barra="incluidas" sx={{ height: `${String((d.incluidas / maximo) * 100)}%`, bgcolor: COLOR_INCLUIDAS }} />
                </Box>
                <Typography component="span" sx={{ display: 'block', textAlign: 'center', fontSize: 10, lineHeight: '16px', color: 'text.secondary' }}>
                  {d.dia === 1 || d.dia % 5 === 0 ? d.dia : ''}
                </Typography>
              </Box>
            ))}
          </Box>
          <Table size="small" aria-label={t('ia.uso.porDia')} sx={visualmenteOculto}>
            <TableHead>
              <TableRow>
                <TableCell>{t('ia.uso.dia')}</TableCell>
                <TableCell>{t('ia.uso.leyendaIncluidas')}</TableCell>
                <TableCell>{t('ia.uso.leyendaAdicionales')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {conUso.map((d) => (
                <TableRow key={d.dia}>
                  <TableCell>{d.dia}</TableCell>
                  <TableCell>{formatearConteo(d.incluidas)}</TableCell>
                  <TableCell>{formatearConteo(d.adicionales)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </>
      )}
    </Tarjeta>
  );
}
