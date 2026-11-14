import { QueryClient, QueryClientProvider, useQuery } from '@tanstack/react-query';
import { act, render, screen, waitFor } from '@testing-library/react';
import {
  configurarCliente,
  registrarEdicion,
  reiniciarCliente,
  reiniciarEdiciones,
  useCambiosPendientes,
  type BandejaNotificacionesTerceroDto,
  type SesionTerceroDto,
} from '@validasoft/api-client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { crearFetchSse } from '@validasoft/api-client/pruebas/sse';
import { useSesionTercero } from '../sesion/sesionTercero';
import { claveBandeja } from './api';
import { ALCANCES_PUBLICO, MAPA_PUBLICO, useStreamDelTercero } from './tiempoReal';

const SESION = { token: 'x', accessToken: 'tok', expira: new Date(Date.now() + 30 * 60_000).toISOString(), terceroId: 't1', otpVerificado: true, vinculos: [] } as unknown as SesionTerceroDto;
const RAPIDO = { base: 5, maximo: 20 };

/** Muestra cuántas veces se pidió «el expediente» y si hay cambios nuevos sin aplicar; el tiempo real corre en la misma pantalla. */
function Pantalla({ llamadas }: { llamadas: { n: number } }) {
  const estado = useStreamDelTercero(RAPIDO);
  const consulta = useQuery({
    queryKey: ['/public/v1/expedientes/e1'],
    queryFn: () => {
      llamadas.n += 1;
      return Promise.resolve(llamadas.n);
    },
    staleTime: 0,
  });
  const pendientes = useCambiosPendientes(ALCANCES_PUBLICO.expedientes);
  return (
    <p>
      {`estado ${estado} · pedido ${String(consulta.data ?? 0)} · ${pendientes ? 'hay cambios nuevos' : 'al día'}`}
    </p>
  );
}

function montar() {
  const { fetchFalso, peticiones } = crearFetchSse();
  configurarCliente({ obtenerToken: () => 'tok', generarId: () => 'corr-1', fetch: fetchFalso });
  const cliente = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const llamadas = { n: 0 };
  render(
    <QueryClientProvider client={cliente}>
      <Pantalla llamadas={llamadas} />
    </QueryClientProvider>,
  );
  return { cliente, llamadas, peticiones };
}

const evento = (tipo: string, datos: unknown) => `event: ${tipo}\ndata: ${typeof datos === 'string' ? datos : JSON.stringify(datos)}\n\n`;

beforeEach(() => {
  useSesionTercero.setState({ sesion: SESION, estado: 'activa' });
});

afterEach(() => {
  reiniciarCliente();
  reiniciarEdiciones();
  useSesionTercero.setState({ sesion: null, estado: 'sin-sesion' });
  vi.restoreAllMocks();
});

