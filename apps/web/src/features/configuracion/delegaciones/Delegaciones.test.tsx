import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type AreaDto, type CandidatoDeDelegacionDto, type DelegacionDto, type SesionValidasoft } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useSesion } from '../../../sesion/sesionStore';
import { useAvisos } from '../../../shared/useAvisos';
import { PaginaDelegaciones } from './PaginaDelegaciones';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

// Las interacciones con diálogos de MUI en jsdom tardan segundos y, con el equipo cargado, pueden pasar de los 20 s globales.
vi.setConfig({ testTimeout: 60_000 });

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

const CANDIDATOS: CandidatoDeDelegacionDto[] = [
  { id: 'ana', nombre: 'Ana Ruiz', correo: 'ana@norte.co' },
  { id: 'luis', nombre: 'Luis Pérez', correo: 'luis@norte.co' },
  { id: 'yo', nombre: 'Marta Gil', correo: 'marta@norte.co' },
];

const area = (id: string, nombre: string, miembros: string[]): AreaDto => ({ id, empresaId: 'emp', codigo: nombre.toUpperCase(), nombre, miembros, activa: true });
const AREAS: AreaDto[] = [area('a1', 'Compras', ['yo']), area('a2', 'Jurídica', ['yo', 'ana']), area('a3', 'Cartera', ['luis'])];

const delegacion = (cambios: Partial<DelegacionDto> = {}): DelegacionDto => ({
  id: 'd1',
  deUsuarioId: 'yo',
  de: 'Marta Gil',
  aUsuarioId: 'ana',
  a: 'Ana Ruiz',
  areas: [{ areaId: 'a1', codigo: 'COMPRAS', nombre: 'Compras' }],
  todasLasAreas: false,
  desde: '2026-11-10',
  hasta: '2026-11-20',
  motivo: 'Vacaciones',
  estado: 'Vigente',
  creadaPor: 'Usuario:yo',
  creadaEn: '2026-11-09T12:00:00Z',
  revocadaEn: null,
  motivoDeRevocacion: null,
  ...cambios,
});

interface Peticion {
  metodo: string;
  ruta: string;
  cuerpo: unknown;
}

function montar(responderPost: (cuerpo: unknown) => Response = () => json(delegacion(), 201), lista: DelegacionDto[] = [delegacion()]) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    obtenerToken: () => 'tok',
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const texto = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const ruta = texto.split('?')[0] ?? texto;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      peticiones.push({ metodo, ruta, cuerpo });
      if (metodo === 'POST' && ruta.endsWith('/revocar')) return Promise.resolve(json(delegacion({ estado: 'Revocada' })));
      if (metodo === 'POST') return Promise.resolve(responderPost(cuerpo));
      if (ruta === '/api/v1/delegaciones/candidatos') return Promise.resolve(json(CANDIDATOS));
      if (ruta === '/api/v1/areas') return Promise.resolve(json(AREAS));
      return Promise.resolve(json(lista));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={['/e/emp/delegaciones']}>
          <PaginaDelegaciones />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

function iniciarSesion() {
  const sesion: SesionValidasoft = {
    token: 'tok',
    expira: new Date(Date.now() + 30 * 60_000),
    usuarioId: 'yo',
    tenantId: 't-1',
    nombre: null,
    sistema: null,
    sesionId: null,
    metodoIngreso: null,
    actorTipo: 'Usuario',
    scopesDeTenant: new Set(),
    scopesPorEmpresa: new Map(),
    empresas: '*',
  };
  useSesion.setState({ sesion, estado: 'activa', empresaSugerida: null, sugerirPasskey: false });
}

const conPermisos = (...scopes: string[]) => (permisos.scopes = new Set(scopes));
const REVISOR = [SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.configuracionLeer];

