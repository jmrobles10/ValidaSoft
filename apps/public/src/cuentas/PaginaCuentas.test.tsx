import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type CuentaBancariaPublicaDto, type VinculoPublicoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaCuentas } from './PaginaCuentas';

const json = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': estado >= 400 ? 'application/problem+json' : 'application/json' } });

function vinculo(id: string, nombre: string, cambios: Partial<VinculoPublicoDto> = {}): VinculoPublicoDto {
  return {
    id,
    empresa: { id: `emp-${id}`, nombre, logoUrl: null },
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    estado: 'Activo',
    invitacionAceptada: true,
    invitacionAceptadaEn: '2026-10-01T10:00:00Z',
    ...cambios,
  };
}

function cuenta(id: string, cambios: Partial<CuentaBancariaPublicaDto> = {}): CuentaBancariaPublicaDto {
  return {
    id,
    banco: 'Bancolombia',
    tipoCuenta: 'Ahorros',
    numeroEnmascarado: '****5678',
    titular: 'Aceros del Norte S.A.S.',
    identificacionTitular: '900123456-7',
    moneda: 'COP',
    estado: 'Activa',
    registradaEn: '2026-10-02T10:00:00Z',
    designadaEn: [],
    ...cambios,
  };
}

const BANCOS = [
  { codigo: '1007', nombre: 'Bancolombia', activo: true },
  { codigo: '1051', nombre: 'Banco Davivienda', activo: true },
];

const VINCULOS = [vinculo('v1', 'Constructora Norte'), vinculo('v2', 'Ferretería Sur'), vinculo('v3', 'Minera Andina', { invitacionAceptada: false })];

interface Peticion {
  metodo: string;
  url: string;
  cuerpo: unknown;
}

