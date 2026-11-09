import { Alert, Snackbar } from '@mui/material';
import { useAvisos } from './useAvisos';

export function Avisos() {
  const actual = useAvisos((s) => s.actual);
  const cerrar = useAvisos((s) => s.cerrar);
  return (
    <Snackbar
      key={actual?.id}
      open={Boolean(actual)}
      autoHideDuration={actual?.severidad === 'error' ? 8000 : 4000}
      onClose={(_evento, razon) => {
        if (razon !== 'clickaway') cerrar();
      }}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
    >
      {actual ? (
        <Alert severity={actual.severidad} variant="filled" onClose={cerrar} sx={{ width: '100%' }}>
          {actual.mensaje}
        </Alert>
      ) : undefined}
    </Snackbar>
  );
}
