import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type TipoVinculoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DrawerTipoVinculo } from './DrawerTipoVinculo';
import { borradorDeTipo, borradorTipoNuevo, codigoValido, cuerpoTipoVinculo, opcionesDeTexto, validarTipoVinculo, type BorradorTipoVinculo } from './modelo';

const PROVEEDOR: TipoVinculoDto = {
  id: 't1',
  codigo: 'PROVEEDOR',
  nombre: 'Proveedor',
  atributosPermitidos: [
    { clave: 'montoEstimado', tipo: 'Numero', obligatorio: false, opciones: null },
    { clave: 'nivelRiesgo', tipo: 'Opcion', obligatorio: false, opciones: ['Bajo', 'Medio', 'Alto'] },
  ],
  exigeCuentaDesignada: true,
  activo: true,
  exigeOtpEnEnlace: true,
};

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

function montar(ui: ReactNode, responder: (cuerpo: unknown) => Response) {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      peticiones.push({ url, metodo: init?.method ?? 'GET', cuerpo });
      return Promise.resolve(responder(cuerpo));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient()}>{ui}</QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => reiniciarCliente());

const valido = (parcial: Partial<BorradorTipoVinculo> = {}): BorradorTipoVinculo => ({ ...borradorTipoNuevo(), codigo: 'CONTRATISTA', nombre: 'Contratista', ...parcial });

describe('validación de un tipo de vínculo (misma regla que TipoVinculo.Crear)', () => {
  it('código: empieza con letra, mayúsculas, números y guion bajo, de 2 a 40', () => {
    expect(codigoValido('PROVEEDOR')).toBe(true);
    expect(codigoValido(' proveedor_2 ')).toBe(true);
    expect(codigoValido('P')).toBe(false);
    expect(codigoValido('2PROVEEDOR')).toBe(false);
    expect(codigoValido('PROVEE-DOR')).toBe(false);
    expect(codigoValido(`A${'B'.repeat(39)}`)).toBe(true);
    expect(codigoValido(`A${'B'.repeat(40)}`)).toBe(false);
    expect(validarTipoVinculo(borradorTipoNuevo())).toEqual([
      { campo: 'codigo', codigo: 'codigo-invalido' },
      { campo: 'nombre', codigo: 'nombre-requerido' },
    ]);
  });

  it('atributos: clave requerida y sin repetir; Opcion y ListaOpciones necesitan opciones', () => {
    const errores = validarTipoVinculo(
      valido({
        atributos: [
          { clave: 'nivelRiesgo', tipo: 'Opcion', obligatorio: false, opciones: 'Bajo\nAlto' },
          { clave: ' nivelRiesgo ', tipo: 'Texto', obligatorio: false, opciones: '' },
          { clave: '', tipo: 'Numero', obligatorio: true, opciones: '' },
          { clave: 'categorias', tipo: 'ListaOpciones', obligatorio: false, opciones: ' \n ' },
        ],
      }),
    );
    expect(errores).toEqual([
      { campo: 'atributo', indice: 1, codigo: 'atributo-duplicado' },
      { campo: 'atributo', indice: 2, codigo: 'clave-requerida' },
      { campo: 'atributo', indice: 3, codigo: 'opciones-requeridas' },
    ]);
    expect(validarTipoVinculo(valido({ atributos: [{ clave: 'cargo', tipo: 'Texto', obligatorio: false, opciones: '' }] }))).toEqual([]);
  });

  it('el cuerpo normaliza código, claves y opciones; las opciones solo viajan en Opcion y ListaOpciones', () => {
    expect(opcionesDeTexto(' Bajo \n\nMedio\nBajo\n Alto')).toEqual(['Bajo', 'Medio', 'Alto']);
    const cuerpo = cuerpoTipoVinculo({
      codigo: ' contratista ',
      nombre: ' Contratista ',
      exigeCuentaDesignada: false,
      exigeOtpEnEnlace: true,
      atributos: [
        { clave: ' nivel ', tipo: 'Opcion', obligatorio: true, opciones: 'A\nB' },
        { clave: 'cargo', tipo: 'Texto', obligatorio: false, opciones: 'sobra' },
      ],
    });
    expect(cuerpo).toEqual({
      codigo: 'CONTRATISTA',
      nombre: 'Contratista',
      exigeCuentaDesignada: false,
      exigeOtpEnEnlace: true,
      atributosPermitidos: [
        { clave: 'nivel', tipo: 'Opcion', obligatorio: true, opciones: ['A', 'B'] },
        { clave: 'cargo', tipo: 'Texto', obligatorio: false, opciones: null },
      ],
    });
    // Ida y vuelta: lo guardado se edita y se reenvía igual.
    expect(cuerpoTipoVinculo(borradorDeTipo(PROVEEDOR))).toEqual({
      codigo: 'PROVEEDOR',
      nombre: 'Proveedor',
      atributosPermitidos: PROVEEDOR.atributosPermitidos,
      exigeCuentaDesignada: true,
      exigeOtpEnEnlace: true,
    });
  });
});

