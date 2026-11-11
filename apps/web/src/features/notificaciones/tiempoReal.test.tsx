import { useQuery } from '@tanstack/react-query';
import { act, screen, waitFor } from '@testing-library/react';
import { reiniciarCliente, reiniciarEdiciones } from '@validasoft/api-client';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { useSesion } from '../../sesion/sesionStore';
import { MAPA_WEB } from './cambios';
import { iniciarSesionDePrueba, json, montar, prepararApi } from './pruebas/entorno';
import { useStreamNotificaciones } from './useStreamNotificaciones';

/** Una pantalla con el stream de la web de empresa y dos consultas abiertas: el expediente y la ficha del tercero. */
function Pantalla({ llamadas }: { llamadas: { expediente: number; ficha: number } }) {
  useStreamNotificaciones('emp', { base: 5, maximo: 20 });
  const expediente = useQuery({
    queryKey: ['/api/v1/expedientes/e1'],
    queryFn: () => Promise.resolve((llamadas.expediente += 1)),
  });
  const ficha = useQuery({
    queryKey: ['/api/v1/terceros/t1/ficha'],
    queryFn: () => Promise.resolve((llamadas.ficha += 1)),
  });
  return <p>{`expediente ${String(expediente.data ?? 0)} · ficha ${String(ficha.data ?? 0)}`}</p>;
}

const evento = (tipo: string, datos: unknown) => `event: ${tipo}\ndata: ${JSON.stringify(datos)}\n\n`;

beforeEach(() => {
  iniciarSesionDePrueba();
});

afterEach(() => {
  reiniciarCliente();
  reiniciarEdiciones();
  useSesion.setState({ sesion: null, estado: 'sin-sesion' });
});

describe('tiempo real de la web de empresa', () => {
  it('un aviso de cambio refresca lo que la pantalla muestra de ese recurso: el expediente cambia y la ficha del tercero, no', async () => {
    const { streams, peticiones } = prepararApi(() => json(200, {}));
    const llamadas = { expediente: 0, ficha: 0 };
    montar(<Pantalla llamadas={llamadas} />);

    await waitFor(() => expect(streams).toHaveLength(1));
    const stream = peticiones.find((p) => p.ruta.endsWith('/notificaciones/stream'));
    expect(stream?.cabeceras.get('X-Empresa')).toBe('emp');
    expect(stream?.cabeceras.get('Authorization')).toBe('Bearer tok');
    await screen.findByText('expediente 1 · ficha 1');

    // El expediente de la empresa cambió (lo observó otra persona): se vuelve a pedir; la ficha del tercero no.
    act(() => streams[0]?.enviar(evento('cambio', { recurso: 'expediente', id: 'e1', vinculoId: 'v1' })));
    await screen.findByText('expediente 2 · ficha 1');
    expect(MAPA_WEB.expediente).not.toContain('/api/v1/terceros');

    // El tercero cambió su ficha (o designó una cuenta): se vuelve a pedir lo del tercero, sin recargar la página.
    act(() => streams[0]?.enviar(evento('cambio', { recurso: 'cuenta', id: 'c1', vinculoId: 'v1' })));
    await screen.findByText('expediente 2 · ficha 2');
    act(() => streams[0]?.enviar(evento('cambio', { recurso: 'ficha', id: 's1', vinculoId: 'v1' })));
    await screen.findByText('expediente 3 · ficha 3');
  });

  it('al reconectar tras una caída vuelve a pedir lo que mostraba y la bandeja se mantiene al día', async () => {
    const { streams } = prepararApi(() => json(200, {}));
    const llamadas = { expediente: 0, ficha: 0 };
    montar(<Pantalla llamadas={llamadas} />);
    await screen.findByText('expediente 1 · ficha 1');
    await waitFor(() => expect(streams).toHaveLength(1));

    act(() => streams[0]?.cerrar());
    await waitFor(() => expect(streams).toHaveLength(2));
    await screen.findByText('expediente 2 · ficha 2');
  });
});
