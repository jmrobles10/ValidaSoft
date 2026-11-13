import { Button, Link, Stack, TableCell, TableRow, Typography } from '@mui/material';
import type { FilaTerceroSagrilafDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { estadoCanonico, estadoDeEstudio, estadoDeListas, revisionDe } from '../informeSagrilaft';
import { ChipNivelRiesgo } from './ChipNivelRiesgo';

const SECUNDARIO = { color: 'text.secondary', fontSize: 12 } as const;

/** Un tercero del informe: sus listas, su estudio, su riesgo, cuándo toca la próxima revisión y sus personas. */
export function FilaSagrilaft({ fila: f, empresaId, onVerPersonas }: { fila: FilaTerceroSagrilafDto; empresaId: string; onVerPersonas: () => void }) {
  const { t } = useTranslation();
  const listas = estadoDeListas(f);
  const estudio = estadoDeEstudio(f);
  const revision = revisionDe(f);
  const debida = f.debidaDiligencia as 'Simple' | null;

  return (
    <TableRow hover>
      <TableCell>
        <Link component={RouterLink} to={`/e/${empresaId}/vinculos/${f.vinculoId}`} aria-label={t('cumplimiento.sagrilaft.tabla.abrir', { tercero: f.tercero })} sx={{ fontWeight: 500, fontSize: 14 }}>
          {f.tercero}
        </Link>
        <Typography sx={SECUNDARIO}>{`${f.tipoIdentificacion} ${f.identificacion} · ${f.tipoVinculo}`}</Typography>
        <Typography component="span" variant="mono" sx={SECUNDARIO}>
          {f.folio}
        </Typography>
      </TableCell>
      <TableCell>
        <ChipEstado estado={estadoCanonico(listas)} etiqueta={t(`cumplimiento.sagrilaft.tabla.listas.${listas}`)} />
        <Typography sx={{ ...SECUNDARIO, mt: 0.5 }}>
          {f.ultimaConsultaListas ? t('cumplimiento.sagrilaft.tabla.ultimaConsulta', { fecha: formatearFecha(f.ultimaConsultaListas) }) : t('cumplimiento.sagrilaft.tabla.sinConsulta')}
        </Typography>
        {f.coincidenciasPorAnalizar > 0 && (
          <Typography sx={{ ...SECUNDARIO, color: 'warning.main' }}>{t('cumplimiento.sagrilaft.tabla.porAnalizar', { count: f.coincidenciasPorAnalizar })}</Typography>
        )}
      </TableCell>
      <TableCell>
        <ChipEstado estado={estadoCanonico(estudio)} etiqueta={t(`cumplimiento.sagrilaft.tabla.estudios.${estudio}`)} />
        <Typography sx={{ ...SECUNDARIO, mt: 0.5 }}>
          {f.fechaEstudio && f.concepto
            ? t('cumplimiento.sagrilaft.tabla.estudiadoEl', { concepto: t(`cumplimiento.conceptos.${f.concepto as 'Favorable'}`, { defaultValue: f.concepto }), fecha: formatearFecha(f.fechaEstudio) })
            : t('cumplimiento.sagrilaft.tabla.sinEstudioTexto')}
        </Typography>
        {f.conflictoDeInteres === true && <Typography sx={{ ...SECUNDARIO, color: 'warning.main' }}>{t('cumplimiento.sagrilaft.tabla.conflicto')}</Typography>}
      </TableCell>
      <TableCell>
        {f.nivelRiesgo === null ? (
          '—'
        ) : (
          <Stack spacing={0.5} sx={{ alignItems: 'flex-start' }}>
            <ChipNivelRiesgo nivel={f.nivelRiesgo} />
            {debida && <Typography sx={SECUNDARIO}>{t(`cumplimiento.debida.${debida}`, { defaultValue: f.debidaDiligencia ?? '' })}</Typography>}
          </Stack>
        )}
      </TableCell>
      <TableCell>
        {revision.tipo === 'sin-fecha' ? (
          '—'
        ) : (
          <>
            {formatearFecha(f.proximaRevision ?? '')}
            <Typography sx={{ ...SECUNDARIO, color: revision.tipo === 'vencida' ? 'error.main' : 'text.secondary' }}>
              {revision.tipo === 'vencida'
                ? t('cumplimiento.sagrilaft.tabla.revisionVencida', { count: revision.dias })
                : revision.tipo === 'hoy'
                  ? t('cumplimiento.sagrilaft.tabla.revisionHoy')
                  : t('cumplimiento.sagrilaft.tabla.revisionVence', { count: revision.dias })}
            </Typography>
          </>
        )}
      </TableCell>
      <TableCell align="right">
        {f.personas === 0 ? (
          '—'
        ) : (
          <Stack spacing={0.25} sx={{ alignItems: 'flex-end' }}>
            <Button size="small" variant="text" onClick={onVerPersonas} aria-label={t('cumplimiento.sagrilaft.tabla.verPersonas', { tercero: f.tercero })}>
              {f.personas}
            </Button>
            {f.pep > 0 && <Typography sx={{ ...SECUNDARIO, color: 'warning.main' }}>{t('cumplimiento.sagrilaft.tabla.pep', { count: f.pep })}</Typography>}
            {(f.pepSinDeclaracion ?? 0) > 0 && <Typography sx={{ ...SECUNDARIO, color: 'error.main' }}>{t('cumplimiento.sagrilaft.tabla.pepSinDeclaracion', { count: f.pepSinDeclaracion ?? 0 })}</Typography>}
          </Stack>
        )}
      </TableCell>
    </TableRow>
  );
}
