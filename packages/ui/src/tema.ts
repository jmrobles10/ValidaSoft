/* Validasoft: tema MUI v6 (dirección Registro). createValidasoftTheme(modo, { app, colorEmpresa }). */
import { createTheme, type Theme } from '@mui/material';
import { esES } from '@mui/material/locale';
import type {} from '@mui/x-date-pickers/themeAugmentation';
import { createElement, type CSSProperties } from 'react';
import { BORDE, COLOR, DENSIDAD, FUENTE, MOVIMIENTO, RADIO, SOMBRA, TIPOGRAFIA, type Modo, type TokenColor } from './tokens';
import type { TonoEstado } from './estados';
import { OpcionesEnCarga } from './esqueletos';
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
/** public en escritorio (bp-sm): densidad media; por debajo, densidad cómoda táctil. */
const ESCRITORIO = '@media (min-width:600px)';
const MOVIL = '@media (max-width:599.95px)';
const capital = (s: string) => s.charAt(0).toUpperCase() + s.slice(1);

/**
 * Textos por omisión de MUI en español (UI-3): `esES` traduce los de todos los componentes (paginador, migas, aviso…); aquí se
 * ajusta lo del autocompletar a la voz de Validasoft («Abrir», no «Abierto») y, mientras busca, muestra un esqueleto de opciones
 * en lugar del texto «Cargando…».
 */
