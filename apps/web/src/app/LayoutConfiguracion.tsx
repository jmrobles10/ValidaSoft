import { Box, Typography } from '@mui/material';
import { VARIABLE_TOPE } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, Outlet, useLocation } from 'react-router';
import { useEmpresaActiva, type ContextoEmpresa } from '../features/empresas/useEmpresas';
import { useScrollPropio } from '../sesion/alturaFija';
import { usePermisos } from '../shared/usePermisos';
import { SECCIONES_CONFIGURACION, type SeccionConfiguracion } from './secciones';

function Grupo({ titulo, secciones, empresaId, actual }: { titulo: string; secciones: readonly SeccionConfiguracion[]; empresaId: string; actual: string }) {
  const { t } = useTranslation();
  if (secciones.length === 0) return null;
  return (
    <>
      <Typography variant="overline" component="p" color="text.secondary" sx={{ display: 'block', px: 1, pt: 2, pb: 0.75, '&:first-of-type': { pt: 0 } }}>
        {titulo}
      </Typography>
      <Box component="ul" sx={{ m: 0, p: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '2px' }}>
        {secciones.map(({ ruta, icono: Icono }) => {
          const activa = actual === ruta;
          return (
            <li key={ruta}>
              <Box
                component={RouterLink}
                to={`/e/${empresaId}/${ruta}`}
                aria-current={activa ? 'page' : undefined}
                sx={{
                  display: 'flex',
                  alignItems: 'center',
                  gap: 1.25,
                  height: 34,
                  px: 1.25,
                  borderRadius: (th) => `${String(th.radio.sm)}px`,
                  color: activa ? 'text.primary' : 'text.secondary',
                  bgcolor: activa ? 'acento.tinte' : 'transparent',
                  fontSize: 14,
                  fontWeight: activa ? 600 : 500,
                  textDecoration: 'none',
                  '& svg': { color: activa ? 'acento.main' : 'inherit' },
                  '&:hover': { bgcolor: activa ? 'acento.tinte' : 'superficie.nivel2', color: 'text.primary' },
                }}
              >
                <Icono size={18} strokeWidth={1.75} aria-hidden />
                {t(`nav.${ruta}`)}
              </Box>
            </li>
          );
        })}
      </Box>
    </>
  );
}

/** Configuración: barra de secciones a la izquierda, separada en grupo (tenant) y empresa activa. */
export function LayoutConfiguracion() {
  const { t } = useTranslation();
  const contexto = useEmpresaActiva();
  const { empresa } = contexto;
  const permisos = usePermisos(empresa.id);
  const { pathname } = useLocation();
  const actual = pathname.split('/')[3] ?? '';
  const visibles = SECCIONES_CONFIGURACION.filter((s) => permisos.puede(s.scope));
  // Los formularios de configuración crecen (etapas, documentos, miembros): embebida, la sección usa scroll propio.
  useScrollPropio();

  return (
    <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: '232px minmax(0, 1fr)' }, gap: 3, alignItems: 'start' }}>
      <Box component="nav" aria-label={t('nav.configuracion')} sx={{ position: { md: 'sticky' }, top: `calc(var(${VARIABLE_TOPE}, 0px) + 24px)` }}>
        <Grupo titulo={t('configuracion.grupo')} secciones={visibles.filter((s) => s.alcance === 'grupo')} empresaId={empresa.id} actual={actual} />
        <Grupo titulo={empresa.nombre} secciones={visibles.filter((s) => s.alcance === 'empresa')} empresaId={empresa.id} actual={actual} />
      </Box>
      <Box sx={{ minWidth: 0 }}>
        <Outlet context={contexto satisfies ContextoEmpresa} />
      </Box>
    </Box>
  );
}
