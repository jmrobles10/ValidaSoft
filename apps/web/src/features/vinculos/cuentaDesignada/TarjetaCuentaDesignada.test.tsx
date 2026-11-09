import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type CuentaDesignadaDelVinculoDto, type CuentaDesignadaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { TarjetaCuentaDesignada } from './TarjetaCuentaDesignada';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const CUENTA: CuentaDesignadaDto = {
  cuentaId: 'c1',
  banco: 'Bancolombia',
  tipoCuenta: 'Ahorros',
  numeroEnmascarado: '•••• 4321',
  numero: '00123454321',
  titular: 'Ferretería El Tornillo S.A.S.',
  identificacionTitular: '900123456',
  moneda: 'COP',
  estado: 'Propuesta',
  propuestaEn: '2026-11-08T15:00:00Z',
  aprobaciones: [{ usuarioId: 'u-carlos', nombre: 'Carlos Cumplimiento', fecha: '2026-11-08T16:00:00Z' }],
  aprobadaEn: null,
  motivoRechazo: null,
};

const CON_PROPUESTA: CuentaDesignadaDelVinculoDto = { actual: null, propuesta: CUENTA, rechazada: null, aprobadoresRequeridos: 2, puedesAprobar: true };

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

/** API falsa con estado: lo que responde un POST (si es la cuenta del vínculo) es lo que devuelve el GET siguiente. */
function montar(inicial: CuentaDesignadaDelVinculoDto, responder: (url: string, metodo: string) => CuentaDesignadaDelVinculoDto | Response | null = () => null) {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  let datos = inicial;
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      if (metodo !== 'GET') peticiones.push({ url, metodo, cuerpo });
      const respuesta = responder(url, metodo);
      if (respuesta instanceof Response) return Promise.resolve(respuesta);
      if (respuesta) datos = respuesta;
      return Promise.resolve(json(datos));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <TarjetaCuentaDesignada vinculo={{ id: 'v1' }} />
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

describe('Cuenta bancaria designada del vínculo', () => {
  it('sin vinculos:leer no se muestra', () => {
    montar(CON_PROPUESTA);
    expect(screen.queryByText('Cuenta bancaria designada')).not.toBeInTheDocument();
  });

  it('sin ninguna cuenta dice que el tercero aún no la ha designado', async () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer]);
    montar({ actual: null, propuesta: null, rechazada: null, aprobadoresRequeridos: 2, puedesAprobar: false });
    expect(await screen.findByText('El tercero aún no ha designado una cuenta')).toBeInTheDocument();
  });

  it('muestra la propuesta con «1 de 2» y quién aprobó; aprobar envía el POST', async () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer, SCOPES.cuentasAprobar]);
    const usuario = userEvent.setup();
    const designada = { ...CUENTA, estado: 'Aprobada', aprobadaEn: '2026-11-09T10:00:00Z' };
    const peticiones = montar(CON_PROPUESTA, (url, metodo) => (metodo === 'POST' && url.endsWith('/aprobar') ? { ...CON_PROPUESTA, actual: designada, propuesta: null, puedesAprobar: false } : null));

    expect(await screen.findByText('1 de 2 aprobaciones: Carlos Cumplimiento')).toBeInTheDocument();
    expect(screen.getByText(/personas distintas/)).toBeInTheDocument();
    expect(screen.getByText('00123454321')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Aprobar' }));
    const dialogo = await screen.findByRole('dialog');
    expect(within(dialogo).getByText(/la cuenta queda designada/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Aprobar' }));

    await waitFor(() => expect(peticiones).toContainEqual({ url: '/api/v1/vinculos/v1/cuenta-designada/aprobar', metodo: 'POST', cuerpo: null }));
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('La cuenta quedó designada.'));
    expect(await screen.findByRole('region', { name: 'Designada' })).toBeInTheDocument();
  });

  it('rechazar exige el motivo (botón deshabilitado sin él) y lo envía', async () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer, SCOPES.cuentasAprobar]);
    const usuario = userEvent.setup();
    const rechazada = { ...CUENTA, estado: 'Rechazada', motivoRechazo: 'El titular no coincide' };
    const peticiones = montar(CON_PROPUESTA, (url) => (url.endsWith('/rechazar') ? { ...CON_PROPUESTA, propuesta: null, rechazada, puedesAprobar: false } : null));

    await usuario.click(await screen.findByRole('button', { name: 'Rechazar' }));
    const dialogo = await screen.findByRole('dialog');
    const confirmar = within(dialogo).getByRole('button', { name: 'Rechazar' });
    expect(confirmar).toBeDisabled();
    await usuario.click(within(dialogo).getByRole('textbox', { name: /Motivo para el tercero/ }));
    await usuario.paste('El titular no coincide');
    await usuario.click(confirmar);

    await waitFor(() => expect(peticiones).toContainEqual({ url: '/api/v1/vinculos/v1/cuenta-designada/rechazar', metodo: 'POST', cuerpo: { motivo: 'El titular no coincide' } }));
    expect(await screen.findByText('Motivo: El titular no coincide')).toBeInTheDocument();
  });

  it('aprobar dos veces explica que falta otra persona (aprobador-repetido)', async () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer, SCOPES.cuentasAprobar]);
    const usuario = userEvent.setup();
    montar(CON_PROPUESTA, (url) => (url.endsWith('/aprobar') ? json({ codigo: 'aprobador-repetido', detail: 'Ya aprobaste.', status: 409 }, 409) : null));
    await usuario.click(await screen.findByRole('button', { name: 'Aprobar' }));
    await usuario.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Aprobar' }));
    await waitFor(() => expect(useAvisos.getState().actual).toMatchObject({ mensaje: 'Ya aprobaste esta cuenta; falta que la apruebe otra persona.', severidad: 'error' }));
  });

  it('sin cuentas:aprobar muestra el número enmascarado y no ofrece acciones', async () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer]);
    montar({ ...CON_PROPUESTA, propuesta: { ...CUENTA, numero: null }, puedesAprobar: false });
    expect(await screen.findByText('•••• 4321')).toBeInTheDocument();
    expect(screen.queryByText('00123454321')).not.toBeInTheDocument();
    expect(screen.getByText('Solo quien aprueba cuentas ve el número completo.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Aprobar' })).not.toBeInTheDocument();
  });
});
