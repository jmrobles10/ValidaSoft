import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type CampoFormularioDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { FormularioRequisito } from './FormularioRequisito';

const CAMPOS: CampoFormularioDto[] = [
  { clave: 'razon', etiqueta: 'Razón social', tipo: 'Texto', obligatorio: true, ayuda: 'Como aparece en el RUT.' },
  { clave: 'monto', etiqueta: 'Monto', tipo: 'Numero', obligatorio: false },
  { clave: 'pep', etiqueta: '¿Es PEP?', tipo: 'SiNo', obligatorio: true },
];

const json = (estado: number, cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/problem+json' } });

function montar(respuesta: () => Response) {
  const cuerpos: unknown[] = [];
  configurarCliente({
    fetch: vi.fn((_url: RequestInfo | URL, init?: RequestInit) => {
      cuerpos.push(typeof init?.body === 'string' ? JSON.parse(init.body) : null);
      return Promise.resolve(respuesta());
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter>
          <FormularioRequisito tipo="Declaración de origen de fondos" campos={CAMPOS} destino={{ tipoDocumentoTenantId: 't1', vinculoId: 'v1' }} volver="/expediente/e1" />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return cuerpos;
}

afterEach(() => reiniciarCliente());

describe('formulario de un tipo de documento', () => {
  it('pinta los campos con su ayuda y deja el envío deshabilitado, diciendo qué falta, hasta completarlo', async () => {
    const usuario = userEvent.setup();
    montar(() => json(500, {}));
    const enviar = screen.getByRole('button', { name: 'Enviar datos' });

    expect(screen.getByText('Como aparece en el RUT.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /Razón social/ })).toBeRequired();
    expect(enviar).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Para enviar, llena: Razón social, ¿Es PEP?.');

    // El error de un campo aparece cuando ya se tocó.
    expect(screen.queryByText('Llena este dato.')).not.toBeInTheDocument();
    await usuario.click(screen.getByRole('textbox', { name: /Razón social/ }));
    await usuario.tab();
    expect(screen.getByText('Llena este dato.')).toBeInTheDocument();

    await usuario.type(screen.getByRole('textbox', { name: /Razón social/ }), 'Aceros del Norte');
    expect(screen.getByRole('status')).toHaveTextContent('Para enviar, llena: ¿Es PEP?.');
    await usuario.click(screen.getByRole('radio', { name: 'No' }));

    expect(enviar).toBeEnabled();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('un 422 de la API marca el campo que señala y vuelve a deshabilitar el envío', async () => {
    const usuario = userEvent.setup();
    const cuerpos = montar(() => json(422, { status: 422, codigo: 'valor-invalido', detail: '«Monto» debe ser un número (con punto decimal).', campo: 'monto' }));

    await usuario.type(screen.getByRole('textbox', { name: /Razón social/ }), 'Aceros del Norte');
    await usuario.type(screen.getByRole('textbox', { name: /Monto/ }), '12,5');
    await usuario.click(screen.getByRole('radio', { name: 'Sí' }));
    await usuario.click(screen.getByRole('button', { name: 'Enviar datos' }));

    expect(await screen.findByText('«Monto» debe ser un número (con punto decimal).')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /Monto/ })).toHaveAttribute('aria-invalid', 'true');
    expect(screen.getByRole('button', { name: 'Enviar datos' })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Para enviar, corrige: Monto.');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(cuerpos[0]).toEqual({ vinculoId: 'v1', tipoDocumentoTenantId: 't1', datos: { razon: 'Aceros del Norte', monto: '12.5', pep: true } });

    // Al cambiar el campo se quita la marca de la API.
    await usuario.type(screen.getByRole('textbox', { name: /Monto/ }), '0');
    expect(screen.getByRole('button', { name: 'Enviar datos' })).toBeEnabled();
  });
});
