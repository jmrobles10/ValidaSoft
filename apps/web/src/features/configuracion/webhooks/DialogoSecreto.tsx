import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, Stack, Tooltip, Typography } from '@mui/material';
import { formatearFecha } from '@validasoft/ui';
import { Check, Copy } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * El secreto de firma se muestra **una sola vez** (al crear y al rotar): se copia aquí y se guarda en el sistema que
 * recibe. No se puede cerrar con un clic afuera para que nadie lo pierda sin querer.
 */
export function DialogoSecreto({ secreto, anteriorVence, onCerrar }: { secreto: string; anteriorVence: string | null; onCerrar: () => void }) {
  const { t } = useTranslation();
  const [copiado, setCopiado] = useState(false);

  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(secreto);
      setCopiado(true);
    } catch {
      setCopiado(false);
    }
  };

  return (
    <Dialog open maxWidth={false} slotProps={{ paper: { sx: { width: 540, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-secreto">
      <DialogTitle id="titulo-secreto">{t('webhooks.secreto.titulo')}</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Alert severity="warning">{t('webhooks.secreto.unaVez')}</Alert>
          <Stack direction="row" alignItems="center" spacing={1} sx={{ px: 1.5, py: 1, bgcolor: 'superficie.nivel2', borderRadius: (th) => `${String(th.radio.sm)}px` }}>
            <Box component="code" sx={{ flex: 1, minWidth: 0, fontFamily: (th) => th.typography.mono.fontFamily, fontSize: 13, overflowWrap: 'anywhere' }} data-secreto="">
              {secreto}
            </Box>
            <Tooltip title={copiado ? t('webhooks.secreto.copiado') : t('webhooks.secreto.copiar')}>
              <IconButton aria-label={t('webhooks.secreto.copiar')} onClick={() => void copiar()}>
                {copiado ? <Check size={18} aria-hidden /> : <Copy size={18} aria-hidden />}
              </IconButton>
            </Tooltip>
          </Stack>
          <Typography variant="body2" color="text.secondary">
            {t('webhooks.secreto.comoVerificar')}
          </Typography>
          {anteriorVence && (
            <Typography variant="body2" color="text.secondary">
              {t('webhooks.secreto.anteriorVence', { fecha: formatearFecha(anteriorVence) })}
            </Typography>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onCerrar}>
          {t('webhooks.secreto.listo')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
