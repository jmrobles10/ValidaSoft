import { Alert, Snackbar } from '@mui/material';
import { useAvisos } from './useAvisos';

/** Snackbar único: abajo y al centro en el celular, por encima de la barra de navegación. */
export function Avisos() {
  const actual = useAvisos((s) => s.actual);
  const cerrar = useAvisos((s) => s.cerrar);
  return (
    <Snackbar
      key={actual?.id}
      open={Boolean(actual)}
      autoHideDuration={6000}
      onClose={(_, razon) => razon !== 'clickaway' && cerrar()}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      sx={{ bottom: { xs: 80, md: 24 } }}
    >
      <Alert severity={actual?.severidad ?? 'success'} onClose={cerrar} variant="outlined" sx={{ bgcolor: 'background.paper', width: '100%' }}>
        {actual?.mensaje}
      </Alert>
    </Snackbar>
  );
}