const textosPropios = {
  components: {
    MuiAutocomplete: {
      defaultProps: {
        openText: 'Abrir',
        closeText: 'Cerrar',
        clearText: 'Limpiar',
        noOptionsText: 'Sin resultados',
        loadingText: createElement(OpcionesEnCarga),
      },
    },
  },
};

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
  const radioMarco = RADIO.md;
  const radioDialogo = web ? RADIO.md : RADIO.lg;
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
      h2: web ? TIPOGRAFIA.titulo2 : { ...TIPOGRAFIA.publicoTitulo, [ESCRITORIO]: TIPOGRAFIA.titulo2 },
      h3: TIPOGRAFIA.titulo3,
      h4: TIPOGRAFIA.titulo4,
      h5: TIPOGRAFIA.titulo4,
      h6: TIPOGRAFIA.titulo4,
      body1: web ? TIPOGRAFIA.cuerpo : { ...TIPOGRAFIA.publicoCuerpo, [ESCRITORIO]: { fontSize: 14, lineHeight: '20px' } },
      body2: web ? TIPOGRAFIA.cuerpo2 : TIPOGRAFIA.publicoEtiqueta,
      subtitle1: TIPOGRAFIA.titulo4,
      subtitle2: TIPOGRAFIA.etiqueta,
      caption: TIPOGRAFIA.etiqueta,
      button: { ...TIPOGRAFIA.boton, textTransform: 'none', ...(web ? {} : { fontSize: 15, [ESCRITORIO]: { fontSize: 14 } }) },
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
          // Sin salto lateral al pasar de una pantalla con barra de desplazamiento a una sin ella: el espacio de la barra
          // queda reservado siempre (invisible). Los diálogos y menús de MUI bloquean el scroll del documento y le suman
          // al body un `padding-right` del ancho de la barra para que no se corra; con el espacio ya reservado ese
          // relleno correría todo, así que se anula.
          '@supports (scrollbar-gutter: stable)': {
            html: { scrollbarGutter: 'stable' },
            body: { paddingRight: '0 !important' },
          },
          ':focus-visible': { outline: `${BORDE.anilloFoco}px solid ${c.foco}`, outlineOffset: BORDE.separacionFoco },
          '@media (prefers-reduced-motion: reduce)': {
            '*, *::before, *::after': { animationDuration: '1ms !important', transitionProperty: 'opacity !important', transitionDuration: `${MOVIMIENTO.duracionCorta} !important` },
          },
        },
      },
      MuiButtonBase: { defaultProps: { disableRipple: true } },
      // Botones sobrios (UI-2): `medium` por omisión y el mismo alto que el campo de al lado (32 px en web, 36 en el tercero
      // en escritorio). `small` es para avisos, filas y tarjetas; `large` solo para la acción principal de una pantalla. En el
      // celular el botón del tercero sube un poco su relleno (40 / 32 / 44 px) y el área táctil de 44 px se completa con un
      // pseudoelemento invisible: el botón no se infla.
      MuiButton: {
        defaultProps: { disableElevation: true, size: 'medium' },
        styleOverrides: {
          root: {
            borderRadius: radioControl,
            gap: 6,
            fontWeight: 600,
            whiteSpace: 'nowrap',
            '& .MuiButton-startIcon, & .MuiButton-endIcon': { margin: 0 },
            ...(web ? {} : { [MOVIL]: { '&::after': { content: '""', position: 'absolute', left: 0, right: 0, top: '50%', height: DENSIDAD.objetivoTactil, transform: 'translateY(-50%)' } } }),
          },
          sizeSmall: web
            ? { minHeight: 28, padding: '3px 10px', fontSize: 13, lineHeight: '18px' }
            : { minHeight: 32, padding: '5px 12px', fontSize: 14, lineHeight: '20px', [ESCRITORIO]: { minHeight: 30, padding: '4px 12px', fontSize: 13, lineHeight: '18px' } },
          sizeMedium: web
            ? { minHeight: control, padding: '5px 12px', fontSize: 14, lineHeight: '20px' }
            : { minHeight: 40, padding: '9px 16px', [ESCRITORIO]: { minHeight: DENSIDAD.controlMedio, padding: '7px 14px', fontSize: 14, lineHeight: '20px' } },
          sizeLarge: web
            ? { minHeight: DENSIDAD.controlMedio, padding: '7px 16px', fontSize: 14, lineHeight: '20px' }
            : { minHeight: DENSIDAD.objetivoTactil, padding: '11px 18px', fontSize: 15, lineHeight: '22px', [ESCRITORIO]: { minHeight: 40, padding: '9px 16px', fontSize: 14, lineHeight: '22px' } },
          outlined: { borderColor: c.bordeControl, color: c.textoPrimario, backgroundColor: c.superficieNivel1, '&:hover': { borderColor: c.bordeControl, backgroundColor: c.superficieNivel2 } },
          text: { color: acento, '&:hover': { backgroundColor: primario ? primario.tinte : c.acentoTinte } },
          contained: { '&.MuiButton-colorPrimary:hover': { backgroundColor: primario ? primario.accion : c.accionPrimariaHover } },
        },
      },
      MuiIconButton: {
        defaultProps: { size: web ? 'small' : 'large' },
        styleOverrides: {
          root: { borderRadius: RADIO.sm, color: c.textoSecundario, '&:hover': { backgroundColor: c.superficieNivel2, color: c.textoPrimario } },
          sizeSmall: { width: DENSIDAD.controlCompacto, height: DENSIDAD.controlCompacto },
          sizeLarge: { width: DENSIDAD.objetivoTactil, height: DENSIDAD.objetivoTactil, [ESCRITORIO]: { width: DENSIDAD.controlMedio, height: DENSIDAD.controlMedio } },
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
            '&.MuiInputBase-sizeSmall .MuiInputBase-input': { padding: '6px 10px' },
          },
          input: {
            padding: web ? '6px 10px' : '10px 14px', lineHeight: web ? '20px' : '24px', height: 'auto', '&::placeholder': { color: c.textoSecundario, opacity: 1 },
            ...(web ? {} : { [ESCRITORIO]: { padding: '8px 12px', fontSize: 14, lineHeight: '20px' } }),
          },
        },
      },
      // Sin los adornos del navegador (UI-7): ni la «x» de limpiar del `type="search"` ni el ojo de Edge en las claves.
      MuiInputBase: {
        styleOverrides: {
          input: {
            '&[type="search"]::-webkit-search-cancel-button, &[type="search"]::-webkit-search-decoration': { display: 'none', WebkitAppearance: 'none' },
            '&::-ms-reveal, &::-ms-clear': { display: 'none' },
          },
        },
      },
      // Selector de fecha (UI-7, `CampoFecha`): mismo alto (32 en web, 44 en el celular del tercero y 36 en escritorio), borde,
      // radio y etiqueta fija encima que los `TextField`. El calendario va en una tarjeta con el estilo de los menús.
      MuiPickersOutlinedInput: {
        styleOverrides: {
          root: {
            borderRadius: radioControl, backgroundColor: c.superficieNivel1, padding: web ? '0 4px 0 10px' : '0 6px 0 14px',
            '& .MuiPickersOutlinedInput-notchedOutline': { top: 0, borderColor: c.bordeControl, '& legend': { display: 'none' } },
            '&:hover .MuiPickersOutlinedInput-notchedOutline': { borderColor: c.textoSecundario },
            '&.Mui-focused .MuiPickersOutlinedInput-notchedOutline': { borderColor: c.foco, borderWidth: BORDE.anilloFoco },
            '&.Mui-error .MuiPickersOutlinedInput-notchedOutline': { borderColor: c.accionDestructiva },
            '&.Mui-disabled': { backgroundColor: c.superficieNivel2 },
            '&.Mui-disabled .MuiPickersOutlinedInput-notchedOutline': { borderColor: c.bordeControl },
          },
          sectionsContainer: {
            '&&': {
              padding: web ? '6px 0' : '10px 0',
              lineHeight: web ? '20px' : '24px',
              ...(web ? {} : { [ESCRITORIO]: { padding: '8px 0', fontSize: 14, lineHeight: '20px' } }),
            },
          },
        },
      },
      MuiPickerPopper: {
        styleOverrides: {
          paper: { backgroundColor: c.superficieElevada, backgroundImage: 'none', border: `1px solid ${c.bordeSutil}`, boxShadow: s.menu, borderRadius: radioMarco, marginTop: 4 },
        },
      },
      MuiPickersCalendarHeader: { styleOverrides: { label: { ...TIPOGRAFIA.titulo4, textTransform: 'capitalize' } } },
      MuiFormHelperText: {
        styleOverrides: {
          root: { margin: '4px 0 0', fontSize: web ? 12 : 14, lineHeight: web ? '16px' : '20px', color: c.textoSecundario, '&.Mui-error': { color: c.estadoErrorTexto } },
        },
      },
      // El ícono de abrir queda centrado en el alto del campo (la posición por omisión de MUI: `calc(50% - .5em)`).
      MuiSelect: {
        styleOverrides: { icon: { right: 8, color: c.textoSecundario }, select: { minHeight: 0 } },
      },
      MuiAutocomplete: {
        styleOverrides: {
          paper: { backgroundColor: c.superficieElevada, border: `1px solid ${c.bordeSutil}`, boxShadow: s.menu, borderRadius: RADIO.sm },
          listbox: { padding: 4, '& .MuiAutocomplete-option': { minHeight: control, borderRadius: RADIO.xs, fontSize: 14 } },
          tag: { height: 24, borderRadius: RADIO.xs, margin: 2 },
          // MUI 9 ya centra el adorno (`top: 50%` + `translateY(-50%)`): sin tocar `top`, queda dentro del campo y a su altura media.
          endAdornment: { right: 6 },
          popupIndicator: { '&&': { width: 28, height: 28, padding: 0, marginRight: 0 } },
          clearIndicator: { '&&': { width: 28, height: 28, padding: 0, marginRight: 0 } },
          // Mismo alto que los demás campos (32 web, 44 public móvil, 36 public escritorio): el relleno lo pone el input,
          // no el contenedor (MUI le suma 9 px arriba y abajo al contenedor, más en size="small").
          inputRoot: {
            paddingTop: 0,
            paddingBottom: 0,
            paddingLeft: 0,
            '&.MuiInputBase-sizeSmall': { paddingTop: 0, paddingBottom: 0, paddingLeft: 0 },
            '& .MuiAutocomplete-input, &.MuiInputBase-sizeSmall .MuiAutocomplete-input': {
              padding: web ? '6px 10px' : '10px 14px',
              ...(web ? {} : { [ESCRITORIO]: { padding: '8px 12px' } }),
            },
          },
        },
      },
      MuiCheckbox: {
        defaultProps: { size: 'small' },
        styleOverrides: { root: { color: c.bordeControl, padding: web ? 6 : 12, ...(web ? {} : { [ESCRITORIO]: { padding: 8 } }), '&.Mui-checked, &.MuiCheckbox-indeterminate': { color: primario ? primario.accion : c.accionPrimaria } } },
      },
      MuiRadio: {
        defaultProps: { size: 'small' },
        styleOverrides: { root: { color: c.bordeControl, padding: web ? 6 : 12, ...(web ? {} : { [ESCRITORIO]: { padding: 8 } }), '&.Mui-checked': { color: primario ? primario.accion : c.accionPrimaria } } },
      },
      // Apagado = «no», no «deshabilitado»: pista clara con borde de control y perilla en texto secundario (≥ 3:1 sobre la
      // superficie). Encendido: pista de acción y perilla blanca. Deshabilitado: pista nivel 2 con borde sutil y perilla en
      // texto deshabilitado (apagado) o la pista de acción al 40 % (encendido). Una pista sólida en borde-control se leía
      // como deshabilitada.
      MuiSwitch: {
        defaultProps: { disableRipple: true },
        styleOverrides: {
          root: {
            // La geometría va en la raíz y para todos los tamaños: con size="small" MUI aplica su propio relleno con más
            // especificidad y descentra la perilla. Pista 36 × 20 con borde de 1,5 px; perilla 14 con 3 px por lado.
            '&, &.MuiSwitch-sizeSmall, &.MuiSwitch-sizeMedium': {
              width: 36, height: 20, padding: 0, margin: web ? 6 : 12, overflow: 'visible',
              '& .MuiSwitch-switchBase': { top: 0, left: 0, padding: 3, '&.Mui-checked': { transform: 'translateX(16px)' } },
              '& .MuiSwitch-thumb': { width: 14, height: 14 },
            },
          },
          switchBase: {
            color: c.textoSecundario,
            '&.Mui-checked': {
              color: '#FFFFFF',
              '& + .MuiSwitch-track': { backgroundColor: primario ? primario.accion : c.accionPrimaria, borderColor: primario ? primario.accion : c.accionPrimaria, opacity: 1 },
            },
            '&.Mui-focusVisible + .MuiSwitch-track': { outline: `${BORDE.anilloFoco}px solid ${c.foco}`, outlineOffset: 2 },
            '&.Mui-disabled': { color: c.textoDeshabilitado, '& + .MuiSwitch-track': { backgroundColor: c.superficieNivel2, borderColor: c.bordeSutil, opacity: 1 } },
            '&.Mui-checked.Mui-disabled': { color: '#FFFFFF', '& + .MuiSwitch-track': { backgroundColor: primario ? primario.accion : c.accionPrimaria, borderColor: 'transparent', opacity: 0.4 } },
          },
          thumb: { boxShadow: 'none' },
          track: { boxSizing: 'border-box', borderRadius: 10, backgroundColor: c.superficieNivel1, border: `1.5px solid ${c.bordeControl}`, opacity: 1 },
        },
      },
      MuiFormControlLabel: { styleOverrides: { root: { marginLeft: web ? -6 : -12, gap: 4 }, label: { fontSize: web ? 14 : 16, lineHeight: '20px', ...(web ? {} : { [ESCRITORIO]: { fontSize: 14 } }) } } },
      MuiTabs: { styleOverrides: { root: { minHeight: web ? 40 : 44 }, indicator: { height: BORDE.grosorIndicador, backgroundColor: acento } } },
      MuiTab: {
        styleOverrides: {
          root: {
            textTransform: 'none', minWidth: 0, minHeight: web ? 40 : 44, padding: '0 10px', fontSize: 14, fontWeight: 500, color: c.textoSecundario,
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
            ...(web ? {} : { [ESCRITORIO]: { minHeight: DENSIDAD.controlMedio, fontSize: 14 } }),
            '&:hover': { backgroundColor: c.superficieNivel2 },
            '&.Mui-selected, &.Mui-selected:hover': { backgroundColor: c.acentoTinte },
          },
        },
      },
      MuiListItemIcon: { styleOverrides: { root: { minWidth: 0, color: c.textoSecundario } } },
      MuiBackdrop: { styleOverrides: { root: ({ ownerState }) => (ownerState.invisible ? {} : { backgroundColor: c.scrim }) } },
      MuiDialog: {
        styleOverrides: { paper: { borderRadius: radioDialogo, backgroundColor: c.superficieNivel1, backgroundImage: 'none', boxShadow: s.dialogo } },
      },
      MuiDialogTitle: { styleOverrides: { root: { ...TIPOGRAFIA.titulo3, padding: '16px 20px 4px' } } },
      MuiDialogContent: { styleOverrides: { root: { padding: '8px 20px' } } },
      MuiDialogActions: { styleOverrides: { root: { padding: '12px 20px 16px', gap: 8, '& > :not(style) ~ :not(style)': { marginLeft: 0 } } } },
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
            // Con botón de cerrar o acción, la fila mide lo que el botón (32 px): ícono y primera línea del texto se
            // centran en esa altura (antes quedaban arriba y el aviso se veía descentrado). En varias líneas, el ícono
            // sigue alineado con la primera.
            const conAccion = Boolean(ownerState.onClose ?? ownerState.action);
            return {
              borderRadius: RADIO.md,
              backgroundColor: t.fondo,
              borderColor: t.borde,
              color: c.textoPrimario,
              padding: '6px 12px',
              '& .MuiAlert-icon': { color: t.texto, opacity: 1, padding: conAccion ? '7px 0' : '2px 0', marginRight: 10, fontSize: 18 },
              ...(conAccion && { '& .MuiAlert-message': { padding: '6px 0' } }),
            };
          },
          message: { fontSize: 14, lineHeight: '20px', padding: '1px 0' },
          action: { padding: '0 0 0 12px', marginRight: 0, alignItems: 'flex-start' },
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
          root: { borderRadius: RADIO.xs, height: web ? 24 : 28, fontSize: web ? 12 : 13, fontWeight: 500, ...(web ? {} : { [ESCRITORIO]: { height: 24, fontSize: 12 } }) },
          outlined: { borderColor: c.bordeSutil, backgroundColor: 'transparent' },
          icon: { marginLeft: 6, marginRight: -2 },
        },
      },
      MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { backgroundImage: 'none' }, rounded: { borderRadius: radioMarco }, outlined: { borderColor: c.bordeSutil } } },
      // Plegable (UI-4): mismo borde y radio que las tarjetas en cualquier posición (MUI solo redondea el primero y el último
      // de una pila) y sin la línea superior que dibuja entre acordeones.
      MuiAccordion: {
        defaultProps: { disableGutters: true, elevation: 0, square: false },
        styleOverrides: {
          root: {
            '&, &:first-of-type, &:last-of-type': { borderRadius: radioMarco },
            border: `1px solid ${c.bordeSutil}`,
            backgroundColor: c.superficieNivel1,
            '&::before': { display: 'none' },
            '&.Mui-expanded': { margin: 0 },
          },
        },
      },
      MuiAccordionSummary: {
        styleOverrides: {
          root: { minHeight: web ? 44 : 52, padding: '0 16px', borderRadius: radioMarco, '&.Mui-expanded': { minHeight: web ? 44 : 52, borderBottomLeftRadius: 0, borderBottomRightRadius: 0 } },
          content: { margin: '10px 0', '&.Mui-expanded': { margin: '10px 0' } },
        },
      },
      MuiAccordionDetails: { styleOverrides: { root: { padding: '8px 16px 16px' } } },
      MuiCard: { defaultProps: { variant: 'outlined' }, styleOverrides: { root: { borderRadius: radioMarco } } },
      MuiCardHeader: { styleOverrides: { root: { padding: '12px 16px 4px' }, title: TIPOGRAFIA.titulo4, subheader: { fontSize: 13, lineHeight: '18px', color: c.textoSecundario }, action: { margin: 0, alignSelf: 'center' } } },
      MuiCardContent: { styleOverrides: { root: { padding: '12px 16px', '&:last-child': { paddingBottom: 12 } } } },
      MuiCardActions: { styleOverrides: { root: { padding: '4px 16px 12px', gap: 8 } } },
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
  }, esES, textosPropios);
}
