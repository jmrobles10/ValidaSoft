import { Box, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import type { ResultadoDeMatrizDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { claveRiesgo, formatearPuntaje } from '../modelo';
import { ChipNivelRiesgo } from './ChipNivelRiesgo';

export interface ResultadoMatrizProps {
  resultado: ResultadoDeMatrizDto;
  /** Se está recalculando: se conserva lo anterior, atenuado, hasta que llegue lo nuevo. */
  recalculando?: boolean;
}

/** Lo que dio la matriz: puntaje, nivel (con su piso si una opción lo subió), la debida diligencia que exige y el desglose por factor. */
export function ResultadoMatriz({ resultado: r, recalculando = false }: ResultadoMatrizProps) {
  const { t } = useTranslation();
  return (
    <Box
      aria-label={t('cumplimiento.matriz.resultado')}
      aria-busy={recalculando || undefined}
      sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5, opacity: recalculando ? 0.6 : 1, transition: 'opacity 120ms' }}
    >
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'repeat(2, minmax(0, 1fr))', sm: 'repeat(3, minmax(0, 1fr))' }, gap: 2 }}>
        <Stack spacing={0.25}>
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {t('cumplimiento.matriz.puntaje')}
          </Typography>
          <Typography sx={{ fontSize: 20, fontWeight: 600, lineHeight: '28px' }}>{t('cumplimiento.matriz.puntajeDe', { puntaje: formatearPuntaje(r.puntaje) })}</Typography>
        </Stack>
        <Stack spacing={0.5} sx={{ alignItems: 'flex-start' }}>
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {t('cumplimiento.matriz.nivel')}
          </Typography>
          <ChipNivelRiesgo nivel={r.nivel} />
        </Stack>
        <Stack spacing={0.25} sx={{ gridColumn: { xs: '1 / -1', sm: 'auto' } }}>
          <Typography variant="caption" sx={{ color: 'text.secondary' }}>
            {t('cumplimiento.matriz.debida')}
          </Typography>
          <Typography sx={{ fontSize: 14 }}>{t(`cumplimiento.debida.${r.debidaDiligencia}`)}</Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('cumplimiento.matriz.revision', { meses: String(r.mesesRevision) })}</Typography>
        </Stack>
      </Box>
      {r.forzadoPor && (
        <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 1 }}>
          {t('cumplimiento.matriz.piso', { causa: r.forzadoPor, nivel: t(`cumplimiento.riesgos.${claveRiesgo(r.nivelPorPuntaje)}`) })}
        </Typography>
      )}
      <Box component="details" sx={{ mt: 1.5, '& > summary': { cursor: 'pointer', color: 'text.secondary', fontSize: 13 } }}>
        <summary>{t('cumplimiento.matriz.verCalculo')}</summary>
        <Box sx={{ overflowX: 'auto', mt: 1 }}>
          <Table size="small" aria-label={t('cumplimiento.matriz.verCalculo')}>
            <TableHead>
              <TableRow>
                {(['factor', 'opcion', 'puntaje', 'peso'] as const).map((c) => (
                  <TableCell key={c} align={c === 'puntaje' || c === 'peso' ? 'right' : 'left'}>
                    {t(`cumplimiento.matriz.columnas.${c}`)}
                  </TableCell>
                ))}
              </TableRow>
            </TableHead>
            <TableBody>
              {r.desglose.map((d) => (
                <TableRow key={d.factor}>
                  <TableCell>
                    {d.factorNombre}
                    {d.origen === 'Automatico' && (
                      <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
                        {` · ${t('cumplimiento.matriz.automatico')}`}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>{d.opcionNombre}</TableCell>
                  <TableCell align="right">{d.puntaje}</TableCell>
                  <TableCell align="right">{d.peso}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Box>
        <Typography sx={{ color: 'text.secondary', fontSize: 12, mt: 0.5 }}>{t('cumplimiento.matriz.version', { version: String(r.versionMatriz) })}</Typography>
      </Box>
    </Box>
  );
}
