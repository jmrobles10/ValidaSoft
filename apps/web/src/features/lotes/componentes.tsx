import { Box, Chip, LinearProgress, Typography } from '@mui/material';
import type { EstadoLote, LoteDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { porcentaje, procesables, TONO_LOTE } from './modelo';

/** Estado del lote (Procesando, Pausado, Completado, Con errores, Cancelado). */
export function ChipLote({ estado }: { estado: EstadoLote }) {
  const { t } = useTranslation();
  return <Chip size="small" variant="outlined" color={TONO_LOTE[estado]} label={t(`lotes.estados.${estado}`)} data-estado={estado} />;
}

/** Barra de avance con «n de m»; en proceso, la barra se anima mientras llega la siguiente tanda. */
export function AvanceLote({ lote, ancho = 160 }: { lote: LoteDto; ancho?: number | string }) {
  const { t } = useTranslation();
  const valor = porcentaje(lote.totales);
  const texto = t('lotes.avance', { procesadas: String(lote.totales.procesadas), total: String(procesables(lote.totales)) });
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
      <LinearProgress
        variant="determinate"
        value={valor}
        aria-label={texto}
        color={lote.estado === 'CompletadoConErrores' ? 'error' : lote.estado === 'Completado' ? 'success' : 'primary'}
        sx={{ width: ancho, flex: 'none', height: 6, borderRadius: 3 }}
      />
      <Typography variant="monoTabla" component="span" sx={{ whiteSpace: 'nowrap' }}>
        {texto}
      </Typography>
    </Box>
  );
}
