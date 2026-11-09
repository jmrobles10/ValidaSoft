import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type BandejaNotificacionesDto } from '@validasoft/api-client';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { useSesion } from '../../sesion/sesionStore';
import { useAvisos } from '../../shared/useAvisos';
import { Campana } from './Campana';
import { notificacion } from './pruebas/datos';
import { iniciarSesionDePrueba, json, montar, prepararApi } from './pruebas/entorno';

const BASE = '/api/v1/notificaciones';

function conBandeja(bandeja: BandejaNotificacionesDto) {
  return prepararApi((metodo, ruta) => {
    if (metodo === 'GET' && ruta === BASE) return json(200, bandeja);
    if (metodo === 'POST' && ruta === `${BASE}/leidas`) return json(200, { marcadas: bandeja.noLeidas });
    const leida = /\/notificaciones\/([^/]+)\/leida$/.exec(ruta);
    if (metodo === 'POST' && leida) return json(200, { ...notificacion({ id: leida[1] }), leidaEn: '2026-11-08T12:00:00Z' });
    return json(404, { title: 'No encontrado' });
  });
}

beforeEach(() => iniciarSesionDePrueba());

afterEach(() => {
  reiniciarCliente();
  useAvisos.getState().cerrar();
  useSesion.setState({ sesion: null, estado: 'sin-sesion' });
});

describe('Campana', () => {
  const sinLeer = notificacion({ id: 'n1', titulo: 'Nuevo expediente en tu área' });
  const leida = notificacion({ id: 'n2', titulo: 'Expediente aprobado', tipo: 'expediente.aprobado', leidaEn: '2026-11-08T11:00:00Z' });

  it('muestra el contador, la lista con la marca de no leída y al hacer clic la marca como leída y navega al expediente', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = conBandeja({ datos: [sinLeer, leida], siguienteCursor: null, noLeidas: 1 });
    montar(<Campana empresaId="emp" />);

    const campana = await screen.findByRole('button', { name: 'Notificaciones, 1 sin leer' });
    await usuario.click(campana);
    const panel = await screen.findByRole('dialog', { name: 'Notificaciones' });
    const items = within(panel).getAllByRole('listitem');
    expect(items).toHaveLength(2);
    expect(within(items[0]!).getByText(/Sin leer/)).toBeInTheDocument();
    expect(within(items[0]!).getByText(sinLeer.mensaje)).toBeInTheDocument();
    expect(within(items[1]!).queryByText(/Sin leer/)).not.toBeInTheDocument();

    await usuario.click(within(items[0]!).getByRole('button'));
    expect(await screen.findByText('Detalle del expediente /e/emp/expedientes/exp-1')).toBeInTheDocument();
    const marcar = peticiones.find((p) => p.metodo === 'POST' && p.ruta === `${BASE}/n1/leida`);
    expect(marcar?.cabeceras.get('X-Empresa')).toBe('emp');
  });

  it('«Marcar todas como leídas» llama a la API, apaga el contador y lo confirma', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = conBandeja({ datos: [sinLeer, notificacion({ id: 'n3' })], siguienteCursor: null, noLeidas: 2 });
    montar(<Campana empresaId="emp" />);

    await usuario.click(await screen.findByRole('button', { name: 'Notificaciones, 2 sin leer' }));
    await usuario.click(await screen.findByRole('button', { name: 'Marcar todas como leídas' }));

    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Marcamos 2 notificaciones como leídas.'));
    expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === `${BASE}/leidas`)).toBe(true);
    // Con el panel abierto, la campana queda detrás del modal (oculta para el lector de pantalla).
    expect(screen.getByRole('button', { name: 'Notificaciones', hidden: true })).toBeInTheDocument();
    expect(screen.queryByText(/Sin leer/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Marcar todas como leídas' })).toBeDisabled();
  });

  it('sin notificaciones muestra el estado vacío y no deja marcar todas', async () => {
    const usuario = userEvent.setup();
    conBandeja({ datos: [], siguienteCursor: null, noLeidas: 0 });
    montar(<Campana empresaId="emp" />);

    await usuario.click(await screen.findByRole('button', { name: 'Notificaciones' }));
    expect(await screen.findByText('No tienes notificaciones')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Marcar todas como leídas' })).toBeDisabled();
  });

  it('una notificación que llega por el stream entra arriba y sube el contador', async () => {
    const usuario = userEvent.setup();
    const { streams, peticiones } = conBandeja({ datos: [leida], siguienteCursor: null, noLeidas: 0 });
    montar(<Campana empresaId="emp" />);

    await screen.findByRole('button', { name: 'Notificaciones' });
    await waitFor(() => expect(streams).toHaveLength(1));
    const stream = peticiones.find((p) => p.ruta.endsWith('/stream'));
    expect(stream?.cabeceras.get('Authorization')).toBe('Bearer tok');
    expect(stream?.cabeceras.get('X-Empresa')).toBe('emp');

    const nueva = notificacion({ id: 'n9', titulo: 'El tercero respondió', tipo: 'expediente.tercero_respondio' });
    streams[0]!.enviar(`: conectado\n\nid: 638000000000000001\nevent: notificacion\ndata: ${JSON.stringify(nueva)}\n\n`);
    streams[0]!.enviar('id: 638000000000000001\nevent: no_leidas\ndata: 1\n\n');

    await usuario.click(await screen.findByRole('button', { name: 'Notificaciones, 1 sin leer' }));
    const items = within(await screen.findByRole('dialog', { name: 'Notificaciones' })).getAllByRole('listitem');
    expect(items.map((i) => within(i).getByRole('button').textContent)).toEqual([
      expect.stringContaining('El tercero respondió'),
      expect.stringContaining('Expediente aprobado'),
    ]);
  });
});
