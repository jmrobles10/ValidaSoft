import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField } from '@mui/material';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';

interface DialogoRechazarCuentaProps {
  ocupado: boolean;
  /** Mensaje del último intento fallido (ya traducido). */
  error: string | null;
  onRechazar: (motivo: string) => void;
  onCerrar: () => void;
}

/** Rechazar la cuenta propuesta: el motivo es obligatorio porque lo lee el tercero para corregirla. */
export function DialogoRechazarCuenta({ ocupado, error, onRechazar, onCerrar }: DialogoRechazarCuentaProps) {
  const { t } = useTranslation();
  const [motivo, setMotivo] = useState('');
  const [tocado, setTocado] = useState(false);
  const falta = !motivo.trim();

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (falta) return;
    onRechazar(motivo.trim());
  };

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 480, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-rechazar-cuenta">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-rechazar-cuenta">{t('cuentas.rechazarTitulo')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Alert severity="info">{t('cuentas.rechazarAyuda')}</Alert>
            {error && <Alert severity="error">{error}</Alert>}
            <TextField
              required
              multiline
              minRows={3}
              label={t('cuentas.motivoCampo')}
              value={motivo}
              onChange={(e) => setMotivo(e.target.value.slice(0, 500))}
              onBlur={() => setTocado(true)}
              error={tocado && falta}
              helperText={t('cuentas.motivoAyuda')}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          {falta && <FaltaParaEnviar>{t('cuentas.faltaMotivo')}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" color="error" disabled={falta || ocupado} startIcon={ocupado ? <IconoCarga /> : <X size={16} aria-hidden />}>
            {t('cuentas.rechazar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
