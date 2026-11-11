import { Box, IconButton, Tooltip, Typography } from '@mui/material';
import { esSesionDePlataforma } from '@validasoft/api-client';
import { Isotipo, VARIABLE_TOPE } from '@validasoft/ui';
import { Moon, ShieldCheck, Sun } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, Navigate, Outlet, useLocation } from 'react-router';
import { RELLENO_CONTENIDO } from '../../app/rellenoContenido';
import { AvisoDePasskey } from '../cuenta/AvisoDePasskey';
import { MenuDeCuenta } from '../cuenta/MenuDeCuenta';
import { useAlturaFija, useScrollPropio } from '../../sesion/alturaFija';
import { useSesion } from '../../sesion/sesionStore';
import { usePreferencias } from '../../shared/preferencias';

const PESTANAS = [
  { clave: 'grupos', ruta: '/plataforma/grupos' },
  { clave: 'usoIa', ruta: '/plataforma/uso-ia' },
  { clave: 'operadores', ruta: '/plataforma/operadores' },
  { clave: 'ingresos', ruta: '/plataforma/ingresos' },
  { clave: 'secretos', ruta: '/plataforma/secretos' },
] as const;

/** `/`: la sesión de un operador de Plataforma va a su consola; la de una persona de un tenant, a su empresa. */
export function SegunSesion({ deTenant }: { deTenant: ReactNode }) {
  const sesion = useSesion((s) => s.sesion);
  return sesion && esSesionDePlataforma(sesion) ? <Navigate to="/plataforma/grupos" replace /> : deTenant;
}

/** Las rutas de un tenant no aplican a la sesión de Plataforma (no tiene tenant ni empresas). */
export function SoloTenant({ children }: { children: ReactNode }) {
  const sesion = useSesion((s) => s.sesion);
  return sesion && esSesionDePlataforma(sesion) ? <Navigate to="/plataforma/grupos" replace /> : children;
}

/**
 * Consola de Plataforma (P-15-2, PT-4): solo con la sesión de un operador (sin tenant, `plataforma:tenants`). Una fila de
 * 48 px como el AppShell de la empresa: isotipo, «Plataforma», sus secciones y quién opera desde qué sistema.
 */
export function ShellPlataforma() {
  const { t } = useTranslation();
  const sesion = useSesion((s) => s.sesion);
  const { modo, temaDelContenedor, fijarModo } = usePreferencias();
  const { pathname } = useLocation();
  // Embebida, toda la consola tiene alto fijo y `main` con scroll propio (ver `sesion/alturaFija.ts`): la lista de grupos es
  // virtual y necesita un área que desplace, y así el iframe no cambia de alto ni de ancho útil al pasar de una pestaña a otra.
  useScrollPropio();
  const alturaFija = useAlturaFija((s) => s.altura);

  if (!sesion || !esSesionDePlataforma(sesion)) return <Navigate to="/" replace />;

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        bgcolor: 'background.default',
        ...(alturaFija === null ? { minHeight: '100vh' } : { height: alturaFija, overflow: 'hidden' }),
        // Con scroll de ventana, bajo la barra; con scroll propio de `main`, se resta el relleno de `main` para que el
        // encabezado de la página quede pegado a la barra (igual que en AppShell).
        [VARIABLE_TOPE]: (th) => (alturaFija === null ? `${String(th.densidad.cabeceraEmbebida)}px` : `-${th.spacing(RELLENO_CONTENIDO.y)}`),
      }}
    >
      <Box
        component="header"
        sx={{
          position: 'sticky',
          top: 0,
          zIndex: (th) => th.zIndex.appBar,
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          height: (th) => th.densidad.cabeceraEmbebida,
          pl: 2,
          pr: 1.5,
          borderBottom: 1,
          borderColor: 'divider',
          bgcolor: 'background.paper',
          minWidth: 0,
        }}
      >
        <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 1, flex: 'none' }}>
          <Isotipo />
          <Typography component="span" sx={{ fontSize: 15, lineHeight: '20px', fontWeight: 600, letterSpacing: '-0.01em' }}>
            {t('app.nombre')}
          </Typography>
        </Box>
        <Box aria-hidden sx={{ flex: 'none', width: '1px', height: 24, bgcolor: 'divider' }} />
        <Typography component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75, fontSize: 14, fontWeight: 600, flex: 'none' }}>
          <ShieldCheck size={16} strokeWidth={1.75} aria-hidden />
          {t('plataforma.titulo')}
        </Typography>
        <Box component="nav" aria-label={t('nav.principal')} sx={{ display: 'flex', alignSelf: 'stretch', gap: '2px', ml: 1, minWidth: 0, overflowX: 'auto' }}>
          {PESTANAS.map((p) => {
            const actual = pathname.startsWith(p.ruta);
            return (
              <Box
                key={p.clave}
                component={RouterLink}
                to={p.ruta}
                aria-current={actual ? 'page' : undefined}
                sx={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  px: 1.25,
                  borderBottom: 2,
                  borderColor: actual ? 'acento.main' : 'transparent',
                  color: actual ? 'text.primary' : 'text.secondary',
                  fontSize: 14,
                  lineHeight: '20px',
                  fontWeight: actual ? 600 : 500,
                  textDecoration: 'none',
                  whiteSpace: 'nowrap',
                  '&:hover': { color: 'text.primary' },
                  '&:focus-visible': { outlineOffset: -2 },
                }}
              >
                {t(`plataforma.nav.${p.clave}`)}
              </Box>
            );
          })}
        </Box>
        <Box sx={{ flex: 1 }} />
        {sesion.sistema && (
          <Typography component="span" sx={{ color: 'text.secondary', display: { xs: 'none', md: 'inline' }, fontSize: 12, lineHeight: '16px', whiteSpace: 'nowrap' }}>
            {t('plataforma.sesion', { persona: sesion.nombre ?? sesion.usuarioId, sistema: sesion.sistema })}
          </Typography>
        )}
        <MenuDeCuenta rutaMiCuenta="/plataforma/mi-cuenta" />
        {!temaDelContenedor && (
          <Tooltip title={modo === 'dark' ? t('app.tema.claro') : t('app.tema.oscuro')}>
            <IconButton aria-label={modo === 'dark' ? t('app.tema.claro') : t('app.tema.oscuro')} onClick={() => fijarModo(modo === 'dark' ? 'light' : 'dark')}>
              {modo === 'dark' ? <Sun size={18} strokeWidth={1.75} /> : <Moon size={18} strokeWidth={1.75} />}
            </IconButton>
          </Tooltip>
        )}
      </Box>
      <AvisoDePasskey rutaMiCuenta="/plataforma/mi-cuenta" />
      <Box component="main" sx={{ flex: 1, py: RELLENO_CONTENIDO.y, px: RELLENO_CONTENIDO.x, minWidth: 0, ...(alturaFija !== null && { minHeight: 0, overflowY: 'auto', scrollbarGutter: 'stable' }) }}>
        <Outlet />
      </Box>
    </Box>
  );
}
