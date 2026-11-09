import { Alert, Button, Stack, Typography } from '@mui/material';
import type { CuentaDesignadaDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { Check, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { DatosCuenta, SeccionCuenta } from './DatosCuenta';
import { nombresDe } from './modelo';

interface PropuestaCuentaProps {
  cuenta: CuentaDesignadaDto;
  aprobadoresRequeridos: number;
  /** Hay una cuenta designada que sigue vigente mientras se aprueba esta. */
  hayDesignada: boolean;
  /** `cuentas:aprobar` y la API dice que esta persona aún puede aprobar (no lo ha hecho). */
  puedeAprobar: boolean;
  /** Tiene el permiso pero ya aprobó: falta otra persona. */
  yaAprobo: boolean;
  ocupado: boolean;
  onAprobar: () => void;
  onRechazar: () => void;
}

/** Cuenta que propuso el tercero: progreso de los cuatro ojos y, a quien puede, aprobar o rechazar. */
export function PropuestaCuenta({ cuenta, aprobadoresRequeridos, hayDesignada, puedeAprobar, yaAprobo, ocupado, onAprobar, onRechazar }: PropuestaCuentaProps) {
  const { t } = useTranslation();
  const n = String(cuenta.aprobaciones.length);
  const total = String(aprobadoresRequeridos);
  const progreso =
    cuenta.aprobaciones.length > 0 ? t('cuentas.aprobacionesDe', { n, total, nombres: nombresDe(cuenta.aprobaciones, t) }) : t('cuentas.aprobaciones', { n, total });

  return (
    <SeccionCuenta titulo={t('cuentas.secciones.porAprobar')} cuenta={cuenta} fecha={cuenta.propuestaEn}>
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {t('cuentas.propuestaEl', { fecha: formatearFecha(cuenta.propuestaEn) })}
      </Typography>
      <DatosCuenta cuenta={cuenta} />
      <Alert severity="info">
        <Typography variant="body2" component="span" sx={{ display: 'block' }}>
          {progreso}
        </Typography>
        {t(hayDesignada ? 'cuentas.cuatroOjos' : 'cuentas.cuatroOjosSinAnterior', { total })}
      </Alert>
      {yaAprobo && (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('cuentas.yaAprobaste')}
        </Typography>
      )}
      {puedeAprobar && (
        <Stack direction="row" spacing={1}>
          <Button variant="contained" startIcon={<Check size={16} aria-hidden />} disabled={ocupado} onClick={onAprobar}>
            {t('cuentas.aprobar')}
          </Button>
          <Button variant="text" color="error" startIcon={<X size={16} aria-hidden />} disabled={ocupado} onClick={onRechazar} sx={{ color: 'estado.error.texto' }}>
            {t('cuentas.rechazar')}
          </Button>
        </Stack>
      )}
    </SeccionCuenta>
  );
}
