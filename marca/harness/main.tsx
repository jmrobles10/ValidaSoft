import '@fontsource-variable/geist';
import { ThemeProvider } from '@mui/material';
import {
  AnimacionExito,
  ALTO_V,
  ARCO_DE_CARGA,
  ARO,
  ARO_ABIERTO,
  CAJA_ICONO,
  CAJA_V,
  Cargando,
  CHECK_RELLENO,
  coloresDeLogo,
  createValidasoftTheme,
  FAVICON,
  GROSOR_GUIA,
  GUIA_CHECK,
  LONGITUD_GUIA,
  Logo,
  MARGEN_V,
  RADIO_RELLENO,
  TEXTO_LOGOTIPO,
  TIEMPOS_CARGA,
  TIEMPOS_EXITO,
  TRANSFORMACION_CHECK_DENTRO,
  TRANSFORMACION_CHECK_ICONO,
  type Modo,
} from '@validasoft/ui';
import { createRoot } from 'react-dom/client';

/**
 * Arnés del generador de recursos de marca (`marca/generar.mjs`): pinta una escena con los componentes reales de `@validasoft/ui` y
 * deja su geometría y colores en `window.marca` para que el generador escriba los SVG con los mismos números. Se controla por la URL:
 * `?escena=logotipo|icono|favicon|exito|carga|app&modo=light|dark&tamano=<px>&fondo=<css>`.
 */
const parametros = new URLSearchParams(window.location.search);
const escena = parametros.get('escena') ?? 'logotipo';
const modo: Modo = parametros.get('modo') === 'dark' ? 'dark' : 'light';
const tamano = Number(parametros.get('tamano') ?? '0') || undefined;
const fondo = parametros.get('fondo') ?? 'transparent';
const proporcion = Number(parametros.get('proporcion') ?? '0.62');
const radio = Number(parametros.get('radio') ?? '0');

declare global {
  interface Window {
    marca: unknown;
    marcaLista: boolean;
    /** Congela todas las animaciones CSS en el instante `ms` (la escena se captura cuadro a cuadro). */
    fijarInstante: (ms: number, cicloDeCarga?: number) => void;
  }
}

window.marca = {
  aro: ARO,
  aroAbierto: ARO_ABIERTO,
  cajaIcono: CAJA_ICONO,
  cajaV: CAJA_V,
  altoV: ALTO_V,
  margenV: MARGEN_V,
  textoLogotipo: TEXTO_LOGOTIPO,
  checkRelleno: CHECK_RELLENO,
  transformacionCheckIcono: TRANSFORMACION_CHECK_ICONO,
  transformacionCheckDentro: TRANSFORMACION_CHECK_DENTRO,
  radioRelleno: RADIO_RELLENO,
  favicon: FAVICON,
  guiaCheck: GUIA_CHECK,
  grosorGuia: GROSOR_GUIA,
  longitudGuia: LONGITUD_GUIA,
  arcoDeCarga: ARCO_DE_CARGA,
  tiemposExito: TIEMPOS_EXITO,
  tiemposCarga: TIEMPOS_CARGA,
  colores: { light: coloresDeLogo('light'), dark: coloresDeLogo('dark') },
};

window.fijarInstante = (ms, cicloDeCarga) => {
  for (const animacion of document.getAnimations()) {
    const objetivo = animacion.effect instanceof KeyframeEffect ? animacion.effect.target : null;
    // En la carga, el check dura `cicloDeCarga` (el GIF necesita un ciclo común con la vuelta del arco para repetirse sin saltos).
    if (cicloDeCarga && objetivo instanceof Element && objetivo.classList.contains('guia')) animacion.effect?.updateTiming({ duration: cicloDeCarga });
    animacion.pause();
    animacion.currentTime = ms;
  }
};

/** Hoja de contacto: todas las variantes en claro y oscuro a varios tamaños (para revisar la marca de un vistazo y para docs/marca.md). */
function Hoja() {
  const fondos: Record<Modo, string> = { light: '#FFFFFF', dark: '#0B1120' };
  return (
    <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', width: 1760 }}>
      {(['light', 'dark'] as const).map((m) => (
        <div key={m} style={{ background: fondos[m], padding: 32, display: 'grid', gap: 28 }}>
          <div style={{ display: 'flex', gap: 28, alignItems: 'flex-end' }}>
            <Logo variante="logotipo" tamano={64} modo={m} />
            <Logo variante="logotipo" tamano={40} modo={m} />
            <Logo variante="logotipo" tamano={26} modo={m} />
            <Logo variante="logotipo" tamano={18} modo={m} />
          </div>
          <div style={{ display: 'flex', gap: 24, alignItems: 'flex-end' }}>
            {[128, 64, 32, 24, 16].map((l) => (
              <Logo key={l} variante="icono" tamano={l} modo={m} />
            ))}
          </div>
          <div style={{ display: 'flex', gap: 24, alignItems: 'flex-end' }}>
            {[128, 64, 32, 24, 16].map((l) => (
              <Logo key={l} variante="favicon" tamano={l} modo={m} />
            ))}
          </div>
          <div style={{ display: 'flex', gap: 24, alignItems: 'flex-end' }}>
            <AnimacionExito tamano={96} modo={m} animar={false} />
            <Cargando tamano={96} modo={m} />
          </div>
        </div>
      ))}
    </div>
  );
}

function Escena() {
  switch (escena) {
    case 'hoja':
      return <Hoja />;
    case 'icono':
      return <Logo variante="icono" tamano={tamano ?? 96} />;
    case 'favicon':
      return <Logo variante="favicon" tamano={tamano ?? 96} />;
    case 'exito':
      return <AnimacionExito tamano={tamano ?? 96} />;
    case 'carga':
      return <Cargando tamano={tamano ?? 96} />;
    case 'app': {
      // Ícono con fondo (favicon, app instalada): el ícono centrado a `proporcion` del lado sobre el color de fondo, con `radio` de esquina.
      const lado = tamano ?? 512;
      return (
        <div style={{ width: lado, height: lado, background: fondo, borderRadius: `${String(radio * lado)}px`, display: 'grid', placeItems: 'center' }}>
          <Logo variante="icono" tamano={Math.round(lado * proporcion)} decorativo />
        </div>
      );
    }
    default:
      return <Logo variante="logotipo" tamano={tamano ?? 64} />;
  }
}

const raiz = document.getElementById('raiz');
if (!raiz) throw new Error('Falta #raiz.');
createRoot(raiz).render(
  <ThemeProvider theme={createValidasoftTheme(modo)}>
    <div id="escena" style={{ display: 'inline-flex', background: escena === 'app' ? 'transparent' : fondo, padding: Number(parametros.get('margen') ?? '0') }}>
      <Escena />
    </div>
  </ThemeProvider>,
);

void document.fonts.ready.then(() => {
  // Un cuadro más para que React y la fuente asienten el diseño antes de capturar.
  requestAnimationFrame(() => requestAnimationFrame(() => (window.marcaLista = true)));
});
