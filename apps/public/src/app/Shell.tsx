import { BottomNavigation, BottomNavigationAction, Box, Button, Stack, Typography } from '@mui/material';
import { Isotipo } from '@validasoft/ui';
import { Building2, FolderCheck, IdCard, Landmark, LogOut } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Outlet, Link as RouterLink, useLocation } from 'react-router';
import { Campana } from '../notificaciones/Campana';
import { MODO_EMBEBIDO } from '../embebido/modo';
import { usePuenteEmbebido } from '../embebido/usePuenteEmbebido';
import { useSesionTercero } from '../sesion/sesionTercero';

const SECCIONES = [
  { ruta: '/boveda', clave: 'nav.boveda', icono: FolderCheck },
  { ruta: '/empresas', clave: 'nav.empresas', icono: Building2 },
  { ruta: '/ficha', clave: 'nav.ficha', icono: IdCard },
  { ruta: '/cuentas', clave: 'nav.cuentas', icono: Landmark },
] as const;

/**
 * Shell del tercero, móvil primero (design system «Registro», PantallaPublicoShell): cabecera con la marca, contenido de
 * una columna (máx. 720 px), pie "con Validasoft" y, con sesión, navegación inferior en el celular y superior en escritorio.
 */
export function Shell() {
  // Dentro del iframe de un sistema integrador: sin cabecera, navegación ni pie (el sistema pone su propio marco) y con el puente de mensajes.
  usePuenteEmbebido();
  if (MODO_EMBEBIDO) return <ShellEmbebido />;
  return <ShellNormal />;
}

/** El contenido solo, a todo el ancho del iframe y sin saltos laterales. */
function ShellEmbebido() {
  return (
    <Box component="main" sx={{ minHeight: '100dvh', bgcolor: 'background.default', px: { xs: 1.5, sm: 2 }, py: { xs: 1.5, sm: 2 }, maxWidth: 960, mx: 'auto' }}>
      <Outlet />
    </Box>
  );
}

function ShellNormal() {
  const { t } = useTranslation();
  const { pathname } = useLocation();
  const activa = useSesionTercero((s) => s.estado === 'activa');
  const salir = useSesionTercero((s) => s.salir);
  const actual = SECCIONES.find((s) => pathname.startsWith(s.ruta))?.ruta ?? (pathname.startsWith('/expediente') ? '/empresas' : false);
  // La verificación del certificado es pública: quien escanea el QR no es el tercero.
  const verificando = pathname.startsWith('/verificar/');
  // Una sección de Mi información lleva el panel de secciones a la izquierda: la página se ensancha.
  const ancha = /^\/ficha\/[^/]+/.test(pathname);

  return (
    <Box sx={{ minHeight: '100dvh', display: 'flex', flexDirection: 'column', bgcolor: 'background.default' }}>
      {/* Fija: al desplazarse, el tercero sigue viendo dónde está. */}
      <Box component="header" sx={{ position: 'sticky', top: 0, zIndex: 20, borderBottom: 1, borderColor: 'divider', bgcolor: 'background.paper' }}>
        <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', maxWidth: ancha ? 1160 : 960, mx: 'auto', px: 2, height: 56 }}>
          <Isotipo tamano={28} />
          <Box sx={{ minWidth: 0, flex: 1 }}>
            <Typography sx={{ fontWeight: 600, fontSize: 15, lineHeight: '20px' }}>{t('comun.marca')}</Typography>
            <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
              {verificando ? t('verificacion.marca') : t('comun.misDocumentos')}
            </Typography>
          </Box>
          {activa && (
            <Stack component="nav" aria-label={t('nav.secciones')} direction="row" spacing={0.5} sx={{ display: { xs: 'none', md: 'flex' } }}>
              {SECCIONES.map((s) => (
                <Button
                  key={s.ruta}
                  component={RouterLink}
                  to={s.ruta}
                  variant="text"
                  aria-current={actual === s.ruta ? 'page' : undefined}
                  sx={{ color: actual === s.ruta ? 'text.primary' : 'text.secondary', fontWeight: actual === s.ruta ? 600 : 500 }}
                >
                  {t(s.clave)}
                </Button>
              ))}
            </Stack>
          )}
          {activa && <Campana />}
          {activa && (
            <Button variant="text" onClick={() => void salir()} startIcon={<LogOut size={18} aria-hidden />} sx={{ color: 'text.secondary' }}>
              {t('nav.salir')}
            </Button>
          )}
        </Stack>
      </Box>

      <Box component="main" sx={{ flex: 1, width: '100%', maxWidth: ancha ? 1160 : 880, mx: 'auto', px: { xs: 1.5, sm: 3 }, pt: { xs: 1.5, sm: 2.5 }, pb: activa ? { xs: 12, md: 4 } : 4 }}>
        <Outlet />
      </Box>

      <Box component="footer" sx={{ display: activa ? { xs: 'none', md: 'block' } : 'block', borderTop: 1, borderColor: 'divider' }}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={{ xs: 0.5, sm: 1 }} sx={{ alignItems: 'center', justifyContent: 'center', py: 2, px: 2, color: 'text.secondary', fontSize: 13, textAlign: 'center' }}>
          <span>{t('comun.pie')}</span>
          <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
            <Isotipo tamano={14} />
            {t('comun.conValidasoft')}
          </Box>
        </Stack>
      </Box>

      {activa && (
        <Box
          component="nav"
          aria-label={t('nav.secciones')}
          sx={{ display: { xs: 'block', md: 'none' }, position: 'fixed', insetInline: 0, bottom: 0, borderTop: 1, borderColor: 'divider', zIndex: 10, pb: 'env(safe-area-inset-bottom)', bgcolor: 'background.paper' }}
        >
          <BottomNavigation showLabels value={actual} sx={{ height: 64, bgcolor: 'transparent' }}>
            {SECCIONES.map((s) => (
              <BottomNavigationAction
                key={s.ruta}
                component={RouterLink}
                to={s.ruta}
                value={s.ruta}
                label={t(s.clave)}
                icon={<s.icono size={22} aria-hidden />}
                aria-current={actual === s.ruta ? 'page' : undefined}
              />
            ))}
          </BottomNavigation>
        </Box>
      )}
    </Box>
  );
}
