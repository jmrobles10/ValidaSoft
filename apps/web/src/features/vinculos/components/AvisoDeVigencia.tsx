import { Alert, AlertTitle } from '@mui/material';
import type { VinculoDto } from '@validasoft/api-client';
import { CalendarClock } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { formatearFechaDeclarada } from '../../documentos/formatoDatos';

/**
 * Vigencia periódica del vínculo (VV-1): con el vínculo por vencer (pre-vencimiento), la fecha en que vence y si su renovación está
 * abierta; vencido, que la renovación sigue abierta (o que aún no se abre). Un vínculo vencido por un documento no trae fecha propia: solo se dice que venció.
 * El expediente abierto de un vínculo «En revisión» (VR-1) es una actualización, no la renovación: no se dice que la renovación esté abierta.
 */
export function AvisoDeVigencia({ vinculo }: { vinculo: Pick<VinculoDto, 'estado' | 'vigenteHasta' | 'expedienteAbiertoId' | 'enRevision'> }) {
  const { t } = useTranslation();
  const renovacionAbierta = Boolean(vinculo.expedienteAbiertoId) && !vinculo.enRevision;
  if (vinculo.estado === 'PorVencer' && vinculo.vigenteHasta) {
    return (
      <Alert severity="warning" icon={<CalendarClock size={18} strokeWidth={1.75} />} sx={{ mb: 2 }}>
        <AlertTitle>{t('vigenciaVinculo.porVencerTitulo')}</AlertTitle>
        {t(renovacionAbierta ? 'vigenciaVinculo.porVencerTexto' : 'vigenciaVinculo.porVencerSinRenovacion', { fecha: formatearFechaDeclarada(vinculo.vigenteHasta) })}
      </Alert>
    );
  }
  if (vinculo.estado === 'Vencido') {
    return (
      <Alert severity="error" icon={<CalendarClock size={18} strokeWidth={1.75} />} sx={{ mb: 2 }}>
        <AlertTitle>{t('vigenciaVinculo.vencidoTitulo')}</AlertTitle>
        {vinculo.vigenteHasta
          ? t(renovacionAbierta ? 'vigenciaVinculo.vencidoTexto' : 'vigenciaVinculo.vencidoSinRenovacion', { fecha: formatearFechaDeclarada(vinculo.vigenteHasta) })
          : null}
      </Alert>
    );
  }
  return null;
}
