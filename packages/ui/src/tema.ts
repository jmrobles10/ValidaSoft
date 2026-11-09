/* Validasoft: tema MUI v6 (dirección Registro). createValidasoftTheme(modo, { app, colorEmpresa }). */
import { createTheme, type Theme } from '@mui/material';
import type { CSSProperties } from 'react';
import { BORDE, COLOR, DENSIDAD, FUENTE, MOVIMIENTO, RADIO, SOMBRA, TIPOGRAFIA, type Modo, type TokenColor } from './tokens';
import type { TonoEstado } from './estados';
import { derivarPaletaEmpresa, type PaletaEmpresa } from './marcaEmpresa';

export type App = 'web' | 'public';

export interface TonoPaleta {
  /** Punto, barra, nodo, ícono suelto (≥ 3:1). */
  solido: string;
  /** Texto e ícono del chip o alerta (≥ 4.5:1, también sobre `fondo`). */
  texto: string;
  fondo: string;
  borde: string;
}

declare module '@mui/material/styles' {
  interface Palette {
    estado: Record<TonoEstado, TonoPaleta>;
    superficie: { nivel1: string; nivel2: string; elevada: string };
    borde: { sutil: string; control: string };
    acento: { main: string; tinte: string };
    marca: { azul: string; verde: string; profundo: string; degradado: string };
    tooltip: { fondo: string; texto: string };
    esqueleto: string;
    scrim: string;
    /** Paleta derivada de la empresa (solo public con color configurado). */
    empresa: PaletaEmpresa | null;
  }
  interface PaletteOptions {
    estado?: Record<TonoEstado, TonoPaleta>;
    superficie?: { nivel1: string; nivel2: string; elevada: string };
    borde?: { sutil: string; control: string };
    acento?: { main: string; tinte: string };
    marca?: { azul: string; verde: string; profundo: string; degradado: string };
    tooltip?: { fondo: string; texto: string };
    esqueleto?: string;
    scrim?: string;
    empresa?: PaletaEmpresa | null;
  }
  interface Theme {
    app: App;
    radio: typeof RADIO;
    densidad: typeof DENSIDAD;
    sombra: { menu: string; dialogo: string };
  }
  interface ThemeOptions {
    app?: App;
    radio?: typeof RADIO;
    densidad?: typeof DENSIDAD;
    sombra?: { menu: string; dialogo: string };
  }
  interface TypographyVariants {
    mono: CSSProperties;
    monoTabla: CSSProperties;
    kpi: CSSProperties;
  }
  interface TypographyVariantsOptions {
    mono?: CSSProperties;
    monoTabla?: CSSProperties;
    kpi?: CSSProperties;
  }
}

declare module '@mui/material/Typography' {
  interface TypographyPropsVariantOverrides {
    mono: true;
    monoTabla: true;
    kpi: true;
  }
}

export interface OpcionesTema {
  /** `web` (revisor/admin, densidad compacta) o `public` (tercero, densidad cómoda). */
  app?: App;
  /** Color de la empresa que pide los documentos (solo public). Se deriva una paleta segura. */
  colorEmpresa?: string | null;
}

const TONOS: readonly TonoEstado[] = ['encurso', 'exito', 'advertencia', 'error', 'info', 'neutro'];
const capital = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);

