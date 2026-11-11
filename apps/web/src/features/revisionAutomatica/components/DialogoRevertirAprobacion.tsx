import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, TextField, Typography } from '@mui/material';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { MAXIMO_TEXTO_EXPEDIENTE } from '../../expedientes/reglas';
import { useResolverRevision } from '../api';
import type { DestinoDeMuestreo } from '../modelo';

export interface DialogoRevertirAprobacionProps {
  empresaId: string;
  destino: DestinoDeMuestreo;
  onCerrar: () => void;
}

/**
 * Revertir una aprobación por muestreo: se anula el veredicto del sistema y el documento vuelve a revisión humana. Avisa qué
 * pasará y exige la justificación, que queda en el historial con el nombre de quien revierte. Ante un 409/403 explica el
 * caso con el mensaje del código y conserva lo escrito.
 */
export function DialogoRevertirAprobacion({ empresaId, destino, onCerrar }: DialogoRevertirAprobacionProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const resolver = useResolverRevision(empresaId);
  const [texto, setTexto] = useState('');
  const [tocado, setTocado] = useState(false);
  const falta = !texto.trim();
  const ocupado = resolver.isPending;

  const enviar = (evento: SubmitEvent<HTMLFormElement>) => {
    evento.preventDefault();
    if (falta || ocupado) return;
    resolver.mutate(
      { expedienteId: destino.expedienteId, requisitoId: destino.requisitoId, areaId: destino.areaId, decision: 'Revertir', justificacion: texto },
      {
        onSuccess: () => {
          mostrar(t('revisionAutomatica.excepciones.revertirDialogo.exito', { documento: destino.documento }));
          onCerrar();
        },
      },
    );
  };

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 520, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-revertir-aprobacion">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-revertir-aprobacion">
          {t('revisionAutomatica.excepciones.revertirDialogo.titulo')}
          <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
            {destino.folio} · {destino.tercero}
          </Typography>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Alert severity="warning">{t('revisionAutomatica.excepciones.revertirDialogo.aviso', { documento: destino.documento, area: destino.area })}</Alert>
            {resolver.isError && <Alert severity="error">{mensajeDeError(resolver.error, t)}</Alert>}
            <TextField
              required
              multiline
              minRows={3}
              label={t('revisionAutomatica.excepciones.revertirDialogo.justificacion')}
              value={texto}
              onChange={(e) => setTexto(e.target.value.slice(0, MAXIMO_TEXTO_EXPEDIENTE))}
              onBlur={() => setTocado(true)}
              error={tocado && falta}
              helperText={tocado && falta ? t('revisionAutomatica.excepciones.revertirDialogo.falta') : t('revisionAutomatica.excepciones.revertirDialogo.justificacionAyuda')}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          {falta && <FaltaParaEnviar>{t('revisionAutomatica.excepciones.revertirDialogo.falta')}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" color="error" disabled={ocupado || falta} startIcon={ocupado ? <IconoCarga /> : undefined}>
            {t('revisionAutomatica.excepciones.revertirDialogo.confirmar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
