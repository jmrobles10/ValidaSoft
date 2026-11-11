import { Box, IconButton, Tooltip, Typography } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { Isotipo, SelectorEmpresa, VARIABLE_TOPE, visualmenteOculto } from '@validasoft/ui';
import { Link2, Moon, Sun } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Outlet, useLocation, useNavigate } from 'react-router';
import { aOpcion, useEmpresaActiva, type ContextoEmpresa } from '../features/empresas/useEmpresas';
import { Campana } from '../features/notificaciones/Campana';
import { useAlturaFija } from '../sesion/alturaFija';
import { BotonConfiguracion } from './BotonConfiguracion';
import { NavegacionPrincipal, type Pestana } from './NavegacionPrincipal';
import { RELLENO_CONTENIDO } from './rellenoContenido';
import { useSesion } from '../sesion/sesionStore';
import { useYo } from '../sesion/useYo';
import { usePreferencias } from '../shared/preferencias';
import { usePermisos } from '../shared/usePermisos';
import { SECCIONES_CONFIGURACION } from './secciones';

/**
 * AppShell embebido (dirección Registro): una sola fila de 48 px dentro del iframe del anfitrión, sin login ni menú
 * lateral propios. Isotipo, empresa activa, secciones (pestañas con «Más» adaptativo), campana, Configuración (engranaje)
 * y "Sesión desde SRM".
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
    pestanas.push({ clave: 'inicio', ruta: 'inicio', prefijos: ['inicio'] });
    pestanas.push({ clave: 'bandeja', ruta: 'bandeja', prefijos: ['bandeja', 'expedientes'] });
    pestanas.push({ clave: 'revalidaciones', ruta: 'revalidaciones', prefijos: ['revalidaciones'] });
  }
  if (permisos.puede(SCOPES.documentosLeer)) pestanas.push({ clave: 'vencimientos', ruta: 'vencimientos', prefijos: ['vencimientos'] });
  // La pantalla del oficial: quien consulta listas o decide como Cumplimiento (H7).
  if (permisos.puede(SCOPES.cumplimientoDecidir) || permisos.puede(SCOPES.verificacionesEscribir)) {
    pestanas.push({ clave: 'cumplimiento', ruta: 'cumplimiento', prefijos: ['cumplimiento'] });
  }
  if (permisos.puede(SCOPES.certificadosLeer)) pestanas.push({ clave: 'certificados', ruta: 'certificados', prefijos: ['certificados'] });
  if (permisos.puede(SCOPES.vinculosLeer)) pestanas.push({ clave: 'vinculos', ruta: 'vinculos', prefijos: ['vinculos', 'terceros', 'invitaciones'] });
  if (permisos.puede(SCOPES.expedientesLeer) || permisos.puede(SCOPES.vinculosLeer))
    pestanas.push({ clave: 'informes', ruta: permisos.puede(SCOPES.expedientesLeer) ? 'informes/estado-expedientes' : 'informes/vinculos', prefijos: ['informes'] });
  if (permisos.puede(SCOPES.auditoriaLeer)) pestanas.push({ clave: 'auditoria', ruta: 'auditoria', prefijos: ['auditoria'] });
  // Configuración no es una pestaña: es el engranaje de la derecha, activo en cualquiera de sus secciones.
  const primeraConfiguracion = configuracion[0];

  const relativa = pathname.slice(`/e/${empresa.id}/`.length);
  const enRuta = (prefijo: string) => relativa === prefijo || relativa.startsWith(`${prefijo}/`);
  const activa = pestanas.find((p) => p.prefijos.some(enRuta));
  const enConfiguracion = SECCIONES_CONFIGURACION.some((s) => enRuta(s.ruta));
  // Al cambiar de empresa se conserva la sección, no el recurso (el expediente o flujo es de la otra empresa).
  const primerSegmento = relativa.split('/')[0] ?? '';
  // La ficha del tercero cuelga de Vínculos (no hay lista de terceros).
  const seccionActual =
    primerSegmento === 'expedientes' ? 'bandeja' : primerSegmento === 'terceros' ? 'vinculos' : primerSegmento === 'invitaciones' ? 'invitaciones/lotes' : primerSegmento || 'vinculos';

  return (
    <Box
      sx={{
        display: 'flex',
        flexDirection: 'column',
        bgcolor: 'background.default',
        ...(alturaFija === null ? { minHeight: '100vh' } : { height: alturaFija, overflow: 'hidden' }),
        // Con scroll de ventana, bajo esta barra; con scroll propio de `main`, el sticky se mide desde el borde del
        // contenido, así que se resta el relleno de `main` (`RELLENO_CONTENIDO`) para que la franja quede pegada a la barra.
        [VARIABLE_TOPE]: (t) => (alturaFija === null ? `${String(t.densidad.cabeceraEmbebida)}px` : `-${t.spacing(RELLENO_CONTENIDO.y)}`),
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
        <NavegacionPrincipal pestanas={pestanas} empresaId={empresa.id} activa={activa?.clave ?? null} />
        {sistema && (
          // El texto completo solo cabe en pantallas anchas: en las demás queda el ícono con el texto en un tooltip (y
          // oculto solo a la vista, para los lectores de pantalla), así caben más pestañas antes de pasar a «Más».
          <Tooltip title={persona ? t('app.sesionDePersona', { persona, sistema }) : t('app.sesionDe', { sistema })}>
            <Typography
              component="span"
              sx={{ color: 'text.secondary', display: { xs: 'none', md: 'inline-flex' }, flex: 'none', alignItems: 'center', gap: 0.5, fontSize: 12, lineHeight: '16px', whiteSpace: 'nowrap' }}
            >
              <Link2 size={14} strokeWidth={1.75} aria-hidden />
              <Box component="span" sx={{ display: 'inline', '@media (max-width: 1535.95px)': visualmenteOculto }}>
                {persona ? t('app.sesionDePersona', { persona, sistema }) : t('app.sesionDe', { sistema })}
              </Box>
            </Typography>
          </Tooltip>
        )}
        <Campana empresaId={empresa.id} />
        {primeraConfiguracion && <BotonConfiguracion destino={`/e/${empresa.id}/${primeraConfiguracion.ruta}`} activo={enConfiguracion} />}
        {!temaDelContenedor && (
          <Tooltip title={modo === 'dark' ? t('app.tema.claro') : t('app.tema.oscuro')}>
            <IconButton aria-label={modo === 'dark' ? t('app.tema.claro') : t('app.tema.oscuro')} onClick={() => fijarModo(modo === 'dark' ? 'light' : 'dark')}>
              {modo === 'dark' ? <Sun size={18} strokeWidth={1.75} /> : <Moon size={18} strokeWidth={1.75} />}
            </IconButton>
          </Tooltip>
        )}
      </Box>
      <Box component="main" sx={{ flex: 1, py: RELLENO_CONTENIDO.y, px: RELLENO_CONTENIDO.x, minWidth: 0, ...(alturaFija !== null && { minHeight: 0, overflowY: 'auto' }) }}>
        <Outlet context={contexto satisfies ContextoEmpresa} />
      </Box>
    </Box>
  );
}
