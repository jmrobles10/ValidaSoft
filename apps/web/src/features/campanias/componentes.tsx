import { Box, Chip, LinearProgress, Typography } from '@mui/material';
import type { CampaniaDto, EstadoCampania } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { porcentaje, TONO_CAMPANIA } from './modelo';

/** Estado de la campaña (Procesando, Pausada, Completada, Con errores, Cancelada). */
export function ChipCampania({ estado }: { estado: EstadoCampania }) {
  const { t } = useTranslation();
  return <Chip size="small" variant="outlined" color={TONO_CAMPANIA[estado]} label={t(`campanias.estados.${estado}`)} data-estado={estado} />;
}

/** Barra de avance con «n de m»; en proceso, la barra se anima mientras llega la siguiente tanda. */
export function AvanceCampania({ campania, ancho = 160 }: { campania: CampaniaDto; ancho?: number | string }) {
  const { t } = useTranslation();
  const valor = porcentaje(campania.totales);
  const texto = t('campanias.avance', { procesadas: String(campania.totales.procesadas), total: String(campania.totales.filas) });
  return (
    <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, minWidth: 0 }}>
      <LinearProgress
        variant="determinate"
        value={valor}
        aria-label={texto}
        color={campania.estado === 'CompletadaConErrores' ? 'error' : campania.estado === 'Completada' ? 'success' : 'primary'}
        sx={{ width: ancho, flex: 'none', height: 6, borderRadius: 3 }}
      />
      <Typography variant="monoTabla" component="span" sx={{ whiteSpace: 'nowrap' }}>
        {texto}
      </Typography>
    </Box>
  );
}
