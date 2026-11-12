import { Alert, AlertTitle, Button } from '@mui/material';
import type { VinculoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { CircleSlash } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';

/**
 * La última actualización del tercero no se aprobó (VR-13): se rechazó o se anuló y el vínculo conserva su estado (RV-2). Lo dice con la fecha y lleva al
 * expediente (sus motivos y su historial). Dura hasta que se abre otro expediente: la siguiente actualización o una renovación.
 */
export function AvisoActualizacionNoAprobada({ vinculo, empresaId }: { vinculo: Pick<VinculoDto, 'actualizacionNoAprobada'>; empresaId: string }) {
  const { t } = useTranslation();
  const aviso = vinculo.actualizacionNoAprobada;
  if (!aviso) return null;
  const clave = aviso.resultado === 'Rechazado' ? 'rechazada' : 'anulada';
  return (
    <Alert
      severity="warning"
      icon={<CircleSlash size={18} strokeWidth={1.75} />}
      sx={{ mb: 2 }}
      action={
        aviso.expedienteId ? (
          <Button component={RouterLink} to={`/e/${empresaId}/expedientes/${aviso.expedienteId}`} color="inherit" size="small">
            {t('vinculos.actualizacionNoAprobada.ver')}
          </Button>
        ) : undefined
      }
    >
      <AlertTitle>{t(`vinculos.actualizacionNoAprobada.${clave}.titulo`)}</AlertTitle>
      {t(`vinculos.actualizacionNoAprobada.${clave}.texto`, { fecha: aviso.fecha ? formatearFecha(aviso.fecha) : '' })}
    </Alert>
  );
}
