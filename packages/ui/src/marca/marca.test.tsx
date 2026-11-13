import { ThemeProvider } from '@mui/material';
import { act, fireEvent, render, renderHook, screen } from '@testing-library/react';
import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createValidasoftTheme } from '../tema';
import { COLOR } from '../tokens';
import { AnimacionExito, AnimacionExitoUnaVez } from './AnimacionExito';
import { Cargando } from './Cargando';
import {
  ARCO_DE_CARGA,
  ARO_ABIERTO,
  CAJA_ICONO,
  CAJA_V,
  CHECK_RELLENO,
  coloresDeLogo,
  FAVICON,
  GUIA_CHECK,
  LONGITUD_ARO,
  LONGITUD_GUIA,
  MARGEN_V,
  TIEMPOS_CARGA,
  TIEMPOS_EXITO,
  TRANSFORMACION_CHECK_ICONO,
} from './geometria';
import { Logo } from './Logo';
import { usePrimeraVez } from './movimiento';

const conTema = (nodo: ReactNode, modo: 'light' | 'dark' = 'light', colorEmpresa?: string) =>
  render(<ThemeProvider theme={createValidasoftTheme(modo, { app: 'public', ...(colorEmpresa ? { colorEmpresa } : {}) })}>{nodo}</ThemeProvider>);

