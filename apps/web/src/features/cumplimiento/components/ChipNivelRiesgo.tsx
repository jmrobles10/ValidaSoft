import { Chip, useTheme } from '@mui/material';
import { ShieldAlert, ShieldCheck, ShieldX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { claveRiesgo, tonoDeRiesgo } from '../modelo';

const ICONOS = { exito: ShieldCheck, advertencia: ShieldAlert, error: ShieldX } as const;

/** Nivel de riesgo 1–3 como chip del sistema: contorno sutil, texto primario y el tono (verde, ámbar, rojo) solo en el ícono. */
export function ChipNivelRiesgo({ nivel, size = 'small' }: { nivel: number; size?: 'small' | 'medium' }) {
  const { t } = useTranslation();
  const tema = useTheme();
  const tono = tonoDeRiesgo(nivel);
  const Icono = ICONOS[tono];
  return (
    <Chip
      variant="outlined"
      size={size}
      data-nivel={nivel}
      icon={<Icono size={size === 'small' ? 14 : 16} strokeWidth={1.75} aria-hidden />}
      label={t(`cumplimiento.riesgos.${claveRiesgo(nivel)}`)}
      sx={{
        color: 'text.primary',
        borderColor: 'borde.sutil',
        borderRadius: `${String(tema.radio.xs)}px`,
        '& .MuiChip-icon': { color: tema.palette.estado[tono].texto },
      }}
    />
  );
}
