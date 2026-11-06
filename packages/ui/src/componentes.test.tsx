import { ThemeProvider } from '@mui/material';
import { act, render, renderHook, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { APARIENCIA_ESTADO, ChipEstado, textoTooltip, tonoDe, type EstadoVinculo } from './ChipEstado';
import { agruparPorDia, LineaDeTiempo, type EventoTimeline } from './LineaDeTiempo';
import { SelectorEmpresa } from './SelectorEmpresa';
import { TablaConCursor } from './TablaConCursor';
import { createValidasoftTheme } from './tema';
import { usePaginacionCursor } from './usePaginacionCursor';

const tema = createValidasoftTheme('light');
const conTema = (nodo: ReactNode) => render(<ThemeProvider theme={tema}>{nodo}</ThemeProvider>);

describe('ChipEstado', () => {
  const estadosVinculo: EstadoVinculo[] = ['Invitado', 'EnValidacion', 'Activo', 'PorVencer', 'Vencido', 'Bloqueado', 'Desvinculado', 'Anulado'];

  it('tiene apariencia para todos los estados del vínculo', () => {
    for (const estado of estadosVinculo) expect(APARIENCIA_ESTADO[estado].etiqueta).toBeTruthy();
  });

  it('muestra la etiqueta en español y la fecha en el texto accesible', () => {
    conTema(<ChipEstado estado="Vencido" fecha="2026-09-28T15:00:00Z" />);
    const chip = screen.getByLabelText('Vencido. Venció el 28/09/2026');
    expect(chip).toHaveAttribute('data-estado', 'Vencido');
    expect(screen.getByText('Vencido')).toBeInTheDocument();
  });

  it('pinta como error un pendiente bloqueante y avisa la decisión automática', () => {
    expect(tonoDe('ConPendientes')).toBe('advertencia');
    expect(tonoDe('ConPendientes', true)).toBe('error');
    expect(tonoDe('Activo', true)).toBe('exito');
    expect(textoTooltip('Aprobado', null, true)).toBe('Decidido por Sistema (IA)');
    expect(textoTooltip('EnValidacion')).toBeNull();
  });
});

describe('SelectorEmpresa', () => {
  const empresas = [
    { id: 'e1', nombre: 'Norte S.A.S.', identificacion: '800197268-4' },
    { id: 'e2', nombre: 'Sur S.A.S.', identificacion: '899999068-1' },
  ];

  it('con una sola empresa la muestra fija', () => {
    conTema(<SelectorEmpresa empresas={[empresas[0]!]} actual="e1" onCambiar={vi.fn()} />);
    expect(screen.getByLabelText('Empresa: Norte S.A.S.')).toBeInTheDocument();
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });

  it('cambia de empresa y no avisa si se elige la misma', async () => {
    const onCambiar = vi.fn();
    const usuario = userEvent.setup();
    conTema(<SelectorEmpresa empresas={empresas} actual="e1" onCambiar={onCambiar} />);

    await usuario.click(screen.getByRole('combobox'));
    const lista = screen.getByRole('listbox');
    await usuario.click(within(lista).getByText('Norte S.A.S.'));
    expect(onCambiar).not.toHaveBeenCalled();

    await usuario.click(screen.getByRole('combobox'));
    await usuario.click(within(screen.getByRole('listbox')).getByText('Sur S.A.S.'));
    expect(onCambiar).toHaveBeenCalledWith('e2');
  });
});

describe('LineaDeTiempo', () => {
  const eventos: EventoTimeline[] = [
    { id: '1', titulo: 'Vínculo creado', fecha: '2026-11-05T14:00:00Z', actor: { nombre: 'SRM', tipo: 'integracion' } },
    { id: '2', titulo: 'Vínculo bloqueado', fecha: '2026-11-06T03:00:00Z', actor: { nombre: 'Marta', tipo: 'humano' }, sistema: 'SRM' },
    { id: '3', titulo: 'Vínculo desbloqueado', fecha: '2026-11-06T15:00:00Z', actor: { nombre: 'Marta', tipo: 'humano' } },
  ];

  it('agrupa por día en la zona del tenant, del más reciente al más antiguo', () => {
    // 03:00 UTC del 6 es todavía el 5 en Bogotá (UTC-5).
    const grupos = agruparPorDia(eventos, 'America/Bogota');
    expect(grupos.map((g) => g.clave)).toEqual(['2026-11-06', '2026-11-05']);
    expect(grupos[1]?.eventos.map((e) => e.id)).toEqual(['2', '1']);
  });

  it('muestra actor, sistema y estado vacío', () => {
    const { rerender } = conTema(<LineaDeTiempo eventos={eventos} />);
    expect(screen.getByText('Marta · desde SRM')).toBeInTheDocument();
    expect(screen.getByText('Viernes, 6 de noviembre de 2026')).toBeInTheDocument();
    rerender(
      <ThemeProvider theme={tema}>
        <LineaDeTiempo eventos={[]} vacio="Nada por aquí" />
      </ThemeProvider>,
    );
    expect(screen.getByText('Nada por aquí')).toBeInTheDocument();
  });
});

describe('usePaginacionCursor', () => {
  it('avanza, retrocede y reinicia', () => {
    const { result } = renderHook(() => usePaginacionCursor());
    expect(result.current).toMatchObject({ cursor: null, pagina: 1, hayAnterior: false });

    act(() => result.current.irASiguiente('c2'));
    act(() => result.current.irASiguiente('c3'));
    expect(result.current).toMatchObject({ cursor: 'c3', pagina: 3, hayAnterior: true });

    act(() => result.current.irASiguiente('c3'));
    expect(result.current.pagina).toBe(3);

    act(() => result.current.irAAnterior());
    expect(result.current).toMatchObject({ cursor: 'c2', pagina: 2 });

    act(() => result.current.reiniciar());
    expect(result.current).toMatchObject({ cursor: null, pagina: 1 });
    act(() => result.current.irAAnterior());
    expect(result.current.pagina).toBe(1);
  });
});

describe('TablaConCursor', () => {
  interface Fila {
    id: string;
    nombre: string;
  }
  const columnas = [{ id: 'nombre', encabezado: 'Nombre', celda: (f: Fila) => f.nombre }];

  function Prueba(props: { filas?: Fila[]; siguiente?: string | null; cargando?: boolean; error?: string; onFila?: (f: Fila) => void; onReintentar?: () => void }) {
    const paginacion = usePaginacionCursor();
    return (
      <ThemeProvider theme={tema}>
        <span data-testid="cursor">{paginacion.cursor ?? 'inicio'}</span>
        <TablaConCursor
          etiqueta="Vínculos"
          columnas={columnas}
          filas={props.filas}
          claveFila={(f) => f.id}
          cargando={props.cargando ?? false}
          error={props.error ?? null}
          onReintentar={props.onReintentar}
          paginacion={paginacion}
          siguienteCursor={props.siguiente}
          onFila={props.onFila}
          vacio="No hay vínculos con esos filtros."
        />
      </ThemeProvider>
    );
  }

  it('pasa a la página siguiente con el cursor de la respuesta', async () => {
    const usuario = userEvent.setup();
    render(<Prueba filas={[{ id: '1', nombre: 'Aceros' }]} siguiente="abc" />);

    expect(screen.getByRole('button', { name: 'Página anterior' })).toBeDisabled();
    await usuario.click(screen.getByRole('button', { name: 'Página siguiente' }));
    expect(screen.getByTestId('cursor')).toHaveTextContent('abc');
    expect(screen.getByText('Página 2')).toBeInTheDocument();
  });

  it('no deja avanzar sin cursor y muestra el vacío', () => {
    render(<Prueba filas={[]} siguiente={null} />);
    expect(screen.getByRole('button', { name: 'Página siguiente' })).toBeDisabled();
    expect(screen.getByText('No hay vínculos con esos filtros.')).toBeInTheDocument();
  });

  it('abre la fila con clic o Enter', async () => {
    const onFila = vi.fn();
    const usuario = userEvent.setup();
    render(<Prueba filas={[{ id: '1', nombre: 'Aceros' }]} onFila={onFila} />);

    await usuario.click(screen.getByText('Aceros'));
    screen.getByText('Aceros').closest('tr')?.focus();
    await usuario.keyboard('{Enter}');
    expect(onFila).toHaveBeenCalledTimes(2);
  });

  it('muestra el error con reintento', async () => {
    const onReintentar = vi.fn();
    const usuario = userEvent.setup();
    render(<Prueba error="No pudimos cargar los vínculos." onReintentar={onReintentar} />);

    expect(screen.getByRole('alert')).toHaveTextContent('No pudimos cargar los vínculos.');
    await usuario.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(onReintentar).toHaveBeenCalled();
  });
});
