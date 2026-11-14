import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type PreferenciasNotificacionDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../shared/useAvisos';
import { DialogoPreferencias } from './DialogoPreferencias';
import { json, montar, prepararApi } from './pruebas/entorno';

const RUTA = '/api/v1/usuarios/me/preferencias-notificacion';
const GUARDADAS: PreferenciasNotificacionDto = {
  correo: { 'expediente.nuevo_en_area': true, 'expediente.tercero_respondio': true, 'webhook.deshabilitado': false },
  tiposConCorreo: ['expediente.nuevo_en_area', 'expediente.tercero_respondio', 'webhook.deshabilitado'],
};

afterEach(() => {
  reiniciarCliente();
  useAvisos.getState().cerrar();
});

describe('DialogoPreferencias', () => {
  it('muestra un interruptor por tipo, no deja guardar sin cambios y envía en el PUT solo lo que cambió', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = prepararApi((metodo, ruta) => {
      if (ruta !== RUTA) return json(404, {});
      if (metodo === 'PUT') return json(200, { ...GUARDADAS, correo: { ...GUARDADAS.correo, 'expediente.tercero_respondio': false } });
      return json(200, GUARDADAS);
    });
    const onCerrar = vi.fn();
    montar(<DialogoPreferencias onCerrar={onCerrar} />);

    const nuevo = await screen.findByRole('switch', { name: /Nuevo expediente en tu área/ });
    const respondio = screen.getByRole('switch', { name: /El tercero respondió/ });
    const webhook = screen.getByRole('switch', { name: /Webhook deshabilitado/ });
    expect(nuevo).toBeChecked();
    expect(webhook).not.toBeChecked();
    expect(screen.getByText(/Cuando el tercero corrige lo observado/)).toBeInTheDocument();

    const guardar = screen.getByRole('button', { name: 'Guardar' });
    expect(guardar).toBeDisabled();
    expect(screen.getByText('No has cambiado nada.')).toBeInTheDocument();

    // Cambiar y deshacer vuelve a «sin cambios».
    await usuario.click(nuevo);
    expect(guardar).toBeEnabled();
    await usuario.click(nuevo);
    expect(guardar).toBeDisabled();

    await usuario.click(respondio);
    await usuario.click(guardar);

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    const put = peticiones.find((p) => p.metodo === 'PUT');
    expect(put?.ruta).toBe(RUTA);
    expect(put?.cuerpo).toEqual({ correo: { 'expediente.tercero_respondio': false } });
    expect(put?.cabeceras.has('X-Empresa')).toBe(false);
    expect(useAvisos.getState().actual?.mensaje).toBe('Guardamos tus preferencias de correo.');
  });
});
