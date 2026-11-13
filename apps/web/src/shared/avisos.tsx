import { Alert, Snackbar } from '@mui/material';
import { AnimacionExito } from '@validasoft/ui';
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
        actual.celebrar ? (
          // La celebración no va sobre el verde lleno del aviso (el círculo de marca se perdería): superficie de la página y la animación.
          <Alert
            severity="success"
            variant="outlined"
            icon={<AnimacionExito tamano={26} decorativa />}
            onClose={cerrar}
            sx={{ width: '100%', bgcolor: 'background.paper', color: 'text.primary', alignItems: 'center' }}
          >
            {actual.mensaje}
          </Alert>
        ) : (
          <Alert severity={actual.severidad} variant="filled" onClose={cerrar} sx={{ width: '100%' }}>
            {actual.mensaje}
          </Alert>
        )
      ) : undefined}
    </Snackbar>
  );
}
