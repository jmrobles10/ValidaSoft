import { Box } from '@mui/material';
import { useRef, type ClipboardEvent, type KeyboardEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { normalizarCodigo } from './canje';

/**
 * Seis casillas de un dígito (AceptacionConEvidencia del design system): `inputMode="numeric"`, `one-time-code` en la
 * primera para que el celular ofrezca el código del correo, pegar los 6 dígitos de una vez y retroceso entre casillas.
 */
export function CodigoOtp({ valor, onCambiar, deshabilitado, error }: { valor: string; onCambiar: (valor: string) => void; deshabilitado?: boolean; error?: boolean }) {
  const { t } = useTranslation();
  const casillas = useRef<(HTMLInputElement | null)[]>([]);
  const digitos = Array.from({ length: 6 }, (_, i) => valor[i] ?? '');

  const enfocar = (i: number) => casillas.current[Math.max(0, Math.min(5, i))]?.focus();

  const escribir = (i: number, texto: string) => {
    const limpio = normalizarCodigo(texto);
    if (limpio.length > 1) {
      // Pegado o autocompletado: reparte desde esta casilla.
      const nuevo = normalizarCodigo(valor.slice(0, i) + limpio);
      onCambiar(nuevo);
      enfocar(nuevo.length);
      return;
    }
    const arreglo = [...digitos];
    arreglo[i] = limpio;
    onCambiar(normalizarCodigo(arreglo.join('')));
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
    <Box role="group" aria-label={t('enlace.otpCampo')} sx={{ display: 'grid', gridTemplateColumns: 'repeat(6, minmax(0, 52px))', gap: 1 }}>
      {digitos.map((d, i) => (
        <Box
          key={i}
          component="input"
          ref={(nodo: HTMLInputElement | null) => {
            casillas.current[i] = nodo;
          }}
          value={d}
          onChange={(e) => escribir(i, e.target.value)}
          onKeyDown={(e) => alTeclear(i, e)}
          onPaste={(e) => alPegar(i, e)}
          onFocus={(e) => e.target.select()}
          inputMode="numeric"
          autoComplete={i === 0 ? 'one-time-code' : 'off'}
          maxLength={i === 0 ? 6 : 1}
          disabled={deshabilitado}
          aria-label={t('enlace.otpDigito', { n: String(i + 1) })}
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
