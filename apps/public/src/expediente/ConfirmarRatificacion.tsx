import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from '@mui/material';
import { CheckCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useRatificarRequisito } from '../shared/api';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';

interface Props {
  expedienteId: string;
  requisitoId: string;
  documento: string;
  empresa: string;
  /** `seccion`: una sección de la ficha en la renovación periódica del vínculo (VV-3); por omisión, un documento vencido. */
  tipo?: 'documento' | 'seccion';
  /** Botón pequeño, sin sangría (Mi información y el wizard). */
  compacto?: boolean;
  /** Texto del botón (por omisión el del tipo). */
  etiqueta?: string;
}

/**
 * Ratificar un documento vencido en una renovación (RN-2) o una sección de la ficha en la renovación periódica del vínculo (VV-3): el
 * tercero declara que no cambió y la empresa lo da por renovado sin volver a revisarlo. Si algo cambió, debe subir una versión
 * nueva (o actualizar su información).
 */
export function ConfirmarRatificacion({ expedienteId, requisitoId, documento, empresa, tipo = 'documento', compacto = false, etiqueta }: Props) {
  const { t } = useTranslation();
  const k = tipo === 'seccion' ? 'expediente.ratificar.seccion' : 'expediente.ratificar';
  const mostrar = useAvisos((s) => s.mostrar);
  const ratificacion = useRatificarRequisito(expedienteId);
  const [abierto, setAbierto] = useState(false);
  const cerrar = () => {
    if (!ratificacion.isPending) setAbierto(false);
  };
  const confirmar = () =>
    ratificacion.mutate(requisitoId, {
      onSuccess: () => {
        mostrar(t(`${k}.hecho`, { documento, empresa }));
        setAbierto(false);
      },
    });

  return (
    <>
      <Button
        variant="outlined"
        size={compacto ? 'small' : 'medium'}
        startIcon={<CheckCheck size={16} aria-hidden />}
        onClick={() => setAbierto(true)}
        sx={compacto ? { minHeight: 36 } : { mt: 1, ml: 3.25 }}
      >
        {etiqueta ?? t(`${k}.boton`)}
      </Button>
      <Dialog open={abierto} onClose={cerrar} fullWidth maxWidth="xs" aria-labelledby="titulo-ratificar">
        <DialogTitle id="titulo-ratificar">{t(`${k}.titulo`, { documento })}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography>{t(`${k}.texto`, { documento, empresa })}</Typography>
            <Typography sx={{ color: 'text.secondary' }}>{t(`${k}.siCambio`)}</Typography>
            {ratificacion.isError && <Alert severity="error">{mensajeDeError(ratificacion.error, t)}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={cerrar} disabled={ratificacion.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button variant="contained" onClick={confirmar} disabled={ratificacion.isPending}>
            {t(`${k}.confirmar`)}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
