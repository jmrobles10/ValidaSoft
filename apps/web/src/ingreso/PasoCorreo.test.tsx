import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type PasoDeIngresoDto } from '@validasoft/api-client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { montarPantalla } from '../pruebas/montar';
import { OPCIONES_DE_FIRMA, simularNavegadorConPasskeys, type NavegadorSimulado } from '../pruebas/navegadorSimulado';
import { ProblemaSimulado, simularApi } from '../pruebas/simularApi';
import { PasoCorreo } from './PasoCorreo';

let navegador: NavegadorSimulado | null = null;
const asignar = vi.fn();
const original = window.location;

beforeEach(() => {
  asignar.mockReset();
  vi.stubGlobal('location', { href: original.href, pathname: original.pathname, search: original.search, assign: asignar, hash: '' });
});

afterEach(() => {
  navegador?.restaurar();
  navegador = null;
  reiniciarCliente();
  vi.unstubAllGlobals();
});

const SIN_SSO = { disponible: false, nombre: null, reemplazaElCodigo: false };
const SSO_COMPLEMENTARIO = { disponible: true, nombre: 'Andina', reemplazaElCodigo: false };
const SSO_EXCLUSIVO = { disponible: true, nombre: 'Andina', reemplazaElCodigo: true };

function montar(alResultado: (paso: PasoDeIngresoDto) => void = () => undefined) {
  montarPantalla(<PasoCorreo alCodigo={() => undefined} alResultado={alResultado} />);
  return userEvent.setup();
}

