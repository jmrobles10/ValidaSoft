import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material';
import type { ExencionDeVinculoDto } from '@validasoft/api-client';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { Undo2 } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { useAvisos } from '../../../shared/useAvisos';
import { useRevocarExencion } from './api';
import { alcanceDe, errorDeExencion, MAXIMO_JUSTIFICACION } from './modelo';

export interface DialogoRevocarExencionProps {
  empresaId: string;
  vinculoId: string;
  /** Basta lo que identifica y describe la exención (la fila de vencimientos solo conoce su resumen). */
  exencion: Pick<ExencionDeVinculoDto, 'id' | 'todos' | 'objetivos'>;
  onCerrar: () => void;
}

const ANCHO = { width: 460, maxWidth: 'calc(100% - 32px)' } as const;

/** Revocar una exención vigente (EX-5): justificación obligatoria; desde ya el vínculo vuelve a regirse por las vigencias. */
export function DialogoRevocarExencion({ empresaId, vinculoId, exencion, onCerrar }: DialogoRevocarExencionProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const mutacion = useRevocarExencion(empresaId, vinculoId);
  const [justificacion, setJustificacion] = useState('');
  const [tocada, setTocada] = useState(false);
  const vacia = !justificacion.trim();
  const ocupado = mutacion.isPending;

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (vacia) return;
    mutacion.mutate(
      { exencionId: exencion.id, justificacion: justificacion.trim() },
      {
        onSuccess: () => {
          mostrar(t('exenciones.revocacion.exito'));
          onCerrar();
        },
      },
    );
  };

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: ANCHO } }} aria-labelledby="titulo-revocar-exencion">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-revocar-exencion">
          {t('exenciones.revocacion.titulo')}
          <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
            {alcanceDe(exencion, t)}
          </Typography>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Alert severity="warning">{t('exenciones.revocacion.ayuda')}</Alert>
            {mutacion.isError && <Alert severity="error">{errorDeExencion(mutacion.error, t)}</Alert>}
            <TextField
              required
              label={t('exenciones.revocacion.justificacion')}
              value={justificacion}
              onChange={(e) => setJustificacion(e.target.value.slice(0, MAXIMO_JUSTIFICACION))}
              onBlur={() => setTocada(true)}
              multiline
              minRows={3}
              error={tocada && vacia}
              helperText={
                <Stack component="span" direction="row" sx={{ justifyContent: 'space-between' }} spacing={1}>
                  <span>{tocada && vacia ? t('acciones.requerido') : t('acciones.quedaEnHistorial')}</span>
                  <Typography component="span" variant="monoTabla" sx={{ color: 'text.secondary', flex: 'none' }}>
                    {justificacion.length} / {MAXIMO_JUSTIFICACION}
                  </Typography>
                </Stack>
              }
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          {vacia && <FaltaParaEnviar>{t('exenciones.revocacion.faltaJustificacion')}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" color="error" disabled={ocupado || vacia} startIcon={ocupado ? <IconoCarga /> : <Undo2 size={16} aria-hidden />}>
            {t('exenciones.revocacion.confirmar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
