import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { escribir, estadoFicha, json, montarFicha } from './pruebas/montar';

afterEach(() => reiniciarCliente());

const faltan = (...campos: string[]) => ({ completa: false, faltantes: campos.map((c) => ({ campo: c, regla: null, mensaje: `Falta «${c}».` })), reglaVersion: 1 });

describe('Mi información: barra de acciones (FM-46)', () => {
  it('«Guardar y continuar» se habilita solo cuando la API dice que la sección quedaría completa', async () => {
    const usuario = userEvent.setup();
    const evaluados: unknown[] = [];
    montarFicha('/ficha/general', {
      estado: estadoFicha(),
      // La API evalúa lo escrito: vacía le faltan dos datos; con el teléfono le falta uno; con ambos, completa.
      alEvaluar: (peticion) => {
        evaluados.push(peticion.cuerpo);
        const datos = (peticion.cuerpo as { datos: Record<string, unknown> }).datos;
        if (datos.telefono && datos.direccion) return json(200, { completa: true, faltantes: [], reglaVersion: 1 });
        return json(200, datos.telefono ? faltan('direccion') : faltan('telefono', 'direccion'));
      },
    });

    const continuar = await screen.findByRole('button', { name: 'Guardar y continuar' });
    expect(await screen.findByText('Faltan 2 datos para continuar')).toBeInTheDocument();
    expect(continuar).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();

    await escribir(usuario, screen.getByRole('textbox', { name: 'Teléfono' }), '601 555 1234');
    expect(await screen.findByText('Falta 1 dato para continuar', {}, { timeout: 3000 })).toBeInTheDocument();
    expect(continuar).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeEnabled();

    await escribir(usuario, screen.getByRole('textbox', { name: 'Dirección' }), 'Calle 45 # 12-08');
    expect(await screen.findByText('Lista: puedes continuar', {}, { timeout: 3000 })).toBeInTheDocument();
    expect(continuar).toBeEnabled();
    // Una consulta por pausa al escribir, no una por tecla.
    await vi.waitFor(() => expect(evaluados.length).toBeLessThanOrEqual(3));
  });

  it('un dato mal escrito lo dice la barra y no deja continuar', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/general', {
      estado: estadoFicha(),
      alEvaluar: (peticion) =>
        (peticion.cuerpo as { datos: Record<string, unknown> }).datos.correo
          ? json(422, { status: 422, codigo: 'valor-invalido', detail: '«compras@» no es un correo válido.', campo: 'correo' })
          : json(200, faltan('correo')),
    });

    await escribir(usuario, await screen.findByRole('textbox', { name: 'Correo electrónico' }), 'compras@');
    expect(await screen.findByText('Revisa un dato: «compras@» no es un correo válido.', {}, { timeout: 3000 })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar y continuar' })).toBeDisabled();
  });
});
