import { Box, FormHelperText, FormLabel, MenuItem, Stack, TextField, ToggleButton, ToggleButtonGroup } from '@mui/material';
import type { DebidaDiligencia } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { claveRiesgo, DEBIDAS_EN_ORDEN, type BorradorEstudio, type ErrorEstudio } from '../modelo';

export interface SeccionRiesgoManualProps {
  borrador: BorradorEstudio;
  cambiar: (cambio: Partial<BorradorEstudio>) => void;
  visible: (codigo: ErrorEstudio) => boolean;
}

/** Sin matriz activa: el oficial elige el nivel de riesgo (1–3) y la debida diligencia a mano, como en el legado. */
export function SeccionRiesgoManual({ borrador, cambiar, visible }: SeccionRiesgoManualProps) {
  const { t } = useTranslation();
  return (
    <Stack spacing={2.5}>
      <Box>
        <FormLabel id="riesgo" sx={{ fontSize: 13 }}>
          {t('cumplimiento.panel.estudio.riesgo')}
        </FormLabel>
        <ToggleButtonGroup
          exclusive
          size="small"
          aria-labelledby="riesgo"
          value={borrador.nivelRiesgo}
          onChange={(_, valor: 1 | 2 | 3 | null) => cambiar({ nivelRiesgo: valor })}
          sx={{ display: 'flex', mt: 0.5 }}
        >
          {([1, 2, 3] as const).map((n) => (
            <ToggleButton key={n} value={n} sx={{ flex: 1 }}>
              {t(`cumplimiento.riesgos.${claveRiesgo(n)}`)}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>
        {visible('nivel-riesgo-requerido') && <FormHelperText error>{t('cumplimiento.errores.nivel-riesgo-requerido')}</FormHelperText>}
      </Box>
      <TextField
        select
        label={t('cumplimiento.panel.estudio.debida')}
        value={borrador.debidaDiligencia}
        onChange={(e) => cambiar({ debidaDiligencia: e.target.value as DebidaDiligencia })}
        error={visible('debida-diligencia-ampliada-requerida')}
        helperText={visible('debida-diligencia-ampliada-requerida') ? t('cumplimiento.errores.debida-diligencia-ampliada-requerida') : t('cumplimiento.matriz.sinMatriz')}
      >
        {DEBIDAS_EN_ORDEN.map((d) => (
          <MenuItem key={d} value={d}>
            {t(`cumplimiento.debida.${d}`)}
          </MenuItem>
        ))}
      </TextField>
    </Stack>
  );
}
