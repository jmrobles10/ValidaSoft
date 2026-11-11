import { Box, ButtonBase, Menu, MenuItem } from '@mui/material';
import { ChevronDown } from 'lucide-react';
import { useId, useState, type RefObject } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { cuantasPestanasCaben, SEPARACION_PESTANAS, type MedidasPestanas } from './pestanasAdaptativas';
import { useMedidasPestanas } from './useMedidasPestanas';

export interface Pestana {
  clave: 'inicio' | 'bandeja' | 'revalidaciones' | 'vencimientos' | 'cumplimiento' | 'certificados' | 'vinculos' | 'informes' | 'auditoria';
  ruta: string;
  /** Rutas que marcan la pestaña como activa. */
  prefijos: readonly string[];
}

/** Estilo de una pestaña de la barra (el enlace, el botón «Más» y las medidas ocultas): mismo alto y tipografía. */
function estiloPestana(actual: boolean) {
  return {
    display: 'inline-flex',
    alignItems: 'center',
    flex: 'none',
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
  } as const;
}

/**
 * Fila oculta con una medida por pestaña y, al final, una del botón «Más»: son las pestañas en negrita (el ancho mayor),
 * con el texto en un pseudoelemento para que no duplique texto en la página ni en los lectores de pantalla.
 */
function FilaDeMedidas({ etiquetas, etiquetaMas, refFila }: { etiquetas: readonly string[]; etiquetaMas: string; refFila: RefObject<HTMLElement | null> }) {
  const medida = { ...estiloPestana(true), borderColor: 'transparent', '&::before': { content: 'attr(data-texto)' } } as const;
  return (
    <Box
      ref={refFila}
      aria-hidden
      sx={{ position: 'absolute', top: 0, left: 0, display: 'flex', gap: `${String(SEPARACION_PESTANAS)}px`, width: 'max-content', height: 0, overflow: 'hidden', visibility: 'hidden', pointerEvents: 'none' }}
    >
      {etiquetas.map((texto) => (
        <Box key={texto} component="span" data-texto={texto} sx={medida} />
      ))}
      <Box component="span" data-texto={etiquetaMas} sx={{ ...medida, gap: '4px' }}>
        <ChevronDown size={16} strokeWidth={1.75} />
      </Box>
    </Box>
  );
}

interface Props {
  pestanas: readonly Pestana[];
  empresaId: string;
  /** Clave de la pestaña de la sección en que está la persona; `null` si es otra (Configuración, una ruta sin pestaña). */
  activa: Pestana['clave'] | null;
  /** Solo para pruebas: sin layout (jsdom) no hay medidas; con ellas se simula el ancho de la barra. */
  medidas?: MedidasPestanas;
}

/**
 * Barra de secciones del AppShell (docs/06 §3.3): las pestañas que caben en el ancho disponible, en su orden; las demás,
 * en un menú «Más». Nunca hay desplazamiento horizontal. Si la sección activa quedó en «Más», el botón se subraya como
 * una pestaña activa y lleva su nombre («Auditoría» en lugar de «Más»).
 */
export function NavegacionPrincipal({ pestanas, empresaId, activa, medidas: inyectadas }: Props) {
  const { t } = useTranslation();
  const idMenu = useId();
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const etiquetas = pestanas.map((p) => t(`nav.${p.clave}`));
  const { refBarra, refFila, medidas } = useMedidasPestanas(etiquetas.join('|'), inyectadas);

  const indiceActiva = pestanas.findIndex((p) => p.clave === activa);
  // Sin medidas (aún no se midió, o no hay layout) o con medidas de otras pestañas: todas a la vista.
  const visibles =
    medidas?.anchos.length === pestanas.length
      ? cuantasPestanasCaben({ ...medidas, indiceActiva: indiceActiva < 0 ? null : indiceActiva })
      : pestanas.length;
  const enMas = pestanas.slice(visibles);
  const activaEnMas = indiceActiva >= visibles;
  const abierto = ancla !== null && enMas.length > 0;
  const destino = (p: Pestana) => `/e/${empresaId}/${p.ruta}`;

  return (
    <Box
      component="nav"
      ref={refBarra}
      aria-label={t('nav.principal')}
      sx={{ position: 'relative', display: 'flex', alignSelf: 'stretch', gap: `${String(SEPARACION_PESTANAS)}px`, ml: 1, flex: '1 1 0', minWidth: 72, overflow: 'hidden' }}
    >
      {pestanas.slice(0, visibles).map((p) => {
        const actual = activa === p.clave;
        return (
          <Box key={p.clave} component={RouterLink} to={destino(p)} aria-current={actual ? 'page' : undefined} sx={estiloPestana(actual)}>
            {t(`nav.${p.clave}`)}
          </Box>
        );
      })}
      {enMas.length > 0 && (
        <>
          <ButtonBase
            aria-haspopup="menu"
            aria-expanded={abierto}
            aria-controls={abierto ? idMenu : undefined}
            aria-current={activaEnMas ? 'page' : undefined}
            onClick={(e) => setAncla(e.currentTarget)}
            sx={{ ...estiloPestana(activaEnMas), gap: '4px', minWidth: 0, flex: '0 1 auto', fontFamily: 'inherit' }}
          >
            <Box component="span" sx={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>
              {activaEnMas ? etiquetas[indiceActiva] : t('nav.mas')}
            </Box>
            <Box component={ChevronDown} size={16} strokeWidth={1.75} aria-hidden sx={{ flex: 'none', transition: 'transform 150ms', transform: abierto ? 'rotate(180deg)' : 'none' }} />
          </ButtonBase>
          <Menu
            id={idMenu}
            anchorEl={ancla}
            open={abierto}
            onClose={() => setAncla(null)}
            // La barra es sticky: el menú queda pegado a su botón sin bloquear el desplazamiento. Y sin bloqueo no se
            // quita ni se devuelve la barra de scroll de la ventana, que cambiaría el ancho (y las pestañas que caben) con el menú abierto.
            disableScrollLock
            anchorOrigin={{ vertical: 'bottom', horizontal: 'left' }}
            transformOrigin={{ vertical: 'top', horizontal: 'left' }}
            slotProps={{ list: { 'aria-label': t('nav.masSecciones') } }}
          >
            {enMas.map((p) => {
              const actual = activa === p.clave;
              return (
                <MenuItem key={p.clave} component={RouterLink} to={destino(p)} selected={actual} aria-current={actual ? 'page' : undefined} onClick={() => setAncla(null)} sx={{ fontWeight: actual ? 600 : 500 }}>
                  {t(`nav.${p.clave}`)}
                </MenuItem>
              );
            })}
          </Menu>
        </>
      )}
      <FilaDeMedidas etiquetas={etiquetas} etiquetaMas={t('nav.mas')} refFila={refFila} />
    </Box>
  );
}
