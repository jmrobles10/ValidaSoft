import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type PasoDeIngresoDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { montarPantalla } from '../pruebas/montar';
import { OPCIONES_DE_FIRMA, simularNavegadorConPasskeys, type NavegadorSimulado } from '../pruebas/navegadorSimulado';
import { ProblemaSimulado, simularApi } from '../pruebas/simularApi';
import { PasoVerificarPasskey } from './PasoPasskey';

let navegador: NavegadorSimulado | null = null;

afterEach(() => {
  navegador?.restaurar();
  navegador = null;
  reiniciarCliente();
});

function montar(alResultado: (paso: PasoDeIngresoDto) => void = () => undefined) {
  navegador = simularNavegadorConPasskeys();
  // La passkey del navegador no está a la mano: la petición se rechaza, como si la persona no la tuviera.
  navegador.get.mockRejectedValue(new DOMException('cancelada', 'NotAllowedError'));
  montarPantalla(<PasoVerificarPasskey desafio="d1" opciones={OPCIONES_DE_FIRMA} alResultado={alResultado} alVolver={() => undefined} />);
  return userEvent.setup();
}

describe('recuperar el acceso sin la passkey (PI-15)', () => {
  it('ofrece usar un código de recuperación y volver a la passkey', async () => {
    const usuario = montar();

    await usuario.click(await screen.findByRole('button', { name: 'No tengo mi passkey' }));
    expect(screen.getByRole('heading', { name: 'Usa un código de recuperación' })).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Volver a mi passkey' }));
    expect(screen.getByRole('button', { name: 'Usar mi passkey' })).toBeInTheDocument();
  });

  it('escribe el código con su formato, no deja enviarlo incompleto y lo envía con el desafío', async () => {
    const siguiente: PasoDeIngresoDto = { estado: 'RegistrarPasskey', desafio: 'd1', opciones: OPCIONES_DE_FIRMA };
    const peticiones = simularApi({ 'POST /api/v1/ingreso/recuperacion': siguiente });
    const alResultado = vi.fn();
    const usuario = montar(alResultado);
    await usuario.click(await screen.findByRole('button', { name: 'No tengo mi passkey' }));
    const campo = screen.getByLabelText('Código de recuperación');
    const usar = screen.getByRole('button', { name: 'Usar el código' });

    await usuario.type(campo, 'ab3cd');
    expect(usar).toBeDisabled();
    await usuario.type(campo, 'efgh2zzz');

    expect(campo).toHaveValue('AB3CD-EFGH2');
    expect(usar).toBeEnabled();
    await usuario.click(usar);

    await waitFor(() => expect(alResultado).toHaveBeenCalledTimes(1));
    expect(alResultado.mock.calls[0]?.[0]).toEqual(siguiente);
    expect(peticiones.find((p) => p.ruta === '/api/v1/ingreso/recuperacion')?.cuerpo).toEqual({ desafio: 'd1', codigo: 'AB3CD-EFGH2' });
  });

  it('un código malo muestra lo que dice la API (con los intentos que quedan) y deja intentar otro', async () => {
    simularApi({
      'POST /api/v1/ingreso/recuperacion': new ProblemaSimulado(422, 'codigo-de-recuperacion-invalido', 'Ese código de recuperación no es válido o ya se usó. Te quedan 2 intentos.'),
    });
    const usuario = montar();
    await usuario.click(await screen.findByRole('button', { name: 'No tengo mi passkey' }));

    await usuario.type(screen.getByLabelText('Código de recuperación'), 'AB3CDEFGH2');
    await usuario.click(screen.getByRole('button', { name: 'Usar el código' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Ese código de recuperación no es válido o ya se usó. Te quedan 2 intentos.');
    expect(screen.getByRole('button', { name: 'Usar el código' })).toBeEnabled();
  });

  it('con los intentos agotados pide empezar de nuevo', async () => {
    simularApi({ 'POST /api/v1/ingreso/recuperacion': new ProblemaSimulado(429, 'recuperacion-bloqueada', 'agotados') });
    const usuario = montar();
    await usuario.click(await screen.findByRole('button', { name: 'No tengo mi passkey' }));

    await usuario.type(screen.getByLabelText('Código de recuperación'), 'AB3CDEFGH2');
    await usuario.click(screen.getByRole('button', { name: 'Usar el código' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Se agotaron los intentos con códigos de recuperación. Empieza de nuevo.');
  });
});
