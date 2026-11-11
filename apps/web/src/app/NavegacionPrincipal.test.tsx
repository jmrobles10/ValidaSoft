import { ThemeProvider } from '@mui/material';
import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { NavegacionPrincipal, type Pestana } from './NavegacionPrincipal';
import type { MedidasPestanas } from './pestanasAdaptativas';

const PESTANAS: readonly Pestana[] = [
  { clave: 'inicio', ruta: 'inicio', prefijos: ['inicio'] },
  { clave: 'bandeja', ruta: 'bandeja', prefijos: ['bandeja'] },
  { clave: 'vencimientos', ruta: 'vencimientos', prefijos: ['vencimientos'] },
  { clave: 'vinculos', ruta: 'vinculos', prefijos: ['vinculos'] },
  { clave: 'auditoria', ruta: 'auditoria', prefijos: ['auditoria'] },
];
const NOMBRES = ['Inicio', 'Bandeja', 'Vencimientos', 'Vínculos', 'Auditoría'];

/** Cada pestaña mide 100 px, el botón «Más» 60 y la barra el ancho que se indique (separación de 2 px entre pestañas). */
const medidas = (disponible: number): MedidasPestanas => ({ anchos: PESTANAS.map(() => 100), anchoMas: 60, disponible });

function montar(activa: Pestana['clave'] | null, medidasDeLaBarra?: MedidasPestanas, pestanas: readonly Pestana[] = PESTANAS) {
  return render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <MemoryRouter>
        <NavegacionPrincipal pestanas={pestanas} empresaId="emp" activa={activa} medidas={medidasDeLaBarra} />
      </MemoryRouter>
    </ThemeProvider>,
  );
}

const enlaces = () => within(screen.getByRole('navigation', { name: 'Secciones' })).queryAllByRole('link');

