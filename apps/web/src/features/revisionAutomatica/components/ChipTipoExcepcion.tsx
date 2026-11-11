import { Chip, Tooltip } from '@mui/material';
import { Eye, TriangleAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { TipoDeExcepcion } from '../modelo';

const ICONOS = { Excepcion: TriangleAlert, Muestreo: Eye } as const;
const TONOS = { Excepcion: 'estado.advertencia.texto', Muestreo: 'estado.info.texto' } as const;

/** Tipo de una fila de la bandeja: «Excepción» (la IA no pudo aprobarlo) o «Muestreo» (aprobado por el sistema, por confirmar). */
export function ChipTipoExcepcion({ tipo }: { tipo: TipoDeExcepcion }) {
  const { t } = useTranslation();
  const Icono = ICONOS[tipo];
  return (
    <Tooltip title={t(`revisionAutomatica.excepciones.tiposAyuda.${tipo}`)} describeChild>
      <Chip
        size="small"
        variant="outlined"
        data-tipo={tipo}
        icon={<Icono size={14} strokeWidth={1.75} aria-hidden />}
        label={t(`revisionAutomatica.excepciones.tipos.${tipo}`)}
        sx={{ flex: 'none', color: 'text.primary', borderColor: 'borde.sutil', borderRadius: (th) => `${String(th.radio.xs)}px`, '& .MuiChip-icon': { color: TONOS[tipo] } }}
      />
    </Tooltip>
  );
}
