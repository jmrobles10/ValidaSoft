import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from '@mui/material';
import { CheckCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useRatificarRequisito } from '../shared/api';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';

/**
 * Ratificar un documento vencido en una renovación (RN-2): el tercero declara que no cambió y la empresa lo da por
 * renovado sin volver a revisarlo. Si algo cambió, debe subir una versión nueva.
 */
export function ConfirmarRatificacion({ expedienteId, requisitoId, documento, empresa }: { expedienteId: string; requisitoId: string; documento: string; empresa: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const ratificacion = useRatificarRequisito(expedienteId);
  const [abierto, setAbierto] = useState(false);
  const cerrar = () => {
    if (!ratificacion.isPending) setAbierto(false);
  };
  const confirmar = () =>
    ratificacion.mutate(requisitoId, {
      onSuccess: () => {
        mostrar(t('expediente.ratificar.hecho', { documento, empresa }));
        setAbierto(false);
      },
    });

  return (
    <>
      <Button variant="outlined" size="large" startIcon={<CheckCheck size={16} aria-hidden />} onClick={() => setAbierto(true)} sx={{ mt: 1, ml: 3.25 }}>
        {t('expediente.ratificar.boton')}
      </Button>
      <Dialog open={abierto} onClose={cerrar} fullWidth maxWidth="xs" aria-labelledby="titulo-ratificar">
        <DialogTitle id="titulo-ratificar">{t('expediente.ratificar.titulo', { documento })}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography>{t('expediente.ratificar.texto', { documento, empresa })}</Typography>
            <Typography color="text.secondary">{t('expediente.ratificar.siCambio')}</Typography>
            {ratificacion.isError && <Alert severity="error">{mensajeDeError(ratificacion.error, t)}</Alert>}
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button variant="text" onClick={cerrar} disabled={ratificacion.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button variant="contained" onClick={confirmar} disabled={ratificacion.isPending}>
            {t('expediente.ratificar.confirmar')}
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}
