import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { Ban } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useRevocarCertificado } from './api';

/** Revocar una emisión: irreversible, con justificación obligatoria (la verá quien verifique el certificado). */
export function DialogoRevocarCertificado({ empresaId, folio, onCerrar }: { empresaId: string; folio: string; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const revocar = useRevocarCertificado(empresaId);
  const [justificacion, setJustificacion] = useState('');
  const [tocado, setTocado] = useState(false);
  const falta = !justificacion.trim();

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (falta) return;
    revocar.mutate(
      { folio, justificacion: justificacion.trim() },
      {
        onSuccess: () => {
          mostrar(t('certificados.revocado_ok'));
          onCerrar();
        },
      },
    );
  };

  return (
    <Dialog open onClose={revocar.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 480, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-revocar">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-revocar">{t('certificados.revocarTitulo', { folio })}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Alert severity="warning">{t('certificados.revocarAyuda')}</Alert>
            {revocar.isError && <Alert severity="error">{mensajeDeError(revocar.error, t)}</Alert>}
            <TextField
              required
              multiline
              minRows={3}
              label={t('certificados.justificacion')}
              value={justificacion}
              onChange={(e) => setJustificacion(e.target.value.slice(0, 1000))}
              onBlur={() => setTocado(true)}
              error={tocado && falta}
              helperText={t('certificados.justificacionAyuda')}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          {falta && <FaltaParaEnviar>{t('certificados.faltaJustificacion')}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={revocar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" color="error" disabled={falta || revocar.isPending} startIcon={revocar.isPending ? <IconoCarga /> : <Ban size={16} aria-hidden />}>
            {t('certificados.revocar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
