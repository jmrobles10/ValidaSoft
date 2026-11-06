import { Box, Chip, LinearProgress, Typography } from '@mui/material';
import type { EstadoAreaExpediente, TerceroResumenDto } from '@validasoft/api-client';
import { ChipEstado } from '@validasoft/ui';
import { Clock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { identificacionResumida } from '../vinculos/tercero';
import { estadoCanonicoDeArea } from './reglas';

/**
 * Estado del área en el expediente. `Pendiente`, `Aprobada` y `Observada` usan el mapa canónico; `Esperando` (su
 * etapa aún no empieza) no tiene estado en el design system y se pinta como chip neutro con reloj.
 */
export function ChipEstadoArea({ estado, desde }: { estado: EstadoAreaExpediente; desde?: string | null }) {
  const { t } = useTranslation();
  const canonico = estadoCanonicoDeArea(estado);
  if (canonico) return <ChipEstado estado={canonico} fecha={desde ?? null} />;
  return (
    <Chip
      size="small"
      variant="outlined"
      icon={<Clock size={14} strokeWidth={1.75} aria-hidden />}
      label={t('bandeja.estadosArea.Esperando')}
      sx={{ color: 'text.secondary', borderColor: 'borde.sutil', borderRadius: (th) => `${String(th.radio.xs)}px`, '& .MuiChip-icon': { color: 'estado.neutro.texto' } }}
    />
  );
}

/** "1 de 2" con una barra fina; el texto accesible dice "1 de 2 aprobaciones". */
export function Aprobaciones({ aprobaciones, requeridas }: { aprobaciones: number; requeridas: number }) {
  const { t } = useTranslation();
  const valor = requeridas > 0 ? Math.min(100, (aprobaciones / requeridas) * 100) : 0;
  return (
    <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 1, minWidth: 0 }} aria-label={t('bandeja.aprobacionesAccesible', { aprobaciones: String(aprobaciones), requeridas: String(requeridas) })} role="img">
      <LinearProgress
        variant="determinate"
        value={valor}
        aria-hidden
        sx={{ width: 40, height: 4, borderRadius: 2, bgcolor: 'superficie.nivel2', '& .MuiLinearProgress-bar': { bgcolor: 'estado.exito.solido', borderRadius: 2 } }}
      />
      <Typography component="span" variant="monoTabla" aria-hidden>
        {t('bandeja.aprobaciones', { aprobaciones: String(aprobaciones), requeridas: String(requeridas) })}
      </Typography>
    </Box>
  );
}

/** Nombre del tercero con su identificación en la misma línea. */
export function TerceroEnLinea({ tercero }: { tercero: TerceroResumenDto }) {
  return (
    <Box component="span" sx={{ display: 'inline-flex', alignItems: 'baseline', gap: 1, minWidth: 0, maxWidth: '100%' }}>
      <Typography component="span" noWrap sx={{ fontWeight: 500 }}>
        {tercero.nombre}
      </Typography>
      <Typography component="span" color="text.secondary" sx={{ fontSize: 12, lineHeight: '16px', whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>
        {identificacionResumida(tercero.identificacion)}
      </Typography>
    </Box>
  );
}
