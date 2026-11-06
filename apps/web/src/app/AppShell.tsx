import { AppBar, Box, Container, IconButton, Stack, Tab, Tabs, Toolbar, Tooltip, Typography } from '@mui/material';
import { SCOPES, type Scope } from '@validasoft/api-client';
import { SelectorEmpresa } from '@validasoft/ui';
import { Moon, Sun } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, Outlet, useLocation, useNavigate } from 'react-router';
import { aOpcion, useEmpresaActiva, type ContextoEmpresa } from '../features/empresas/useEmpresas';
import { useSesion } from '../sesion/sesionStore';
import { usePreferencias } from '../shared/preferencias';
import { usePermisos } from '../shared/usePermisos';

interface Seccion {
  ruta: string;
  clave: 'vinculos' | 'areas' | 'usuarios' | 'sistemas';
  scope: Scope;
}

const SECCIONES: readonly Seccion[] = [
  { ruta: 'vinculos', clave: 'vinculos', scope: SCOPES.vinculosLeer },
  { ruta: 'areas', clave: 'areas', scope: SCOPES.configuracionLeer },
  { ruta: 'usuarios', clave: 'usuarios', scope: SCOPES.usuariosAdministrar },
  { ruta: 'sistemas', clave: 'sistemas', scope: SCOPES.sistemasAdministrar },
];

function Marca() {
  return (
    <Stack direction="row" spacing={1} alignItems="center">
      <Box aria-hidden sx={{ width: 26, height: 26, borderRadius: '9px', background: (tema) => tema.palette.degradadoMarca, display: 'grid', placeItems: 'center' }}>
        <Typography component="span" sx={{ color: '#fff', fontWeight: 700, fontSize: 15, lineHeight: 1 }}>
          V
        </Typography>
      </Box>
      <Typography component="span" sx={{ fontWeight: 600, letterSpacing: '-0.01em' }}>
        Validasoft
      </Typography>
    </Stack>
  );
}

/** Cabecera compacta (la pantalla vive dentro de otro sistema): marca, empresa activa, secciones y tema. */
export function AppShell() {
  const { t } = useTranslation();
  const contexto = useEmpresaActiva();
  const { empresa, empresas } = contexto;
  const permisos = usePermisos(empresa.id);
  const sistema = useSesion((s) => s.sesion?.sistema);
  const { modo, temaDelContenedor, fijarModo } = usePreferencias();
  const navegar = useNavigate();
  const { pathname } = useLocation();

  const visibles = SECCIONES.filter((s) => permisos.puede(s.scope));
  const actual = visibles.find((s) => pathname.startsWith(`/e/${empresa.id}/${s.ruta}`))?.ruta ?? false;

  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <AppBar position="sticky" color="inherit" elevation={0} sx={{ borderBottom: 1, borderColor: 'divider', bgcolor: 'background.paper' }}>
        <Toolbar sx={{ gap: 2, flexWrap: 'wrap', py: 1 }}>
          <Marca />
          <SelectorEmpresa
            etiqueta={t('empresas.etiqueta')}
            empresas={empresas.map(aOpcion)}
            actual={empresa.id}
            onCambiar={(id) => void navegar(`/e/${id}/${actual || 'vinculos'}`)}
          />
          <Box sx={{ flex: 1 }} />
          {sistema && (
            <Typography variant="caption" color="text.secondary">
              {t('app.sesionDe', { sistema })}
            </Typography>
          )}
          {!temaDelContenedor && (
            <Tooltip title={modo === 'dark' ? t('app.tema.claro') : t('app.tema.oscuro')}>
              <IconButton
                aria-label={modo === 'dark' ? t('app.tema.claro') : t('app.tema.oscuro')}
                onClick={() => fijarModo(modo === 'dark' ? 'light' : 'dark')}
              >
                {modo === 'dark' ? <Sun size={18} /> : <Moon size={18} />}
              </IconButton>
            </Tooltip>
          )}
        </Toolbar>
        <Tabs value={actual} aria-label={t('nav.principal')} sx={{ px: 2, minHeight: 40, '& .MuiTab-root': { minHeight: 40 } }}>
          {visibles.map((s) => (
            <Tab key={s.ruta} value={s.ruta} label={t(`nav.${s.clave}`)} component={RouterLink} to={`/e/${empresa.id}/${s.ruta}`} />
          ))}
        </Tabs>
      </AppBar>
      <Container maxWidth="xl" component="main" sx={{ py: 3 }}>
        <Outlet context={contexto satisfies ContextoEmpresa} />
      </Container>
    </Box>
  );
}
