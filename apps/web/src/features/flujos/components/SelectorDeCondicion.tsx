import { MenuItem, TextField } from '@mui/material';
import type { CondicionDto } from '@validasoft/api-client';
import { atajoDe, condicionDe, type AtajoCondicion } from '../condiciones';

export interface SelectorDeCondicionProps {
  etiqueta: string;
  condicion: CondicionDto | null;
  onCambiar: (condicion: CondicionDto | null) => void;
  deshabilitado: boolean;
  /** Texto de cada opción; `personalizada` solo se ofrece cuando la condición la armó la API y no es un atajo. */
  textoDe: (atajo: AtajoCondicion) => string;
  ayuda?: string;
}

const ATAJOS = ['siempre', 'juridica', 'natural'] as const;

/**
 * Elige la condición entre los atajos del uso diario (sin condición, solo personas jurídicas, solo naturales): el mismo editor
 * de condiciones de «Se pide a» de los requisitos (RQ-12) y de «Se escoge cuando» de la selección del flujo (FR-1). Una condición armada
 * por la API (país, atributos del vínculo, combinaciones) aparece como «personalizada» y se conserva tal cual.
 */
export function SelectorDeCondicion({ etiqueta, condicion, onCambiar, deshabilitado, textoDe, ayuda }: SelectorDeCondicionProps) {
  const atajo = atajoDe(condicion);
  return (
    <TextField
      select
      size="small"
      label={etiqueta}
      value={atajo}
      onChange={(ev) => onCambiar(condicionDe(ev.target.value as AtajoCondicion, condicion))}
      disabled={deshabilitado}
      helperText={ayuda}
    >
      {ATAJOS.map((a) => (
        <MenuItem key={a} value={a}>
          {textoDe(a)}
        </MenuItem>
      ))}
      {atajo === 'personalizada' && <MenuItem value="personalizada">{textoDe('personalizada')}</MenuItem>}
    </TextField>
  );
}
