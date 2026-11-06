import { ThemeProvider } from '@mui/material';
import { act, render, renderHook, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import { ChipEstado, textoTooltip, tonoDe } from './ChipEstado';
import { iniciales } from './datos';
import { APARIENCIA_ESTADO, aparienciaDe, type Estado, type EstadoVinculo } from './estados';
import { agruparPorDia, LineaDeTiempo, type EventoTimeline } from './LineaDeTiempo';
import { contraste, derivarPaletaEmpresa } from './marcaEmpresa';
import { SelectorEmpresa } from './SelectorEmpresa';
import { TablaConCursor } from './TablaConCursor';
import { createValidasoftTheme } from './tema';
import { COLOR } from './tokens';
import { usePaginacionCursor } from './usePaginacionCursor';

const tema = createValidasoftTheme('light');
const conTema = (nodo: ReactNode) => render(<ThemeProvider theme={tema}>{nodo}</ThemeProvider>);

describe('tema', () => {
  it('el claro es el de partida y los textos de estado pasan 4.5:1 en ambos modos', () => {
    expect(createValidasoftTheme().palette.mode).toBe('light');
    for (const modo of ['light', 'dark'] as const) {
      const c = COLOR[modo];
      for (const tono of ['Encurso', 'Exito', 'Advertencia', 'Error', 'Info', 'Neutro'] as const) {
        const texto = c[`estado${tono}Texto`];
        expect(contraste(texto, c.superficieNivel1)).toBeGreaterThanOrEqual(4.5);
        expect(contraste(texto, c[`estado${tono}Fondo`])).toBeGreaterThanOrEqual(4.5);
      }
    }
  });

  it('public deriva una paleta segura del color de la empresa', () => {
    const t = createValidasoftTheme('light', { app: 'public', colorEmpresa: '#FFD400' });
    const empresa = derivarPaletaEmpresa('#FFD400', 'light');
    expect(t.palette.primary.main).toBe(empresa.accion);
    expect(contraste(empresa.textoAccion, empresa.accion)).toBeGreaterThanOrEqual(4.5);
    expect(contraste(empresa.enlace, COLOR.light.superficieNivel1)).toBeGreaterThanOrEqual(4.5);
    // web ignora el color de la empresa.
    expect(createValidasoftTheme('light', { app: 'web', colorEmpresa: '#FFD400' }).palette.empresa).toBeNull();
  });
});

describe('estados canónicos', () => {
  const estadosVinculo: EstadoVinculo[] = ['Invitado', 'EnValidacion', 'Activo', 'PorVencer', 'Vencido', 'Bloqueado', 'Desvinculado', 'Anulado'];

  it('cada estado del vínculo tiene etiqueta e ícono propio', () => {
    const iconos = new Set(estadosVinculo.map((e) => APARIENCIA_ESTADO[e].icono));
    expect(iconos.size).toBe(estadosVinculo.length);
    for (const estado of estadosVinculo) expect(APARIENCIA_ESTADO[estado].etiqueta).toBeTruthy();
  });

  it('los estados que antes compartían el círculo punteado ya se distinguen sin color', () => {
    const estados: Estado[] = ['Pendiente', 'Cargado', 'Eximido', 'NoAplica', 'Abierto', 'Recibida'];
    expect(new Set(estados.map((e) => APARIENCIA_ESTADO[e].icono)).size).toBe(estados.length);
  });

  it('un pendiente bloqueante pasa a error y cambia de ícono', () => {
    expect(tonoDe('ConPendientes')).toBe('advertencia');
    expect(tonoDe('ConPendientes', true)).toBe('error');
    expect(aparienciaDe('ConPendientes', true).icono).not.toBe(APARIENCIA_ESTADO.ConPendientes.icono);
    expect(tonoDe('Activo', true)).toBe('exito');
  });
});

describe('ChipEstado', () => {
  it('muestra la etiqueta y expone la fecha a lectores de pantalla', () => {
    conTema(<ChipEstado estado="Vencido" fecha="2026-09-28T15:00:00Z" />);
    const chip = screen.getByText('Vencido').closest('[data-estado]');
    expect(chip).toHaveAttribute('data-estado', 'Vencido');
    expect(chip).toHaveTextContent('Vencido. Venció el 28/09/2026');
  });

  it('arma el tooltip con fecha, bloqueante y decisión automática', () => {
    expect(textoTooltip('Aprobado', null, true)).toBe('Decidido por Sistema (IA)');
    expect(textoTooltip('ConPendientes', null, false, true)).toBe('Bloqueante');
    expect(textoTooltip('EnValidacion')).toBeNull();
  });
});

describe('SelectorEmpresa', () => {
  const empresas = [
    { id: 'e1', nombre: 'Norte S.A.S.', identificacion: '800197268-4', prefijo: 'NORTE' },
    { id: 'e2', nombre: 'Sur S.A.S.', identificacion: '899999068-1', prefijo: 'SUR' },
  ];

  it('con una sola empresa la muestra fija, sin menú', () => {
    conTema(<SelectorEmpresa empresas={[empresas[0]!]} actual="e1" onCambiar={vi.fn()} />);
    expect(screen.getByLabelText('Empresa: Norte S.A.S., NIT 800197268-4')).toBeInTheDocument();
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });

  it('cambia de empresa y no avisa si se elige la misma', async () => {
    const onCambiar = vi.fn();
    const usuario = userEvent.setup();
    conTema(<SelectorEmpresa empresas={empresas} actual="e1" onCambiar={onCambiar} />);

    await usuario.click(screen.getByRole('button', { name: /Empresa: Norte/ }));
    await usuario.click(within(screen.getByRole('menu')).getByText('Norte S.A.S.'));
    expect(onCambiar).not.toHaveBeenCalled();

    await usuario.click(screen.getByRole('button', { name: /Empresa: Norte/ }));
    const menu = screen.getByRole('menu');
    expect(within(menu).getByText('NIT 899999068-1 · SUR')).toBeInTheDocument();
    await usuario.click(within(menu).getByText('Sur S.A.S.'));
    expect(onCambiar).toHaveBeenCalledWith('e2');
  });
});

describe('LineaDeTiempo', () => {
  const eventos: EventoTimeline[] = [
    { id: '1', titulo: 'Vínculo creado', fecha: '2026-11-05T14:00:00Z', actor: { nombre: 'Integración', tipo: 'integracion' }, sistema: 'SRM' },
    {
      id: '2',
      titulo: 'Vínculo bloqueado',
      fecha: '2026-11-06T03:00:00Z',
      actor: { nombre: 'Marta Ruiz', tipo: 'persona' },
      antes: 'Invitado',
      despues: 'Bloqueado',
      motivo: 'Coincidencia en lista restrictiva.',
    },
    { id: '3', titulo: 'Vínculo desbloqueado', fecha: '2026-11-06T15:00:00Z', actor: { nombre: 'Marta Ruiz', tipo: 'persona' } },
  ];

  it('agrupa por día en la zona del tenant, del más reciente al más antiguo', () => {
    // 03:00 UTC del 6 es todavía el 5 en Bogotá (UTC-5).
    const grupos = agruparPorDia(eventos, 'America/Bogota');
    expect(grupos.map((g) => g.clave)).toEqual(['2026-11-06', '2026-11-05']);
    expect(grupos.map((g) => g.etiqueta)).toEqual(['06/11/2026', '05/11/2026']);
    expect(grupos[1]?.eventos.map((e) => e.id)).toEqual(['2', '1']);
  });

  it('muestra actor, sistema, cambio de estado, motivo y vacío', () => {
    const { rerender } = conTema(<LineaDeTiempo eventos={eventos} />);
    expect(screen.getByText(/desde SRM/)).toBeInTheDocument();
    expect(screen.getByText('Coincidencia en lista restrictiva.').tagName).toBe('BLOCKQUOTE');
    const bloqueo = screen.getByText('Vínculo bloqueado').closest('li')!;
    expect(within(bloqueo).getByText('Invitado')).toBeInTheDocument();
    expect(within(bloqueo).getByText('Bloqueado')).toBeInTheDocument();
    rerender(
      <ThemeProvider theme={tema}>
        <LineaDeTiempo eventos={[]} vacio="Nada por aquí" />
      </ThemeProvider>,
    );
    expect(screen.getByText('Nada por aquí')).toBeInTheDocument();
  });

  it('saca iniciales de nombres de una o varias palabras', () => {
    expect(iniciales('Ana Administradora')).toBe('AA');
    expect(iniciales('Olga María Oficial')).toBe('OO');
    expect(iniciales('Tú')).toBe('T');
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

    expect(screen.getByRole('button', { name: 'Anterior' })).toBeDisabled();
    await usuario.click(screen.getByRole('button', { name: 'Siguiente' }));
    expect(screen.getByTestId('cursor')).toHaveTextContent('abc');
    expect(screen.getByText('Página 2')).toBeInTheDocument();
  });

  it('sin más páginas no muestra la paginación y sí el vacío', () => {
    render(<Prueba filas={[]} siguiente={null} />);
    expect(screen.queryByRole('navigation', { name: 'Paginación' })).not.toBeInTheDocument();
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

    expect(screen.getAllByRole('alert')[0]).toHaveTextContent('No pudimos cargar los vínculos.');
    await usuario.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(onReintentar).toHaveBeenCalled();
  });
});
