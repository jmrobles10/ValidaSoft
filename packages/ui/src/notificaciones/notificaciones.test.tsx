import { ThemeProvider } from '@mui/material';
import { act, render, renderHook, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useState, type ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createValidasoftTheme } from '../tema';
import { CampanaDeNotificaciones } from './CampanaDeNotificaciones';
import { haceCuanto, textoContador, useAhora } from './formato';
import type { NotificacionVisible } from './ItemDeNotificacion';
import { PanelDeNotificaciones, type PanelDeNotificacionesProps, type TextosDelPanelDeNotificaciones } from './PanelDeNotificaciones';

const AHORA = Date.parse('2026-11-08T12:00:00Z');

const TEXTOS: TextosDelPanelDeNotificaciones = {
  titulo: 'Notificaciones',
  marcarTodas: 'Marcar todas',
  preferencias: 'Preferencias de notificación',
  cargando: 'Cargando',
  reintentar: 'Reintentar',
  vacio: 'Sin notificaciones',
  vacioAyuda: 'Aquí aparecerán los avisos',
  hayMas: 'Hay más antiguas',
  sinConexion: 'Sin conexión en vivo',
  sinLeer: 'Sin leer',
};

const aviso = (id: string, cambios: Partial<NotificacionVisible> = {}): NotificacionVisible => ({
  id,
  titulo: `Título ${id}`,
  mensaje: `Mensaje ${id}`,
  creadaEn: '2026-11-08T11:55:00Z',
  leidaEn: null,
  ...cambios,
});

const conTema = (nodo: ReactNode) => render(<ThemeProvider theme={createValidasoftTheme('light')}>{nodo}</ThemeProvider>);

function panel(cambios: Partial<PanelDeNotificacionesProps<NotificacionVisible>> = {}) {
  const acciones = { onReintentar: vi.fn(), onAbrir: vi.fn(), onMarcarTodas: vi.fn(), onPreferencias: vi.fn() };
  conTema(
    <PanelDeNotificaciones
      bandeja={{ datos: [aviso('a'), aviso('b', { leidaEn: '2026-11-08T11:00:00Z' })], noLeidas: 1, siguienteCursor: null }}
      cargando={false}
      error={null}
      textos={TEXTOS}
      alturaEsqueleto={48}
      {...acciones}
      {...cambios}
    />,
  );
  return acciones;
}

afterEach(() => vi.useRealTimers());

describe('formato de la campana', () => {
  it('el contador no muestra nada en cero y se corta en 99+', () => {
    expect([0, 1, 99, 100, 250].map(textoContador)).toEqual(['', '1', '99', '99+', '99+']);
  });

  it('dice hace cuánto en es-CO, con «ahora» para menos de un minuto o un reloj adelantado', () => {
    expect(haceCuanto('2026-11-08T11:59:40Z', AHORA)).toBe('ahora');
    expect(haceCuanto('2026-11-08T12:00:30Z', AHORA)).toBe('ahora');
    expect(haceCuanto('2026-11-08T11:55:00Z', AHORA)).toBe('hace 5 minutos');
    expect(haceCuanto('2026-11-08T09:00:00Z', AHORA)).toBe('hace 3 horas');
    expect(haceCuanto('2026-11-07T12:00:00Z', AHORA)).toBe('ayer');
    expect(haceCuanto('2026-10-25T12:00:00Z', AHORA)).toBe('hace 2 semanas');
    expect(haceCuanto('2026-10-18T12:00:00Z', AHORA)).toBe('hace 3 semanas');
  });

  it('la hora de referencia avanza sola con el intervalo', () => {
    vi.useFakeTimers({ now: AHORA });
    const { result } = renderHook(() => useAhora(1_000));
    expect(result.current).toBe(AHORA);
    act(() => {
      vi.advanceTimersByTime(3_000);
    });
    expect(result.current).toBe(AHORA + 3_000);
  });
});

