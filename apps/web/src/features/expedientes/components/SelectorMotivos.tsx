import { Box, Checkbox, FormControl, FormControlLabel, FormHelperText, FormLabel, Typography } from '@mui/material';
import type { MotivoTipificadoDto } from '@validasoft/api-client';
import { useId } from 'react';

export interface SelectorMotivosProps {
  etiqueta: string;
  ayuda: string;
  motivos: readonly MotivoTipificadoDto[];
  elegidos: readonly string[];
  onCambiar: (codigos: string[]) => void;
  error?: string | null;
  deshabilitado?: boolean;
}

/** Lista de motivos tipificados con casilla: nombre del motivo y, debajo, el texto que leerá el tercero. */
export function SelectorMotivos({ etiqueta, ayuda, motivos, elegidos, onCambiar, error, deshabilitado = false }: SelectorMotivosProps) {
  const idAyuda = useId();
  const alternar = (codigo: string) =>
    onCambiar(elegidos.includes(codigo) ? elegidos.filter((c) => c !== codigo) : [...elegidos, codigo]);

  return (
    <FormControl component="fieldset" error={Boolean(error)} disabled={deshabilitado} aria-describedby={idAyuda}>
      <FormLabel component="legend" sx={{ fontSize: 13, fontWeight: 600, color: 'text.primary', mb: 0.5 }}>
        {etiqueta}
      </FormLabel>
      <Box
        sx={{
          maxHeight: 232,
          overflowY: 'auto',
          border: 1,
          borderColor: error ? 'error.main' : 'divider',
          borderRadius: (t) => `${String(t.radio.sm)}px`,
          px: 1,
          py: 0.5,
        }}
      >
        {motivos.map((m) => (
          <FormControlLabel
            key={m.codigo}
            control={<Checkbox size="small" checked={elegidos.includes(m.codigo)} onChange={() => alternar(m.codigo)} sx={{ mt: '-2px' }} />}
            label={
              <Box sx={{ py: 0.5 }}>
                <Typography component="span" sx={{ display: 'block', fontSize: 14, lineHeight: '20px' }}>
                  {m.nombre}
                </Typography>
                <Typography component="span" sx={{ color: 'text.secondary', display: 'block', fontSize: 12, lineHeight: '16px' }}>
                  {m.textoAlTercero}
                </Typography>
              </Box>
            }
            sx={{ alignItems: 'flex-start', mx: 0, width: '100%' }}
          />
        ))}
      </Box>
      <FormHelperText id={idAyuda} sx={{ mx: 0 }}>
        {error ?? ayuda}
      </FormHelperText>
    </FormControl>
  );
}
