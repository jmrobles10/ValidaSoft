import { Checkbox, Tooltip } from '@mui/material';
import type { RevalidacionDto } from '@validasoft/api-client';
import { APARIENCIA_ESTADO, ChipEstado, type Estado } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { esDevuelta } from './modelo';

/**
 * Casilla de una revalidación: se marca para aprobarla en lote (si no cambió) o para recordarle al tercero (si está devuelta).
 * Si no admite ninguna de las dos, queda deshabilitada y el tooltip dice por qué.
 */
export function CasillaRevalidacion({
  fila,
  marcada,
  puedeAprobar,
  puedeRecordar,
  onAlternar,
}: {
  fila: RevalidacionDto;
  marcada: boolean;
  puedeAprobar: boolean;
  puedeRecordar: boolean;
  onAlternar: () => void;
}) {
  const { t } = useTranslation();
  const seleccionable = (puedeAprobar && fila.aprobableEnLote) || (puedeRecordar && esDevuelta(fila));
  const casilla = (
    <Checkbox
      size="small"
      checked={marcada}
      disabled={!seleccionable}
      onClick={(e) => e.stopPropagation()}
      onChange={onAlternar}
      slotProps={{ input: { 'aria-label': t('revalidaciones.seleccionar', { documento: fila.tipoDocumento, tercero: fila.tercero.nombre }) } }}
      sx={{ p: 0.5 }}
    />
  );
  return seleccionable ? (
    casilla
  ) : (
    <Tooltip title={t('revalidaciones.noSeleccionable')}>
      <span>{casilla}</span>
    </Tooltip>
  );
}

const esEstadoConocido = (valor: string): valor is Estado => Object.hasOwn(APARIENCIA_ESTADO, valor);

/** Estado del documento en la revisión; el observado se dice como lo entiende la empresa: «Devuelta al tercero». */
export function EstadoDeRevalidacion({ fila }: { fila: RevalidacionDto }) {
  const { t } = useTranslation();
  if (!esEstadoConocido(fila.estadoRequisito)) return <>{fila.estadoRequisito}</>;
  return <ChipEstado estado={fila.estadoRequisito} etiqueta={esDevuelta(fila) ? t('revalidaciones.devuelta') : undefined} />;
}