describe('NavegacionPrincipal', () => {
  it('sin medidas (sin layout) muestra todas las pestañas y no hay botón «Más»', () => {
    montar('bandeja');
    expect(enlaces().map((e) => e.textContent)).toEqual(NOMBRES);
    expect(screen.queryByRole('button', { name: 'Más' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Bandeja' })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', { name: 'Bandeja' })).toHaveAttribute('href', '/e/emp/bandeja');
  });

  it('la fila oculta de medidas no repite el texto de las pestañas', () => {
    montar('inicio');
    expect(screen.getAllByText('Vínculos')).toHaveLength(1);
  });

  it('con ancho para todas no hay «Más»', () => {
    montar('inicio', medidas(5 * 100 + 4 * 2 + 1));
    expect(enlaces()).toHaveLength(5);
    expect(screen.queryByRole('button', { name: 'Más' })).not.toBeInTheDocument();
  });

  it('cuando no caben todas, las últimas pasan a «Más» y el menú las lista en su orden como enlaces', async () => {
    const usuario = userEvent.setup();
    // 3 pestañas + «Más» caben (3·100 + 2·2 + 2 + 60 = 366); la cuarta no.
    montar('inicio', medidas(370));
    expect(enlaces().map((e) => e.textContent)).toEqual(['Inicio', 'Bandeja', 'Vencimientos']);
    const mas = screen.getByRole('button', { name: 'Más' });
    expect(mas).toHaveAttribute('aria-haspopup', 'menu');
    expect(mas).toHaveAttribute('aria-expanded', 'false');
    // La activa está a la vista: «Más» no se marca activo.
    expect(mas).not.toHaveAttribute('aria-current');

    await usuario.click(mas);
    expect(mas).toHaveAttribute('aria-expanded', 'true');
    const menu = screen.getByRole('menu', { name: 'Más secciones' });
    const items = within(menu).getAllByRole('menuitem');
    expect(items.map((i) => i.textContent)).toEqual(['Vínculos', 'Auditoría']);
    expect(items[0]).toHaveAttribute('href', '/e/emp/vinculos');
    expect(items[1]).toHaveAttribute('href', '/e/emp/auditoria');
    for (const item of items) expect(item).not.toHaveAttribute('aria-current');
  });

  it('con la activa en «Más», el botón lleva su nombre, queda activo y el menú la marca', async () => {
    const usuario = userEvent.setup();
    montar('auditoria', medidas(370));
    expect(screen.queryByRole('button', { name: 'Más' })).not.toBeInTheDocument();
    const boton = screen.getByRole('button', { name: 'Auditoría' });
    expect(boton).toHaveAttribute('aria-current', 'page');
    expect(boton).toHaveAttribute('aria-haspopup', 'menu');
    // Ninguna pestaña a la vista está activa.
    for (const enlace of enlaces()) expect(enlace).not.toHaveAttribute('aria-current');

    await usuario.click(boton);
    const activo = screen.getByRole('menuitem', { name: 'Auditoría' });
    expect(activo).toHaveAttribute('aria-current', 'page');
    expect(activo).toHaveClass('Mui-selected');
    expect(screen.getByRole('menuitem', { name: 'Vínculos' })).not.toHaveAttribute('aria-current');
  });

  it('un nombre largo de la activa en «Más» deja fuera una pestaña más para que el botón quepa', () => {
    const anchos = [100, 100, 100, 100, 220];
    // Con la activa a la vista caben cuatro pestañas junto a «Más»…
    const { unmount } = montar('inicio', { anchos, anchoMas: 60, disponible: 520 });
    expect(enlaces()).toHaveLength(4);
    unmount();
    // …pero con «Auditoría» en «Más» el botón mide 220 + 20 px: caben dos.
    montar('auditoria', { anchos, anchoMas: 60, disponible: 520 });
    expect(enlaces().map((e) => e.textContent)).toEqual(['Inicio', 'Bandeja']);
  });

  it('se navega por teclado: Enter abre el menú, las flechas recorren los ítems y Escape lo cierra', async () => {
    const usuario = userEvent.setup();
    montar('inicio', medidas(370));
    const mas = screen.getByRole('button', { name: 'Más' });
    mas.focus();
    await usuario.keyboard('{Enter}');
    const menu = await screen.findByRole('menu', { name: 'Más secciones' });
    expect(within(menu).getByRole('menuitem', { name: 'Vínculos' })).toHaveFocus();
    await usuario.keyboard('{ArrowDown}');
    expect(within(menu).getByRole('menuitem', { name: 'Auditoría' })).toHaveFocus();
    await usuario.keyboard('{Escape}');
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
    expect(mas).toHaveAttribute('aria-expanded', 'false');
  });

  it('elegir una sección del menú navega y lo cierra', async () => {
    const usuario = userEvent.setup();
    montar('inicio', medidas(370));
    await usuario.click(screen.getByRole('button', { name: 'Más' }));
    await usuario.click(screen.getByRole('menuitem', { name: 'Vínculos' }));
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('con una barra estrechísima solo queda «Más»', () => {
    montar(null, medidas(50));
    expect(enlaces()).toHaveLength(0);
    expect(screen.getByRole('button', { name: 'Más' })).toBeInTheDocument();
  });

  it('sin pestañas no pinta botón «Más»', () => {
    montar(null, { anchos: [], anchoMas: 60, disponible: 300 }, []);
    expect(screen.getByRole('navigation', { name: 'Secciones' })).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});

describe('NavegacionPrincipal midiendo el diseño real', () => {
  /** Un ResizeObserver y unas medidas de elemento simulados: en jsdom no hay layout. */
  const observadores = new Set<() => void>();
  let anchoBarra = 0;

  function simularLayout() {
    class ResizeObserverSimulado {
      constructor(private readonly alCambiar: () => void) {}
      observe() {
        observadores.add(this.alCambiar);
      }
      disconnect() {
        observadores.delete(this.alCambiar);
      }
      unobserve() {
        observadores.delete(this.alCambiar);
      }
    }
    vi.stubGlobal('ResizeObserver', ResizeObserverSimulado);
    vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
      const ancho = this.tagName === 'NAV' ? anchoBarra : this.hasAttribute('data-texto') ? (this.getAttribute('data-texto') === 'Más' ? 60 : 100) : 0;
      return { width: ancho, height: 0, top: 0, left: 0, right: ancho, bottom: 0, x: 0, y: 0, toJSON: () => ({}) };
    });
  }

  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    observadores.clear();
  });

  it('mide la barra al montar y la recalcula cuando cambia su ancho', () => {
    anchoBarra = 800;
    simularLayout();
    montar('inicio');
    expect(enlaces()).toHaveLength(5);

    anchoBarra = 370;
    act(() => observadores.forEach((alCambiar) => alCambiar()));
    expect(enlaces().map((e) => e.textContent)).toEqual(['Inicio', 'Bandeja', 'Vencimientos']);
    expect(screen.getByRole('button', { name: 'Más' })).toBeInTheDocument();

    anchoBarra = 800;
    act(() => observadores.forEach((alCambiar) => alCambiar()));
    expect(enlaces()).toHaveLength(5);
    expect(screen.queryByRole('button', { name: 'Más' })).not.toBeInTheDocument();
  });
});
