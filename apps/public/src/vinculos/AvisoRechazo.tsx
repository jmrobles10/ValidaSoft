import { Alert, Box, Button, Stack, Typography } from '@mui/material';
import type { VinculoPublicoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { RotateCcw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { mensajeDeError } from '../shared/errores';
import { useReintentarPostulacion } from '../shared/api';

/**
 * El rechazo de una empresa (DS-4): cuándo y con qué motivos (los textos que la empresa le comunicó, nunca áreas ni revisores)
 * y, si el flujo lo permite, «Volver a postularme» (con su espera en días); si no, cómo seguir. Compacto: va dentro de la
 * tarjeta de la empresa.
 */
export function AvisoRechazo({ vinculo }: { vinculo: VinculoPublicoDto }) {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const reintentar = useReintentarPostulacion(vinculo.id);
  const reintento = vinculo.reintento;
  if (!reintento) return null;

  // Con `mutateAsync` y no con el `onSuccess` de `mutate`: ese callback se pierde si el aviso se desmonta antes de la respuesta, y se desmonta
  // cuando el aviso en tiempo real refresca la lista (el vínculo ya no trae el rechazo) y esa lista llega antes que la respuesta del POST.
  // La promesa se cumple siempre; el error ya lo muestra `reintentar.isError`.
  const postular = () => {
    reintentar.mutateAsync().then(
      (expediente) => void navegar(`/expediente/${expediente.id}`),
      () => undefined,
    );
  };

  return (
    <Box
      sx={{
        mt: 1.5,
        p: 1.5,
        border: 1,
        borderColor: 'divider',
        borderRadius: (th) => `${String(th.radio.sm)}px`,
        bgcolor: 'background.default',
      }}
    >
      <Typography sx={{ fontSize: 14, fontWeight: 600, lineHeight: '20px' }}>
        {t('rechazo.titulo', { empresa: vinculo.empresa.nombre, fecha: formatearFecha(reintento.rechazadoEn) })}
      </Typography>
      {reintento.motivos.length > 0 && (
        <Box component="ul" sx={{ m: 0, mt: 0.5, pl: 2.5, color: 'text.secondary', fontSize: 14, lineHeight: '20px' }}>
          {reintento.motivos.map((motivo) => (
            <li key={motivo.codigo}>{motivo.texto}</li>
          ))}
        </Box>
      )}

      {reintentar.isError && (
        <Alert severity="error" sx={{ mt: 1 }}>
          {mensajeDeError(reintentar.error, t)}
        </Alert>
      )}

      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mt: 1.5, alignItems: { sm: 'center' } }}>
        {reintento.disponible ? (
          <>
            <Button
              variant="contained"
              size="small"
              startIcon={<RotateCcw size={14} aria-hidden />}
              onClick={postular}
              disabled={reintentar.isPending}
              sx={{ alignSelf: { xs: 'stretch', sm: 'auto' } }}
            >
              {t('rechazo.volver')}
            </Button>
            <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('rechazo.volverAyuda')}</Typography>
          </>
        ) : (
          <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>
            {reintento.permitido && reintento.desde
              ? t('rechazo.enEspera', { fecha: formatearFecha(reintento.desde) })
              : t('rechazo.noPermitido', { empresa: vinculo.empresa.nombre })}
          </Typography>
        )}
      </Stack>
    </Box>
  );
}
