import { Alert, AlertTitle, Stack, Typography } from '@mui/material';
import { useTranslation } from 'react-i18next';
import { ConfirmarRatificacion } from '../expediente/ConfirmarRatificacion';
import type { RatificacionPendiente } from './useRatificacionesPendientes';

/**
 * Arriba del formulario de una sección: las empresas que le piden al tercero ratificar esta información (renovación periódica del
 * vínculo, VV-3). Ratificar confirma que sigue igual sin tocar ningún dato; si cambió, se edita y se guarda como siempre.
 */
export function RatificacionesDeSeccion({ pendientes }: { pendientes: readonly RatificacionPendiente[] }) {
  const { t } = useTranslation();
  if (pendientes.length === 0) return null;
  return (
    <Stack spacing={1.5}>
      {pendientes.map((p) => {
        const titulo = t('expediente.ratificar.seccion.enSeccionTitulo', { empresa: p.empresa });
        return (
          <Alert key={p.requisitoId} severity="info" aria-label={titulo}>
            <AlertTitle>{titulo}</AlertTitle>
            <Typography sx={{ fontSize: 14, mb: 1 }}>{t('expediente.ratificar.seccion.enSeccionTexto', { empresa: p.empresa })}</Typography>
            <ConfirmarRatificacion tipo="seccion" compacto expedienteId={p.expedienteId} requisitoId={p.requisitoId} documento={p.nombre} empresa={p.empresa} />
          </Alert>
        );
      })}
    </Stack>
  );
}
