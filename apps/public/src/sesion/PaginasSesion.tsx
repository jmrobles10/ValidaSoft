import { Button } from '@mui/material';
import { EsqueletoFormulario, PaginaEstado } from '@validasoft/ui';
import { SearchX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Navigate, Outlet, Link as RouterLink } from 'react-router';
import { PaginaAcceso } from './PaginaAcceso';
import { EsperandoSesion } from '../embebido/EsperandoSesion';
import { MODO_EMBEBIDO } from '../embebido/modo';
import { useSesionTercero } from './sesionTercero';

/** Mientras se intenta recuperar la sesión con la cookie (al arrancar), un instante de espera. */
function Restaurando() {
  const { t } = useTranslation();
  return <EsqueletoFormulario campos={1} conTarjeta etiqueta={t('acceso.restaurando')} sx={{ maxWidth: 420, mx: 'auto', mt: 4 }} />;
}

/** Rutas que necesitan la sesión del tercero: sin ella, «Entrar con mi correo» (no hay usuario ni contraseña). */
export function RequiereSesion() {
  const estado = useSesionTercero((s) => s.estado);
  if (estado === 'activa') return <Outlet />;
  // Dentro del iframe de un sistema integrador la identidad la pone el sistema: nunca se pide el correo (IO-5).
  if (MODO_EMBEBIDO) return <EsperandoSesion />;
  if (estado === 'restaurando') return <Restaurando />;
  return <PaginaAcceso motivo={estado} />;
}

/**
 * Rutas del tercero entero (su bóveda, su información, sus cuentas, su privacidad y sus accesos): una sesión aislada —su correo aún no es contacto del tercero— no
 * las alcanza y vuelve a «Mis empresas», donde está lo de su relación con la empresa que la invitó. La API las rechaza igual (403).
 */
export function RequiereAccesoCompleto() {
  const aislada = useSesionTercero((s) => s.sesion?.aislada === true);
  return aislada ? <Navigate to="/empresas" replace /> : <Outlet />;
}

/** `/entrar`: la entrada con correo; con sesión, directo a Mis empresas. */
export function PaginaEntrar() {
  const estado = useSesionTercero((s) => s.estado);
  if (estado === 'activa') return <Navigate to="/empresas" replace />;
  if (estado === 'restaurando') return <Restaurando />;
  return <PaginaAcceso motivo={estado} />;
}

export function PaginaNoEncontrada() {
  const { t } = useTranslation();
  const activa = useSesionTercero((s) => s.estado === 'activa');
  return (
    <PaginaEstado
      icono={SearchX}
      titulo={t('enlace.fallos.no-encontrado.titulo')}
      texto={t('acceso.paginaNoEncontrada')}
      acciones={
        <Button component={RouterLink} to={activa ? '/empresas' : '/entrar'} variant="contained">
          {activa ? t('nav.empresas') : t('acceso.titulo')}
        </Button>
      }
    />
  );
}
