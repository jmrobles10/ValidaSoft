import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type AreaDto, type ConfiguracionSlaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { PaginaPlazosDeRevision } from './PaginaPlazosDeRevision';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

// Las interacciones con diálogos de MUI en jsdom tardan segundos y, con el equipo cargado, pueden pasar de los 20 s globales.
vi.setConfig({ testTimeout: 60_000 });

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

const area = (id: string, nombre: string): AreaDto => ({ id, empresaId: 'emp', codigo: nombre.toUpperCase(), nombre, miembros: [], activa: true });
const AREAS = [area('a1', 'Compras'), area('a2', 'Jurídica')];
const CONFIGURACION: ConfiguracionSlaDto = {
  porcentajeAviso: 80,
  avisarAdministradores: true,
  areas: [
    { areaId: 'a1', horas: 48 },
    { areaId: 'a2', horas: null },
  ],
  actualizadaEn: '2026-11-10T12:00:00Z',
};

interface Peticion {
  metodo: string;
  ruta: string;
  cuerpo: unknown;
}

const guardadoPorOmision = (cuerpo: unknown) => json({ ...CONFIGURACION, ...(cuerpo as Partial<ConfiguracionSlaDto>), actualizadaEn: '2026-11-11T12:00:00Z' });

function montar(responderPut: (cuerpo: unknown) => Response = guardadoPorOmision) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    obtenerToken: () => 'tok',
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const texto = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const ruta = texto.split('?')[0] ?? texto;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      peticiones.push({ metodo, ruta, cuerpo });
      if (metodo === 'PUT') return Promise.resolve(responderPut(cuerpo));
      if (ruta === '/api/v1/areas') return Promise.resolve(json(AREAS));
      return Promise.resolve(json(CONFIGURACION));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <PaginaPlazosDeRevision />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

const conPermisos = (...scopes: string[]) => (permisos.scopes = new Set(scopes));
const EDITOR = [SCOPES.configuracionLeer, SCOPES.configuracionEscribir];
const PORCENTAJE = /^Avisar al consumir/;
const ADMINISTRADORES = 'Avisar a los administradores cuando se incumple';

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.getState().cerrar();
});

describe('Plazos de revisión', () => {
  it('sin configuracion:leer dice que la sección no es suya y no pide nada', () => {
    const peticiones = montar();
    expect(screen.getByRole('alert')).toHaveTextContent('Plazos de revisión');
    expect(peticiones).toHaveLength(0);
  });

  it('muestra el porcentaje, el interruptor y el plazo de cada área; el área sin plazo queda vacía', async () => {
    conPermisos(...EDITOR);
    montar();
    expect(await screen.findByRole('textbox', { name: PORCENTAJE })).toHaveValue('80');
    expect(screen.getByRole('switch', { name: ADMINISTRADORES })).toBeChecked();
    expect(screen.getByRole('textbox', { name: 'Plazo en horas de Compras' })).toHaveValue('48');
    expect(screen.getByRole('textbox', { name: 'Plazo en horas de Jurídica' })).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Guardar cambios' })).toBeDisabled();
  });

  it('guarda con PUT todas las áreas (el plazo vacío va null) y lo confirma con un aviso', async () => {
    const usuario = userEvent.setup();
    conPermisos(...EDITOR);
    const peticiones = montar();
    const porcentaje = await screen.findByRole('textbox', { name: PORCENTAJE });
    await usuario.clear(porcentaje);
    await usuario.type(porcentaje, '70');
    await usuario.type(screen.getByRole('textbox', { name: 'Plazo en horas de Jurídica' }), '24');
    await usuario.click(screen.getByRole('switch', { name: ADMINISTRADORES }));
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'PUT')).toMatchObject({
      ruta: '/api/v1/configuracion/sla',
      cuerpo: {
        porcentajeAviso: 70,
        avisarAdministradores: false,
        areas: [
          { areaId: 'a1', horas: 48 },
          { areaId: 'a2', horas: 24 },
        ],
      },
    });
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Plazos de revisión guardados.'));
  });

  it('un porcentaje fuera de 50–95 se marca en su campo y no deja guardar', async () => {
    const usuario = userEvent.setup();
    conPermisos(...EDITOR);
    const peticiones = montar();
    const porcentaje = await screen.findByRole('textbox', { name: PORCENTAJE });
    await usuario.clear(porcentaje);
    await usuario.type(porcentaje, '30');

    expect(await screen.findByText('Escribe un número entre 50 y 95.')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(false);
  });

  it('un plazo de más de 2160 horas se marca en la fila de su área y no se envía', async () => {
    const usuario = userEvent.setup();
    conPermisos(...EDITOR);
    const peticiones = montar();
    await usuario.type(await screen.findByRole('textbox', { name: 'Plazo en horas de Jurídica' }), '3000');
    expect(await screen.findByText('De 1 a 2160 horas (90 días).')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(false);
  });

  it('el 422 porcentaje-invalido del servidor se muestra en el campo del porcentaje', async () => {
    const usuario = userEvent.setup();
    conPermisos(...EDITOR);
    montar(() => json({ status: 422, codigo: 'porcentaje-invalido', title: 'Validación', detail: 'x' }, 422));
    await usuario.type(await screen.findByRole('textbox', { name: 'Plazo en horas de Jurídica' }), '24');
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }));
    expect(await screen.findByText('El aviso previo sale cuando se consume entre el 50 y el 95 % del plazo.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: PORCENTAJE })).toBeInvalid();
  });

  it('sin configuracion:escribir es de solo lectura: campos deshabilitados y sin botón de guardar', async () => {
    conPermisos(SCOPES.configuracionLeer);
    montar();
    expect(await screen.findByRole('textbox', { name: PORCENTAJE })).toBeDisabled();
    expect(screen.getByRole('textbox', { name: 'Plazo en horas de Compras' })).toBeDisabled();
    expect(screen.getByRole('switch', { name: ADMINISTRADORES })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Guardar cambios' })).not.toBeInTheDocument();
    expect(screen.getByText(/no cambiarlos/)).toBeInTheDocument();
  });
});
