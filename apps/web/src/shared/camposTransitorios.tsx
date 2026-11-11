import { TextField, type SxProps, type Theme } from '@mui/material';
import { useState, type ReactNode } from 'react';
import { isoDeTexto, mascaraDeFecha, textoDeIso } from './fechaEnTexto';

/**
 * TRANSITORIO. Esta rama partió de `main` antes de UI-7 (`CampoFecha` y `CampoNumero` de `@validasoft/ui`, que además
 * traen `@mui/x-date-pickers` y `dayjs`). Estos dos campos tienen **el mismo contrato** de esos (valor y salida ISO
 * `aaaa-mm-dd`, o solo dígitos) y no usan ningún control nativo del navegador. Al integrar con `origin/main`: cambiar el
 * import a `@validasoft/ui` y borrar este archivo y su prueba.
 */

export interface CampoFechaProps {
  etiqueta: string;
  /** Fecha ISO `aaaa-mm-dd`; `''`, `null` o sin definir es «sin fecha». */
  valor: string | null | undefined;
  /** Recibe la fecha ISO, o `''` si el campo quedó vacío o con una fecha incompleta o inexistente. */
  onCambiar: (iso: string) => void;
  requerido?: boolean;
  deshabilitado?: boolean;
  error?: boolean;
  ayuda?: ReactNode;
  /** Primera y última fecha elegibles (ISO). */
  minimo?: string;
  maximo?: string;
  onBlur?: () => void;
  fullWidth?: boolean;
  id?: string;
  name?: string;
  sx?: SxProps<Theme>;
}

export function CampoFecha({ etiqueta, valor, onCambiar, requerido, deshabilitado, error, ayuda, minimo, maximo, onBlur, fullWidth, id, name, sx }: CampoFechaProps) {
  const entrante = valor ?? '';
  const [texto, setTexto] = useState(() => textoDeIso(entrante));
  const [conocido, setConocido] = useState(entrante);
  const [tocado, setTocado] = useState(false);
  // Si el padre cambia el valor por su cuenta (limpiar filtros), se vuelve a leer; lo que el campo emitió, no.
  if (entrante !== conocido) {
    setConocido(entrante);
    setTexto(textoDeIso(entrante));
  }

  const iso = isoDeTexto(texto);
  const fueraDeRango = iso !== '' && ((minimo !== undefined && iso < minimo) || (maximo !== undefined && iso > maximo));
  const incompleta = texto !== '' && iso === '';
  const conError = error === true || (tocado && (incompleta || fueraDeRango));

  return (
    <TextField
      id={id}
      name={name}
      label={etiqueta}
      value={texto}
      placeholder="dd/mm/aaaa"
      required={requerido}
      disabled={deshabilitado}
      fullWidth={fullWidth}
      error={conError}
      helperText={error === true ? ayuda : tocado && incompleta ? 'Escribe una fecha válida (dd/mm/aaaa).' : tocado && fueraDeRango ? 'La fecha está fuera del rango permitido.' : ayuda}
      onChange={(e) => {
        const nuevo = mascaraDeFecha(e.target.value);
        const nuevoIso = isoDeTexto(nuevo);
        setTexto(nuevo);
        setConocido(nuevoIso);
        onCambiar(nuevoIso);
      }}
      onBlur={() => {
        setTocado(true);
        onBlur?.();
      }}
      sx={sx}
      slotProps={{ htmlInput: { inputMode: 'numeric', autoComplete: 'off', maxLength: 10 }, inputLabel: { shrink: true } }}
    />
  );
}

export interface CampoNumeroProps {
  etiqueta: string;
  /** El número (o su texto); `''`, `null` o sin definir es «vacío». */
  valor: number | string | null | undefined;
  /** Recibe el texto del campo con solo dígitos (`''` = vacío). */
  onCambiar: (texto: string) => void;
  placeholder?: string;
  requerido?: boolean;
  deshabilitado?: boolean;
  error?: boolean;
  ayuda?: ReactNode;
  maxDigitos?: number;
  fullWidth?: boolean;
  onBlur?: () => void;
  id?: string;
  name?: string;
  sx?: SxProps<Theme>;
}

/** Entero no negativo: texto con teclado numérico, sin las flechas ni la rueda del `<input type="number">`. */
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