/** Simula `prefers-reduced-motion` (jsdom no trae matchMedia). */
function menosMovimiento(reducido: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn().mockImplementation((consulta: string) => ({
      matches: reducido && consulta.includes('prefers-reduced-motion'),
      media: consulta,
      onchange: null,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      addListener: vi.fn(),
      removeListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  );
}

/**
 * Termina una animación CSS. Sin `AnimationEvent` en la ventana (jsdom) React escucha el nombre con prefijo, así que se dispara con los dos:
 * el que no escucha se ignora y `onAnimationEnd` se llama una sola vez por cada fin.
 */
function terminarAnimacion(elemento: Element) {
  fireEvent(elemento, new Event('animationend', { bubbles: true }));
  fireEvent(elemento, new Event('webkitAnimationEnd', { bubbles: true }));
}

afterEach(() => {
  vi.unstubAllGlobals();
  vi.useRealTimers();
  window.sessionStorage.clear();
});

describe('colores del logo (MA-1)', () => {
  it('son los del encargo: claro #3D6BFF / #22D38A / #0A0F1C / #2F55D9 y oscuro #6E8FFF / #2BE29A / #F2F5FB / #7C9BFF', () => {
    expect(coloresDeLogo('light')).toMatchObject({ aro: '#3D6BFF', check: '#22D38A', texto: '#0A0F1C', soft: '#2F55D9' });
    expect(coloresDeLogo('dark')).toMatchObject({ aro: '#6E8FFF', check: '#2BE29A', texto: '#F2F5FB', soft: '#7C9BFF' });
  });

  it('salen de los tokens del design system y llegan a la paleta del tema', () => {
    for (const modo of ['light', 'dark'] as const) {
      const c = COLOR[modo];
      expect(coloresDeLogo(modo)).toMatchObject({ aro: c.marcaAzul, check: c.marcaVerde, texto: c.marcaTexto, soft: c.marcaSoft });
      const { marca } = createValidasoftTheme(modo).palette;
      expect([marca.azul, marca.verde, marca.texto, marca.soft]).toEqual([c.marcaAzul, c.marcaVerde, c.marcaTexto, c.marcaSoft]);
    }
  });
});

describe('geometría del logo', () => {
  it('el aro completo mide 2πr y la guía del check cabe en su largo de trazo', () => {
    expect(LONGITUD_ARO).toBeCloseTo(282.74, 1);
    const largoGuia = Math.hypot(46 - 28, 68 - 37) + Math.hypot(82 - 46, 20 - 68);
    expect(LONGITUD_GUIA).toBeGreaterThanOrEqual(largoGuia);
    expect(LONGITUD_GUIA - largoGuia).toBeLessThan(1);
    expect(GUIA_CHECK).toBe('M28 37 L46 68 L82 20');
  });

  it('el check y el aro abierto son los aprobados', () => {
    expect(CHECK_RELLENO).toBe('M24 38 C26 34 30 34 32 37 L47 61 C56 45 66 32 80 19 C82 17 85 19 83 22 C70 37 60 52 51 71 C49 75 44 75 42 71 Z');
    expect(ARO_ABIERTO).toBe('M71.1 10.3 A45 45 0 1 0 89 27.5');
    expect(TRANSFORMACION_CHECK_ICONO).toBe('translate(47 70) scale(1.15) translate(-47 -70)');
    expect(CAJA_ICONO).toEqual({ x: -3, y: -6, ancho: 110, alto: 110 });
    expect(CAJA_V).toEqual({ x: 22, y: 16, ancho: 64, alto: 60 });
    expect(MARGEN_V).toBe(-0.23);
  });

  it('los tiempos del éxito suman 1,45 s y los de la carga son 1 s de vuelta y 1,4 s de check', () => {
    expect(Object.values(TIEMPOS_EXITO).reduce((a, b) => a + b, 0)).toBe(1450);
    expect(TIEMPOS_CARGA).toEqual({ vuelta: 1000, check: 1400 });
    expect(ARCO_DE_CARGA).toEqual({ visible: 80, hueco: 210 });
  });
});

describe('Logo', () => {
  it('el logotipo se anuncia como «Validasoft» y no lee sus letras sueltas', () => {
    conTema(<Logo />);
    const logo = screen.getByRole('img', { name: 'Validasoft' });
    expect(logo).toBeInTheDocument();
    expect(logo.querySelectorAll('[aria-hidden="true"]').length).toBeGreaterThanOrEqual(2);
    expect(logo).toHaveTextContent('alidaSoft');
  });

  it('la «V» es el check verde recortado a su caja y «alida» y «Soft» llevan sus pesos y colores (claro)', () => {
    const { container } = conTema(<Logo tamano={40} />);
    const svg = container.querySelector('svg');
    expect(svg).toHaveAttribute('viewBox', '22 16 64 60');
    const check = container.querySelector('path');
    expect(check).toHaveAttribute('d', CHECK_RELLENO);
    expect(check).toHaveAttribute('fill', '#22D38A');
    expect(container.querySelector('circle')).toBeNull();
    const cuerpo = container.querySelector('[data-parte="texto"]');
    const soft = cuerpo?.querySelector('span');
    expect(cuerpo).toHaveStyle({ color: '#0A0F1C', fontWeight: '500' });
    expect(soft).toHaveTextContent('Soft');
    expect(soft).toHaveStyle({ color: '#2F55D9', fontWeight: '400' });
  });

  it('la «V» mide lo que la letra y va pegada a la «a» (margen −0,23 del tamaño)', () => {
    const { container } = conTema(<Logo tamano={40} />);
    const v = container.querySelector('[data-parte="v"]');
    expect(v).toHaveStyle({ height: '40px', width: `${String((40 * 64) / 60)}px`, marginRight: '-9.200000000000001px' });
  });

  it('cambia con el tema: en oscuro usa los colores del modo oscuro', () => {
    const { container } = conTema(<Logo />, 'dark');
    expect(container.querySelector('path')).toHaveAttribute('fill', '#2BE29A');
    expect(container.querySelector('[data-parte="texto"]')).toHaveStyle({ color: '#F2F5FB' });
    expect(container.querySelector('[data-parte="texto"] span')).toHaveStyle({ color: '#7C9BFF' });
  });

  it('`modo` fuerza los colores aunque el tema sea otro', () => {
    const { container } = conTema(<Logo modo="dark" />, 'light');
    expect(container.querySelector('path')).toHaveAttribute('fill', '#2BE29A');
  });

  it('no toma el color de la empresa aunque la página use su paleta', () => {
    const { container } = conTema(<Logo />, 'light', '#C2410C');
    expect(container.querySelector('path')).toHaveAttribute('fill', '#22D38A');
    expect(container.querySelector('[data-parte="texto"] span')).toHaveStyle({ color: '#2F55D9' });
  });

  it('el ícono es el aro abierto y el check escalado desde su vértice, en los colores del modo', () => {
    const { container, rerender } = conTema(<Logo variante="icono" tamano={48} />);
    const svg = screen.getByRole('img', { name: 'Validasoft' });
    expect(svg).toHaveAttribute('viewBox', '-3 -6 110 110');
    expect(svg).toHaveAttribute('width', '48');
    const [aro, check] = Array.from(container.querySelectorAll('path'));
    expect(aro).toHaveAttribute('d', ARO_ABIERTO);
    expect(aro).toHaveAttribute('stroke', '#3D6BFF');
    expect(aro).toHaveAttribute('stroke-width', '5');
    expect(aro).toHaveAttribute('stroke-linecap', 'round');
    expect(check).toHaveAttribute('fill', '#22D38A');
    expect(check).toHaveAttribute('transform', TRANSFORMACION_CHECK_ICONO);
    rerender(
      <ThemeProvider theme={createValidasoftTheme('dark')}>
        <Logo variante="icono" tamano={48} />
      </ThemeProvider>,
    );
    expect(container.querySelector('path')).toHaveAttribute('stroke', '#6E8FFF');
  });

  it('el favicon es el círculo verde lleno con el check blanco (y contorno solo ahí)', () => {
    const { container } = conTema(<Logo variante="favicon" tamano={16} />);
    expect(container.querySelector('circle')).toHaveAttribute('fill', '#22D38A');
    expect(container.querySelector('circle')).toHaveAttribute('r', String(FAVICON.radio));
    const check = container.querySelector('path');
    expect(check).toHaveAttribute('fill', '#FFFFFF');
    expect(check).toHaveAttribute('stroke-width', String(FAVICON.contorno));
  });

  it('decorativo no se anuncia', () => {
    const { container } = conTema(
      <>
        <Logo decorativo />
        <Logo variante="icono" decorativo />
      </>,
    );
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
    expect(container.querySelectorAll('[aria-hidden="true"]').length).toBeGreaterThanOrEqual(2);
  });

  it('el nombre accesible se puede cambiar', () => {
    conTema(<Logo titulo="Validasoft, inicio" />);
    expect(screen.getByRole('img', { name: 'Validasoft, inicio' })).toBeInTheDocument();
  });
});

describe('AnimacionExito', () => {
  beforeEach(() => menosMovimiento(false));

  it('se anuncia como imagen «Listo» (o con la etiqueta pedida) y dibuja aro, relleno y el check blanco con máscara', () => {
    const { container } = conTema(<AnimacionExito etiqueta="Expediente aprobado" tamano={64} />);
    const svg = screen.getByRole('img', { name: 'Expediente aprobado' });
    expect(svg).toHaveAttribute('width', '64');
    expect(container.querySelector('circle.relleno')).toHaveAttribute('fill', '#22D38A');
    expect(container.querySelector('circle.aro')).toHaveAttribute('stroke', '#22D38A');
    const check = container.querySelector('g.check path');
    expect(check).toHaveAttribute('fill', '#FFFFFF');
    const mascara = container.querySelector('mask');
    expect(check?.getAttribute('mask')).toBe(`url(#${mascara?.id ?? ''})`);
    expect(mascara?.querySelector('path')).toHaveAttribute('d', GUIA_CHECK);
  });

  it('en oscuro usa el verde del modo oscuro y el check sigue blanco', () => {
    const { container } = conTema(<AnimacionExito />, 'dark');
    expect(container.querySelector('circle.relleno')).toHaveAttribute('fill', '#2BE29A');
    expect(container.querySelector('g.check path')).toHaveAttribute('fill', '#FFFFFF');
  });

  it('las animaciones siguen el orden del encargo: aro 0,45 s, relleno 0,3 s, check 0,4 s y pop 0,3 s', () => {
    conTema(<AnimacionExito />);
    const css = Array.from(document.querySelectorAll('style'))
      .map((estilo) => estilo.textContent)
      .join('\n');
    expect(css).toMatch(/450ms cubic-bezier\(0\.4, 0, 0\.2, 1\) both/);
    expect(css).toMatch(/300ms ease-out 450ms both/);
    expect(css).toMatch(/400ms cubic-bezier\(0\.22, 1, 0\.36, 1\) 750ms both/);
    expect(css).toMatch(/300ms ease-in-out 1150ms both/);
  });

  it('avisa una sola vez cuando termina el «pop»', () => {
    const alTerminar = vi.fn();
    const { container } = conTema(<AnimacionExito onFin={alTerminar} />);
    const pop = container.querySelector('g.pop');
    expect(pop).not.toBeNull();
    expect(alTerminar).not.toHaveBeenCalled();
    terminarAnimacion(pop!);
    terminarAnimacion(pop!);
    expect(alTerminar).toHaveBeenCalledTimes(1);
  });

  it('con menos movimiento no anima y avisa enseguida', () => {
    menosMovimiento(true);
    vi.useFakeTimers();
    const alTerminar = vi.fn();
    conTema(<AnimacionExito onFin={alTerminar} />);
    expect(alTerminar).not.toHaveBeenCalled();
    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(alTerminar).toHaveBeenCalledTimes(1);
    const css = Array.from(document.querySelectorAll('style'))
      .map((estilo) => estilo.textContent)
      .join('\n');
    expect(css).toMatch(/animation:\s*none\s*!important/);
  });

  it('decorativa no se anuncia', () => {
    conTema(<AnimacionExito decorativa />);
    expect(screen.queryByRole('img')).not.toBeInTheDocument();
  });

  it('con `animar` en falso se ve terminada, sin animaciones, y no cambia el dibujo', () => {
    const { container } = conTema(<AnimacionExito animar={false} />);
    const css = Array.from(document.querySelectorAll('style'))
      .map((estilo) => estilo.textContent)
      .join('\n');
    expect(css).toMatch(/animation:\s*none\s*!important/);
    expect(container.querySelector('g.check path')).toHaveAttribute('fill', '#FFFFFF');
    expect(container.querySelector('circle.relleno')).toHaveAttribute('fill', '#22D38A');
  });
});

describe('AnimacionExitoUnaVez', () => {
  /** La animación corre cuando su estilo no la anula: se mira la clase de emoción del svg, que cambia con `animar`. */
  const anima = (contenedor: HTMLElement) => {
    const svg = contenedor.querySelector('svg');
    const clase = Array.from(svg?.classList ?? []).find((c) => c.startsWith('css-')) ?? '';
    const reglas = Array.from(document.styleSheets).flatMap((hoja) => Array.from(hoja.cssRules).map((regla) => regla.cssText));
    return !reglas.some((regla) => regla.includes(clase) && /animation:\s*none\s*!important/.test(regla));
  };

  beforeEach(() => menosMovimiento(false));

  it('anima la primera vez que se ve su clave y las siguientes la muestra quieta', () => {
    const primera = conTema(<AnimacionExitoUnaVez clave="ficha-1" decorativa />);
    expect(anima(primera.container)).toBe(true);
    primera.unmount();
    const segunda = conTema(<AnimacionExitoUnaVez clave="ficha-1" decorativa />);
    expect(anima(segunda.container)).toBe(false);
    // Otra clave vuelve a celebrar.
    segunda.unmount();
    expect(anima(conTema(<AnimacionExitoUnaVez clave="ficha-2" decorativa />).container)).toBe(true);
  });
});

describe('Cargando', () => {
  it('es un estado accesible con nombre «Cargando» (o el pedido)', () => {
    conTema(<Cargando />);
    expect(screen.getByRole('status', { name: 'Cargando' })).toBeInTheDocument();
    conTema(<Cargando etiqueta="Abriendo el documento" />);
    expect(screen.getByRole('status', { name: 'Abriendo el documento' })).toBeInTheDocument();
  });

  it('dibuja el aro tenue (opacidad 0,2), el arco que gira (80 de 210) y el check verde con máscara', () => {
    const { container } = conTema(<Cargando tamano={64} />);
    const [tenue, arco] = Array.from(container.querySelectorAll('circle'));
    expect(tenue).toHaveAttribute('opacity', '0.2');
    expect(tenue).toHaveAttribute('stroke', '#3D6BFF');
    expect(arco).toHaveAttribute('stroke', '#3D6BFF');
    expect(arco).toHaveAttribute('stroke-dasharray', '80 210');
    expect(arco).toHaveClass('giro');
    const check = container.querySelector('g[transform] path');
    expect(check).toHaveAttribute('fill', '#22D38A');
    expect(check?.getAttribute('mask')).toMatch(/^url\(#.+\)$/);
  });

  it('gira 1 s lineal sin fin, traza el check en 1,4 s y con menos movimiento queda estática con un pulso suave', () => {
    conTema(<Cargando />);
    const css = Array.from(document.querySelectorAll('style'))
      .map((estilo) => estilo.textContent)
      .join('\n');
    expect(css).toMatch(/1000ms linear infinite/);
    expect(css).toMatch(/1400ms ease-in-out infinite/);
    expect(css).toMatch(/@media \(prefers-reduced-motion: reduce\)/);
    expect(css).toMatch(/2s ease-in-out infinite/);
  });

  it('decorativa no es un estado: la región que la contiene ya dice qué se espera', () => {
    const { container } = conTema(<Cargando decorativa />);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
    expect(container.querySelector('svg')).toHaveAttribute('aria-hidden', 'true');
  });

  it('en oscuro usa el aro y el check del modo oscuro', () => {
    const { container } = conTema(<Cargando />, 'dark');
    expect(container.querySelector('circle')).toHaveAttribute('stroke', '#6E8FFF');
    expect(container.querySelector('g[transform] path')).toHaveAttribute('fill', '#2BE29A');
  });
});

describe('usePrimeraVez', () => {
  it('es verdadero la primera vez que se pide una clave y falso después', () => {
    expect(renderHook(() => usePrimeraVez('expediente-1')).result.current).toBe(true);
    expect(renderHook(() => usePrimeraVez('expediente-1')).result.current).toBe(false);
    expect(renderHook(() => usePrimeraVez('expediente-2')).result.current).toBe(true);
  });

  it('sin almacenamiento celebra siempre', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('bloqueado');
    });
    expect(renderHook(() => usePrimeraVez('x')).result.current).toBe(true);
    vi.restoreAllMocks();
  });
});
