import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type MedicionDePlanDto, type UsoDelGrupoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import '../../i18n';
import { PaginaPlanGrupo as PlanDelGrupoEnConfiguracion } from '../configuracion/planGrupo/PaginaPlanGrupo';
import { PaginaPlanGrupo as PlanDelGrupoEnPlataforma } from '../plataforma/PaginaPlanGrupo';
import { borradorDesdePlan, cuerpoPlan, enAlerta, formatearUso, hayCambiosDePlan, porcentajeDeBarra, validarPlan } from './modelo';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const MB = 1024 * 1024;

function medicion(metrica: string, usado: number, limite: number | null, nivel = 'Normal'): MedicionDePlanDto {
  return { metrica, usado, limite, porcentaje: limite ? Math.floor((usado * 100) / limite) : null, nivel };
}

const USO: UsoDelGrupoDto = {
  mes: '2026-11',
  plan: {
    tenantId: 't1',
    codigo: 'empresarial',
    limites: { usuarios: 50, empresas: 10, vinculosActivos: 1000, almacenamientoMb: 2048, llamadasApiMes: 100000 },
    bloqueoSuave: true,
    configurado: true,
    actualizadoEn: '2026-11-01T10:00:00Z',
  },
  mediciones: [
    medicion('Usuarios', 41, 50, 'PorAgotar'),
    medicion('Empresas', 10, 10, 'Agotado'),
    medicion('VinculosActivos', 120, 1000),
    medicion('Almacenamiento', 512 * MB, 2048 * MB),
    medicion('LlamadasApi', 1234, 100000),
  ],
  alertas: ['usuarios al 82 %', 'empresas al 100 %'],
  bloqueado: true,
  almacenamientoMedidoEn: '2026-11-11T09:00:00Z',
};

const SIN_PLAN: UsoDelGrupoDto = {
  ...USO,
  plan: { tenantId: 't1', codigo: '', limites: { usuarios: null, empresas: null, vinculosActivos: null, almacenamientoMb: null, llamadasApiMes: null }, bloqueoSuave: false, configurado: false, actualizadoEn: null },
  mediciones: USO.mediciones.map((m) => ({ ...m, limite: null, porcentaje: null, nivel: 'Normal' })),
  alertas: [],
  bloqueado: false,
};

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

interface Peticion {
  url: URL;
  metodo: string;
  cuerpo: unknown;
}

function montar(ruta: string, patron: string, pagina: React.ReactElement, uso: UsoDelGrupoDto) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url, 'http://localhost');
      const metodo = init?.method ?? 'GET';
      peticiones.push({ url, metodo, cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null });
      if (metodo === 'PUT') return Promise.resolve(json(uso));
      if (url.pathname.endsWith('/plan') || url.pathname.endsWith('/uso')) return Promise.resolve(json(uso));
      return Promise.resolve(json({ tenant: { id: 't1', nombre: 'Constructora Demo', slug: 'demo', activo: true, creadoEn: '2026-10-01T00:00:00Z' }, empresas: [] }));
    }),
  });
  const router = createMemoryRouter([{ path: patron, element: pagina }], { initialEntries: [ruta] });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
});

describe('modelo del plan', () => {
  it('escribe el almacenamiento en KB, MB o GB y lo demás con separador de miles', () => {
    expect(formatearUso('Almacenamiento', 900)).toBe('0,9 KB');
    expect(formatearUso('Almacenamiento', 512 * MB)).toBe('512 MB');
    expect(formatearUso('Almacenamiento', 3 * 1024 * MB)).toBe('3 GB');
    expect(formatearUso('LlamadasApi', 12345)).toBe((12345).toLocaleString('es-CO'));
  });

  it('la barra se llena hasta el 100 % y sin límite no se llena; las alertas son lo que no está en nivel normal', () => {
    expect(porcentajeDeBarra({ porcentaje: 140 })).toBe(100);
    expect(porcentajeDeBarra({ porcentaje: null })).toBe(0);
    expect(enAlerta(USO.mediciones).map((m) => m.metrica)).toEqual(['Usuarios', 'Empresas']);
  });

  it('un campo vacío es «sin límite» y el cero es un límite de verdad', () => {
    const borrador = { ...borradorDesdePlan(USO.plan), usuarios: '', empresas: '0', almacenamientoMb: '4096' };
    expect(cuerpoPlan({ ...borrador, codigo: '  estándar ' })).toEqual({
      codigo: 'estándar',
      limites: { usuarios: null, empresas: 0, vinculosActivos: 1000, almacenamientoMb: 4096, llamadasApiMes: 100000 },
      bloqueoSuave: true,
    });
  });

  it('pide un código de 2 a 40 caracteres y solo habilita guardar con cambios', () => {
    const base = borradorDesdePlan(USO.plan);
    expect(validarPlan({ ...base, codigo: 'x' })).toEqual(['codigo-invalido']);
    expect(validarPlan({ ...base, codigo: 'x'.repeat(41) })).toEqual(['codigo-invalido']);
    expect(validarPlan(base)).toEqual([]);
    expect(hayCambiosDePlan(base, USO.plan)).toBe(false);
    expect(hayCambiosDePlan({ ...base, bloqueoSuave: false }, USO.plan)).toBe(true);
  });
});

