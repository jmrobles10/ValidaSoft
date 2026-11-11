import { ThemeProvider } from '@mui/material';
import { act, fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { CONTENEDOR_CON_SCROLL, desplazarVentana, simularDisenoDeLista } from '../pruebas/listaVirtual';
import { useAlturaFija } from '../sesion/alturaFija';
import { ListaVirtual } from './ListaVirtual';

const ALTO_FILA = 100;
const ALTO_CONTENEDOR = 500;

interface Prueba {
  total?: number;
  hayMas?: boolean;
  cargandoMas?: boolean;
  errorAlCargarMas?: boolean;
  alCargarMas?: () => void;
}

const claveDeFila = (f: { id: string }) => f.id;

function Lista({ total = 100, hayMas = false, cargandoMas = false, errorAlCargarMas = false, alCargarMas = () => undefined }: Prueba) {
  const filas = Array.from({ length: total }, (_, i) => ({ id: `f${String(i + 1)}`, nombre: `Fila ${String(i + 1)}` }));
  return (
    <ListaVirtual
      filas={filas}
      claveFila={claveDeFila}
      altoEstimado={ALTO_FILA}
      etiqueta="Filas de prueba"
      renderFila={(f) => <div>{f.nombre}</div>}
      hayMas={hayMas}
      cargandoMas={cargandoMas}
      errorAlCargarMas={errorAlCargarMas}
      alCargarMas={alCargarMas}
      textoCargandoMas="Cargando más filas…"
      textoErrorMas="No pudimos cargar más filas."
      textoReintentar="Reintentar"
    />
  );
}

const montar = (ui: React.ReactElement) => render(<ThemeProvider theme={createValidasoftTheme('light')}>{ui}</ThemeProvider>);

describe('ListaVirtual', () => {
  beforeEach(() => {
    simularDisenoDeLista({ altoFila: ALTO_FILA, altoContenedor: ALTO_CONTENEDOR });
  });
  afterEach(() => {
    vi.restoreAllMocks();
    useAlturaFija.setState({ altura: null });
  });

  it('solo dibuja las filas visibles y las que quedan cerca, no las 100', () => {
    montar(<Lista total={100} />);
    const filas = screen.getAllByRole('listitem');
    expect(filas.length).toBeGreaterThan(5);
    expect(filas.length).toBeLessThan(20);
    expect(screen.getByText('Fila 1')).toBeInTheDocument();
    expect(screen.queryByText('Fila 100')).not.toBeInTheDocument();
    expect(screen.getByRole('list', { name: 'Filas de prueba' })).toBeInTheDocument();
  });

  it('al desplazar cambia lo que hay en el DOM y suelta lo que quedó atrás', () => {
    montar(<Lista total={100} />);
    desplazarVentana(5000);
    expect(screen.getByText('Fila 51')).toBeInTheDocument();
    expect(screen.queryByText('Fila 1')).not.toBeInTheDocument();
    expect(screen.getAllByRole('listitem').length).toBeLessThan(20);
  });

  it('pide la siguiente página al acercarse al final y no antes', () => {
    const alCargarMas = vi.fn();
    montar(<Lista total={20} hayMas alCargarMas={alCargarMas} />);
    expect(alCargarMas).not.toHaveBeenCalled();
    expect(screen.queryByText('Fila 20')).not.toBeInTheDocument();

    desplazarVentana(1500);
    expect(alCargarMas).toHaveBeenCalled();
    // Mientras llega la página se ve un esqueleto de la fila que viene, no un texto ni un spinner.
    const pie = screen.getByRole('status', { name: 'Cargando más filas…' });
    expect(pie).toHaveAttribute('aria-busy', 'true');
    expect(pie).toHaveTextContent('');
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  it('no pide nada si el servidor no tiene más ni mientras ya está pidiendo', () => {
    const sinMas = vi.fn();
    const pidiendo = vi.fn();
    const { rerender } = montar(<Lista total={20} alCargarMas={sinMas} />);
    desplazarVentana(5000);
    expect(sinMas).not.toHaveBeenCalled();
    rerender(
      <ThemeProvider theme={createValidasoftTheme('light')}>
        <Lista total={20} hayMas cargandoMas alCargarMas={pidiendo} />
      </ThemeProvider>,
    );
    desplazarVentana(5100);
    expect(pidiendo).not.toHaveBeenCalled();
  });

  it('si la página falla no insiste sola: ofrece reintentar', async () => {
    const alCargarMas = vi.fn();
    montar(<Lista total={20} hayMas errorAlCargarMas alCargarMas={alCargarMas} />);
    desplazarVentana(1500);
    expect(screen.getByRole('alert')).toHaveTextContent('No pudimos cargar más filas.');
    expect(alCargarMas).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(alCargarMas).toHaveBeenCalledTimes(1);
  });

  it('con altura fija observa el contenedor con scroll propio en vez de la ventana', () => {
    useAlturaFija.setState({ altura: ALTO_CONTENEDOR });
    const alCargarMas = vi.fn();
    const { container } = montar(
      <div data-testid={CONTENEDOR_CON_SCROLL} style={{ overflowY: 'auto', height: ALTO_CONTENEDOR }}>
        <Lista total={20} hayMas alCargarMas={alCargarMas} />
      </div>,
    );
    const contenedor = screen.getByTestId(CONTENEDOR_CON_SCROLL);
    expect(container).toBeInTheDocument();
    expect(screen.getAllByRole('listitem').length).toBeLessThan(12);

    // Desplazar la ventana no hace nada: lo que se mueve es el contenedor.
    desplazarVentana(1500);
    expect(alCargarMas).not.toHaveBeenCalled();
    act(() => {
      Object.defineProperty(contenedor, 'scrollTop', { value: 1500, configurable: true });
      fireEvent.scroll(contenedor);
    });
    expect(alCargarMas).toHaveBeenCalled();
    expect(screen.getByText('Fila 16')).toBeInTheDocument();
  });
});
