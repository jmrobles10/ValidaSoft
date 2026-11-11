import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type ContactoDeEmpresaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { TarjetaContactos } from './TarjetaContactos';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const CONTACTOS: ContactoDeEmpresaDto[] = [
  { nombre: 'Marta Ríos', cargo: 'Tesorera', correo: 'pagos@aceros.co', telefono: '6015551234', rol: 'Pagos' },
  { nombre: 'Luis Pérez', cargo: null, correo: 'comercial@aceros.co', telefono: null, rol: 'Comercial' },
];

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' } });

function montar(contactos: ContactoDeEmpresaDto[] | undefined, estado = 'Activo', responder: (url: string) => Response = () => json({ id: 'v1', contactos: [] })) {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = entrada instanceof Request ? entrada.url : entrada.toString();
      peticiones.push({ url, metodo: init?.method ?? 'GET', cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : null });
      return Promise.resolve(responder(url));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <TarjetaContactos vinculo={{ id: 'v1', contactos, estado }} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.getState().cerrar();
});

describe('contactos del tercero con la empresa', () => {
  it('lista cada contacto con su rol, cargo, correo y teléfono, y sin contactos explica que cada empresa tiene los suyos', () => {
    montar(CONTACTOS);
    const tarjeta = screen.getByRole('region', { name: 'Contactos del tercero' });
    expect(tarjeta).toHaveTextContent('Pagos');
    expect(tarjeta).toHaveTextContent('Marta Ríos · Tesorera');
    expect(tarjeta).toHaveTextContent('pagos@aceros.co · 6015551234');
    expect(tarjeta).toHaveTextContent('Luis Pérez');
    expect(tarjeta).not.toHaveTextContent('Luis Pérez ·');
  });

  it('sin contactos dice que cada empresa tiene los suyos', () => {
    montar(undefined);
    expect(screen.getByText(/Cada empresa tiene los suyos/)).toBeInTheDocument();
  });

  it('solo quien puede escribir vínculos ve «Editar», y un vínculo anulado ya no se edita', () => {
    montar(CONTACTOS);
    expect(screen.queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument();
  });

  it('un vínculo anulado no se edita aunque haya permiso', () => {
    permisos.scopes = new Set([SCOPES.vinculosEscribir]);
    montar(CONTACTOS, 'Anulado');
    expect(screen.queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument();
  });

  it('valida antes de enviar y reemplaza la lista completa con lo que se edita', async () => {
    permisos.scopes = new Set([SCOPES.vinculosEscribir]);
    const usuario = userEvent.setup();
    const peticiones = montar(CONTACTOS);

    await usuario.click(screen.getByRole('button', { name: 'Editar' }));
    const dialogo = screen.getByRole('dialog', { name: 'Contactos del tercero con esta empresa' });
    expect(within(dialogo).getAllByRole('group')).toHaveLength(2);

    // Un contacto nuevo sin nombre ni correo válido no se envía.
    await usuario.click(within(dialogo).getByRole('button', { name: 'Agregar contacto' }));
    const nuevo = within(dialogo).getByRole('group', { name: 'Contacto 3' });
    await usuario.click(within(nuevo).getByRole('textbox', { name: /Correo/ }));
    await usuario.paste('sin-arroba');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar contactos' }));
    expect(dialogo).toHaveTextContent('Escribe el nombre de la persona.');
    expect(dialogo).toHaveTextContent('Escribe un correo válido');
    expect(peticiones.filter((p) => p.metodo === 'PUT')).toHaveLength(0);

    await usuario.click(within(nuevo).getByRole('textbox', { name: /Nombre/ }));
    await usuario.paste(' Carla Ruiz ');
    await usuario.clear(within(nuevo).getByRole('textbox', { name: /Correo/ }));
    await usuario.click(within(nuevo).getByRole('textbox', { name: /Correo/ }));
    await usuario.paste('notificaciones@aceros.co');
    await usuario.click(within(nuevo).getByRole('combobox', { name: /Rol/ }));
    await usuario.click(await screen.findByRole('option', { name: 'Notificaciones' }));
    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar contactos' }));

    await vi.waitFor(() => expect(peticiones.filter((p) => p.metodo === 'PUT')).toHaveLength(1));
    const put = peticiones.find((p) => p.metodo === 'PUT');
    expect(put?.url).toContain('/api/v1/vinculos/v1/contactos');
    expect(put?.cuerpo).toEqual({
      contactos: [
        { nombre: 'Marta Ríos', cargo: 'Tesorera', correo: 'pagos@aceros.co', telefono: '6015551234', rol: 'Pagos' },
        { nombre: 'Luis Pérez', cargo: null, correo: 'comercial@aceros.co', telefono: null, rol: 'Comercial' },
        { nombre: 'Carla Ruiz', cargo: null, correo: 'notificaciones@aceros.co', telefono: null, rol: 'Notificaciones' },
      ],
    });
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(useAvisos.getState().actual?.mensaje).toBe('Guardaste los contactos.');
  });

  it('un error de la API se muestra en el diálogo y no lo cierra', async () => {
    permisos.scopes = new Set([SCOPES.vinculosEscribir]);
    const usuario = userEvent.setup();
    montar(CONTACTOS, 'Activo', () => json({ status: 409, codigo: 'vinculo-anulado', detail: 'Un vínculo anulado ya no se modifica.' }, 409));

    await usuario.click(screen.getByRole('button', { name: 'Editar' }));
    const dialogo = screen.getByRole('dialog');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Guardar contactos' }));
    expect(await within(dialogo).findByRole('alert')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});
