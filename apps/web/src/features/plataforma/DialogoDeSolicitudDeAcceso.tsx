import { Alert, Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, TextField } from '@mui/material';
import type { SolicitudDeAccesoDePlataformaDto } from '@validasoft/api-client';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../shared/componentes';
import { listaDeEmpresas } from './modeloAccesos';

export interface Decision {
  solicitud: SolicitudDeAccesoDePlataformaDto;
  aprobar: boolean;
}

/** Confirma una decisión que cambia quién entra a la bóveda de un tercero: dice qué pasa y, al rechazar, deja anotar el motivo (queda en la auditoría). */
export function DialogoDeSolicitudDeAcceso({
  decision,
  ocupado,
  error,
  onConfirmar,
  onCerrar,
}: {
  decision: Decision;
  ocupado: boolean;
  error: string | null;
  onConfirmar: (motivo: string) => void;
  onCerrar: () => void;
}) {
  const { t } = useTranslation();
  const [motivo, setMotivo] = useState('');
  const { solicitud, aprobar } = decision;
  const clave = aprobar ? 'aprobar' : 'rechazar';
  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} fullWidth maxWidth="sm" aria-labelledby="dialogo-solicitud-titulo">
      <DialogTitle id="dialogo-solicitud-titulo">{t(`plataforma.accesos.dialogo.${clave}Titulo`, { correo: solicitud.correo })}</DialogTitle>
      <DialogContent>
        <DialogContentText sx={{ color: 'text.primary' }}>
          {t(`plataforma.accesos.dialogo.${clave}Texto`, { tercero: solicitud.tercero, empresas: listaDeEmpresas(solicitud) })}
        </DialogContentText>
        {!aprobar && (
          <TextField
            label={t('plataforma.accesos.dialogo.motivo')}
            helperText={t('plataforma.accesos.dialogo.motivoAyuda')}
            value={motivo}
            onChange={(e) => setMotivo(e.target.value)}
            fullWidth
            multiline
            minRows={2}
            sx={{ mt: 2 }}
            slotProps={{ htmlInput: { maxLength: 500 } }}
          />
        )}
        {error && (
          <Alert severity="error" role="alert" sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button variant="contained" color={aprobar ? 'primary' : 'error'} onClick={() => onConfirmar(motivo)} disabled={ocupado} startIcon={ocupado ? <IconoCarga /> : undefined}>
          {t(`plataforma.accesos.dialogo.${clave}Confirmar`)}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