describe('PanelDeNotificaciones', () => {
  it('lista los avisos con su texto y hace cuánto llegaron, marca solo la no leída para lectores de pantalla y abre el que se pulsa', async () => {
    const { onAbrir } = panel({ bandeja: { datos: [aviso('a'), aviso('b', { leidaEn: '2026-11-08T11:00:00Z' })], noLeidas: 1, siguienteCursor: '2' } });

    const lista = screen.getByRole('list');
    expect(within(lista).getAllByRole('listitem')).toHaveLength(2);
    expect(within(lista).getByRole('button', { name: /Sin leer: Título a/ })).toBeInTheDocument();
    expect(within(lista).getByRole('button', { name: /Título b/ })).not.toHaveAccessibleName(/Sin leer/);
    expect(screen.getByText('Hay más antiguas')).toBeInTheDocument();

    await userEvent.click(within(lista).getByRole('button', { name: /Título b/ }));
    expect(onAbrir).toHaveBeenCalledWith(expect.objectContaining({ id: 'b' }));
  });

  it('«Marcar todas» y las preferencias llaman a la app', async () => {
    const { onMarcarTodas, onPreferencias } = panel();
    await userEvent.click(screen.getByRole('button', { name: 'Marcar todas' }));
    await userEvent.click(screen.getByRole('button', { name: 'Preferencias de notificación' }));
    expect(onMarcarTodas).toHaveBeenCalledTimes(1);
    expect(onPreferencias).toHaveBeenCalledTimes(1);
  });

  it('con todas leídas deshabilita «Marcar todas»', () => {
    panel({ bandeja: { datos: [aviso('a', { leidaEn: '2026-11-08T11:00:00Z' })], noLeidas: 0, siguienteCursor: null } });
    expect(screen.getByRole('button', { name: 'Marcar todas' })).toBeDisabled();
  });

  it('mientras marca deshabilita «Marcar todas» y muestra su ícono', () => {
    panel({ marcandoTodas: true, iconoMarcarTodas: <span data-testid="icono-carga" /> });
    expect(screen.getByRole('button', { name: 'Marcar todas' })).toBeDisabled();
    expect(screen.getByTestId('icono-carga')).toBeInTheDocument();
  });

  it('mientras carga muestra el esqueleto y no la lista', () => {
    panel({ bandeja: undefined, cargando: true });
    expect(screen.getByRole('status', { name: 'Cargando' })).toBeInTheDocument();
    expect(screen.queryByRole('list')).not.toBeInTheDocument();
  });

  it('un error sin bandeja ofrece reintentar', async () => {
    const { onReintentar } = panel({ bandeja: undefined, error: 'No se pudo cargar' });
    expect(screen.getByText('No se pudo cargar')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar' }));
    expect(onReintentar).toHaveBeenCalledTimes(1);
  });

  it('una bandeja vacía muestra el texto de vacío', () => {
    panel({ bandeja: { datos: [], noLeidas: 0, siguienteCursor: null } });
    expect(screen.getByText('Sin notificaciones')).toBeInTheDocument();
    expect(screen.getByText('Aquí aparecerán los avisos')).toBeInTheDocument();
  });

  it('en respaldo muestra el aviso de «sin conexión»', () => {
    panel({ respaldo: true });
    expect(screen.getByText('Sin conexión en vivo')).toBeInTheDocument();
  });

  it('sin respaldo no muestra el aviso de «sin conexión»', () => {
    panel();
    expect(screen.queryByText('Sin conexión en vivo')).not.toBeInTheDocument();
  });

  it('con marca de empresa, cada aviso lleva su logo y el color de la empresa en el borde', () => {
    panel({ marcaDe: (n) => ({ icono: <span data-testid={`logo-${n.id}`} />, color: n.id === 'a' ? '#c00' : null }) });
    expect(screen.getByTestId('logo-a').closest('.MuiListItemButton-root')).toHaveStyle({ borderLeftColor: '#c00' });
    expect(screen.getByTestId('logo-b').closest('.MuiListItemButton-root')).not.toHaveStyle({ borderLeftColor: '#c00' });
  });
});

describe('CampanaDeNotificaciones', () => {
  function Campana({ noLeidas }: { noLeidas: number }) {
    const [ancla, setAncla] = useState<HTMLElement | null>(null);
    return (
      <CampanaDeNotificaciones
        noLeidas={noLeidas}
        etiqueta={noLeidas > 0 ? `Abrir notificaciones (${String(noLeidas)} sin leer)` : 'Abrir notificaciones'}
        titulo="Notificaciones"
        ancla={ancla}
        onAnclar={setAncla}
        tamanoIcono={18}
        ancho={380}
        alturaMaxima="min(520px, calc(100vh - 64px))"
      >
        <p>Contenido del panel</p>
      </CampanaDeNotificaciones>
    );
  }

  it('muestra el contador, abre el panel como diálogo al pulsar y lo cierra con Escape', async () => {
    conTema(<Campana noLeidas={120} />);
    const boton = screen.getByRole('button', { name: 'Abrir notificaciones (120 sin leer)' });
    expect(boton).toHaveAttribute('aria-expanded', 'false');
    expect(screen.getByText('99+')).toBeInTheDocument();
    expect(screen.queryByText('Contenido del panel')).not.toBeInTheDocument();

    await userEvent.click(boton);
    expect(boton).toHaveAttribute('aria-expanded', 'true');
    expect(await screen.findByRole('dialog')).toHaveTextContent('Contenido del panel');

    await userEvent.keyboard('{Escape}');
    await vi.waitFor(() => expect(screen.queryByText('Contenido del panel')).not.toBeInTheDocument());
    expect(boton).toHaveAttribute('aria-expanded', 'false');
  });

  it('sin no leídas no muestra contador', () => {
    conTema(<Campana noLeidas={0} />);
    expect(screen.getByRole('button', { name: 'Abrir notificaciones' })).toBeInTheDocument();
    expect(screen.queryByText('0')).not.toBeInTheDocument();
  });
});
