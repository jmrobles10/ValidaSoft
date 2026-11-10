import { Alert, AlertTitle, Box, Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { etiquetaDeCampo } from './etiquetas';
import type { ObservacionDeEmpresa } from './useObservacionesDeSeccion';

/**
 * Arriba del formulario: lo que cada empresa observó de esta sección (motivos, su nota y los datos que señaló). Los
 * datos señalados también quedan marcados en el formulario hasta que el tercero los toque.
 */
export function ObservacionesDeEmpresas({ observaciones }: { observaciones: readonly ObservacionDeEmpresa[] }) {
  const { t } = useTranslation();
  if (observaciones.length === 0) return null;
  return (
    <Stack spacing={1.5}>
      {observaciones.map(({ empresa, expedienteId, observacion }) => {
        const titulo = t('ficha.observada.titulo', { empresa });
        const campos = observacion.campos ?? [];
        return (
          <Alert key={`${expedienteId}-${observacion.fecha}`} severity="warning" aria-label={titulo}>
            <AlertTitle>{titulo}</AlertTitle>
            <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
              {observacion.motivos.map((m) => (
                <li key={m.codigo}>{m.texto}</li>
              ))}
            </Box>
            {observacion.observacion && <Typography sx={{ mt: 0.5, fontSize: 14, whiteSpace: 'pre-wrap' }}>“{observacion.observacion}”</Typography>}
            {campos.length > 0 && (
              <Typography sx={{ mt: 0.5, fontSize: 14 }}>{t('ficha.observada.campos', { campos: campos.map((c) => etiquetaDeCampo(c, t)).join(', ') })}</Typography>
            )}
          </Alert>
        );
      })}
    </Stack>
  );
}
