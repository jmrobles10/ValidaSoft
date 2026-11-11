import { TextField, type TextFieldProps } from '@mui/material';

type Props = Omit<TextFieldProps, 'value' | 'onChange' | 'type' | 'variant'> & {
  /** Los días como texto: vacío = sin valor. */
  valor: string;
  onCambiar: (valor: string) => void;
  /** El mayor valor que acepta el campo: define cuántos dígitos se escriben. */
  maximo: number;
};

/**
 * Una cantidad de días con el campo de texto del sistema (teclado numérico y solo dígitos), no el input numérico del navegador con
 * sus flechas. El rango lo valida el editor con su mensaje, no el navegador.
 */
export function CampoDias({ valor, onCambiar, maximo, slotProps, ...resto }: Props) {
  return (
    <TextField
      size="small"
      {...resto}
      value={valor}
      onChange={(evento) => onCambiar(evento.target.value.replace(/\D/g, ''))}
      slotProps={{ ...slotProps, htmlInput: { inputMode: 'numeric', autoComplete: 'off', maxLength: String(maximo).length } }}
    />
  );
}
