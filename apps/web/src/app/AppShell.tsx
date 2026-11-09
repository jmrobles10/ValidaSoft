import { Box, IconButton, Tooltip, Typography } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { Isotipo, SelectorEmpresa, VARIABLE_TOPE } from '@validasoft/ui';
import { Link2, Moon, Sun } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, Outlet, useLocation, useNavigate } from 'react-router';
import { aOpcion, useEmpresaActiva, type ContextoEmpresa } from '../features/empresas/useEmpresas';
import { Campana } from '../features/notificaciones/Campana';
import { useAlturaFija } from '../sesion/alturaFija';
import { useSesion } from '../sesion/sesionStore';
import { useYo } from '../sesion/useYo';
import { usePreferencias } from '../shared/preferencias';
import { usePermisos } from '../shared/usePermisos';
import { SECCIONES_CONFIGURACION } from './secciones';

interface Pestana {
  clave: 'bandeja' | 'revalidaciones' | 'vencimientos' | 'vinculos' | 'configuracion';
  ruta: string;
  /** Rutas que marcan la pestaña como activa. */
  prefijos: readonly string[];
}

/**
 * AppShell embebido (dirección Registro): una sola fila de 48 px dentro del iframe del anfitrión, sin login ni menú
 * lateral propios. Isotipo, empresa activa, secciones y "Sesión desde SRM".
 */
export function AppShell() {
  const { t } = useTranslation();
  const contexto = useEmpresaActiva();
  const { empresa, empresas } = contexto;
  const permisos = usePermisos(empresa.id);
  const sistema = useSesion((s) => s.sesion?.sistema);
  // Quién soy (`GET /yo`, RQ-9): el nombre de la persona junto al sistema desde el que entró.
  const yo = useYo();
  const persona = yo.data?.usuario?.nombre ?? null;
  const { modo, temaDelContenedor, fijarModo } = usePreferencias();
  const navegar = useNavigate();
  const { pathname } = useLocation();
  // Embebida en una página de edición: alto fijo y `main` con scroll propio, para que el encabezado de la página se
  // pegue arriba de él (ver `sesion/alturaFija.ts`). Si no, desplaza la ventana y se pega bajo esta barra.
  const alturaFija = useAlturaFija((s) => s.altura);

  const configuracion = SECCIONES_CONFIGURACION.filter((s) => permisos.puede(s.scope));
  const pestanas: Pestana[] = [];
  if (permisos.puede(SCOPES.expedientesLeer)) {
    pestanas.push({ clave: 'bandeja', ruta: 'bandeja', prefijos: ['bandeja', 'expedientes'] });
    pestanas.push({ clave: 'revalidaciones', ruta: 'revalidaciones', prefijos: ['revalidaciones'] });
  }
  if (permisos.puede(SCOPES.documentosLeer)) pestanas.push({ clave: 'vencimientos', ruta: 'vencimientos', prefijos: ['vencimientos'] });
  if (permisos.puede(SCOPES.vinculosLeer)) pestanas.push({ clave: 'vinculos', ruta: 'vinculos', prefijos: ['vinculos'] });
  const primera = configuracion[0];
  if (primera) pestanas.push({ clave: 'configuracion', ruta: primera.ruta, prefijos: SECCIONES_CONFIGURACION.map((s) => s.ruta) });

  const relativa = pathname.slice(`/e/${empresa.id}/`.length);
  const activa = pestanas.find((p) => p.prefijos.some((r) => relativa === r || relativa.startsWith(`${r}/`)));
  // Al cambiar de empresa se conserva la sección, no el recurso (el expediente o flujo es de la otra empresa).
  const primerSegmento = relativa.split('/')[0] ?? '';
  const seccionActual = primerSegmento === 'expedientes' ? 'bandeja' : primerSegmento || 'vinculos';

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        bgcolor: 'background.default',
        ...(alturaFija === null ? { minHeight: '100vh' } : { height: alturaFija, overflow: 'hidden' }),
        // Con scroll de ventana, bajo esta barra; con scroll propio de `main`, el sticky se mide desde el borde del
        // contenido, así que se resta el relleno de `main` para que la franja quede pegada a la barra.
        [VARIABLE_TOPE]: (t) => (alturaFija === null ? `${String(t.densidad.cabeceraEmbebida)}px` : `-${t.spacing(3)}`),
      }}
    >
      <Box
        component="header"
        sx={{
          position: 'sticky',
          top: 0,
          zIndex: (t) => t.zIndex.appBar,
          display: 'flex',
          alignItems: 'center',
          gap: 1.5,
          height: (t) => t.densidad.cabeceraEmbebida,
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
        <SelectorEmpresa
          etiqueta={t('empresas.etiqueta')}
          empresas={empresas.map(aOpcion)}
          actual={empresa.id}
          onCambiar={(id) => void navegar(`/e/${id}/${seccionActual}`)}
        />
        <Box component="nav" aria-label={t('nav.principal')} sx={{ display: 'flex', alignSelf: 'stretch', gap: '2px', ml: 1, minWidth: 0, overflowX: 'auto' }}>
          {pestanas.map((p) => {
            const actual = activa?.clave === p.clave;
            return (
              <Box
                key={p.clave}
                component={RouterLink}
                to={`/e/${empresa.id}/${p.ruta}`}
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
                {t(`nav.${p.clave}`)}
              </Box>
            );
          })}
        </Box>
        <Box sx={{ flex: 1 }} />
        {sistema && (
          <Typography
            component="span"
            color="text.secondary"
            sx={{ display: { xs: 'none', md: 'inline-flex' }, alignItems: 'center', gap: 0.5, fontSize: 12, lineHeight: '16px', whiteSpace: 'nowrap' }}
          >
            <Link2 size={14} strokeWidth={1.75} aria-hidden />
            {persona ? t('app.sesionDePersona', { persona, sistema }) : t('app.sesionDe', { sistema })}
          </Typography>
        )}
        <Campana empresaId={empresa.id} />
        {!temaDelContenedor && (
          <Tooltip title={modo === 'dark' ? t('app.tema.claro') : t('app.tema.oscuro')}>
            <IconButton aria-label={modo === 'dark' ? t('app.tema.claro') : t('app.tema.oscuro')} onClick={() => fijarModo(modo === 'dark' ? 'light' : 'dark')}>
              {modo === 'dark' ? <Sun size={18} strokeWidth={1.75} /> : <Moon size={18} strokeWidth={1.75} />}
            </IconButton>
          </Tooltip>
        )}
      </Box>
      <Box component="main" sx={{ flex: 1, py: 2, px: 2.5, minWidth: 0, ...(alturaFija !== null && { minHeight: 0, overflowY: 'auto' }) }}>
        <Outlet context={contexto satisfies ContextoEmpresa} />
      </Box>
    </Box>
  );
}
