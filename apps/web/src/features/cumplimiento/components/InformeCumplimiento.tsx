import { Stack, ToggleButton, ToggleButtonGroup } from '@mui/material';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { InformeSagrilaft } from './InformeSagrilaft';
import { InformeVerificaciones } from './InformeVerificaciones';

type Vista = 'sagrilaft' | 'verificaciones';

/**
 * Informe de cumplimiento: por omisión el SAGRILAFT completo (situación de cada tercero, riesgo, tendencia y tiempos) y, como
 * segunda vista, el de consultas y estudios del periodo (VE-14).
 */
export function InformeCumplimiento({ empresaId }: { empresaId: string }) {
  const { t } = useTranslation();
  const [vista, setVista] = useState<Vista>('sagrilaft');
  return (
    <Stack spacing={2}>
      <ToggleButtonGroup
        exclusive
        size="small"
        value={vista}
        onChange={(_, nueva: Vista | null) => nueva && setVista(nueva)}
        aria-label={t('cumplimiento.pestanas.informe')}
        sx={{ alignSelf: 'flex-start' }}
      >
        {(['sagrilaft', 'verificaciones'] as const).map((v) => (
          <ToggleButton key={v} value={v}>
            {t(`cumplimiento.informeVistas.${v}`)}
          </ToggleButton>
        ))}
      </ToggleButtonGroup>
      {vista === 'sagrilaft' ? <InformeSagrilaft empresaId={empresaId} /> : <InformeVerificaciones empresaId={empresaId} />}
    </Stack>
  );
}
