import { Typography } from '@mui/material';
import type { FlujoDeExpedienteDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { explicarOrigenDelFlujo } from '../origenFlujo';

/** Explica en una línea por qué el expediente tiene ese flujo: elegido, escogido por una regla (con su condición) o el por defecto. */
export function OrigenDelFlujo({ flujo }: { flujo: FlujoDeExpedienteDto }) {
  const { t } = useTranslation();
  return (
    <Typography variant="body2" data-origen-flujo={flujo.origen} sx={{ color: 'text.secondary', display: 'block', fontSize: 12, mt: 0.25 }}>
      {explicarOrigenDelFlujo(flujo, t)}
    </Typography>
  );
}
