import { Alert, Button, type SxProps, type Theme } from '@mui/material';
import { RefreshCw } from 'lucide-react';

/**
 * «Hay cambios nuevos» (NT-12): lo que se muestra en una pantalla con cambios sin guardar cuando el tiempo real avisa de que los datos
 * que está editando cambiaron. En vez de pisar lo escrito, deja decidir: «Actualizar» toma lo nuevo (y descarta lo que no se guardó).
 */
export function AvisoCambiosNuevos({
  titulo,
  texto,
  accion,
  onActualizar,
  sx,
}: {
  titulo: string;
  texto: string;
  /** Texto del botón (p. ej. «Actualizar»). */
  accion: string;
  onActualizar: () => void;
  sx?: SxProps<Theme>;
}) {
  return (
    <Alert
      severity="info"
      role="status"
      sx={sx}
      action={
        <Button color="inherit" size="small" startIcon={<RefreshCw size={16} aria-hidden />} onClick={onActualizar}>
          {accion}
        </Button>
      }
    >
      <strong>{titulo}</strong> {texto}
    </Alert>
  );
}
