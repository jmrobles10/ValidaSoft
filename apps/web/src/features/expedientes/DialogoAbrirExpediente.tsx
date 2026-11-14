import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography } from '@mui/material';
import type { TipoVinculoResumenDto } from '@validasoft/api-client';
import { FolderOpen } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { IconoCarga } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useFlujos } from '../flujos/api';
import { useAbrirExpediente } from './api';

export interface DialogoAbrirExpedienteProps {
  empresaId: string;
  vinculoId: string;
  tipoVinculo: TipoVinculoResumenDto;
  /** Nombre del tercero para el subtítulo. */
  tercero?: string | null;
  onCerrar: () => void;
}

/** Apertura manual (`POST /vinculos/{id}/expedientes`): el flujo que corresponda (reglas de selección o el por defecto del tipo) o uno publicado elegido. */
export function DialogoAbrirExpediente({ empresaId, vinculoId, tipoVinculo, tercero, onCerrar }: DialogoAbrirExpedienteProps) {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const mostrar = useAvisos((s) => s.mostrar);
  const flujos = useFlujos(empresaId, tipoVinculo.codigo);
  const mutacion = useAbrirExpediente(empresaId);
  const [flujoId, setFlujoId] = useState('');

  const publicados = (flujos.data ?? []).filter((f) => f.publicada);
  const sinFlujos = flujos.isSuccess && publicados.length === 0;

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    mutacion.mutate(
      { vinculoId, flujoId: flujoId || null },
      {
        onSuccess: (expediente) => {
          mostrar(t('expedientes.abrir.exito', { folio: expediente.folio }));
          onCerrar();
          void navegar(`/e/${empresaId}/expedientes/${expediente.id}`);
        },
      },
    );
  };

  return (
    <Dialog
      open
      onClose={mutacion.isPending ? undefined : onCerrar}
      maxWidth={false}
      slotProps={{ paper: { sx: { width: 460, maxWidth: 'calc(100% - 32px)' } } }}
      aria-labelledby="titulo-abrir-expediente"
    >
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-abrir-expediente">
          {t('expedientes.abrir.titulo')}
          {tercero && (
            <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
              {tercero} · {tipoVinculo.nombre}
            </Typography>
          )}
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography>{t('expedientes.abrir.ayuda')}</Typography>
            {mutacion.isError && <Alert severity="error">{mensajeDeError(mutacion.error, t)}</Alert>}
            {sinFlujos ? (
              <Alert severity="warning">{t('expedientes.abrir.sinFlujos', { tipo: tipoVinculo.nombre })}</Alert>
            ) : (
              <TextField select label={t('expedientes.abrir.flujo')} value={flujoId} onChange={(e) => setFlujoId(e.target.value)} disabled={!flujos.isSuccess}>
                <MenuItem value="">{t('expedientes.abrir.porDefecto', { tipo: tipoVinculo.nombre })}</MenuItem>
                {publicados.map((f) => (
                  <MenuItem key={f.id} value={f.id}>
                    {f.nombre} · v{f.publicada?.version}
                  </MenuItem>
                ))}
              </TextField>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={onCerrar} disabled={mutacion.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button
            type="submit"
            variant="contained"
            disabled={mutacion.isPending || sinFlujos || !flujos.isSuccess}
            startIcon={mutacion.isPending ? <IconoCarga /> : <FolderOpen size={16} aria-hidden />}
          >
            {t('expedientes.abrir.confirmar')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