describe('primer paso del ingreso con SSO (PI-1)', () => {
  it('con un dominio sin SSO todo sigue como siempre: código y, si el navegador puede, passkey', async () => {
    navegador = simularNavegadorConPasskeys();
    const peticiones = simularApi({ 'POST /api/v1/ingreso/sso/descubrir': SIN_SSO });
    const usuario = montar();

    await usuario.type(screen.getByLabelText('Correo'), 'ana@aceros.co');
    await waitFor(() => expect(peticiones.some((p) => p.ruta === '/api/v1/ingreso/sso/descubrir')).toBe(true));

    expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Entrar con una passkey' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Entrar con Andina/ })).not.toBeInTheDocument();
  });

  it('no pregunta por el SSO mientras el correo no parece un correo', async () => {
    const peticiones = simularApi({ 'POST /api/v1/ingreso/sso/descubrir': SIN_SSO });
    const usuario = montar();

    await usuario.type(screen.getByLabelText('Correo'), 'ana@aceros');
    await new Promise((r) => setTimeout(r, 700));

    expect(peticiones).toEqual([]);
  });

  it('con SSO complementario ofrece entrar con la empresa y deja el código y la passkey', async () => {
    navegador = simularNavegadorConPasskeys();
    simularApi({ 'POST /api/v1/ingreso/sso/descubrir': SSO_COMPLEMENTARIO });
    const usuario = montar();

    await usuario.type(screen.getByLabelText('Correo'), 'ana@andina.co');

    expect(await screen.findByRole('button', { name: 'Entrar con Andina' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Continuar' })).toBeEnabled();
    expect(screen.getByRole('button', { name: 'Entrar con una passkey' })).toBeInTheDocument();
  });

  it('con SSO exclusivo solo queda el botón de la empresa: ni código ni passkey', async () => {
    navegador = simularNavegadorConPasskeys();
    simularApi({ 'POST /api/v1/ingreso/sso/descubrir': SSO_EXCLUSIVO });
    const usuario = montar();

    await usuario.type(screen.getByLabelText('Correo'), 'ana@andina.co');

    expect(await screen.findByRole('button', { name: 'Entrar con Andina' })).toBeEnabled();
    expect(screen.queryByRole('button', { name: 'Continuar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Entrar con una passkey' })).not.toBeInTheDocument();
    expect(screen.getByText(/Tu empresa ingresa con Andina/)).toBeInTheDocument();
  });

  it('al pulsar el botón pide la dirección al servidor y lleva al navegador al proveedor', async () => {
    const peticiones = simularApi({
      'POST /api/v1/ingreso/sso/descubrir': SSO_COMPLEMENTARIO,
      'POST /api/v1/ingreso/sso/iniciar': { desafio: 'd1', urlDeAutorizacion: 'https://login.andina.co/authorize?state=abc' },
    });
    const usuario = montar();
    await usuario.type(screen.getByLabelText('Correo'), 'ana@andina.co');

    await usuario.click(await screen.findByRole('button', { name: 'Entrar con Andina' }));

    await waitFor(() => expect(asignar).toHaveBeenCalledWith('https://login.andina.co/authorize?state=abc'));
    expect(peticiones.find((p) => p.ruta === '/api/v1/ingreso/sso/iniciar')?.cuerpo).toEqual({ correo: 'ana@andina.co' });
    expect(screen.getByRole('button', { name: 'Entrar con Andina' })).toBeDisabled();
  });

  it('con SSO exclusivo, Enter en el correo inicia el SSO y no pide un código', async () => {
    const peticiones = simularApi({
      'POST /api/v1/ingreso/sso/descubrir': SSO_EXCLUSIVO,
      'POST /api/v1/ingreso/sso/iniciar': { desafio: 'd1', urlDeAutorizacion: 'https://login.andina.co/authorize' },
    });
    const usuario = montar();
    await usuario.type(screen.getByLabelText('Correo'), 'ana@andina.co');
    await screen.findByRole('button', { name: 'Entrar con Andina' });

    await usuario.type(screen.getByLabelText('Correo'), '{Enter}');

    await waitFor(() => expect(asignar).toHaveBeenCalledTimes(1));
    expect(peticiones.some((p) => p.ruta === '/api/v1/ingreso/codigo')).toBe(false);
  });

  it('nunca navega a una dirección que no sea web (un proveedor comprometido no puede mandar javascript:)', async () => {
    simularApi({
      'POST /api/v1/ingreso/sso/descubrir': SSO_COMPLEMENTARIO,
      'POST /api/v1/ingreso/sso/iniciar': { desafio: 'd1', urlDeAutorizacion: 'javascript:alert(1)' },
    });
    const usuario = montar();
    await usuario.type(screen.getByLabelText('Correo'), 'ana@andina.co');

    await usuario.click(await screen.findByRole('button', { name: 'Entrar con Andina' }));

    await waitFor(() => expect(screen.getByRole('button', { name: 'Entrar con Andina' })).toBeEnabled());
    expect(asignar).not.toHaveBeenCalled();
  });

  it('un error al iniciar se explica y el botón queda libre para reintentar', async () => {
    simularApi({
      'POST /api/v1/ingreso/sso/descubrir': SSO_COMPLEMENTARIO,
      'POST /api/v1/ingreso/sso/iniciar': new ProblemaSimulado(503, 'sso-proveedor-no-responde', 'no responde'),
    });
    const usuario = montar();
    await usuario.type(screen.getByLabelText('Correo'), 'ana@andina.co');

    await usuario.click(await screen.findByRole('button', { name: 'Entrar con Andina' }));

    expect(await screen.findByText('El proveedor de identidad de tu empresa no respondió. Inténtalo de nuevo en un momento.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Entrar con Andina' })).toBeEnabled();
  });
});

describe('passkeys en el campo del correo (PI-14)', () => {
  const SESION: PasoDeIngresoDto = {
    estado: 'Sesion',
    desafio: 'd9',
    sesion: { token: 't', usuarioId: 'u1', nombre: 'Ana', tenantId: 'x', expiraEn: '2026-11-12T00:00:00Z' } as unknown as PasoDeIngresoDto['sesion'],
  };

  it('deja abierta una petición condicional y, al elegir una passkey en el autocompletado, verifica y entra', async () => {
    navegador = simularNavegadorConPasskeys({ condicional: true });
    const peticiones = simularApi({
      'POST /api/v1/ingreso/passkey/opciones': { desafio: 'd9', opciones: OPCIONES_DE_FIRMA },
      'POST /api/v1/ingreso/passkey/verificar': SESION,
    });
    const alResultado = vi.fn();

    montar(alResultado);

    await waitFor(() => expect(alResultado).toHaveBeenCalledWith(SESION));
    expect(navegador.llamadas).toEqual([{ mediation: 'conditional', conSenal: true }]);
    expect(peticiones.find((p) => p.ruta === '/api/v1/ingreso/passkey/verificar')?.cuerpo).toMatchObject({ desafio: 'd9', respuesta: { id: 'cred-1' } });
    // El campo anuncia las passkeys al navegador.
    expect(screen.getByLabelText('Correo')).toHaveAttribute('autocomplete', 'username webauthn');
  });

  it('al salir del paso aborta la petición abierta y no muestra ningún error', async () => {
    navegador = simularNavegadorConPasskeys({ condicional: true, esperaLaFirma: true });
    simularApi({ 'POST /api/v1/ingreso/passkey/opciones': { desafio: 'd9', opciones: OPCIONES_DE_FIRMA } });
    const alResultado = vi.fn();
    const { unmount } = montarPantalla(<PasoCorreo alCodigo={() => undefined} alResultado={alResultado} />);
    await waitFor(() => expect(navegador?.senales).toHaveLength(1));
    expect(navegador.senales[0]?.aborted).toBe(false);

    unmount();

    expect(navegador.senales[0]?.aborted).toBe(true);
    expect(alResultado).not.toHaveBeenCalled();
  });

  it('si el navegador no la admite no pide nada: queda el botón de siempre', async () => {
    navegador = simularNavegadorConPasskeys({ condicional: false });
    const peticiones = simularApi({});

    montar();
    await new Promise((r) => setTimeout(r, 100));

    expect(navegador.llamadas).toEqual([]);
    expect(peticiones).toEqual([]);
    expect(screen.getByRole('button', { name: 'Entrar con una passkey' })).toBeInTheDocument();
  });

  it('usar el botón de la passkey aborta la condicional sin dejar un error y entra por la ventana', async () => {
    navegador = simularNavegadorConPasskeys({ condicional: true, esperaLaFirma: true });
    simularApi({
      'POST /api/v1/ingreso/passkey/opciones': { desafio: 'd9', opciones: OPCIONES_DE_FIRMA },
      'POST /api/v1/ingreso/passkey/verificar': SESION,
    });
    const alResultado = vi.fn();
    const usuario = montar(alResultado);
    await waitFor(() => expect(navegador?.senales).toHaveLength(1));

    await usuario.click(screen.getByRole('button', { name: 'Entrar con una passkey' }));

    await waitFor(() => expect(alResultado).toHaveBeenCalledTimes(1));
    expect(alResultado.mock.calls[0]?.[0]).toEqual(SESION);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(navegador.llamadas.map((l) => l.mediation)).toEqual(['conditional', undefined]);
  });
});
