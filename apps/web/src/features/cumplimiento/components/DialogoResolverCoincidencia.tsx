import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, TextField, Typography } from '@mui/material';
import type { CoincidenciaDto, DecisionCoincidencia } from '@validasoft/api-client';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';

export interface DialogoResolverCoincidenciaProps {
  coincidencia: CoincidenciaDto;
  decision: DecisionCoincidencia;
  ocupado: boolean;
  error: unknown;
  onGuardar: (justificacion: string) => void;
  onCerrar: () => void;
}

/** Descartar (homónimo, vale 12 meses) o confirmar (la verificación falla; nunca rechaza sola) una coincidencia, con justificación. */
export function DialogoResolverCoincidencia({ coincidencia: c, decision, ocupado, error, onGuardar, onCerrar }: DialogoResolverCoincidenciaProps) {
  const { t } = useTranslation();
  const [justificacion, setJustificacion] = useState('');
  const [intentado, setIntentado] = useState(false);
  const vacia = justificacion.trim() === '';
  const confirmar = decision === 'Confirmada';

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 520, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-resolver">
      <DialogTitle id="titulo-resolver">{confirmar ? t('cumplimiento.resolver.tituloConfirmar') : t('cumplimiento.resolver.tituloDescartar')}</DialogTitle>
      <DialogContent>
        <Typography sx={{ fontWeight: 500, mb: 1 }}>
          {t('cumplimiento.resolver.coincidencia', {
            persona: c.persona,
            rol: t(`cumplimiento.roles.${c.rol as 'Tercero'}`, { defaultValue: c.rol }),
            lista: c.lista,
            nombre: c.nombreEncontrado,
          })}
        </Typography>
        <Alert severity={confirmar ? 'warning' : 'info'} sx={{ mb: 2 }}>
          {confirmar ? t('cumplimiento.resolver.ayudaConfirmar') : t('cumplimiento.resolver.ayudaDescartar')}
        </Alert>
        <TextField
          label={t('cumplimiento.resolver.justificacion')}
          value={justificacion}
          onChange={(e) => setJustificacion(e.target.value)}
          multiline
          minRows={3}
          fullWidth
          required
          error={intentado && vacia}
          helperText={intentado && vacia ? t('cumplimiento.errores.justificacion-requerida') : t('cumplimiento.resolver.justificacionAyuda')}
          slotProps={{ htmlInput: { maxLength: 4000 } }}
        />
        {error !== null && error !== undefined && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {mensajeDeError(error, t)}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button
          variant="contained"
          color={confirmar ? 'error' : 'primary'}
          disabled={ocupado}
          startIcon={ocupado ? <IconoCarga /> : undefined}
          onClick={() => {
            setIntentado(true);
            if (!vacia) onGuardar(justificacion);
          }}
        >
          {t('cumplimiento.resolver.guardar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
