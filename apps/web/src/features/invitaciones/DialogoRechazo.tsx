import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { Ban } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../shared/componentes';

const MAXIMO_MOTIVO = 500;

/**
 * Rechazar la invitación de un vínculo o un lote completo (decisión AI): siempre con un motivo, que queda en el historial y lo ve quien la pidió. El diálogo no cierra
 * hasta que la decisión se confirme; el error de la API se muestra dentro.
 */
export function DialogoRechazo({
  tipo,
  subtitulo,
  ocupado,
  error,
  onConfirmar,
  onCerrar,
}: {
  tipo: 'invitacion' | 'lote';
  /** A quién o qué se rechaza («Aceros del Norte», «Lote de noviembre»). */
  subtitulo: string;
  ocupado: boolean;
  error: string | null;
  onConfirmar: (motivo: string) => void;
  onCerrar: () => void;
}) {
  const { t } = useTranslation();
  const [motivo, setMotivo] = useState('');
  const [tocado, setTocado] = useState(false);
  const vacio = !motivo.trim();

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (!vacio) onConfirmar(motivo.trim());
  };

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 460, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-rechazo">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-rechazo">
          {tipo === 'lote' ? t('invitaciones.rechazo.tituloLote') : t('invitaciones.rechazo.tituloInvitacion')}
          <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
            {subtitulo}
          </Typography>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Alert severity="warning">{tipo === 'lote' ? t('invitaciones.rechazo.ayudaLote') : t('invitaciones.rechazo.ayudaInvitacion')}</Alert>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              required
              multiline
              minRows={3}
              label={t('invitaciones.rechazo.campo')}
              value={motivo}
              onChange={(e) => setMotivo(e.target.value.slice(0, MAXIMO_MOTIVO))}
              onBlur={() => setTocado(true)}
              error={tocado && vacio}
              helperText={tocado && vacio ? t('invitaciones.rechazo.falta') : t('invitaciones.rechazo.campoAyuda')}
              slotProps={{ htmlInput: { maxLength: MAXIMO_MOTIVO } }}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          {vacio && <FaltaParaEnviar>{t('invitaciones.rechazo.falta')}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" color="error" disabled={ocupado || vacio} startIcon={ocupado ? <IconoCarga /> : <Ban size={16} aria-hidden />}>
            {t('invitaciones.rechazo.confirmar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
