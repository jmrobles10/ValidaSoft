import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { documento, escribir, estadoFicha, guardada, json, montarFicha, seccion } from './pruebas/montar';

afterEach(() => reiniciarCliente());

/** General ya guardada (con lo que fija la API: tipo, número y DV) y sin teléfono. */
const GENERAL = seccion('FICHA_GENERAL', {
  version: 1,
  versiones: 1,
  datos: { razon_social: 'Aceros del Norte S.A.S.', tipo_persona: 'Juridica', tipo_identificacion: 'NIT', numero: '900123456', dv: '8', correo: 'compras@aceros.co', pais: 'CO' },
  completitud: { completa: false, faltantes: [], reglaVersion: 1 },
});

const ubicacion = () => screen.getByTestId('ubicacion').textContent;

describe('Mi información: Información general', () => {
  it('guardar incompleta muestra los faltantes exactos del servidor, los marca en su campo y no avanza', async () => {
    const usuario = userEvent.setup();
    const faltantes = [
      { campo: 'telefono', regla: null, mensaje: 'Falta «Teléfono».' },
      { campo: 'direccion', regla: null, mensaje: 'Falta «Dirección».' },
    ];
    const guardados = montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: GENERAL },
      alGuardar: () => json(200, guardada('FICHA_GENERAL', faltantes, 'FICHA_GENERAL', GENERAL.datos ?? {})),
    });

    // La identificación la fija la API: se ve, pero no se edita.
    const identificacion = await screen.findByRole('textbox', { name: 'Identificación' });
    expect(identificacion).toHaveValue('NIT 900123456-8');
    expect(identificacion).toHaveAttribute('readonly');
    await usuario.click(screen.getByRole('button', { name: 'Guardar y continuar' }));

    const lista = await screen.findByRole('alert', { name: 'Para completar esta sección faltan 2 datos' });
    expect(lista).toHaveTextContent('Falta «Teléfono».');
    expect(lista).toHaveTextContent('Falta «Dirección».');
    expect(screen.getByRole('textbox', { name: 'Teléfono' })).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('textbox', { name: 'Teléfono' })).toHaveAccessibleDescription('Falta «Teléfono».');
    expect(ubicacion()).toBe('/ficha/general');

    // Lo que no edita el tercero no viaja; lo demás, tal cual.
    expect(guardados).toHaveLength(1);
    expect(guardados[0]?.ruta).toBe('/public/v1/ficha/FICHA_GENERAL');
    expect(guardados[0]?.cuerpo).toEqual({ datos: { razon_social: 'Aceros del Norte S.A.S.', correo: 'compras@aceros.co', pais: 'CO' }, soportes: [] });
  });

  it('completa, «Guardar y continuar» lleva a la siguiente sección que dice el servidor', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: GENERAL },
      alGuardar: () => json(200, guardada('FICHA_GENERAL', [], 'FICHA_TRIBUTARIA')),
    });

    await escribir(usuario, await screen.findByRole('textbox', { name: 'Teléfono' }), '601 555 1234');
    await escribir(usuario, screen.getByRole('textbox', { name: 'Dirección' }), 'Calle 45 # 12-08');
    await usuario.click(screen.getByRole('button', { name: 'Guardar y continuar' }));

    await vi.waitFor(() => expect(ubicacion()).toBe('/ficha/tributaria'));
    expect(guardados[0]?.cuerpo).toMatchObject({ datos: { telefono: '601 555 1234', direccion: 'Calle 45 # 12-08' } });
  });

  it('«Guardar» se habilita con cambios, deja el borrador y se queda aunque esté completa', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/general', { estado: estadoFicha(), secciones: { FICHA_GENERAL: GENERAL }, alGuardar: () => json(200, guardada('FICHA_GENERAL', [], 'FICHA_TRIBUTARIA')) });
    const guardar = await screen.findByRole('button', { name: 'Guardar' });
    // Sin cambios no hay nada que guardar.
    expect(guardar).toBeDisabled();
    await escribir(usuario, screen.getByRole('textbox', { name: 'Teléfono' }), '601 555 9999');
    expect(screen.getByText('Tienes cambios sin guardar')).toBeInTheDocument();
    await usuario.click(guardar);
    await vi.waitFor(() => expect(screen.queryByText('Tienes cambios sin guardar')).not.toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();
    expect(ubicacion()).toBe('/ficha/general');
  });

  it('un 422 de formato se muestra en su campo', async () => {
    const usuario = userEvent.setup();
    montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: GENERAL },
      alGuardar: () =>
        json(422, {
          status: 422,
          codigo: 'valor-invalido',
          detail: '«compras@» no es un correo válido.',
          campo: 'correo',
          errores: [
            { campo: 'correo', codigo: 'valor-invalido', mensaje: '«compras@» no es un correo válido.' },
            { campo: 'telefono', codigo: 'valor-invalido', mensaje: 'El teléfono solo admite números.' },
          ],
        }),
    });

    await usuario.click(await screen.findByRole('button', { name: 'Guardar y continuar' }));
    const correo = screen.getByRole('textbox', { name: 'Correo electrónico' });
    await vi.waitFor(() => expect(correo).toHaveAttribute('aria-invalid', 'true'));
    expect(correo).toHaveAccessibleDescription('«compras@» no es un correo válido.');
    // Todos los errores del 422 quedan marcados, no solo el primero.
    expect(screen.getByRole('textbox', { name: 'Teléfono' })).toHaveAccessibleDescription('El teléfono solo admite números.');
    expect(screen.getByRole('alert')).toHaveTextContent('Revisa el dato marcado: «compras@» no es un correo válido.');
    expect(ubicacion()).toBe('/ficha/general');
  });

  it('el soporte se elige entre los documentos de la bóveda que respaldan la sección y su falta se muestra junto al selector', async () => {
    const usuario = userEvent.setup();
    const guardados = montarFicha('/ficha/general', {
      estado: estadoFicha(),
      secciones: { FICHA_GENERAL: GENERAL },
      documentos: [
        documento('d-rut', 'RUT', 'RUT'),
        documento('d-banco', 'CERTIFICACION_BANCARIA', 'Certificación bancaria'),
        documento('d-poliza', 'RUT', 'RUT de la empresa', { ambito: 'PorVinculo', vinculoId: 'v1' }),
      ],
      alGuardar: () => json(200, guardada('FICHA_GENERAL', [{ campo: null, regla: 'soporte-del-cambio', mensaje: 'Falta el soporte del cambio' }], 'FICHA_GENERAL')),
    });

    const selector = await screen.findByRole('group', { name: 'Documento que respalda esta información' });
    const rut = await within(selector).findByRole('checkbox', { name: 'RUT · versión 2 · cargada el 01/10/2026' });
    expect(within(selector).getAllByRole('checkbox')).toHaveLength(1);

    // Un cambio sin soporte: el servidor responde el faltante y se muestra junto al selector.
    await escribir(usuario, screen.getByRole('textbox', { name: 'Teléfono' }), '601 555 9999');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    expect(await within(selector).findByText('Falta el soporte del cambio')).toBeInTheDocument();

    await usuario.click(rut);
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));
    await vi.waitFor(() => expect(guardados).toHaveLength(2));
    expect(guardados[1]?.cuerpo).toMatchObject({ soportes: [{ documentoId: 'd-rut', version: 2 }] });

    const subir = screen.getByRole('link', { name: 'Subir un soporte' });
    expect(subir.getAttribute('href')).toContain('/boveda/subir?tipo=SOPORTE_FICHA');
    expect(subir.getAttribute('href')).toContain('volver=%2Fficha%2Fgeneral');
  });
});