export function createValidasoftTheme(modo: Modo = 'light', { app = 'web', colorEmpresa = null }: OpcionesTema = {}): Theme {
  const c = COLOR[modo];
  const s = SOMBRA[modo];
  const web = app === 'web';
  const empresa = !web && colorEmpresa ? derivarPaletaEmpresa(colorEmpresa, modo) : null;

  const tono = (t: TonoEstado): TonoPaleta => {
    const k = `estado${capital(t)}`;
    return { solido: c[k as TokenColor], texto: c[`${k}Texto` as TokenColor], fondo: c[`${k}Fondo` as TokenColor], borde: c[`${k}Borde` as TokenColor] };
  };
  const estado = Object.fromEntries(TONOS.map((t) => [t, tono(t)])) as Record<TonoEstado, TonoPaleta>;

  const primario = empresa ?? null;
  const acento = primario ? primario.enlace : c.acento;
  const control = web ? DENSIDAD.controlCompacto : DENSIDAD.controlComodo;
  const radioControl = web ? RADIO.sm : RADIO.md;
  const radioMarco = web ? RADIO.md : RADIO.lg;
  const severidad = { success: 'exito', info: 'info', warning: 'advertencia', error: 'error' } as const;

  return createTheme({
    app,
    radio: RADIO,
    densidad: DENSIDAD,
    sombra: s,
    spacing: 8,
    shape: { borderRadius: radioControl },
    palette: {
      mode: modo,
      primary: primario
        ? { main: primario.accion, contrastText: primario.textoAccion, light: primario.enlace }
        : { main: c.accionPrimaria, dark: c.accionPrimariaHover, light: c.acento, contrastText: c.textoSobreAccion },
      // error.main = acción destructiva: el blanco pasa 4.5:1 en ambos modos; el texto de error usa estado.error.texto.
      error: { main: c.accionDestructiva, dark: c.estadoErrorTexto, light: c.estadoErrorFondo, contrastText: c.textoSobreAccion },
      success: { main: c.estadoExito, dark: c.estadoExitoTexto, light: c.estadoExitoFondo },
      warning: { main: c.estadoAdvertencia, dark: c.estadoAdvertenciaTexto, light: c.estadoAdvertenciaFondo },
      info: { main: c.estadoInfo, dark: c.estadoInfoTexto, light: c.estadoInfoFondo },
      background: { default: c.fondo, paper: c.superficieNivel1 },
      text: { primary: c.textoPrimario, secondary: c.textoSecundario, disabled: c.textoDeshabilitado },
      divider: c.bordeSutil,
      estado,
      superficie: { nivel1: c.superficieNivel1, nivel2: c.superficieNivel2, elevada: c.superficieElevada },
      borde: { sutil: c.bordeSutil, control: c.bordeControl },
      acento: { main: acento, tinte: primario ? primario.tinte : c.acentoTinte },
      marca: { azul: c.marcaAzul, verde: c.marcaVerde, profundo: c.marcaProfundo, degradado: `linear-gradient(135deg, ${c.marcaAzul}, ${c.marcaVerde})` },
      tooltip: { fondo: c.tooltipFondo, texto: c.tooltipTexto },
      esqueleto: c.esqueleto,
      scrim: c.scrim,
      empresa,
    },
    typography: {
      fontFamily: FUENTE.sans,
      fontSize: 14,
      h1: TIPOGRAFIA.titulo1,
      h2: web ? TIPOGRAFIA.titulo2 : TIPOGRAFIA.publicoTitulo,
      h3: TIPOGRAFIA.titulo3,
      h4: TIPOGRAFIA.titulo4,
      h5: TIPOGRAFIA.titulo4,
      h6: TIPOGRAFIA.titulo4,
      body1: web ? TIPOGRAFIA.cuerpo : TIPOGRAFIA.publicoCuerpo,
      body2: web ? TIPOGRAFIA.cuerpo2 : TIPOGRAFIA.publicoEtiqueta,
      subtitle1: TIPOGRAFIA.titulo4,
      subtitle2: TIPOGRAFIA.etiqueta,
      caption: TIPOGRAFIA.etiqueta,
      button: { ...TIPOGRAFIA.boton, textTransform: 'none', ...(web ? {} : { fontSize: 16, lineHeight: '24px' }) },
      overline: { ...TIPOGRAFIA.sobretitulo, textTransform: 'uppercase' },
      mono: TIPOGRAFIA.mono,
      monoTabla: TIPOGRAFIA.monoTabla,
      kpi: TIPOGRAFIA.monoKpi,
    },
    transitions: {
      duration: { shortest: 120, shorter: 120, short: 200, standard: 200, complex: 320, enteringScreen: 320, leavingScreen: 200 },
      easing: { easeInOut: MOVIMIENTO.curvaEstandar, easeOut: MOVIMIENTO.curvaEstandar, easeIn: MOVIMIENTO.curvaSalida, sharp: MOVIMIENTO.curvaSalida },
    },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          body: { backgroundColor: c.fondo, color: c.textoPrimario, WebkitFontSmoothing: 'antialiased' },
          ':focus-visible': { outline: `${BORDE.anilloFoco}px solid ${c.foco}`, outlineOffset: BORDE.separacionFoco },
          '@media (prefers-reduced-motion: reduce)': {
            '*, *::before, *::after': { animationDuration: '1ms !important', transitionProperty: 'opacity !important', transitionDuration: `${MOVIMIENTO.duracionCorta} !important` },
          },
        },
      },
      MuiButtonBase: { defaultProps: { disableRipple: true } },
      MuiButton: {
        defaultProps: { disableElevation: true, size: web ? 'small' : 'large' },
        styleOverrides: {
          root: { borderRadius: radioControl, minHeight: control, gap: 6, fontWeight: 600, whiteSpace: 'nowrap', '& .MuiButton-startIcon, & .MuiButton-endIcon': { margin: 0 } },
          sizeSmall: { padding: '4px 12px', fontSize: 14, lineHeight: '20px' },
          sizeMedium: { padding: '6px 14px' },
          sizeLarge: { padding: '10px 20px', fontSize: 16, lineHeight: '24px' },
          outlined: { borderColor: c.bordeControl, color: c.textoPrimario, backgroundColor: c.superficieNivel1, '&:hover': { borderColor: c.bordeControl, backgroundColor: c.superficieNivel2 } },
          text: { color: acento, '&:hover': { backgroundColor: primario ? primario.tinte : c.acentoTinte } },
          containedPrimary: { '&:hover': { backgroundColor: primario ? primario.accion : c.accionPrimariaHover } },
        },
      },
      MuiIconButton: {
        defaultProps: { size: web ? 'small' : 'large' },
        styleOverrides: {
          root: { borderRadius: RADIO.sm, color: c.textoSecundario, '&:hover': { backgroundColor: c.superficieNivel2, color: c.textoPrimario } },
          sizeSmall: { width: DENSIDAD.controlCompacto, height: DENSIDAD.controlCompacto },
          sizeLarge: { width: DENSIDAD.objetivoTactil, height: DENSIDAD.objetivoTactil },
        },
      },
      MuiTextField: { defaultProps: { size: web ? 'small' : 'medium', slotProps: { inputLabel: { shrink: true } } } },
      MuiFormControl: { styleOverrides: { root: { gap: 0 } } },
      MuiInputLabel: {
        styleOverrides: {
          root: {
            position: 'static', transform: 'none', marginBottom: 4, fontSize: web ? 13 : 14, lineHeight: web ? '18px' : '20px', fontWeight: 500, color: c.textoPrimario,
            '&.Mui-focused, &.Mui-error': { color: c.textoPrimario },
            '&.Mui-disabled': { color: c.textoDeshabilitado },
          },
          asterisk: { color: c.estadoErrorTexto },
        },
      },
      MuiOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: radioControl, backgroundColor: c.superficieNivel1,
            '& .MuiOutlinedInput-notchedOutline': { top: 0, borderColor: c.bordeControl, '& legend': { display: 'none' } },
            '&:hover .MuiOutlinedInput-notchedOutline': { borderColor: c.textoSecundario },
            '&.Mui-focused .MuiOutlinedInput-notchedOutline': { borderColor: c.foco, borderWidth: BORDE.anilloFoco },
            '&.Mui-error .MuiOutlinedInput-notchedOutline': { borderColor: c.accionDestructiva },
            '&.Mui-disabled': { backgroundColor: c.superficieNivel2 },
          },
          input: { padding: web ? '6px 10px' : '10px 14px', lineHeight: web ? '20px' : '24px', height: 'auto', '&::placeholder': { color: c.textoSecundario, opacity: 1 } },
          inputSizeSmall: { padding: '6px 10px' },
        },
      },
      MuiFormHelperText: {
        styleOverrides: {
          root: { margin: '4px 0 0', fontSize: web ? 12 : 14, lineHeight: web ? '16px' : '20px', color: c.textoSecundario, '&.Mui-error': { color: c.estadoErrorTexto } },
        },
      },
      MuiAutocomplete: {
        styleOverrides: {
          paper: { backgroundColor: c.superficieElevada, border: `1px solid ${c.bordeSutil}`, boxShadow: s.menu, borderRadius: RADIO.sm },
          listbox: { padding: 4, '& .MuiAutocomplete-option': { minHeight: control, borderRadius: RADIO.xs, fontSize: 14 } },
          tag: { height: 24, borderRadius: RADIO.xs, margin: 2 },
          // Mismo alto que los demás campos (32 web / 44 public): el relleno lo pone el input, no el contenedor.
          inputRoot: {
            paddingTop: 0,
            paddingBottom: 0,
            paddingLeft: 0,
            '&.MuiInputBase-sizeSmall': { paddingTop: 0, paddingBottom: 0, paddingLeft: 0 },
            '& .MuiAutocomplete-input, &.MuiInputBase-sizeSmall .MuiAutocomplete-input': { padding: web ? '6px 10px' : '10px 14px' },
          },
        },
      },
      MuiCheckbox: {
        defaultProps: { size: 'small' },
        styleOverrides: { root: { color: c.bordeControl, padding: web ? 6 : 12, '&.Mui-checked, &.MuiCheckbox-indeterminate': { color: primario ? primario.accion : c.accionPrimaria } } },
      },
      MuiRadio: {
        defaultProps: { size: 'small' },
        styleOverrides: { root: { color: c.bordeControl, padding: web ? 6 : 12, '&.Mui-checked': { color: primario ? primario.accion : c.accionPrimaria } } },
      },
      // Apagado = «no», no «deshabilitado»: pista clara con borde de control y perilla en texto secundario (≥ 3:1 sobre la
      // superficie). Encendido: pista de acción y perilla blanca. Deshabilitado: pista nivel 2 con borde sutil y perilla
      // en texto deshabilitado (apagado) o la pista de acción al 40 % (encendido). Corrige la guía del artifact
      // («pista borde-control» sólida se leía como deshabilitado).
      MuiSwitch: {
        styleOverrides: {
          // `size="small"` de MUI trae su propia raíz (40×24), perilla (16 px) y relleno, que pisan estas medidas y
          // descentran la perilla: el switch Registro tiene un solo tamaño.
          root: {
            width: 36,
            height: 20,
            padding: 0,
            margin: web ? 6 : 12,
            overflow: 'visible',
            '&.MuiSwitch-sizeSmall': { width: 36, height: 20, padding: 0 },
            '&.MuiSwitch-sizeSmall .MuiSwitch-switchBase': { padding: 3 },
            '&.MuiSwitch-sizeSmall .MuiSwitch-switchBase.Mui-checked': { transform: 'translateX(16px)' },
            '&.MuiSwitch-sizeSmall .MuiSwitch-thumb': { width: 14, height: 14 },
          },
          switchBase: {
            padding: 3,
            color: c.textoSecundario,
            '&.Mui-checked': {
              transform: 'translateX(16px)',
              color: '#FFFFFF',
              '& + .MuiSwitch-track': { backgroundColor: primario ? primario.accion : c.accionPrimaria, borderColor: primario ? primario.accion : c.accionPrimaria, opacity: 1 },
            },
            '&.Mui-focusVisible': { outline: `${BORDE.anilloFoco}px solid ${c.foco}`, outlineOffset: 2 },
            '&.Mui-disabled': { color: c.textoDeshabilitado, '& + .MuiSwitch-track': { backgroundColor: c.superficieNivel2, borderColor: c.bordeSutil, opacity: 1 } },
            '&.Mui-checked.Mui-disabled': { color: '#FFFFFF', '& + .MuiSwitch-track': { backgroundColor: primario ? primario.accion : c.accionPrimaria, borderColor: 'transparent', opacity: 0.4 } },
          },
          thumb: { width: 14, height: 14, boxShadow: 'none' },
          track: { boxSizing: 'border-box', borderRadius: 10, backgroundColor: c.superficieNivel1, border: `1.5px solid ${c.bordeControl}`, opacity: 1 },
        },
      },
      MuiFormControlLabel: { styleOverrides: { root: { marginLeft: web ? -6 : -12, gap: 2 }, label: { fontSize: web ? 14 : 16 } } },
      MuiTabs: { styleOverrides: { root: { minHeight: web ? 40 : 48 }, indicator: { height: BORDE.grosorIndicador, backgroundColor: acento } } },
      MuiTab: {
        styleOverrides: {
          root: {
            textTransform: 'none', minWidth: 0, minHeight: web ? 40 : 48, padding: '0 10px', fontSize: 14, fontWeight: 500, color: c.textoSecundario,
            '&.Mui-selected': { color: c.textoPrimario, fontWeight: 600 },
          },
        },
      },
      MuiBreadcrumbs: { styleOverrides: { root: { fontSize: 13, lineHeight: '18px' }, separator: { marginLeft: 4, marginRight: 4, color: c.textoSecundario } } },
      MuiLink: { defaultProps: { underline: 'hover' }, styleOverrides: { root: { color: acento, fontWeight: 500, textUnderlineOffset: 3 } } },
      MuiPopover: { styleOverrides: { paper: { backgroundColor: c.superficieElevada, backgroundImage: 'none', border: `1px solid ${c.bordeSutil}`, boxShadow: s.menu, borderRadius: RADIO.sm } } },
      MuiMenu: { styleOverrides: { list: { padding: 4 } } },
      MuiMenuItem: {
        styleOverrides: {
          root: {
            minHeight: control, borderRadius: RADIO.xs, fontSize: web ? 14 : 16, gap: 8,
            '&:hover': { backgroundColor: c.superficieNivel2 },
            '&.Mui-selected, &.Mui-selected:hover': { backgroundColor: c.acentoTinte },
          },
        },
      },
      MuiListItemIcon: { styleOverrides: { root: { minWidth: 0, color: c.textoSecundario } } },
      MuiBackdrop: { styleOverrides: { root: ({ ownerState }) => (ownerState.invisible ? {} : { backgroundColor: c.scrim }) } },
      MuiDialog: {
        styleOverrides: { paper: { borderRadius: radioMarco, backgroundColor: c.superficieNivel1, backgroundImage: 'none', boxShadow: s.dialogo } },
      },
      MuiDialogTitle: { styleOverrides: { root: { ...TIPOGRAFIA.titulo3, padding: '20px 24px 8px' } } },
      MuiDialogContent: { styleOverrides: { root: { padding: '8px 24px' } } },
      MuiDialogActions: { styleOverrides: { root: { padding: '16px 24px', gap: 8, '& > :not(style) ~ :not(style)': { marginLeft: 0 } } } },
      MuiDrawer: {
        styleOverrides: { paper: { width: web ? 480 : '100%', backgroundColor: c.superficieNivel1, backgroundImage: 'none', borderLeft: `1px solid ${c.bordeSutil}`, boxShadow: s.dialogo } },
      },
      MuiSnackbarContent: {
        styleOverrides: {
          root: { backgroundColor: c.superficieElevada, color: c.textoPrimario, border: `1px solid ${c.bordeSutil}`, boxShadow: s.menu, borderRadius: RADIO.sm, padding: '6px 12px', fontSize: 14 },
          action: { marginRight: -4 },
        },
      },
      MuiAlert: {
        defaultProps: { variant: 'outlined' },
        styleOverrides: {
          root: ({ ownerState }) => {
            const t = estado[severidad[ownerState.severity ?? 'info']];
            return { borderRadius: RADIO.md, backgroundColor: t.fondo, borderColor: t.borde, color: c.textoPrimario, padding: '8px 12px', '& .MuiAlert-icon': { color: t.texto, opacity: 1 } };
          },
          message: { fontSize: 14, lineHeight: '20px' },
        },
      },
      MuiAlertTitle: { styleOverrides: { root: { fontWeight: 600, fontSize: 14, marginBottom: 2 } } },
      MuiTooltip: {
        defaultProps: { arrow: false, enterDelay: 400 },
        styleOverrides: { tooltip: { backgroundColor: c.tooltipFondo, color: c.tooltipTexto, fontSize: 12, lineHeight: '16px', fontWeight: 500, borderRadius: RADIO.sm, padding: '4px 8px' } },
      },
      MuiBadge: { styleOverrides: { badge: { height: 18, minWidth: 18, padding: '0 5px', fontSize: 12, fontWeight: 600 } } },
      MuiAvatar: { styleOverrides: { root: { width: 28, height: 28, fontSize: 12, fontWeight: 600, backgroundColor: c.acentoTinte, color: c.acento } } },
      MuiChip: {
        styleOverrides: {
          root: { borderRadius: RADIO.xs, height: web ? 24 : 28, fontSize: web ? 12 : 13, fontWeight: 500 },
          outlined: { borderColor: c.bordeSutil, backgroundColor: 'transparent' },
          icon: { marginLeft: 6, marginRight: -2 },
        },
      },
      MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { backgroundImage: 'none' }, outlined: { borderColor: c.bordeSutil } } },
      MuiCard: { defaultProps: { variant: 'outlined' }, styleOverrides: { root: { borderRadius: radioMarco } } },
      MuiTableCell: {
        styleOverrides: {
          root: { borderColor: c.bordeSutil },
          head: { ...TIPOGRAFIA.etiqueta, color: c.textoSecundario },
          sizeSmall: { height: DENSIDAD.filaCompacta, padding: '0 12px' },
        },
      },
      MuiTableRow: { styleOverrides: { root: { '&:hover': { backgroundColor: c.superficieNivel2 }, '&.Mui-selected, &.Mui-selected:hover': { backgroundColor: c.acentoTinte } } } },
      MuiSkeleton: { styleOverrides: { root: { backgroundColor: c.esqueleto, borderRadius: RADIO.xs } } },
      MuiLinearProgress: { styleOverrides: { root: { height: 4, borderRadius: RADIO.xs, backgroundColor: c.superficieNivel2 } } },
      MuiTypography: { defaultProps: { variantMapping: { mono: 'span', monoTabla: 'span', kpi: 'span' } } },
    },
  });
}
