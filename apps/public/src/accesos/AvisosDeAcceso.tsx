import { Alert, AlertTitle, Button } from '@mui/material';
import { ShieldAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useLocation } from 'react-router';
import { useSesionTercero } from '../sesion/sesionTercero';
import { useAccesosSolicitados } from './api';
import { nombreDelTercero, porDecidirPorMi } from './modelo';

/**
 * Lo que ve quien entra con un correo que aún no es contacto del tercero (acceso aislado), en todas las pantallas: su acceso espera la aprobación de un contacto y
 * mientras tanto solo ve y sube lo de su relación con la empresa que lo invitó. Nombra al tercero con sus letras tapadas.
 */
export function AvisoDeAccesoAislado() {
  const { t } = useTranslation();
  const sesion = useSesionTercero((s) => s.sesion);
  if (!sesion?.aislada) return null;
  const tercero = nombreDelTercero(sesion.terceroEnmascarado, t('accesoAislado.aviso.sinNombre'));
  return (
    <Alert severity="warning" icon={<ShieldAlert size={20} aria-hidden />} role="status" sx={{ mb: 2 }}>
      <AlertTitle>{t('accesoAislado.aviso.titulo')}</AlertTitle>
      {t('accesoAislado.aviso.texto', { tercero })}
    </Alert>
  );
}

/** Para un contacto verificado: hay correos que una empresa invitó y esperan su decisión (a un contacto sin verificar no le toca decidir). Se oculta en la propia pantalla de decisión. */
export function AvisoDeAccesosPorAprobar() {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const completa = useSesionTercero((s) => s.estado === 'activa' && s.sesion?.aislada !== true);
  const consulta = useAccesosSolicitados(completa);
  const pendientes = consulta.data ? porDecidirPorMi(consulta.data).length : 0;
  if (!completa || pendientes === 0 || pathname.startsWith('/accesos')) return null;
  return (
    <Alert
      severity="info"
      icon={<ShieldAlert size={20} aria-hidden />}
      role="status"
      sx={{ mb: 2 }}
      action={
        <Button component={RouterLink} to="/accesos" color="inherit" size="small">
          {t('accesoAislado.pendientes.revisar')}
        </Button>
      }
    >
      <AlertTitle>{t('accesoAislado.pendientes.titulo', { count: pendientes })}</AlertTitle>
      {t('accesoAislado.pendientes.texto')}
    </Alert>
  );
}