/** Elige una opción de un select de MUI por su nombre accesible. */
async function elegir(usuario: ReturnType<typeof userEvent.setup>, dialogo: HTMLElement, campo: RegExp, opcion: RegExp) {
  await usuario.click(within(dialogo).getByRole('combobox', { name: campo }));
  await usuario.click(await screen.findByRole('option', { name: opcion }));
}

/** Escribe una fecha `ddmmaaaa` en las secciones del campo. */
async function escribirFecha(usuario: ReturnType<typeof userEvent.setup>, dialogo: HTMLElement, campo: RegExp, digitos: string) {
  await usuario.click(within(within(dialogo).getByRole('group', { name: campo })).getByRole('spinbutton', { name: 'Día' }));
  await usuario.keyboard(digitos);
}

beforeEach(() => {
  // Solo se finge `Date`: los temporizadores de user-event y de las consultas siguen reales.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date('2026-11-12T15:00:00Z'));
  iniciarSesion();
});

afterEach(() => {
  vi.useRealTimers();
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.getState().cerrar();
  useSesion.setState({ sesion: null, estado: 'sin-sesion' });
});

describe('Delegación de revisión', () => {
  it('sin expedientes:revisar dice que la sección no es suya y no pide nada', () => {
    conPermisos(SCOPES.expedientesLeer);
    const peticiones = montar();
    expect(screen.getByRole('alert')).toHaveTextContent('Delegación de revisión');
    expect(peticiones).toHaveLength(0);
  });

  it('lista cada delegación con su estado, de quién a quién, áreas y fechas', async () => {
    conPermisos(...REVISOR);
    montar(undefined, [
      delegacion(),
      delegacion({ id: 'd2', deUsuarioId: 'luis', de: 'Luis Pérez', aUsuarioId: 'yo', a: 'Marta Gil', areas: [], todasLasAreas: true, estado: 'Programada', desde: '2026-12-01', hasta: '2026-12-10', motivo: null }),
      delegacion({ id: 'd3', estado: 'Revocada', revocadaEn: '2026-11-11T10:00:00Z', motivoDeRevocacion: 'Cambió el plan' }),
    ]);

    const tabla = await screen.findByRole('table', { name: 'Delegación de revisión' });
    const filas = within(tabla).getAllByRole('row').slice(1);
    expect(filas).toHaveLength(3);
    // Yo delego en Ana: «Tú» a «Ana Ruiz», solo Compras, entre el 10 y el 20 de noviembre.
    expect(within(filas[0]!).getByText('Tú')).toBeInTheDocument();
    expect(within(filas[0]!).getByText('Ana Ruiz')).toBeInTheDocument();
    expect(within(filas[0]!).getByText('Compras')).toBeInTheDocument();
    expect(within(filas[0]!).getByText('10/11/2026 – 20/11/2026')).toBeInTheDocument();
    expect(within(filas[0]!).getByText('Vigente')).toBeInTheDocument();
    expect(within(filas[0]!).getByText('Motivo: Vacaciones')).toBeInTheDocument();
    // Luis delega en mí todas sus áreas: la delegación aún no empieza.
    expect(within(filas[1]!).getByText('Luis Pérez')).toBeInTheDocument();
    expect(within(filas[1]!).getByText('Todas sus áreas')).toBeInTheDocument();
    expect(within(filas[1]!).getByText('Programada')).toBeInTheDocument();
    // Una revocada ya no se puede revocar y dice cuándo y por qué.
    expect(within(filas[2]!).getByText('Revocada')).toBeInTheDocument();
    expect(within(filas[2]!).getByText('el 11/11/2026')).toBeInTheDocument();
    expect(within(filas[2]!).getByText('Motivo de la revocación: Cambió el plan')).toBeInTheDocument();
    expect(within(filas[2]!).queryByRole('button', { name: /Revocar/ })).not.toBeInTheDocument();
  });

  it('el diálogo avisa qué pasará y a quién antes de crear, y solo «Delegar» envía la delegación', async () => {
    const usuario = userEvent.setup();
    conPermisos(...REVISOR);
    const peticiones = montar();
    await usuario.click(await screen.findByRole('button', { name: 'Delegar mi revisión' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Delegar mi revisión' });

    // Sin persona ni fecha final todavía no hay aviso y «Delegar» no se puede usar.
    expect(within(dialogo).getByRole('button', { name: 'Delegar' })).toBeDisabled();
    expect(within(dialogo).queryByText(/revisará en tu lugar/)).not.toBeInTheDocument();
    expect(within(dialogo).getByText('Elige quién revisa en tu lugar.')).toBeInTheDocument();
    // Solo se ofrecen las áreas de las que soy miembro y no yo mismo como delegado.
    expect(within(dialogo).getByRole('button', { name: 'Compras' })).toBeInTheDocument();
    expect(within(dialogo).getByRole('button', { name: 'Jurídica' })).toBeInTheDocument();
    expect(within(dialogo).queryByRole('button', { name: 'Cartera' })).not.toBeInTheDocument();
    expect(within(dialogo).getByText('Sin elegir ninguna, delegas todas: Compras y Jurídica.')).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('combobox', { name: /Quién revisa en su lugar/ }));
    expect(screen.queryByRole('option', { name: /Marta Gil/ })).not.toBeInTheDocument();
    await usuario.click(await screen.findByRole('option', { name: /Ana Ruiz/ }));
    await escribirFecha(usuario, dialogo, /^Hasta/, '20112026');

    const aviso = await within(dialogo).findByRole('status');
    expect(aviso).toHaveTextContent(
      'Ana Ruiz revisará en tu lugar todas tus áreas (Compras y Jurídica) del 12/11/2026 al 20/11/2026: verá tu bandeja y recibirá sus avisos. ' +
        'Las reglas de separación de funciones y de conflicto de intereses se le siguen aplicando a Ana Ruiz. Puedes revocarla cuando quieras.',
    );
    // Hasta aquí no se creó nada.
    expect(peticiones.some((p) => p.metodo === 'POST')).toBe(false);

    // Elegir un área concreta cambia el aviso.
    await usuario.click(within(dialogo).getByRole('button', { name: 'Jurídica' }));
    expect(await within(dialogo).findByText(/revisará en tu lugar el área Jurídica del/)).toBeInTheDocument();

    await usuario.click(within(dialogo).getByRole('button', { name: 'Delegar' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === '/api/v1/delegaciones')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'POST')?.cuerpo).toEqual({ aUsuarioId: 'ana', desde: '2026-11-12', hasta: '2026-11-20', areas: ['a2'] });
    await waitFor(() => expect(screen.queryByRole('dialog', { name: 'Delegar mi revisión' })).not.toBeInTheDocument());
    expect(useAvisos.getState().actual?.mensaje).toBe('Delegación creada: Ana Ruiz revisa en tu lugar desde el 12/11/2026.');
  });

  it('si el servidor rechaza la delegación muestra su error en español y deja corregir', async () => {
    const usuario = userEvent.setup();
    conPermisos(...REVISOR);
    montar(() => json({ status: 409, codigo: 'delegacion-solapada', title: 'Conflicto', detail: 'Ya hay una delegación…' }, 409));
    await usuario.click(await screen.findByRole('button', { name: 'Delegar mi revisión' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Delegar mi revisión' });
    await elegir(usuario, dialogo, /Quién revisa en su lugar/, /Ana Ruiz/);
    await escribirFecha(usuario, dialogo, /^Hasta/, '20112026');

    await usuario.click(within(dialogo).getByRole('button', { name: 'Delegar' }));
    expect(await within(dialogo).findByText(/Ya hay una delegación de esas áreas a esa persona que se cruza con estas fechas/)).toBeInTheDocument();
    // El diálogo sigue abierto con lo escrito, para cambiar las fechas.
    expect(within(dialogo).getByRole('button', { name: 'Delegar' })).toBeEnabled();
    expect(within(dialogo).getByRole('status')).toHaveTextContent('Ana Ruiz revisará en tu lugar');
  });

  it('una fecha final anterior a la inicial se explica bajo «Hasta» y bloquea el envío', async () => {
    const usuario = userEvent.setup();
    conPermisos(...REVISOR);
    montar();
    await usuario.click(await screen.findByRole('button', { name: 'Delegar mi revisión' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Delegar mi revisión' });
    await elegir(usuario, dialogo, /Quién revisa en su lugar/, /Ana Ruiz/);
    await escribirFecha(usuario, dialogo, /^Desde/, '20112026');
    await escribirFecha(usuario, dialogo, /^Hasta/, '15112026');

    expect(await within(dialogo).findByText('La fecha final no puede ser anterior a la inicial.')).toBeInTheDocument();
    expect(within(dialogo).getByRole('button', { name: 'Delegar' })).toBeDisabled();
    expect(within(dialogo).queryByRole('status')).not.toBeInTheDocument();
  });

  it('quien administra expedientes puede delegar en nombre de otra persona y las áreas pasan a ser las de ella', async () => {
    const usuario = userEvent.setup();
    conPermisos(...REVISOR, SCOPES.expedientesAdministrar);
    const peticiones = montar();
    await usuario.click(await screen.findByRole('button', { name: 'Delegar mi revisión' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Delegar mi revisión' });

    await elegir(usuario, dialogo, /En nombre de/, /Luis Pérez/);
    expect(await within(dialogo).findByRole('heading', { name: 'Delegar la revisión de otra persona' })).toBeInTheDocument();
    // Ahora las áreas son las de Luis, no las mías.
    expect(within(dialogo).getByRole('button', { name: 'Cartera' })).toBeInTheDocument();
    expect(within(dialogo).queryByRole('button', { name: 'Compras' })).not.toBeInTheDocument();
    await elegir(usuario, dialogo, /Quién revisa en su lugar/, /Ana Ruiz/);
    await escribirFecha(usuario, dialogo, /^Hasta/, '20112026');
    expect(await within(dialogo).findByRole('status')).toHaveTextContent('Ana Ruiz revisará en lugar de Luis Pérez todas sus áreas (Cartera) del 12/11/2026 al 20/11/2026');

    await usuario.click(within(dialogo).getByRole('button', { name: 'Delegar' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'POST')?.cuerpo).toEqual({ aUsuarioId: 'ana', desde: '2026-11-12', hasta: '2026-11-20', deUsuarioId: 'luis' });
  });

  it('revocar pide confirmación y dice qué cambia antes de enviar', async () => {
    const usuario = userEvent.setup();
    conPermisos(...REVISOR);
    const peticiones = montar();
    await usuario.click(await screen.findByRole('button', { name: 'Revocar la delegación de Marta Gil a Ana Ruiz' }));
    const confirmacion = await screen.findByRole('dialog', { name: /Revocar delegación/ });
    expect(confirmacion).toHaveTextContent('Ana Ruiz deja de revisar en lugar de Marta Gil desde ahora');
    expect(peticiones.some((p) => p.metodo === 'POST')).toBe(false);

    await usuario.click(within(confirmacion).getByRole('button', { name: 'Revocar delegación' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta === '/api/v1/delegaciones/d1/revocar')).toBe(true));
    await waitFor(() => expect(screen.queryByRole('dialog', { name: /Revocar delegación/ })).not.toBeInTheDocument());
    expect(useAvisos.getState().actual?.mensaje).toBe('Delegación revocada.');
  });

  it('sin delegaciones explica para qué sirve y deja delegar desde el botón de la página', async () => {
    conPermisos(...REVISOR);
    montar(undefined, []);
    expect(await screen.findByText('Nadie revisa en lugar de otra persona por ahora.')).toBeInTheDocument();
    expect(screen.getByText(/usa «Delegar mi revisión»/)).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: 'Delegar mi revisión' })).toHaveLength(1);
  });
});
