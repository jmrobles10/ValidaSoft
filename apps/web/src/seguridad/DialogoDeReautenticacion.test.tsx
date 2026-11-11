import { act, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ErrorApi, reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { montarPantalla } from '../pruebas/montar';
import { OPCIONES_DE_FIRMA, simularNavegadorConPasskeys, type NavegadorSimulado } from '../pruebas/navegadorSimulado';
import { ProblemaSimulado, simularApi } from '../pruebas/simularApi';
import { DialogoDeReautenticacion } from './DialogoDeReautenticacion';
import { resolverReautenticacion, solicitarReautenticacion, useReautenticacion } from './reautenticacion';

let navegador: NavegadorSimulado | null = null;

afterEach(() => {
  act(() => resolverReautenticacion(false));
  navegador?.restaurar();
  navegador = null;
  reiniciarCliente();
  window.location.hash = '';
});

function requerir(extensiones: Record<string, unknown> = { accion: 'rotar-api-key', minutos: 5, tienePasskey: true }): Promise<boolean> {
  let respuesta: Promise<boolean> = Promise.resolve(false);
  act(() => {
    respuesta = solicitarReautenticacion(new ErrorApi(403, 'reautenticacion-requerida', 'Confirma con tu passkey', null, null, {}, extensiones));
  });
  return respuesta;
}

describe('diálogo de reautenticación (PI-13)', () => {
  it('no se ve mientras nadie pide confirmar', () => {
    montarPantalla(<DialogoDeReautenticacion />);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('dice qué acción se confirma, pide la passkey a la API y deja repetir la petición', async () => {
    navegador = simularNavegadorConPasskeys();
    const peticiones = simularApi({
      'POST /api/v1/mi-cuenta/reautenticacion/opciones': { desafio: 'desafio-1', opciones: OPCIONES_DE_FIRMA },
      'POST /api/v1/mi-cuenta/reautenticacion': { hasta: '2026-11-11T15:05:00Z', minutos: 5 },
    });
    montarPantalla(<DialogoDeReautenticacion />);
    const usuario = userEvent.setup();

    const confirmada = requerir();

    expect(await screen.findByRole('dialog', { name: 'Confirma que eres tú' })).toBeInTheDocument();
    expect(screen.getByText('Para rotar una API key, confirma con tu passkey.')).toBeInTheDocument();
    expect(screen.getByText('Con esto no te lo volvemos a pedir durante 5 minutos.')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Usar mi passkey' }));

    await expect(confirmada).resolves.toBe(true);
    expect(peticiones.map((p) => `${p.metodo} ${p.ruta}`)).toEqual(['POST /api/v1/mi-cuenta/reautenticacion/opciones', 'POST /api/v1/mi-cuenta/reautenticacion']);
    expect(peticiones[1]?.cuerpo).toMatchObject({ desafio: 'desafio-1', respuesta: { id: 'cred-1', type: 'public-key' } });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(useReautenticacion.getState().peticion).toBeNull();
  });

  it('cancelar cierra el diálogo y responde que no se confirmó', async () => {
    montarPantalla(<DialogoDeReautenticacion />);
    const usuario = userEvent.setup();
    const confirmada = requerir();

    await usuario.click(await screen.findByRole('button', { name: 'Cancelar' }));

    await expect(confirmada).resolves.toBe(false);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('si la persona cierra la ventana de la passkey explica qué pasó y deja intentar otra vez sin cerrar', async () => {
    navegador = simularNavegadorConPasskeys();
    navegador.get.mockRejectedValueOnce(new DOMException('cancelada', 'NotAllowedError'));
    simularApi({
      'POST /api/v1/mi-cuenta/reautenticacion/opciones': { desafio: 'desafio-1', opciones: OPCIONES_DE_FIRMA },
      'POST /api/v1/mi-cuenta/reautenticacion': { hasta: '2026-11-11T15:05:00Z', minutos: 5 },
    });
    montarPantalla(<DialogoDeReautenticacion />);
    const usuario = userEvent.setup();
    const confirmada = requerir();

    await usuario.click(await screen.findByRole('button', { name: 'Usar mi passkey' }));
    expect(await screen.findByText('No se completó el paso de la passkey. Inténtalo de nuevo cuando quieras.')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Usar mi passkey' }));
    await expect(confirmada).resolves.toBe(true);
  });

  it('un error de la API al confirmar se muestra y no se da por confirmado', async () => {
    navegador = simularNavegadorConPasskeys();
    simularApi({
      'POST /api/v1/mi-cuenta/reautenticacion/opciones': { desafio: 'desafio-1', opciones: OPCIONES_DE_FIRMA },
      'POST /api/v1/mi-cuenta/reautenticacion': new ProblemaSimulado(401, 'passkey-invalida', 'La passkey no es válida.'),
    });
    montarPantalla(<DialogoDeReautenticacion />);
    const usuario = userEvent.setup();
    void requerir();

    await usuario.click(await screen.findByRole('button', { name: 'Usar mi passkey' }));

    expect(await screen.findByText('No pudimos verificar tu passkey. Inténtalo de nuevo.')).toBeInTheDocument();
    expect(useReautenticacion.getState().peticion).not.toBeNull();
  });

  it('sin ninguna passkey no hay con qué confirmar: lleva a registrarla en Mi cuenta', async () => {
    window.location.hash = '#/plataforma/operadores';
    montarPantalla(<DialogoDeReautenticacion />);
    const usuario = userEvent.setup();
    const confirmada = requerir({ accion: 'desactivar-operador', minutos: 5, tienePasskey: false });

    expect(await screen.findByRole('dialog', { name: 'Registra una passkey' })).toBeInTheDocument();
    expect(screen.getByText(/Para desactivar un operador de Plataforma necesitas una passkey/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Usar mi passkey' })).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Ir a Mi cuenta' }));

    await expect(confirmada).resolves.toBe(false);
    expect(window.location.hash).toBe('#/plataforma/mi-cuenta');
  });

  it('una acción que la app no conoce se dice de forma genérica', async () => {
    montarPantalla(<DialogoDeReautenticacion />);
    void requerir({ accion: 'accion-nueva', minutos: 5, tienePasskey: true });
    expect(await screen.findByText('Para hacer esta acción, confirma con tu passkey.')).toBeInTheDocument();
  });
});
