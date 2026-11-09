import { useTheme } from '@mui/material';
import { useId } from 'react';

/** Isotipo de Validasoft: la "V" redondeada sobre el degradado 135° de marca. Decorativo (el nombre va en texto). */
export function Isotipo({ tamano = 22 }: { tamano?: number }) {
  const { palette } = useTheme();
  const id = useId();
  return (
    <svg width={tamano} height={tamano} viewBox="0 0 32 32" aria-hidden focusable="false" style={{ flex: 'none', display: 'block' }}>
      <defs>
        <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor={palette.marca.azul} />
          <stop offset="1" stopColor={palette.marca.verde} />
        </linearGradient>
      </defs>
      <rect width="32" height="32" rx="9" fill={`url(#${id})`} />
      <path d="M10 11.25 15.1 21a1 1 0 0 0 1.8 0L22 11.25" fill="none" stroke="#FFFFFF" strokeWidth="3.25" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}
