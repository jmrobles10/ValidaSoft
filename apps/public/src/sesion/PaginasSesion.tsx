import { Button, CircularProgress, Stack, Typography } from '@mui/material';
import { PaginaEstado } from '@validasoft/ui';
import { SearchX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Navigate, Outlet, Link as RouterLink } from 'react-router';
import { PaginaAcceso } from './PaginaAcceso';
import { useSesionTercero } from './sesionTercero';

/** Mientras se intenta recuperar la sesión con la cookie (al arrancar), un instante de espera. */
function Restaurando() {
  const { t } = useTranslation();
  return (
    <Stack alignItems="center" spacing={2} sx={{ py: 10 }} role="status">
      <CircularProgress size={28} aria-hidden />
      <Typography color="text.secondary">{t('acceso.restaurando')}</Typography>
    </Stack>
  );
}

/** Rutas que necesitan la sesión del tercero: sin ella, «Entrar con mi correo» (no hay usuario ni contraseña). */
export function RequiereSesion() {
  const estado = useSesionTercero((s) => s.estado);
  if (estado === 'activa') return <Outlet />;
  if (estado === 'restaurando') return <Restaurando />;
  return <PaginaAcceso motivo={estado} />;
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
