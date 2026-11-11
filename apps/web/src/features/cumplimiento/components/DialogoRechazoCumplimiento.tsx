import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Skeleton, Stack, TextField, Typography } from '@mui/material';
import type { ExpedienteDto, RechazarPorCumplimientoDto } from '@validasoft/api-client';
import { FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useMotivos } from '../../expedientes/api';
import { SelectorMotivos } from '../../expedientes/components/SelectorMotivos';
import { MAXIMO_TEXTO_EXPEDIENTE, motivosPara } from '../../expedientes/reglas';

export interface DialogoRechazoCumplimientoProps {
  expediente: ExpedienteDto;
  empresaId: string;
  ocupado: boolean;
  error: unknown;
  onConfirmar: (dto: RechazarPorCumplimientoDto) => void;
  onCerrar: () => void;
}

/**
 * El oficial rechaza el expediente por un hallazgo grave (coincidencia confirmada o estudio desfavorable): elige motivos del
 * catálogo de rechazo y deja su justificación. Es definitivo y el tercero recibe por correo el texto de los motivos.
 */
export function DialogoRechazoCumplimiento({ expediente, empresaId, ocupado, error, onConfirmar, onCerrar }: DialogoRechazoCumplimientoProps) {
  const { t } = useTranslation();
  const catalogo = useMotivos(empresaId);
  const [elegidos, setElegidos] = useState<string[]>([]);
  const [justificacion, setJustificacion] = useState('');
  const [tocada, setTocada] = useState(false);
  const disponibles = motivosPara(catalogo.data ?? [], 'Rechazo');
  const faltanMotivos = elegidos.length === 0;
  const faltaTexto = justificacion.trim() === '';
  const hallazgos = (expediente.verificaciones ?? []).filter((v) => v.estado === 'Fallida').map((v) => t(`cumplimiento.rechazo.hallazgos.${v.tipo}`));

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (faltanMotivos || faltaTexto) return;
    onConfirmar({ motivos: elegidos, justificacion: justificacion.trim() });
  };

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 540, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-rechazo-cumplimiento">
      <FormularioDeDialogo onSubmit={enviar}>
        <DialogTitle id="titulo-rechazo-cumplimiento">
          {t('cumplimiento.rechazo.titulo')}
          <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
            {expediente.folio} · {expediente.tercero.nombre}
          </Typography>
        </DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Alert severity="warning">
              {t('cumplimiento.rechazo.ayuda')}
              {hallazgos.length > 0 && ` ${t('cumplimiento.rechazo.hallazgo', { hallazgo: hallazgos.join(' · ') })}`}
            </Alert>
            {error !== null && error !== undefined && <Alert severity="error">{mensajeDeError(error, t)}</Alert>}
            {catalogo.isPending ? (
              <Skeleton variant="rounded" height={140} role="status" aria-busy aria-label={t('cumplimiento.rechazo.motivos')} />
            ) : catalogo.isError ? (
              <Alert severity="error">{mensajeDeError(catalogo.error, t)}</Alert>
            ) : disponibles.length === 0 ? (
              <Alert severity="warning">{t('cumplimiento.rechazo.sinMotivos')}</Alert>
            ) : (
              <SelectorMotivos etiqueta={t('cumplimiento.rechazo.motivos')} ayuda={t('cumplimiento.rechazo.motivosAyuda')} motivos={disponibles} elegidos={elegidos} onCambiar={setElegidos} />
            )}
            <TextField
              required
              label={t('cumplimiento.rechazo.justificacion')}
              value={justificacion}
              onChange={(e) => setJustificacion(e.target.value.slice(0, MAXIMO_TEXTO_EXPEDIENTE))}
              onBlur={() => setTocada(true)}
              multiline
              minRows={3}
              error={tocada && faltaTexto}
              helperText={tocada && faltaTexto ? t('cumplimiento.rechazo.faltaJustificacion') : t('cumplimiento.rechazo.justificacionAyuda')}
            />
          </Stack>
        </DialogContent>
        <DialogActions>
          {(faltanMotivos || faltaTexto) && <FaltaParaEnviar>{faltanMotivos ? t('cumplimiento.rechazo.faltaMotivos') : t('cumplimiento.rechazo.faltaJustificacion')}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" color="error" disabled={ocupado || faltanMotivos || faltaTexto} startIcon={ocupado ? <IconoCarga /> : undefined}>
            {t('cumplimiento.rechazo.confirmar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
