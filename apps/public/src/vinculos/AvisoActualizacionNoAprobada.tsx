import { Alert, AlertTitle, Box, Typography } from '@mui/material';
import type { VinculoPublicoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';

/**
 * Una actualización suya que la empresa no aprobó (VR-13): se rechazó (con los motivos que se le comunicaron) o se anuló (sin motivos: son internos). El
 * vínculo conserva lo que ya estaba aprobado. Dura hasta que se abre otro expediente.
 */
export function AvisoActualizacionNoAprobada({
  vinculo,
  breve = false,
}: {
  vinculo: Pick<VinculoPublicoDto, 'empresa' | 'actualizacionNoAprobada'>;
  /** Solo el título y los motivos: la pantalla ya cuenta el resto (el encabezado de su revisión). */
  breve?: boolean;
}) {
  const { t } = useTranslation();
  const aviso = vinculo.actualizacionNoAprobada;
  if (!aviso) return null;
  const empresa = vinculo.empresa.nombre;
  return (
    <Alert severity="warning" sx={{ mt: 1.5 }}>
      <AlertTitle>{t('actualizacionNoAprobada.titulo')}</AlertTitle>
      {!breve && (
        <Typography sx={{ fontSize: 14 }}>
          {t(`actualizacionNoAprobada.${aviso.resultado === 'Anulado' ? 'Anulado' : 'Rechazado'}`, {
            empresa,
            cuando: aviso.fecha ? t('actualizacionNoAprobada.el', { fecha: formatearFecha(aviso.fecha) }) : '',
          })}
        </Typography>
      )}
      {aviso.motivos.length > 0 && (
        <Box sx={{ mt: 0.5 }}>
          <Typography sx={{ fontSize: 14, fontWeight: 600 }}>{t('actualizacionNoAprobada.motivos')}</Typography>
          <Box component="ul" sx={{ m: 0, pl: 2.5 }}>
            {aviso.motivos.map((m) => (
              <li key={m.codigo}>
                <Typography sx={{ fontSize: 14 }}>{m.texto}</Typography>
              </li>
            ))}
          </Box>
        </Box>
      )}
      {!breve && <Typography sx={{ fontSize: 14, mt: 0.5 }}>{t('actualizacionNoAprobada.sigueVigente', { empresa })}</Typography>}
    </Alert>
  );
}
