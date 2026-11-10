import { Button, Dialog, DialogActions, DialogContent, DialogContentText, DialogTitle } from '@mui/material';
import { useTranslation } from 'react-i18next';
import type { Blocker } from 'react-router';

/** Pregunta antes de salir de una sección con cambios sin guardar (`useSalidaSinGuardar`). */
export function DialogoSalidaSinGuardar({ bloqueo, seccion }: { bloqueo: Blocker; seccion: string }) {
  const { t } = useTranslation();
  const abierto = bloqueo.state === 'blocked';
  return (
    <Dialog open={abierto} onClose={() => bloqueo.reset?.()} aria-labelledby="salida-sin-guardar">
      <DialogTitle id="salida-sin-guardar">{t('ficha.salida.titulo')}</DialogTitle>
      <DialogContent>
        <DialogContentText>{t('ficha.salida.texto', { seccion })}</DialogContentText>
      </DialogContent>
      <DialogActions>
        <Button onClick={() => bloqueo.proceed?.()} color="inherit">
          {t('ficha.salida.salir')}
        </Button>
        <Button onClick={() => bloqueo.reset?.()} variant="contained">
          {t('ficha.salida.seguir')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
