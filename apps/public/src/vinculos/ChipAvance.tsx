import { ChipEstado } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { ESTADO_DEL_AVANCE, type AvanceTercero } from './avance';

/** Chip con lo que le toca al tercero («Faltan 3 documentos», «Tienes observaciones»), con el ícono del estado. */
export function ChipAvance({ avance }: { avance: AvanceTercero }) {
  const { t } = useTranslation();
  const etiqueta = avance.tipo === 'faltan' ? t('avance.faltan', { count: avance.documentos }) : t(`avance.${avance.tipo}`);
  return <ChipEstado estado={ESTADO_DEL_AVANCE[avance.tipo]} etiqueta={etiqueta} size="medium" />;
}
