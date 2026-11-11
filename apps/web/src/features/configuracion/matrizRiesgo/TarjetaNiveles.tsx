import { Box, MenuItem, Stack, TextField, Typography } from '@mui/material';
import type { DebidaDiligencia } from '@validasoft/api-client';
import { visualmenteOculto } from '@validasoft/ui';
import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { ChipNivelRiesgo } from '../../cumplimiento/components/ChipNivelRiesgo';
import { claveRiesgo } from '../../cumplimiento/modelo';
import { CampoDigitos } from './FilaOpcion';
import { DEBIDAS, erroresDe, type BorradorNivel, type ErrorMatriz, type Nivel } from './modelo';

const COLUMNAS = { xs: 'minmax(0, 1fr) 110px', sm: '120px minmax(0, 1fr) 140px' } as const;

export interface TarjetaNivelesProps {
  niveles: readonly BorradorNivel[];
  errores: readonly ErrorMatriz[];
  deshabilitado: boolean;
  onCambiar: (nivel: Nivel, cambio: { debidaDiligencia?: DebidaDiligencia; meses?: string }) => void;
}

function FilaNivel({ nivel: n, errores, deshabilitado, onCambiar }: { nivel: BorradorNivel; errores: readonly ErrorMatriz[]; deshabilitado: boolean; onCambiar: TarjetaNivelesProps['onCambiar'] }) {
  const { t } = useTranslation();
  const idDebida = useId();
  const errorMeses = errores.some((e) => e.codigo === 'meses');
  return (
    <>
      <Box sx={{ gridColumn: { xs: '1 / -1', sm: 'auto' }, pt: { sm: '6px' } }}>
        <ChipNivelRiesgo nivel={n.nivel} />
      </Box>
      <Box component="span" id={idDebida} sx={visualmenteOculto}>
        {t('matrizRiesgo.niveles.debida')} · {t(`cumplimiento.riesgos.${claveRiesgo(n.nivel)}`)}
      </Box>
      <TextField
        select
        value={n.debidaDiligencia}
        onChange={(e) => onCambiar(n.nivel, { debidaDiligencia: e.target.value as DebidaDiligencia })}
        disabled={deshabilitado || n.nivel === 3}
        slotProps={{ select: { labelId: idDebida } }}
      >
        {DEBIDAS.map((d) => (
          <MenuItem key={d} value={d}>
            {t(`cumplimiento.debida.${d}`)}
          </MenuItem>
        ))}
      </TextField>
      <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
        <CampoDigitos valor={n.meses} onCambiar={(meses) => onCambiar(n.nivel, { meses })} etiqueta={`${t('matrizRiesgo.niveles.meses')} · ${t(`cumplimiento.riesgos.${claveRiesgo(n.nivel)}`)}`} error={errorMeses} deshabilitado={deshabilitado} maxDigitos={2} ancho={72} />
        <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
          {t('cumplimiento.mesesAbreviado')}
        </Typography>
      </Stack>
    </>
  );
}

/** Qué exige cada nivel: la debida diligencia (simplificada, ordinaria o intensificada) y cada cuántos meses se revisa. */
export function TarjetaNiveles({ niveles, errores, deshabilitado, onCambiar }: TarjetaNivelesProps) {
  const { t } = useTranslation();
  const generales = errores.filter((e) => e.codigo === 'debida-descendente' || e.codigo === 'alto-intensificada');
  return (
    <Stack spacing={1.5}>
      <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('matrizRiesgo.niveles.ayuda')}</Typography>
      <Box sx={{ display: { xs: 'none', sm: 'grid' }, gridTemplateColumns: COLUMNAS, gap: 1.5, color: 'text.secondary', fontSize: 12, fontWeight: 500 }} aria-hidden>
        <span />
        <span>{t('matrizRiesgo.niveles.debida')}</span>
        <span>{t('matrizRiesgo.niveles.meses')}</span>
      </Box>
      <Box sx={{ display: 'grid', gridTemplateColumns: COLUMNAS, gap: 1.5, alignItems: 'start' }}>
        {niveles.map((n) => (
          <FilaNivel key={n.nivel} nivel={n} errores={erroresDe(errores, { nivel: n.nivel })} deshabilitado={deshabilitado} onCambiar={onCambiar} />
        ))}
      </Box>
      {[...new Set(generales.map((e) => e.codigo))].map((codigo) => (
        <Typography key={codigo} role="alert" sx={{ color: 'error.main', fontSize: 12 }}>
          {t(`matrizRiesgo.validacion.${codigo}`)}
        </Typography>
      ))}
      <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('matrizRiesgo.niveles.mesesAyuda')}</Typography>
    </Stack>
  );
}
