import { Alert, AlertTitle } from '@mui/material';
import type { VinculoDto } from '@validasoft/api-client';
import { CalendarClock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { formatearFechaDeclarada } from '../../documentos/formatoDatos';

/**
 * Vigencia periódica del vínculo (VV-1): con el vínculo por vencer, la fecha en que vence; vencido, que tiene su renovación abierta
 * (o que aún no se abre). Un vínculo vencido por un documento no trae fecha propia: solo se dice que venció.
 */
export function AvisoDeVigencia({ vinculo }: { vinculo: Pick<VinculoDto, 'estado' | 'vigenteHasta' | 'expedienteAbiertoId'> }) {
  const { t } = useTranslation();
  if (vinculo.estado === 'PorVencer' && vinculo.vigenteHasta) {
    return (
      <Alert severity="warning" icon={<CalendarClock size={18} strokeWidth={1.75} />} sx={{ mb: 2 }}>
        <AlertTitle>{t('vigenciaVinculo.porVencerTitulo')}</AlertTitle>
        {t('vigenciaVinculo.porVencerTexto', { fecha: formatearFechaDeclarada(vinculo.vigenteHasta) })}
      </Alert>
    );
  }
  if (vinculo.estado === 'Vencido') {
    return (
      <Alert severity="error" icon={<CalendarClock size={18} strokeWidth={1.75} />} sx={{ mb: 2 }}>
        <AlertTitle>{t('vigenciaVinculo.vencidoTitulo')}</AlertTitle>
        {vinculo.vigenteHasta
          ? t(vinculo.expedienteAbiertoId ? 'vigenciaVinculo.vencidoTexto' : 'vigenciaVinculo.vencidoSinRenovacion', { fecha: formatearFechaDeclarada(vinculo.vigenteHasta) })
          : null}
      </Alert>
    );
  }
  return null;
}
