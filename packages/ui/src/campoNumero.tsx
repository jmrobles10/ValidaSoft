import { TextField } from '@mui/material';
import type { ReactNode } from 'react';
import type { SxProps, Theme } from '@mui/material';

export interface CampoNumeroProps {
  etiqueta: string;
  /** El número (o su texto); `''`, `null` o sin definir es «vacío». */
  valor: number | string | null | undefined;
  /** Recibe el texto del campo con solo dígitos (`''` = vacío); quien lo usa decide si lo guarda como texto o como número. */
  onCambiar: (texto: string) => void;
  placeholder?: string;
  requerido?: boolean;
  deshabilitado?: boolean;
  error?: boolean;
  ayuda?: ReactNode;
  /** Cuántos dígitos admite (por omisión 9: cabe en un entero de 32 bits). */
  maxDigitos?: number;
  fullWidth?: boolean;
  onBlur?: () => void;
  id?: string;
  name?: string;
  sx?: SxProps<Theme>;
}

/**
 * Campo para un entero no negativo (UI-7): texto con teclado numérico en el celular, sin las flechas ni la rueda del
 * `<input type="number">` del navegador. Solo deja escribir dígitos (lo demás —letras, signos, el pegado— se descarta).
 */
export function CampoNumero({ etiqueta, valor, onCambiar, placeholder, requerido, deshabilitado, error, ayuda, maxDigitos = 9, fullWidth, onBlur, id, name, sx }: CampoNumeroProps) {
  return (
    <TextField
      id={id}
      name={name}
      label={etiqueta}
      value={valor === null || valor === undefined ? '' : String(valor)}
      onChange={(e) => onCambiar(e.target.value.replace(/\D/g, '').slice(0, maxDigitos))}
      onBlur={onBlur}
      placeholder={placeholder}
      required={requerido}
      disabled={deshabilitado}
      error={error}
      helperText={ayuda}
      fullWidth={fullWidth}
      sx={sx}
      slotProps={{ htmlInput: { inputMode: 'numeric', pattern: '[0-9]*', autoComplete: 'off', maxLength: maxDigitos } }}
    />
  );
}