/** Responde la lista de cuentas y los vínculos; las mutaciones, con `alMutar`. */
function montar(cuentas: CuentaBancariaPublicaDto[], alMutar: (p: Peticion) => Response = () => json(200, {})) {
  const mutaciones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((url: RequestInfo | URL, init?: RequestInit) => {
      const direccion = url instanceof Request ? url.url : url.toString();
      const metodo = init?.method ?? 'GET';
      if (metodo === 'GET' && direccion.includes('/catalogos/bancos')) return Promise.resolve(json(200, { datos: BANCOS, siguienteCursor: null }));
      if (metodo === 'GET') return Promise.resolve(json(200, direccion.includes('/cuentas-bancarias') ? cuentas : VINCULOS));
      const peticion: Peticion = { metodo, url: direccion, cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : null };
      mutaciones.push(peticion);
      return Promise.resolve(alMutar(peticion));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter>
          <PaginaCuentas />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return mutaciones;
}

/** Pega el texto en vez de teclearlo letra por letra: la suite completa corre en paralelo y teclear es lento. */
async function escribir(usuario: ReturnType<typeof userEvent.setup>, campo: HTMLElement, texto: string) {
  await usuario.click(campo);
  await usuario.paste(texto);
}

/** El banco se elige de la lista del catálogo (autocompletar con el código de compensación detrás). */
async function elegirBanco(usuario: ReturnType<typeof userEvent.setup>, dialogo: HTMLElement, nombre: string) {
  await usuario.click(within(dialogo).getByRole('combobox', { name: /Banco/ }));
  await usuario.click(await screen.findByRole('option', { name: nombre }));
}

afterEach(() => reiniciarCliente());

describe('mis cuentas bancarias', () => {
  it('muestra cada cuenta enmascarada con el estado de su designación en cada empresa', async () => {
    montar([
      cuenta('c1', {
        designadaEn: [
          { vinculoId: 'v1', empresaId: 'emp-v1', empresa: 'Constructora Norte', estado: 'Aprobada', motivoRechazo: null },
          { vinculoId: 'v2', empresaId: 'emp-v2', empresa: 'Ferretería Sur', estado: 'Rechazada', motivoRechazo: 'El titular no coincide con el RUT.' },
        ],
      }),
      cuenta('c2', {
        banco: 'Davivienda',
        tipoCuenta: 'Corriente',
        numeroEnmascarado: '****9012',
        designadaEn: [{ vinculoId: 'v2', empresaId: 'emp-v2', empresa: 'Ferretería Sur', estado: 'Propuesta', motivoRechazo: null }],
      }),
      cuenta('c3', { banco: 'BBVA', numeroEnmascarado: '****3333', estado: 'Retirada' }),
    ]);

    const bancolombia = await screen.findByRole('article', { name: 'Bancolombia' });
    expect(bancolombia).toHaveTextContent('Ahorros · ****5678 · COP');
    expect(bancolombia).toHaveTextContent('Titular: Aceros del Norte S.A.S. · 900123456-7');
    expect(bancolombia).toHaveTextContent('Designada para Constructora Norte');
    expect(bancolombia).toHaveTextContent('Ferretería Sur la rechazó: El titular no coincide con el RUT.');
    expect(screen.getByRole('article', { name: 'Davivienda' })).toHaveTextContent('Pendiente de aprobación de Ferretería Sur');

    const retirada = screen.getByRole('article', { name: 'BBVA' });
    expect(retirada).toHaveTextContent('Retirada');
    expect(within(retirada).queryByRole('button', { name: /Retirar/ })).not.toBeInTheDocument();

    // Por empresa: solo las de invitación aceptada, con lo que tienen hoy.
    const porEmpresa = await screen.findByRole('list', { name: 'En qué cuenta te paga cada empresa' });
    expect(porEmpresa).toHaveTextContent('Te paga en Bancolombia ****5678.');
    expect(porEmpresa).toHaveTextContent('Davivienda ****9012 pendiente de su aprobación.');
    expect(porEmpresa).not.toHaveTextContent('Minera Andina');
  });

  it('valida antes de enviar y registra la cuenta con el cuerpo que espera la API', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([], () => json(201, cuenta('nueva')));

    await usuario.click(await screen.findByRole('button', { name: 'Agregar cuenta' }));
    const dialogo = screen.getByRole('dialog', { name: 'Agregar cuenta bancaria' });
    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar cuenta' }));
    expect(dialogo).toHaveTextContent('Elige el banco de la cuenta.');
    expect(dialogo).toHaveTextContent('Elige si la cuenta es de ahorros o corriente.');
    expect(mutaciones).toHaveLength(0);

    await elegirBanco(usuario, dialogo, 'Bancolombia');
    await usuario.click(within(dialogo).getByRole('radio', { name: 'Corriente' }));
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: 'Nombre' }), ' Marta Ríos ');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: 'Correo' }), ' PAGOS@aceros.co ');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Número de cuenta/ }), '123-456 78');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Titular de la cuenta/ }), 'Aceros del Norte S.A.S.');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Identificación del titular/ }), '900123456-7');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar cuenta' }));

    await vi.waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.url).toContain('/public/v1/boveda/cuentas-bancarias');
    expect(mutaciones[0]?.cuerpo).toEqual({
      banco: null,
      bancoCodigo: '1007',
      bancoSwift: null,
      pais: null,
      contactoPagos: { nombre: 'Marta Ríos', correo: 'PAGOS@aceros.co', telefono: null },
      tipoCuenta: 'Corriente',
      numero: '123-456 78',
      titular: 'Aceros del Norte S.A.S.',
      identificacionTitular: '900123456-7',
      moneda: 'COP',
    });
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('un banco que no está en la lista se escribe con su país y su SWIFT, sin código de compensación', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([], () => json(201, cuenta('nueva')));

    await usuario.click(await screen.findByRole('button', { name: 'Agregar cuenta' }));
    const dialogo = screen.getByRole('dialog', { name: 'Agregar cuenta bancaria' });
    await usuario.click(within(dialogo).getByRole('button', { name: 'Mi banco no está en la lista' }));
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Nombre del banco/ }), 'Chase');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Código SWIFT/ }), 'chas us33');
    await usuario.click(within(dialogo).getByRole('radio', { name: 'Corriente' }));
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Número de cuenta/ }), '123456789');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Titular de la cuenta/ }), 'Ana Pérez');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Identificación del titular/ }), '1020304050');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar cuenta' }));

    await vi.waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.cuerpo).toMatchObject({ banco: 'Chase', bancoCodigo: null, bancoSwift: 'CHASUS33', pais: 'CO', contactoPagos: null });
  });

  it('un error de validación de la API se muestra en su campo', async () => {
    const usuario = userEvent.setup();
    montar([], () => json(422, { status: 422, codigo: 'moneda-invalida', detail: 'La moneda es un código ISO de tres letras (COP, USD…).' }));

    await usuario.click(await screen.findByRole('button', { name: 'Agregar cuenta' }));
    const dialogo = screen.getByRole('dialog');
    await elegirBanco(usuario, dialogo, 'Bancolombia');
    await usuario.click(within(dialogo).getByRole('radio', { name: 'Ahorros' }));
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Número de cuenta/ }), '12345678');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Titular de la cuenta/ }), 'Ana Pérez');
    await escribir(usuario, within(dialogo).getByRole('textbox', { name: /Identificación del titular/ }), '1020304050');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar cuenta' }));

    expect(await within(dialogo).findByText('La moneda es un código de tres letras, por ejemplo COP o USD.')).toBeInTheDocument();
    expect(within(dialogo).getByRole('textbox', { name: /Moneda/ })).toHaveAttribute('aria-invalid', 'true');
    expect(within(dialogo).queryByRole('alert')).not.toBeInTheDocument();
  });

  it('al designar sin certificación bancaria explica que primero debe subirla y lleva a hacerlo', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([cuenta('c1')], () =>
      json(422, { status: 422, codigo: 'certificacion-requerida', detail: 'Sube tu certificación bancaria antes de designar una cuenta.' }),
    );

    await usuario.click(await screen.findByRole('button', { name: 'Elegir cuenta: Constructora Norte' }));
    const dialogo = screen.getByRole('dialog', { name: 'Cuenta para Constructora Norte' });
    // La única cuenta activa ya viene elegida.
    expect(within(dialogo).getByRole('radio', { name: /Bancolombia · Ahorros \*\*\*\*5678/ })).toBeChecked();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Designar cuenta' }));

    const alerta = await within(dialogo).findByRole('alert');
    expect(alerta).toHaveTextContent('Para designar una cuenta primero sube tu certificación bancaria a tu bóveda.');
    const enlace = within(alerta).getByRole('link', { name: 'Subir certificación bancaria' });
    expect(enlace.getAttribute('href')).toContain('/boveda/subir?tipo=CERTIFICACION_BANCARIA');
    expect(enlace.getAttribute('href')).toContain('volver=%2Fcuentas');
    expect(mutaciones[0]?.url).toContain('/public/v1/vinculos/v1/cuenta-designada');
    expect(mutaciones[0]?.cuerpo).toEqual({ cuentaId: 'c1' });
  });

  it('no deja retirar una cuenta designada: pide elegir otra primero', async () => {
    const usuario = userEvent.setup();
    montar(
      [cuenta('c1', { designadaEn: [{ vinculoId: 'v1', empresaId: 'emp-v1', empresa: 'Constructora Norte', estado: 'Aprobada', motivoRechazo: null }] })],
      () => json(409, { status: 409, codigo: 'cuenta-en-uso', detail: 'Esa cuenta está designada con una empresa: primero designa otra.' }),
    );

    await usuario.click(await screen.findByRole('button', { name: 'Retirar: Bancolombia ****5678' }));
    const dialogo = screen.getByRole('dialog', { name: 'Retirar la cuenta Bancolombia ****5678' });
    await usuario.click(within(dialogo).getByRole('button', { name: 'Retirar cuenta' }));
    expect(await within(dialogo).findByRole('alert')).toHaveTextContent('Primero elige otra cuenta para esa empresa y luego retírala.');
  });
});