describe('editor de tipos de vínculo', () => {
  it('al crear, dice qué falta, exige opciones para un atributo de opción y manda el cuerpo con POST', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar(<DrawerTipoVinculo tipo={null} claveIdempotencia="idem-1" onCerrar={() => undefined} />, (cuerpo) => json({ ...PROVEEDOR, ...(cuerpo as object), id: 't9', activo: true }, 201));
    const crear = screen.getByRole('button', { name: 'Crear tipo' });
    expect(crear).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent(/El código debe empezar con letra/);

    await usuario.type(screen.getByRole('textbox', { name: /Código/ }), 'contratista');
    expect(screen.getByRole('textbox', { name: /Código/ })).toHaveValue('CONTRATISTA');
    await usuario.type(screen.getByRole('textbox', { name: /Nombre/ }), 'Contratista');

    await usuario.click(screen.getByRole('button', { name: 'Agregar atributo' }));
    const fila = within(screen.getByRole('list', { name: 'Atributos permitidos' })).getByRole('listitem');
    await usuario.type(within(fila).getByRole('textbox', { name: /Clave/ }), 'nivel riesgo');
    expect(within(fila).getByRole('textbox', { name: /Clave/ })).toHaveValue('nivelriesgo');
    await usuario.click(within(fila).getByRole('combobox', { name: /Tipo/ }));
    await usuario.click(await screen.findByRole('option', { name: 'Una opción' }));
    expect(screen.getByRole('status')).toHaveTextContent('Atributo «nivelriesgo»: Escribe al menos una opción.');
    expect(crear).toBeDisabled();

    await usuario.type(within(fila).getByRole('textbox', { name: /Opciones/ }), 'Bajo{Enter}Alto');
    await waitFor(() => expect(crear).toBeEnabled());
    await usuario.click(crear);

    await waitFor(() => expect(peticiones).toHaveLength(1));
    expect(peticiones[0]).toEqual({
      url: '/api/v1/tipos-vinculo',
      metodo: 'POST',
      cuerpo: {
        codigo: 'CONTRATISTA',
        nombre: 'Contratista',
        exigeCuentaDesignada: false,
        exigeOtpEnEnlace: false,
        atributosPermitidos: [{ clave: 'nivelriesgo', tipo: 'Opcion', obligatorio: false, opciones: ['Bajo', 'Alto'] }],
      },
    });
  }, 20_000);

  it('al editar, el código queda fijo, avisa del efecto en los flujos y guarda con PUT', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar(<DrawerTipoVinculo tipo={PROVEEDOR} claveIdempotencia="" onCerrar={() => undefined} />, () => json(PROVEEDOR));
    expect(screen.getByRole('textbox', { name: /Código/ })).toBeDisabled();
    expect(screen.getByText(/revisa los flujos que lo usan en «Se pide a»/)).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Quitar montoEstimado' }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(peticiones).toHaveLength(1));
    expect(peticiones[0]).toMatchObject({
      url: '/api/v1/tipos-vinculo/t1',
      metodo: 'PUT',
      cuerpo: { codigo: 'PROVEEDOR', atributosPermitidos: [{ clave: 'nivelRiesgo', tipo: 'Opcion', obligatorio: false, opciones: ['Bajo', 'Medio', 'Alto'] }] },
    });
  }, 20_000);
});
