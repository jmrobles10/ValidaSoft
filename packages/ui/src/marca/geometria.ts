import { COLOR, type Modo } from '../tokens';

/**
 * Geometría y colores del logo «Anillo» de Validasoft (MA-1, docs/marca.md). Es la única fuente: el componente `Logo`, las
 * animaciones y el generador de recursos (`marca/generar.mjs`) la leen de aquí; el PDF (`LogosDePdf.cs`) la repite con los mismos
 * números y una prueba los compara.
 *
 * - **Check («V»)**: una forma caligráfica rellena, de brazo derecho más largo: se lee como V y como check.
 * - **Logotipo**: sin aro. La «V» es el check verde recortado a su caja, del alto de la letra y apoyado en la línea base, muy pegado a
 *   la «a»; sigue «alida» (peso 500, color de texto) y «Soft» (peso 400, color de marca): se lee «ValidaSoft».
 * - **Ícono** (app, web, PWA, avatar): un aro abierto con el check un poco más grande, cuya punta sale por el hueco del aro.
 * - **Favicon**: un círculo verde lleno con el check blanco.
 * - **Cargando** y **éxito**: el aro completo y el check revelado con una máscara (un relleno no se «dibuja» con `stroke-dashoffset`).
 */

/** Caja del ícono (el check escalado sale un poco del lienzo de 100 × 100 y la punta cruza el hueco del aro). */
export const CAJA_ICONO = { x: -3, y: -6, ancho: 110, alto: 110 } as const;

/** Aro completo (carga y éxito): centro, radio y grosor del trazo. */
export const ARO = { cx: 50, cy: 50, r: 45, grosor: 5 } as const;

/** Largo del aro completo (2πr), para dibujarlo con `stroke-dashoffset`. */
export const LONGITUD_ARO = 2 * Math.PI * ARO.r;

/** Aro abierto del ícono: el hueco queda arriba a la derecha, por donde sale la punta del check. */
export const ARO_ABIERTO = 'M71.1 10.3 A45 45 0 1 0 89 27.5';

/** La «V» caligráfica: un check relleno con el brazo derecho más largo. */
export const CHECK_RELLENO =
  'M24 38 C26 34 30 34 32 37 L47 61 C56 45 66 32 80 19 C82 17 85 19 83 22 C70 37 60 52 51 71 C49 75 44 75 42 71 Z';

/** Check del ícono: escalado 1,15 desde su vértice inferior para que la punta salga por el hueco del aro. */
export const TRANSFORMACION_CHECK_ICONO = 'translate(47 70) scale(1.15) translate(-47 -70)';

/** Check del éxito: centrado dentro del círculo lleno, para que su punta blanca no se pierda fuera de él (sobre fondo claro no se vería). */
export const TRANSFORMACION_CHECK_DENTRO = 'translate(50 50) scale(1) translate(-54.5 -46)';

/** Radio del círculo lleno del éxito. */
export const RADIO_RELLENO = 46;

/**
 * Favicon: círculo verde que llena el lienzo y check blanco más grande y con un contorno propio de 2,4 (solo en el favicon): a 16 px el
 * check de la marca queda fino, y así se sigue leyendo.
 */
export const FAVICON = { radio: 48, transformacion: 'translate(50 50) scale(1.12) translate(-54.5 -46)', contorno: 2.4 } as const;

/**
 * Guía del check: el trazo que lo recorre (brazo corto, vértice, brazo largo). Las animaciones revelan el check con una máscara: esta
 * guía, con trazo ancho y blanco, avanza y descubre el check a su paso.
 */
export const GUIA_CHECK = 'M28 37 L46 68 L82 20';

/** Grosor del trazo de la guía de la máscara: cubre el ancho del check (hasta ≈ 14) en todo su recorrido. */
export const GROSOR_GUIA = 18;

/** Largo de la guía (35,85 + 60), redondeado hacia arriba. */
export const LONGITUD_GUIA = 96;

/** Caja del check recortado, para el logotipo (sin aro): x, y, ancho y alto. */
export const CAJA_V = { x: 22, y: 16, ancho: 64, alto: 60 } as const;

/** Alto de la «V» del logotipo dividido por el tamaño de la letra: mide lo que la letra. */
export const ALTO_V = 1;

/** Margen derecho de la «V» del logotipo, en tamaños de letra: va muy pegada a la «a» (la cola del check se mete bajo ella). */
export const MARGEN_V = -0.23;

/** Texto que sigue a la «V» en el logotipo: «alida» y «Soft». */
export const TEXTO_LOGOTIPO = { cuerpo: 'alida', marca: 'Soft' } as const;

/** Colores del logo en un modo (de `tokens.ts`; no cambian con la paleta de una empresa). */
export interface ColoresDeLogo {
  /** Aro (ícono y carga). */
  aro: string;
  /** Check (la «V») y color del éxito. */
  check: string;
  /** «alida» del logotipo. */
  texto: string;
  /** «Soft» del logotipo. */
  soft: string;
  /** Check sobre el círculo lleno del éxito. */
  blanco: string;
}

export function coloresDeLogo(modo: Modo): ColoresDeLogo {
  const c = COLOR[modo];
  return { aro: c.marcaAzul, check: c.marcaVerde, texto: c.marcaTexto, soft: c.marcaSoft, blanco: '#FFFFFF' };
}

/** Tiempos de la animación de éxito, en milisegundos: el aro se dibuja, el círculo se llena, el check se revela y todo hace «pop». */
export const TIEMPOS_EXITO = { aro: 450, relleno: 300, check: 400, pop: 300 } as const;

/** Duración total de la animación de éxito. */
export const DURACION_EXITO = TIEMPOS_EXITO.aro + TIEMPOS_EXITO.relleno + TIEMPOS_EXITO.check + TIEMPOS_EXITO.pop;

/** Tiempos de la animación de carga: una vuelta del arco y un ciclo del check (se revela y se oculta), en milisegundos. */
export const TIEMPOS_CARGA = { vuelta: 1000, check: 1400 } as const;

/** Arco que gira en la carga: largo visible y hueco (el aro mide ≈ 283). */
export const ARCO_DE_CARGA = { visible: 80, hueco: 210 } as const;
