import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, Stack, Typography } from '@mui/material';
import { ShieldOff } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/** Página 403 propia (docs/06 §2.3). */
export function SinPermiso({ seccion }: { seccion: string }) {
  const { t } = useTranslation();
  return (
    <Stack alignItems="center" spacing={1.5} sx={{ py: 8, textAlign: 'center', color: 'text.secondary' }}>
      <ShieldOff size={32} strokeWidth={1.5} aria-hidden />
      <Typography variant="h3" color="text.primary" component="h1">
        {t('sinPermiso.titulo')}
      </Typography>
      <Typography variant="body2">{t('sinPermiso.ayuda', { seccion })}</Typography>
    </Stack>
  );
}

/** Error recuperable inline con reintento. */
export function ErrorRecuperable({ mensaje, onReintentar }: { mensaje: string; onReintentar?: () => void }) {
  const { t } = useTranslation();
  return (
    <Alert
      severity="error"
      action={
        onReintentar ? (
          <Button color="inherit" size="small" onClick={onReintentar}>
            {t('comun.reintentar')}
          </Button>
        ) : undefined
      }
    >
      {mensaje}
    </Alert>
  );
}

export function EncabezadoPagina({ titulo, subtitulo, acciones }: { titulo: string; subtitulo?: ReactNode; acciones?: ReactNode }) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems={{ sm: 'flex-end' }} justifyContent="space-between" sx={{ mb: 3 }}>
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="h2" component="h1">
          {titulo}
        </Typography>
        {subtitulo && (
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5 }}>
            {subtitulo}
          </Typography>
        )}
      </Box>
      {acciones && <Stack direction="row" spacing={1}>{acciones}</Stack>}
    </Stack>
  );
}

/** Confirmación propia (window.confirm no funciona en iframes con sandbox). */
export function DialogoConfirmacion({
  titulo,
  mensaje,
  confirmar,
  ocupado = false,
  onConfirmar,
  onCerrar,
}: {
  titulo: string;
  mensaje: string;
  confirmar: string;
  ocupado?: boolean;
  onConfirmar: () => void;
  onCerrar: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth="xs" fullWidth aria-labelledby="titulo-confirmacion">
      <DialogTitle id="titulo-confirmacion">{titulo}</DialogTitle>
      <DialogContent>
        <DialogContentText>{mensaje}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={onCerrar} disabled={ocupado}>
          {t('comun.cancelar')}
        </Button>
        <Button variant="contained" color="error" onClick={onConfirmar} disabled={ocupado}>
          {confirmar}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
