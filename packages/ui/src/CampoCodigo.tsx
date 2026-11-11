import { Box } from '@mui/material';
import { useRef, type ClipboardEvent, type KeyboardEvent } from 'react';

/** Solo dígitos y como mucho `longitud`: el código se pega o se escribe en el celular. */
export function normalizarCodigoNumerico(texto: string, longitud = 6): string {
  return texto.replace(/\D/g, '').slice(0, longitud);
}

export interface CampoCodigoProps {
  valor: string;
  onCambiar: (valor: string) => void;
  /** Etiqueta accesible del grupo («Código de 6 dígitos»). */
  etiqueta: string;
  /** Etiqueta accesible de cada casilla («Dígito 2 de 6»). */
  etiquetaDigito: (numero: number) => string;
  longitud?: number;
  deshabilitado?: boolean;
  error?: boolean;
  /** Enfoca la primera casilla al montar (el código es lo único que se espera en la pantalla). */
  enfocarAlMontar?: boolean;
}

/**
 * Casillas de un dígito (AceptacionConEvidencia del design system): `inputMode="numeric"`, `one-time-code` en la primera para que el celular
 * ofrezca el código del correo, pegar los dígitos de una vez y retroceso entre casillas. La usan el código del tercero (OTP) y el del ingreso directo.
 */
export function CampoCodigo({ valor, onCambiar, etiqueta, etiquetaDigito, longitud = 6, deshabilitado, error, enfocarAlMontar }: CampoCodigoProps) {
  const casillas = useRef<(HTMLInputElement | null)[]>([]);
  const yaEnfocada = useRef(false);
  const digitos = Array.from({ length: longitud }, (_, i) => valor[i] ?? '');
  const ultima = longitud - 1;

  const enfocar = (i: number) => casillas.current[Math.max(0, Math.min(ultima, i))]?.focus();

  const escribir = (i: number, texto: string) => {
    const limpio = normalizarCodigoNumerico(texto, longitud);
    if (limpio.length > 1) {
      // Pegado o autocompletado: reparte desde esta casilla.
      const nuevo = normalizarCodigoNumerico(valor.slice(0, i) + limpio, longitud);
      onCambiar(nuevo);
      enfocar(nuevo.length);
      return;
    }
    const arreglo = [...digitos];
    arreglo[i] = limpio;
    onCambiar(normalizarCodigoNumerico(arreglo.join(''), longitud));
    if (limpio) enfocar(i + 1);
  };

  const alTeclear = (i: number, evento: KeyboardEvent<HTMLInputElement>) => {
    if (evento.key === 'Backspace' && !digitos[i] && i > 0) {
      evento.preventDefault();
      onCambiar(valor.slice(0, i - 1));
      enfocar(i - 1);
    } else if (evento.key === 'ArrowLeft') enfocar(i - 1);
    else if (evento.key === 'ArrowRight') enfocar(i + 1);
  };

  const alPegar = (i: number, evento: ClipboardEvent<HTMLInputElement>) => {
    evento.preventDefault();
    escribir(i, evento.clipboardData.getData('text'));
  };

  return (
    <Box role="group" aria-label={etiqueta} sx={{ display: 'grid', gridTemplateColumns: `repeat(${String(longitud)}, minmax(0, 52px))`, gap: 1 }}>
      {digitos.map((d, i) => (
        <Box
          key={i}
          component="input"
          ref={(nodo: HTMLInputElement | null) => {
            casillas.current[i] = nodo;
            // Solo la primera vez que aparece la primera casilla (no en cada renderizado).
            if (nodo && i === 0 && enfocarAlMontar && !yaEnfocada.current) {
              yaEnfocada.current = true;
              nodo.focus();
            }
          }}
          value={d}
          onChange={(e) => escribir(i, e.target.value)}
          onKeyDown={(e) => alTeclear(i, e)}
          onPaste={(e) => alPegar(i, e)}
          onFocus={(e) => e.target.select()}
          inputMode="numeric"
          autoComplete={i === 0 ? 'one-time-code' : 'off'}
          // La primera casilla admite el código completo (autocompletado del celular); el resto, un dígito.
          maxLength={i === 0 ? longitud : 1}
          disabled={deshabilitado}
          aria-label={etiquetaDigito(i + 1)}
          aria-invalid={error ? true : undefined}
          sx={(th) => ({
            width: '100%',
            height: 56,
            textAlign: 'center',
            fontFamily: th.typography.fontFamily,
            fontVariantNumeric: 'tabular-nums',
            fontSize: 22,
            fontWeight: 600,
            color: 'text.primary',
            bgcolor: 'background.paper',
            border: 1,
            borderColor: error ? 'error.main' : 'divider',
            borderRadius: `${String(th.radio.md)}px`,
            outline: 'none',
            '&:focus-visible': { borderColor: 'primary.main', boxShadow: `0 0 0 2px ${th.palette.primary.main}` },
            '&:disabled': { opacity: 0.6 },
          })}
        />
      ))}
    </Box>
  );
}
