import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography } from '@mui/material';
import type { CuentaBancariaPublicaDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { useRetirarCuenta } from '../shared/api';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { nombreCuenta } from './cuentas';

/** Retirar una cuenta (CB-1) con confirmación; si está designada o por aprobar con una empresa, la API lo impide (409 `cuenta-en-uso`). */
export function DialogoRetirarCuenta({ cuenta, onCerrar }: { cuenta: CuentaBancariaPublicaDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const retiro = useRetirarCuenta();
  const nombre = nombreCuenta(cuenta);

  const confirmar = () =>
    retiro.mutate(cuenta.id, {
      onSuccess: () => {
        mostrar(t('cuentas.retiro.exito', { cuenta: nombre }));
        onCerrar();
      },
    });

  return (
    <Dialog open onClose={retiro.isPending ? undefined : onCerrar} fullWidth maxWidth="xs" aria-labelledby="titulo-retiro-cuenta">
      <DialogTitle id="titulo-retiro-cuenta">{t('cuentas.retiro.titulo', { cuenta: nombre })}</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Typography sx={{ color: 'text.secondary' }}>{t('cuentas.retiro.texto')}</Typography>
          {retiro.isError && <Alert severity="error">{mensajeDeError(retiro.error, t)}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={retiro.isPending} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button variant="contained" color="error" onClick={confirmar} disabled={retiro.isPending}>
          {t('cuentas.retiro.confirmar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
