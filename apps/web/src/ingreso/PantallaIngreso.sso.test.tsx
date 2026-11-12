import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { StrictMode } from 'react';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { montarPantalla } from '../pruebas/montar';
import { OPCIONES_DE_FIRMA, simularNavegadorConPasskeys, type NavegadorSimulado } from '../pruebas/navegadorSimulado';
import { ProblemaSimulado, simularApi } from '../pruebas/simularApi';
import { useSesion } from '../sesion/sesionStore';
import { PantallaIngreso } from './PantallaIngreso';

const DESAFIO = '0d0e5a3a-5f0e-47fa-a0a1-6c1e0f9bd7a1';
const token = (exp: number) => `e30.${Buffer.from(JSON.stringify({ sub: 'u1', exp, 'validasoft:tenant': 't1' })).toString('base64url')}.firma`;
const SESION = () => ({
  estado: 'Sesion',
  desafio: DESAFIO,
  sesion: { token: token(Math.floor(Date.now() / 1000) + 600), expira: '2099-01-01T00:00:00Z', sesionId: 's1', metodo: 'Sso', sugerirPasskey: false },
});

let navegador: NavegadorSimulado | null = null;

beforeEach(() => {
  useSesion.setState({ sesion: null, estado: 'sin-sesion', empresaSugerida: null, sugerirPasskey: false });
});

afterEach(() => {
  navegador?.restaurar();
  navegador = null;
  reiniciarCliente();
  window.history.replaceState(null, '', '/');
  useSesion.setState({ sesion: null, estado: 'sin-sesion', empresaSugerida: null, sugerirPasskey: false });
});

function montar() {
  montarPantalla(
    <StrictMode>
      <PantallaIngreso estado="sin-sesion" />
    </StrictMode>,
  );
}

describe('de vuelta del proveedor de identidad de la empresa (PI-1)', () => {
  it('canjea el retorno una sola vez por la sesión y deja limpia la dirección', async () => {
    window.history.replaceState(null, '', `/#/ingreso?sso=${DESAFIO}`);
    const peticiones = simularApi({ 'POST /api/v1/ingreso/sso/completar': SESION });

    montar();

    expect(screen.getByRole('status', { name: 'Terminando tu ingreso' })).toBeInTheDocument();
    await waitFor(() => expect(useSesion.getState().estado).toBe('activa'));
    // StrictMode monta dos veces: el retorno es de un solo uso, así que se canjea una sola.
    expect(peticiones.filter((p) => p.ruta === '/api/v1/ingreso/sso/completar')).toHaveLength(1);
    expect(peticiones[0]?.cuerpo).toEqual({ desafio: DESAFIO });
    expect(window.location.hash).toBe('#/');
  });

  it('si la cuenta exige passkey después del SSO, pasa al paso de la passkey', async () => {
    navegador = simularNavegadorConPasskeys();
    window.history.replaceState(null, '', `/#/ingreso?sso=${DESAFIO}`);
    simularApi({ 'POST /api/v1/ingreso/sso/completar': { estado: 'PasskeyRequerida', desafio: DESAFIO, opciones: OPCIONES_DE_FIRMA } });

    montar();

    expect(await screen.findByText('Confirma que eres tú')).toBeInTheDocument();
    expect(useSesion.getState().estado).toBe('sin-sesion');
  });

  it('si no se puede canjear, vuelve al correo y dice por qué', async () => {
    window.history.replaceState(null, '', `/#/ingreso?sso=${DESAFIO}`);
    simularApi({ 'POST /api/v1/ingreso/sso/completar': new ProblemaSimulado(403, 'sso-sin-cuenta', 'Tu empresa aún no te dio acceso a Validasoft.') });

    montar();

    expect(await screen.findByRole('alert')).toHaveTextContent('Tu empresa aún no te dio acceso a Validasoft. Pide a un administrador de tu grupo que te invite.');
    expect(screen.getByLabelText('Correo')).toBeInTheDocument();
  });

  it('un fallo que dejó el servidor en la dirección se explica al llegar y no se vuelve a mostrar al recargar', async () => {
    window.history.replaceState(null, '', '/#/ingreso?sso_error=sso-cancelado');
    simularApi({});

    montar();

    expect(await screen.findByRole('alert')).toHaveTextContent('El inicio de sesión de tu empresa se canceló o no lo autorizaron. Inténtalo de nuevo.');
    expect(window.location.hash).toBe('#/');
  });

  it('un código de error que la app no conoce da el mensaje general con el siguiente paso', async () => {
    window.history.replaceState(null, '', '/#/ingreso?sso_error=codigo-nuevo-del-servidor');
    simularApi({});

    montar();

    expect(await screen.findByRole('alert')).toHaveTextContent('No pudimos terminar el ingreso con tu empresa. Inténtalo de nuevo o usa el código de tu correo.');
  });

  it('el aviso se puede cerrar para seguir con el correo', async () => {
    window.history.replaceState(null, '', '/#/ingreso?sso_error=sso-cancelado');
    simularApi({});
    montar();
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: /Cerrar|Close/ }));

    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it('sin retorno en la dirección es el ingreso de siempre', () => {
    simularApi({});
    montar();
    expect(screen.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
