import { Chip, Tooltip } from '@mui/material';
import type { ExencionDeVinculoDto, ExencionResumenDto } from '@validasoft/api-client';
import { ShieldOff } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { formatearFechaDeclarada } from '../../documentos/formatoDatos';
import { resumenDeInsignia } from './modelo';

/** La insignia «Exento» (EX-6): se lee sin color, con su ícono y su texto; el tooltip dice de qué y hasta cuándo. */
function Insignia({ etiqueta, ayuda }: { etiqueta: string; ayuda: string }) {
  return (
    <Tooltip title={ayuda} describeChild>
      <Chip size="small" variant="outlined" icon={<ShieldOff size={14} strokeWidth={1.75} aria-hidden />} label={etiqueta} />
    </Tooltip>
  );
}

/** En el encabezado del vínculo: «Exento» (o «Exento de todo») si tiene exenciones vigentes; nada si no. */
export function ChipExento({ exenciones }: { exenciones: readonly ExencionDeVinculoDto[] | null | undefined }) {
  const { t } = useTranslation();
  const resumen = resumenDeInsignia(exenciones, t);
  return resumen ? <Insignia {...resumen} /> : null;
}

/** En una fila de la bandeja de vencimientos: la exención vigente que cubre ese documento. */
export function ChipExentoDeFila({ exencion, documento }: { exencion: ExencionResumenDto | null | undefined; documento: string }) {
  const { t } = useTranslation();
  if (!exencion) return null;
  const vigencia = exencion.hasta ? t('exenciones.hasta', { fecha: formatearFechaDeclarada(exencion.hasta) }) : t('exenciones.sinFecha');
  return (
    <Insignia
      etiqueta={exencion.todos ? t('exenciones.chipTodo') : t('exenciones.chip')}
      ayuda={t('exenciones.chipAyuda', { alcance: exencion.todos ? t('exenciones.alcanceTodo') : documento, vigencia })}
    />
  );
}
