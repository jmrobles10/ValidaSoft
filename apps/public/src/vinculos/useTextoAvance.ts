import { useTranslation } from 'react-i18next';
import { formatearDia } from '../vigencia/vigencia';
import type { AvanceTercero } from './avance';

/** Frase debajo del chip, la misma en «Mis empresas» y en la revisión. */
export function useTextoAvance() {
  const { t } = useTranslation();
  return (avance: AvanceTercero, empresa: string) => {
    switch (avance.tipo) {
      case 'faltan':
        return t('expediente.faltan', { count: avance.documentos, empresa });
      case 'enRevision':
        return t('expediente.estadoTexto.EnRevision', { empresa });
      case 'observado':
        return t('expediente.estadoTexto.Observado', { empresa });
      case 'aprobado':
        return t('expediente.estadoTexto.Aprobado', { empresa });
      case 'preVencimiento':
        return avance.hasta
          ? t('avance.textoPreVencimiento', { empresa, fecha: formatearDia(avance.hasta) })
          : t('avance.textoPreVencimientoSinFecha', { empresa });
      case 'porVencer':
        return avance.hasta
          ? t('avance.textoPorVencer', { empresa, fecha: formatearDia(avance.hasta) })
          : t('avance.textoPorVencerSinFecha', { empresa });
      case 'ratificar':
        return t('avance.textoRatificar', { empresa });
    }
  };
}
