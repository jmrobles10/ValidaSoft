import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type AccesoSolicitadoDto, type ContactosDelTerceroDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../shared/useAvisos';
import { PaginaAccesos } from './PaginaAccesos';

const json = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': estado >= 400 ? 'application/problem+json' : 'application/json' } });

function solicitud(cambios: Partial<AccesoSolicitadoDto> = {}): AccesoSolicitadoDto {
  return {
    id: 'a1',
    correo: 'compras@empresa-a.co',
    estado: 'Pendiente',
    desde: '2026-11-12T15:00:00Z',
    empresas: [{ empresa: 'Empresa A S.A.S.', fecha: '2026-11-12T15:00:00Z' }],
    decididoPor: null,
    decididoEn: null,
    motivo: null,
    ...cambios,
  };
}

const CONTACTOS: ContactosDelTerceroDto = {
  contactos: [{ nombre: 'Gerente', correo: 'gerente@norte.co', rol: 'Principal', verificacion: 'Verificado', medio: 'CorreoDelRut', verificadoEn: '2026-11-01T10:00:00Z', esTuCorreo: true, desplazado: false }],
  historial: [],
  puedesDecidir: true,
  decideLaPlataforma: false,
};

interface Peticion {
  metodo: string;
  url: URL;
  cuerpo: unknown;
}

function montar(
  solicitudes: AccesoSolicitadoDto[],
  alMutar: (p: Peticion) => Response = (p) => json(200, solicitud({ id: p.url.pathname.split('/')[4] ?? 'a1', estado: 'Aprobada' })),
  contactos: ContactosDelTerceroDto = CONTACTOS,
) {
  const mutaciones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost');
      const metodo = init?.method ?? 'GET';
      if (metodo === 'GET') return Promise.resolve(json(200, url.pathname.endsWith('/contactos-del-tercero') ? contactos : solicitudes));
      const peticion: Peticion = { metodo, url, cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null };
      mutaciones.push(peticion);
      return Promise.resolve(alMutar(peticion));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <PaginaAccesos />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return mutaciones;
}

afterEach(() => {
  reiniciarCliente();
  useAvisos.setState({ actual: null });
});

