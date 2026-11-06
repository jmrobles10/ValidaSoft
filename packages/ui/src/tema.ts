import { alpha, createTheme, type PaletteMode, type Theme } from '@mui/material';

/** Tonos semánticos de estado (docs/06 §2.1). `enCurso` = primary; `neutro` = gris de texto secundario. */
export type TonoEstado = 'enCurso' | 'exito' | 'advertencia' | 'error' | 'info' | 'neutro';

declare module '@mui/material/styles' {
  interface Palette {
    estado: Record<TonoEstado, string>;
    superficie: { nivel1: string; nivel2: string };
    degradadoMarca: string;
  }
  interface PaletteOptions {
    estado?: Record<TonoEstado, string>;
    superficie?: { nivel1: string; nivel2: string };
    degradadoMarca?: string;
  }
  interface TypographyVariants {
    mono: React.CSSProperties;
  }
  interface TypographyVariantsOptions {
    mono?: React.CSSProperties;
  }
}

declare module '@mui/material/Typography' {
  interface TypographyPropsVariantOverrides {
    mono: true;
  }
}

/** Tokens de marca (docs/06 §2.1): oscuro por defecto, claro con contraste AA sobre blanco. */
export const TOKENS = {
  oscuro: {
    fondo: '#060A14',
    superficie1: '#0B1120',
    superficie2: '#111A2E',
    primario: '#3D6BFF',
    exito: '#22D38A',
    advertencia: '#F5B544',
    error: '#FF5C6C',
    info: '#7BA7FF',
    texto: '#EEF1F7',
    textoSecundario: '#8791A7',
    textoDeshabilitado: '#5A6480',
  },
  claro: {
    fondo: '#FAFBFD',
    superficie1: '#FFFFFF',
    superficie2: '#F1F4FA',
    primario: '#2F55D9',
    exito: '#159C64',
    advertencia: '#B77A0C',
    error: '#D6303F',
    info: '#3F63C9',
    texto: '#0A0F1C',
    textoSecundario: '#4C5670',
    textoDeshabilitado: '#8791A7',
  },
  primarioProfundo: '#0E2A6B',
  degradadoMarca: 'linear-gradient(135deg,#3D6BFF,#22D38A)',
  radio: { lg: 18, md: 12, sm: 9 },
  fuente: "'Geist Variable', 'Geist', system-ui, -apple-system, 'Segoe UI', sans-serif",
  fuenteMono: "'Geist Mono Variable', 'Geist Mono', ui-monospace, 'Cascadia Mono', monospace",
} as const;

export function createValidasoftTheme(modo: PaletteMode = 'dark'): Theme {
  const t = modo === 'dark' ? TOKENS.oscuro : TOKENS.claro;

  return createTheme({
    palette: {
      mode: modo,
      primary: { main: t.primario, dark: TOKENS.primarioProfundo, contrastText: '#FFFFFF' },
      success: { main: t.exito },
      warning: { main: t.advertencia },
      error: { main: t.error },
      info: { main: t.info },
      background: { default: t.fondo, paper: t.superficie1 },
      text: { primary: t.texto, secondary: t.textoSecundario, disabled: t.textoDeshabilitado },
      divider: alpha(t.textoSecundario, 0.24),
      estado: {
        enCurso: t.primario,
        exito: t.exito,
        advertencia: t.advertencia,
        error: t.error,
        info: t.info,
        neutro: t.textoSecundario,
      },
      superficie: { nivel1: t.superficie1, nivel2: t.superficie2 },
      degradadoMarca: TOKENS.degradadoMarca,
    },
    shape: { borderRadius: TOKENS.radio.md },
    spacing: 8,
    typography: {
      fontFamily: TOKENS.fuente,
      fontSize: 14,
      h1: { fontSize: 32, fontWeight: 600, letterSpacing: '-0.02em' },
      h2: { fontSize: 24, fontWeight: 600, letterSpacing: '-0.01em' },
      h3: { fontSize: 18, fontWeight: 600 },
      h4: { fontSize: 16, fontWeight: 600 },
      body1: { fontSize: 14, fontWeight: 400 },
      body2: { fontSize: 13, fontWeight: 400 },
      caption: { fontSize: 12, fontWeight: 500 },
      button: { textTransform: 'none', fontWeight: 600 },
      mono: { fontFamily: TOKENS.fuenteMono, fontSize: 13 },
    },
    components: {
      MuiCssBaseline: {
        styleOverrides: {
          body: { backgroundColor: t.fondo },
          ':focus-visible': { outline: `2px solid ${t.primario}`, outlineOffset: 2 },
        },
      },
      MuiButton: {
        defaultProps: { disableElevation: true },
        styleOverrides: { root: { borderRadius: TOKENS.radio.md } },
      },
      MuiChip: { styleOverrides: { root: { borderRadius: TOKENS.radio.sm, fontWeight: 500 } } },
      MuiPaper: {
        defaultProps: { elevation: 0 },
        styleOverrides: { root: { backgroundImage: 'none' }, outlined: { borderColor: alpha(t.textoSecundario, 0.24) } },
      },
      MuiCard: {
        defaultProps: { variant: 'outlined' },
        styleOverrides: { root: { borderRadius: TOKENS.radio.lg } },
      },
      MuiDialog: { styleOverrides: { paper: { borderRadius: TOKENS.radio.lg, backgroundColor: t.superficie1 } } },
      MuiTableCell: {
        styleOverrides: {
          root: { borderColor: alpha(t.textoSecundario, 0.16) },
          head: { color: t.textoSecundario, fontWeight: 500, fontSize: 12 },
        },
      },
      MuiTooltip: { defaultProps: { arrow: true } },
      MuiTypography: {
        defaultProps: { variantMapping: { mono: 'span' } },
      },
    },
  });
}