describe('tiempo real del portal del tercero', () => {
  it('abre un solo stream con la sesión del tercero y refresca solo lo que el recurso que cambió muestra', async () => {
    const { llamadas, peticiones } = montar();

    await waitFor(() => expect(peticiones).toHaveLength(1));
    expect(peticiones[0]?.url).toBe('/public/v1/notificaciones/stream');
    expect(peticiones[0]?.cabeceras.get('Authorization')).toBe('Bearer tok');
    expect(peticiones[0]?.cabeceras.get('X-Empresa')).toBeNull();
    await waitFor(() => expect(llamadas.n).toBe(1));
    await screen.findByText(/estado conectado/);

    // Un cambio de cuenta no toca el expediente; uno del expediente sí, y varios seguidos se juntan en una sola recarga.
    act(() => peticiones[0]?.cuerpo.enviar(evento('cambio', { recurso: 'cuenta', id: 'c1', vinculoId: 'v1' })));
    await new Promise((resolver) => setTimeout(resolver, 400));
    expect(llamadas.n).toBe(1);
    act(() => {
      for (let i = 0; i < 3; i += 1) peticiones[0]?.cuerpo.enviar(evento('cambio', { recurso: 'expediente', id: 'e1', vinculoId: 'v1' }));
    });
    await waitFor(() => expect(llamadas.n).toBe(2));
    await new Promise((resolver) => setTimeout(resolver, 400));
    expect(llamadas.n).toBe(2);
    expect(MAPA_PUBLICO.cuenta).not.toContain(ALCANCES_PUBLICO.expedientes);
  });

  it('con cambios sin guardar no pisa la pantalla: avisa «hay cambios nuevos» y al terminar la edición lo aplica', async () => {
    const { llamadas, peticiones } = montar();
    await waitFor(() => expect(llamadas.n).toBe(1));
    await waitFor(() => expect(peticiones).toHaveLength(1));

    const terminar = registrarEdicion(ALCANCES_PUBLICO.expedientes);
    act(() => peticiones[0]?.cuerpo.enviar(evento('cambio', { recurso: 'expediente', id: 'e1', vinculoId: null })));
    await screen.findByText(/hay cambios nuevos/);
    await new Promise((resolver) => setTimeout(resolver, 400));
    expect(llamadas.n).toBe(1);

    act(() => terminar());
    await waitFor(() => expect(llamadas.n).toBe(2));
    await screen.findByText(/al día/);
  });

  it('una notificación que llega por el stream entra arriba en la campana y ajusta el total de no leídas', async () => {
    const { cliente, peticiones } = montar();
    const bandeja: BandejaNotificacionesTerceroDto = { datos: [], siguienteCursor: null, noLeidas: 0 };
    cliente.setQueryData(claveBandeja, bandeja);
    await waitFor(() => expect(peticiones).toHaveLength(1));

    const notificacion = {
      id: 'n1',
      tipo: 'tercero.cuenta',
      titulo: 'Cuenta aprobada',
      mensaje: 'Norte aprobó la cuenta Bancolombia ****5678 con la que le cobras.',
      empresa: { id: 'e', nombre: 'Norte', logoUrl: null },
      creadaEn: '2026-11-10T12:00:00Z',
      leidaEn: null,
      enlace: '/cuentas',
    };
    act(() => peticiones[0]?.cuerpo.enviar(`id: 638\nevent: notificacion\ndata: ${JSON.stringify(notificacion)}\n\n`));
    await waitFor(() => expect(cliente.getQueryData<BandejaNotificacionesTerceroDto>(claveBandeja)?.noLeidas).toBe(1));
    const actual = cliente.getQueryData<BandejaNotificacionesTerceroDto>(claveBandeja);
    expect(actual?.datos[0]).toMatchObject({ id: 'n1', empresa: { nombre: 'Norte' }, expedienteId: null, folio: null, vinculoId: null });

    // Lo que no tiene la forma esperada se ignora.
    act(() => peticiones[0]?.cuerpo.enviar(evento('notificacion', { id: 5 })));
    act(() => peticiones[0]?.cuerpo.enviar(evento('cambio', 'no es json')));
    act(() => peticiones[0]?.cuerpo.enviar(evento('cambio', { recurso: 'otra-cosa', id: 'x' })));
    await new Promise((resolver) => setTimeout(resolver, 100));
    expect(cliente.getQueryData<BandejaNotificacionesTerceroDto>(claveBandeja)?.datos).toHaveLength(1);
  });

  it('si el servidor cierra el stream reconecta con espera creciente y, al volver, refresca todo lo que muestra', async () => {
    const { llamadas, peticiones } = montar();
    await waitFor(() => expect(llamadas.n).toBe(1));
    await waitFor(() => expect(peticiones).toHaveLength(1));

    act(() => peticiones[0]?.cuerpo.cerrar());
    await waitFor(() => expect(peticiones).toHaveLength(2));
    // La conexión nueva lleva el token vigente; al conectar se vuelve a pedir lo que mostraba (pudo cambiar mientras tanto).
    expect(peticiones[1]?.cabeceras.get('Authorization')).toBe('Bearer tok');
    await waitFor(() => expect(llamadas.n).toBe(2));
  });

  it('sin sesión activa no abre ningún stream', async () => {
    useSesionTercero.setState({ sesion: null, estado: 'sin-sesion' });
    const { peticiones } = montar();
    await new Promise((resolver) => setTimeout(resolver, 100));
    expect(peticiones).toHaveLength(0);
  });
});