describe('Accesos por aprobar', () => {
  it('sin solicitudes explica cuándo se avisará', async () => {
    montar([]);
    expect(await screen.findByText('No hay accesos por decidir')).toBeInTheDocument();
    expect(screen.getByText(/te avisamos aquí, en tus notificaciones y por correo/)).toBeInTheDocument();
  });

  it('muestra cada correo con la empresa que lo invitó, lo pendiente primero y lo decidido aparte', async () => {
    montar([
      solicitud({ id: 'r', correo: 'viejo@empresa-b.co', estado: 'Rechazada', decididoPor: 'gerente@norte.co', decididoEn: '2026-11-10T10:00:00Z', motivo: 'No lo conozco' }),
      solicitud({ empresas: [{ empresa: 'Empresa A S.A.S.', fecha: '2026-11-12T15:00:00Z' }, { empresa: 'Empresa C', fecha: '2026-11-13T09:00:00Z' }] }),
    ]);

    await screen.findByRole('heading', { name: 'Decididos' });
    const [pendiente, decidida] = screen.getAllByRole('listitem');
    expect(within(pendiente!).getByRole('heading', { name: 'compras@empresa-a.co' })).toBeInTheDocument();
    expect(within(pendiente!).getByText('Invitado por Empresa A S.A.S. y Empresa C')).toBeInTheDocument();
    expect(within(pendiente!).getByText('Por decidir')).toBeInTheDocument();
    expect(within(pendiente!).getByRole('button', { name: 'Aprobar el acceso de compras@empresa-a.co' })).toBeInTheDocument();
    expect(within(pendiente!).getByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Decididos' })).toBeInTheDocument();
    expect(within(decidida!).getByText('Rechazado')).toBeInTheDocument();
    expect(within(decidida!).getByText(/Lo decidió gerente@norte\.co el 10\/11\/2026/)).toBeInTheDocument();
    expect(within(decidida!).getByText(/Motivo: No lo conozco/)).toBeInTheDocument();
    // Un rechazo se puede reconsiderar (aprobarlo de todos modos); ya no se vuelve a rechazar.
    expect(within(decidida!).getByRole('button', { name: 'Aprobar el acceso de viejo@empresa-b.co' })).toHaveTextContent('Aprobarlo de todos modos');
    expect(within(decidida!).queryByRole('button', { name: /Rechazar/ })).not.toBeInTheDocument();
  });

  it('antes de aprobar explica que el correo pasa a ser contacto y lo subido pasa a la bóveda, y recién entonces manda la aprobación', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([solicitud()]);

    await usuario.click(await screen.findByRole('button', { name: 'Aprobar el acceso de compras@empresa-a.co' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Aprobar el acceso de compras@empresa-a.co' });
    expect(within(dialogo).getByText(/pasará a ser contacto verificado de tu empresa: verá tu bóveda y tu información/)).toBeInTheDocument();
    expect(within(dialogo).getByText(/Lo que subió para Empresa A S\.A\.S\. pasará a tu bóveda/)).toBeInTheDocument();
    expect(mutaciones).toHaveLength(0);

    await usuario.click(within(dialogo).getByRole('button', { name: 'Sí, aprobar' }));
    await waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.metodo).toBe('POST');
    expect(mutaciones[0]?.url.pathname).toBe('/public/v1/accesos-solicitados/a1/aprobar');
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Aprobaste el acceso de compras@empresa-a.co.'));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('al rechazar explica que se cortan sus sesiones, deja anotar el motivo y lo manda', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([solicitud()], () => json(200, solicitud({ estado: 'Rechazada' })));

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Rechazar el acceso de compras@empresa-a.co' });
    expect(within(dialogo).getByText(/Sus sesiones se cierran al instante y no vuelve a entrar/)).toBeInTheDocument();
    await usuario.type(within(dialogo).getByLabelText('Motivo (opcional)'), '  No conozco a esa persona ');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Sí, rechazar' }));

    await waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.url.pathname).toBe('/public/v1/accesos-solicitados/a1/rechazar');
    expect(mutaciones[0]?.cuerpo).toEqual({ motivo: 'No conozco a esa persona' });
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Rechazaste el acceso de compras@empresa-a.co.'));
  });

  it('rechazar sin motivo manda el motivo vacío como nulo y cancelar no manda nada', async () => {
    const usuario = userEvent.setup();
    const mutaciones = montar([solicitud()], () => json(200, solicitud({ estado: 'Rechazada' })));

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' }));
    await usuario.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(mutaciones).toHaveLength(0);

    await usuario.click(screen.getByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' }));
    await usuario.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Sí, rechazar' }));
    await waitFor(() => expect(mutaciones).toHaveLength(1));
    expect(mutaciones[0]?.cuerpo).toEqual({ motivo: null });
  });

  it('si el servidor no deja decidir (ya está aprobado), lo dice dentro del diálogo y no lo cierra', async () => {
    const usuario = userEvent.setup();
    montar([solicitud()], () => json(409, { type: 'x', title: 'solicitud-ya-aprobada', status: 409, detail: 'ya', codigo: 'solicitud-ya-aprobada' }));

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar el acceso de compras@empresa-a.co' }));
    const dialogo = await screen.findByRole('dialog');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Sí, rechazar' }));

    expect(await within(dialogo).findByText('Ese correo ya es contacto de tu empresa: no se puede rechazar.')).toBeInTheDocument();
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});

describe('Verificación de los contactos', () => {
  it('muestra cada contacto con su estado y con qué se verificó, y el tuyo marcado', async () => {
    montar([], undefined, {
      ...CONTACTOS,
      contactos: [
        { nombre: 'Creador', correo: 'creador@a.co', rol: 'Principal', verificacion: 'SinVerificar', medio: null, verificadoEn: null, esTuCorreo: false, desplazado: true },
        { nombre: 'Gerente', correo: 'gerente@norte.co', rol: 'Principal', verificacion: 'Verificado', medio: 'CorreoDelRut', verificadoEn: '2026-11-01T10:00:00Z', esTuCorreo: true, desplazado: false },
        { nombre: 'Tesorería', correo: 'tesoreria@norte.co', rol: 'Contacto', verificacion: 'Verificado', medio: 'Plataforma', verificadoEn: '2026-11-02T10:00:00Z', esTuCorreo: false, desplazado: false },
        { nombre: 'Compras', correo: 'compras@norte.co', rol: 'Contacto', verificacion: 'SinVerificar', medio: null, verificadoEn: null, esTuCorreo: false, desplazado: false },
      ],
      historial: [
        { fecha: '2026-11-02T10:00:00Z', tipo: 'verificado', correo: 'tesoreria@norte.co', detalle: 'Plataforma (plataforma:op)' },
        { fecha: '2026-11-03T10:00:00Z', tipo: 'paso_a_aislado', correo: 'creador@a.co', detalle: 'Otro contacto del tercero se verificó y el verificado prevalece.' },
      ],
    });

    const seccion = await screen.findByRole('region', { name: 'Contactos de tu empresa' });
    const contactos = (await within(seccion).findAllByRole('listitem')).filter((li) => li.getAttribute('aria-label') !== null);
    // Tu correo primero, luego verificados, sin verificar y en acceso aislado.
    expect(contactos.map((li) => li.getAttribute('aria-label'))).toEqual(['gerente@norte.co', 'tesoreria@norte.co', 'compras@norte.co', 'creador@a.co']);
    expect(within(contactos[0]!).getByText(/gerente@norte\.co · Tu correo/)).toBeInTheDocument();
    expect(within(contactos[0]!).getByText('Verificado')).toBeInTheDocument();
    expect(within(contactos[0]!).getByText(/Correo del RUT · 01\/11\/2026/)).toBeInTheDocument();
    expect(within(contactos[1]!).getByText(/Verificado por la plataforma/)).toBeInTheDocument();
    expect(within(contactos[2]!).getByText('Sin verificar')).toBeInTheDocument();
    expect(within(contactos[3]!).getByText('En acceso aislado')).toBeInTheDocument();
    expect(within(seccion).getByText(/03\/11\/2026 · creador@a\.co pasó a acceso aislado: Otro contacto del tercero se verificó/)).toBeInTheDocument();
    // Un contacto verificado no ve el aviso de que no decide.
    expect(within(seccion).queryByText(/aún no está verificado/)).not.toBeInTheDocument();
  });

  it('a un contacto sin verificar le dice que ve las solicitudes pero no las decide, y que decide la plataforma si nadie está verificado', async () => {
    montar([], undefined, {
      contactos: [{ nombre: 'Creador', correo: 'creador@a.co', rol: 'Principal', verificacion: 'SinVerificar', medio: null, verificadoEn: null, esTuCorreo: true, desplazado: false }],
      historial: [],
      puedesDecidir: false,
      decideLaPlataforma: true,
    });

    const aviso = await screen.findByText(/Tu correo aún no está verificado: ves las solicitudes, pero solo un contacto verificado las aprueba o rechaza\./);
    expect(aviso).toHaveTextContent('Mientras ningún contacto esté verificado, la plataforma decide los accesos de otros correos.');
  });

  it('si tu propio correo quedó en acceso aislado lo advierte', async () => {
    montar([], undefined, {
      contactos: [
        { nombre: 'Creador', correo: 'creador@a.co', rol: 'Principal', verificacion: 'SinVerificar', medio: null, verificadoEn: null, esTuCorreo: true, desplazado: true },
        { nombre: 'Gerente', correo: 'gerente@norte.co', rol: 'Principal', verificacion: 'Verificado', medio: 'CorreoDelRut', verificadoEn: '2026-11-01T10:00:00Z', esTuCorreo: false, desplazado: false },
      ],
      historial: [],
      puedesDecidir: false,
      decideLaPlataforma: false,
    });

    expect(await screen.findByText(/Tu correo quedó en acceso aislado porque otro contacto de tu empresa se verificó/)).toBeInTheDocument();
  });

  it('una solicitud que esta sesión no puede decidir no ofrece aprobar ni rechazar y dice quién la decide', async () => {
    montar([
      solicitud({ id: 'p1', correo: 'compras@empresa-a.co', puedesDecidir: false, decideLaPlataforma: true }),
      solicitud({ id: 'p2', correo: 'otro@empresa-b.co', puedesDecidir: false, decideLaPlataforma: false }),
    ]);

    await screen.findByText('Solo un contacto verificado puede decidir esta solicitud.');
    const tarjetas = screen.getAllByRole('listitem');
    const [plataforma, verificados] = tarjetas.filter((li) => li.getAttribute('aria-label')?.includes('@empresa-'));
    expect(within(plataforma!).queryByRole('button')).not.toBeInTheDocument();
    expect(within(plataforma!).getByText('La plataforma revisa esta solicitud porque ningún contacto de tu empresa está verificado.')).toBeInTheDocument();
    expect(within(verificados!).queryByRole('button')).not.toBeInTheDocument();
    expect(within(verificados!).getByText('Solo un contacto verificado puede decidir esta solicitud.')).toBeInTheDocument();
  });

  it('un contacto desplazado dice por qué y lo decidido por la plataforma o por la verificación lo dice con claridad', async () => {
    montar([
      solicitud({ id: 'd', correo: 'creador@a.co', contactoDesplazado: true }),
      solicitud({ id: 'x', correo: 'plataforma@empresa-a.co', estado: 'Aprobada', decididoPor: 'plataforma:op', decididoVia: 'Plataforma', decididoEn: '2026-11-10T10:00:00Z' }),
      solicitud({ id: 'y', correo: 'verificado@empresa-a.co', estado: 'Aprobada', decididoPor: 'verificacion', decididoVia: 'Verificacion', decididoEn: '2026-11-11T10:00:00Z' }),
    ]);

    expect(await screen.findByText('Estaba como contacto sin verificar y otro contacto de tu empresa se verificó: el verificado prevalece.')).toBeInTheDocument();
    expect(screen.getByText(/Lo decidió la plataforma el 10\/11\/2026/)).toBeInTheDocument();
    expect(screen.getByText(/Se aprobó sola el 11\/11\/2026: el correo quedó verificado/)).toBeInTheDocument();
  });
});
