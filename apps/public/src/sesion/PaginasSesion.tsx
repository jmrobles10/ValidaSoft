import { Button } from '@mui/material';
import { PaginaEstado } from '@validasoft/ui';
import { Clock, LogOut, MailOpen, SearchX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Outlet, Link as RouterLink } from 'react-router';
import { useSesionTercero, type EstadoSesionTercero } from './sesionTercero';

/** Rutas que necesitan la sesión del tercero: sin ella, se explica cómo entrar (no hay usuario ni contraseña). */
export function RequiereSesion() {
  const estado = useSesionTercero((s) => s.estado);
  if (estado === 'activa') return <Outlet />;
  return <PaginaSinSesion estado={estado} />;
}

export function PaginaSinSesion({ estado }: { estado: Exclude<EstadoSesionTercero, 'activa'> }) {
  const { t } = useTranslation();
  if (estado === 'vencida') return <PaginaEstado icono={Clock} titulo={t('sesion.vencidaTitulo')} texto={t('sesion.vencida')} rol="alert" />;
  if (estado === 'salio') return <PaginaEstado icono={LogOut} titulo={t('sesion.salioTitulo')} texto={t('sesion.salio')} rol="status" />;
  return <PaginaEstado icono={MailOpen} titulo={t('sesion.sinSesionTitulo')} texto={t('sesion.sinSesion')} />;
}

export function PaginaNoEncontrada() {
  const { t } = useTranslation();
  const activa = useSesionTercero((s) => s.estado === 'activa');
  return (
    <PaginaEstado
      icono={SearchX}
      titulo={t('enlace.fallos.no-encontrado.titulo')}
      texto={t('sesion.sinSesion')}
      acciones={
        activa ? (
          <Button component={RouterLink} to="/empresas" variant="contained">
            {t('nav.empresas')}
          </Button>
        ) : undefined
      }
    />
  );
}
