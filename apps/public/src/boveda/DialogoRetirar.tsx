import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material';
import type { VersionDocumentoDto } from '@validasoft/api-client';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useRetirarVersion } from '../shared/api';
import { useAvisos } from '../shared/useAvisos';
import { mensajeDeError } from '../shared/errores';

/** Retirar una versión (BO-8) con su motivo; avisa si el archivo se borró porque ninguna empresa lo recibió. */
export function DialogoRetirar({ documentoId, tipo, version, onCerrar }: { documentoId: string; tipo: string; version: VersionDocumentoDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const retiro = useRetirarVersion(documentoId, version.n);
  const [motivo, setMotivo] = useState('');
  const [intentado, setIntentado] = useState(false);
  const falta = !motivo.trim();

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (falta) return;
    retiro.mutate(motivo.trim(), {
      onSuccess: (r) => {
        mostrar(r.binarioEliminado ? t('boveda.retiro.exitoBorrado', { n: String(version.n) }) : t('boveda.retiro.exito', { n: String(version.n) }));
        onCerrar();
      },
    });
  };

  return (
    <Dialog open onClose={retiro.isPending ? undefined : onCerrar} fullWidth maxWidth="xs" aria-labelledby="titulo-retiro">
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-retiro">{t('boveda.retiro.titulo', { n: String(version.n), tipo })}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography color="text.secondary">{t('boveda.retiro.ayuda')}</Typography>
            {retiro.isError && <Alert severity="error">{mensajeDeError(retiro.error, t)}</Alert>}
            <TextField
              label={t('boveda.retiro.motivo')}
              value={motivo}
              onChange={(e) => setMotivo(e.target.value.slice(0, 500))}
              required
              multiline
              minRows={2}
              error={intentado && falta}
              helperText={intentado && falta ? t('boveda.retiro.requerido') : t('boveda.retiro.motivoAyuda')}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={retiro.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" color="error" disabled={retiro.isPending}>
            {t('boveda.retiro.confirmar')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
