import { Alert, Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle, Stack, TextField } from '@mui/material';
import { esErrorApi, type DefinicionAccion, type VinculoDto } from '@validasoft/api-client';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { useAvisos } from '../../../shared/useAvisos';
import { mensajeDeError } from '../../../shared/errores';
import { useAccionVinculo } from '../api';

export interface DialogoAccionProps {
  definicion: DefinicionAccion;
  vinculo: VinculoDto;
  empresaId: string;
  onCerrar: () => void;
}

/** Diálogo de una acción del vínculo: pide motivo, justificación o correo y la ejecuta con If-Match. */
export function DialogoAccion({ definicion, vinculo, empresaId, onCerrar }: DialogoAccionProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const mutacion = useAccionVinculo(empresaId);
  const { accion, requiere, destructiva } = definicion;
  const [texto, setTexto] = useState(requiere === 'correo' ? (vinculo.invitacion?.correoDestino ?? vinculo.correoTercero) : '');
  const [hasta, setHasta] = useState('');
  const [vigencia, setVigencia] = useState(72);
  const [intentado, setIntentado] = useState(false);

  const vacio = requiere !== 'correo' && !texto.trim();

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (vacio) return;
    mutacion.mutate(
      {
        accion,
        vinculo,
        texto: texto.trim(),
        hasta: hasta ? new Date(`${hasta}T23:59:59`).toISOString() : null,
        vigenciaHoras: vigencia,
      },
      {
        onSuccess: (resultado) => {
          mostrar(
            resultado.tipo === 'invitacion'
              ? t('acciones.invitar.exito', { correo: resultado.invitacion.correoDestino })
              : t(`acciones.${accion}.exito`),
          );
          onCerrar();
        },
      },
    );
  };

  const error = mutacion.error;
  const conflictoDeVersion = esErrorApi(error) && error.esConflictoDeVersion;

  return (
    <Dialog open onClose={mutacion.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" aria-labelledby="titulo-accion">
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-accion">{t(`acciones.${accion}.titulo`)}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <DialogContentText>{t(`acciones.${accion}.ayuda`)}</DialogContentText>
            {error && <Alert severity={conflictoDeVersion ? 'warning' : 'error'}>{mensajeDeError(error, t)}</Alert>}
            <TextField
              required={requiere !== 'correo'}
              type={requiere === 'correo' ? 'email' : 'text'}
              label={t(`acciones.${accion}.campo`)}
              value={texto}
              onChange={(e) => setTexto(e.target.value)}
              multiline={requiere !== 'correo'}
              minRows={requiere === 'correo' ? undefined : 2}
              error={intentado && vacio}
              helperText={intentado && vacio ? t('acciones.requerido') : undefined}
              disabled={conflictoDeVersion}
            />
            {accion === 'bloquear' && (
              <TextField
                type="date"
                label={t('acciones.bloquear.hasta')}
                value={hasta}
                onChange={(e) => setHasta(e.target.value)}
                slotProps={{ inputLabel: { shrink: true } }}
                disabled={conflictoDeVersion}
              />
            )}
            {accion === 'invitar' && (
              <TextField
                type="number"
                label={t('acciones.invitar.vigencia')}
                value={vigencia}
                onChange={(e) => setVigencia(Math.max(1, Number(e.target.value) || 72))}
                slotProps={{ htmlInput: { min: 1, max: 720 } }}
              />
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={onCerrar} disabled={mutacion.isPending}>
            {conflictoDeVersion ? t('comun.cerrar') : t('comun.cancelar')}
          </Button>
          {!conflictoDeVersion && (
            <Button type="submit" variant="contained" color={destructiva ? 'error' : 'primary'} disabled={mutacion.isPending}>
              {t(`acciones.${accion}.boton`)}
            </Button>
          )}
        </DialogActions>
      </form>
    </Dialog>
  );
}
