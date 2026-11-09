import { Box, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';

/** Qué es una validación, qué pasa al agotar el cupo y cuándo llegan los avisos (IA-3, IA-4). */
export function AyudaUsoIa() {
  const { t } = useTranslation();
  return (
    <Box component="section" aria-labelledby="titulo-ayuda-ia" sx={{ mt: 3, maxWidth: 720 }}>
      <Typography variant="h4" component="h2" id="titulo-ayuda-ia" sx={{ mb: 0.75 }}>
        {t('ia.uso.comoFunciona')}
      </Typography>
      <Box component="ul" sx={{ m: 0, pl: 2.5, color: 'text.secondary', '& li': { mb: 0.5 } }}>
        <Typography component="li" variant="body2">
          {t('ia.uso.ayudaValidacion')}
        </Typography>
        <Typography component="li" variant="body2">
          {t('ia.uso.ayudaCupo')}
        </Typography>
        <Typography component="li" variant="body2">
          {t('ia.uso.ayudaAvisos')}
        </Typography>
      </Box>
    </Box>
  );
}