describe('Plan del grupo (Configuración)', () => {
  it('sin configuracion:leer dice que no hay acceso y no pide nada', () => {
    const peticiones = montar('/e/emp/plan-grupo', '/e/:empresaId/plan-grupo', <PlanDelGrupoEnConfiguracion />, USO);
    expect(screen.getByRole('alert')).toHaveTextContent('Plan del grupo');
    expect(peticiones).toHaveLength(0);
  });

  it('muestra el plan, qué pasa al llegar al límite y las cinco métricas con su nivel', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    const peticiones = montar('/e/emp/plan-grupo', '/e/:empresaId/plan-grupo', <PlanDelGrupoEnConfiguracion />, USO);

    expect(await screen.findByText('empresarial')).toBeInTheDocument();
    expect(peticiones[0]?.url.pathname).toBe('/api/v1/tenant/uso');
    expect(screen.getByText(/No se pueden crear más usuarios, empresas ni vínculos/)).toBeInTheDocument();
    expect(screen.getByText(/Llegaste al límite de tu plan/)).toBeInTheDocument();
    expect(screen.getByText('41 de 50')).toBeInTheDocument();
    expect(screen.getByText('Al 82 %: queda poco')).toBeInTheDocument();
    expect(screen.getByText('En el límite')).toBeInTheDocument();
    expect(screen.getByText('512 MB de 2 GB')).toBeInTheDocument();
    expect(screen.getAllByRole('progressbar')).toHaveLength(5);
  });

  it('sin plan dice que no hay límites y no dibuja barras', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    montar('/e/emp/plan-grupo', '/e/:empresaId/plan-grupo', <PlanDelGrupoEnConfiguracion />, SIN_PLAN);

    expect(await screen.findByText('Sin plan asignado')).toBeInTheDocument();
    expect(screen.getByText(/aún no le fijó un plan a tu grupo/)).toBeInTheDocument();
    expect(screen.queryAllByRole('progressbar')).toHaveLength(0);
    expect(screen.getByText('41 · sin límite')).toBeInTheDocument();
  });
});

describe('Plan y límites de un grupo (Plataforma)', () => {
  it('muestra el plan actual y el uso, y guarda solo lo que cambió con vacío = sin límite', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar('/plataforma/grupos/t1/plan', '/plataforma/grupos/:tenantId/plan', <PlanDelGrupoEnPlataforma />, USO);

    const guardar = await screen.findByRole('button', { name: 'Guardar el plan' });
    expect(screen.getByRole('textbox', { name: 'Código del plan' })).toHaveValue('empresarial');
    expect(screen.getByRole('textbox', { name: 'Usuarios' })).toHaveValue('50');
    expect(screen.getByText('41 de 50')).toBeInTheDocument();
    expect(guardar).toBeDisabled();

    await usuario.clear(screen.getByRole('textbox', { name: 'Usuarios' }));
    await usuario.click(screen.getByRole('textbox', { name: 'Empresas' }));
    await usuario.paste('abc12');
    expect(screen.getByRole('textbox', { name: 'Empresas' })).toHaveValue('1012');
    await usuario.click(screen.getByRole('switch', { name: 'Bloquear al llegar al límite' }));
    expect(screen.getByText(/Al llegar a un límite solo se avisa/)).toBeInTheDocument();
    await waitFor(() => expect(guardar).toBeEnabled());
    await usuario.click(guardar);

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    const envio = peticiones.find((p) => p.metodo === 'PUT');
    expect(envio?.url.pathname).toBe('/api/v1/plataforma/tenants/t1/plan');
    expect(envio?.cuerpo).toEqual({
      codigo: 'empresarial',
      limites: { usuarios: null, empresas: 1012, vinculosActivos: 1000, almacenamientoMb: 2048, llamadasApiMes: 100000 },
      bloqueoSuave: false,
    });
  });

  it('un código demasiado corto no se envía y dice por qué', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar('/plataforma/grupos/t1/plan', '/plataforma/grupos/:tenantId/plan', <PlanDelGrupoEnPlataforma />, USO);

    const codigo = await screen.findByRole('textbox', { name: 'Código del plan' });
    await usuario.clear(codigo);
    await usuario.click(codigo);
    await usuario.paste('x');
    await usuario.click(screen.getByRole('button', { name: 'Guardar el plan' }));

    expect(await screen.findAllByText('El código del plan lleva de 2 a 40 caracteres.')).not.toHaveLength(0);
    expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(false);
  });
});
