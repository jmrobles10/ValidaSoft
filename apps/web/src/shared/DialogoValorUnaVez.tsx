import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, Stack, Tooltip } from '@mui/material';
import { Check, Copy } from 'lucide-react';
import { useState, type ReactNode } from 'react';

export interface DialogoValorUnaVezProps {
  titulo: string;
  /** Advertencia de que no se volverá a mostrar y qué hacer si se pierde. */
  aviso: string;
  valor: string;
  textoCopiar: string;
  textoCopiado: string;
  textoListo: string;
  /** Contenido adicional bajo el valor (cómo usarlo, de qué es). */
  children?: ReactNode;
  onCerrar: () => void;
}

/**
 * Un secreto que la API entrega **una sola vez** (secreto de webhook, API key): se muestra en monoespaciado con botón
 * de copiar. No se cierra con un clic afuera ni con Escape para que nadie lo pierda sin querer; solo con «listo».
 */
export function DialogoValorUnaVez({ titulo, aviso, valor, textoCopiar, textoCopiado, textoListo, children, onCerrar }: DialogoValorUnaVezProps) {
  const [copiado, setCopiado] = useState(false);

  const copiar = async () => {
    try {
      await navigator.clipboard.writeText(valor);
      setCopiado(true);
    } catch {
      setCopiado(false);
    }
  };

  return (
    <Dialog open maxWidth={false} slotProps={{ paper: { sx: { width: 540, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-valor-una-vez">
      <DialogTitle id="titulo-valor-una-vez">{titulo}</DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          <Alert severity="warning">{aviso}</Alert>
          <Stack direction="row" alignItems="center" spacing={1} sx={{ px: 1.5, py: 1, bgcolor: 'superficie.nivel2', borderRadius: (th) => `${String(th.radio.sm)}px` }}>
            <Box component="code" sx={{ flex: 1, minWidth: 0, fontFamily: (th) => th.typography.mono.fontFamily, fontSize: 13, overflowWrap: 'anywhere' }} data-secreto="">
              {valor}
            </Box>
            <Tooltip title={copiado ? textoCopiado : textoCopiar}>
              <IconButton aria-label={textoCopiar} onClick={() => void copiar()}>
                {copiado ? <Check size={18} aria-hidden /> : <Copy size={18} aria-hidden />}
              </IconButton>
            </Tooltip>
          </Stack>
          {children}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="contained" onClick={onCerrar}>
          {textoListo}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
