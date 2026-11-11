import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type AreaDto, type ExcepcionDeRevisionDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { createMemoryRouter, RouterProvider } from 'react-router';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { simularDisenoDeLista } from '../../pruebas/listaVirtual';
import { useAvisos } from '../../shared/useAvisos';
import { expediente } from '../expedientes/datosPrueba';
import { PaginaExcepciones } from './PaginaExcepciones';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));
vi.mock('../../sesion/sesionStore', () => ({ useSesion: (seleccion: (estado: { sesion: { usuarioId: string } }) => unknown) => seleccion({ sesion: { usuarioId: 'u-1' } }) }));

const AHORA = new Date('2026-11-11T15:00:00Z');

const MOTIVOS_DE_EXCEPCION = [
  { codigo: 'confianza-baja', texto: 'La confianza del análisis (62 %) está bajo el umbral de la empresa (95 %).' },
  { codigo: 'alertas-de-manipulacion', texto: 'El análisis detectó señales de manipulación.' },
];

function fila(cambios: Partial<ExcepcionDeRevisionDto> = {}): ExcepcionDeRevisionDto {
  return {
    expedienteId: 'e1',
    folio: 'NORTE-E-2026-000001',
    vinculoId: 'vin',
    tercero: { id: 't1', nombre: 'Aceros del Norte S.A.S.', identificacion: 'CO|NIT|900123456' },
    tipoVinculo: 'Proveedor',
    estadoExpediente: 'EnRevision',
    requisitoId: 'r-rut',
    requisito: 'Registro Único Tributario',
    tipoCanonico: 'RUT',
    version: 2,
    areaId: 'a-compras',
    areaCodigo: 'compras',
    tipo: 'Excepcion',
    resultado: 'Excepcion',
    motivos: MOTIVOS_DE_EXCEPCION,
    confianza: 0.62,
    desde: '2026-11-09T15:00:00Z',
    resuelta: false,
    resolucion: null,
    resueltaPor: null,
    resueltaEn: null,
    ...cambios,
  };
}

const EXCEPCION = fila();
const MUESTREO = fila({
  expedienteId: 'e2',
  folio: 'NORTE-E-2026-000002',
  tercero: { id: 't2', nombre: 'Ladrillera del Valle S.A.S.', identificacion: 'CO|NIT|800555444' },
  requisitoId: 'r-camara',
  requisito: 'Certificado de existencia y representación legal',
  version: 1,
  tipo: 'Muestreo',
  resultado: 'AprobadaConMuestreo',
  motivos: [],
  confianza: 0.97,
  desde: '2026-11-11T12:00:00Z',
});

const AREAS: AreaDto[] = [{ id: 'a-compras', empresaId: 'emp', codigo: 'compras', nombre: 'Compras', miembros: ['u-1'], activa: true }];

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

interface Peticion {
  metodo: string;
  url: URL;
  cuerpo: unknown;
}

function montar({
  pendientes = [EXCEPCION, MUESTREO],
  resueltas = [],
  areas = AREAS,
  resolver = () => json({ ...expediente('EnRevision'), id: 'e2' }),
  ruta = '/e/emp/excepciones',
}: { pendientes?: ExcepcionDeRevisionDto[]; resueltas?: ExcepcionDeRevisionDto[]; areas?: AreaDto[]; resolver?: () => Response; ruta?: string } = {}) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) =>
      Promise.resolve(
        ((): Response => {
      const url = new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost');
      const metodo = (init?.method ?? (entrada instanceof Request ? entrada.method : 'GET')).toUpperCase();
      peticiones.push({ metodo, url, cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null });
      if (url.pathname === '/api/v1/revision-automatica/excepciones') return json(url.searchParams.get('estado') === 'Resueltas' ? resueltas : pendientes);
      if (url.pathname === '/api/v1/areas') return json(areas);
      if (url.pathname === '/api/v1/usuarios/resumen') return json([{ id: 'u-2', nombre: 'Ana Gómez' }]);
      if (metodo === 'POST' && url.pathname.endsWith('/revision-automatica/resolver')) return resolver();
      return json([]);
        })(),
      ),
    ),
  });
  const router = createMemoryRouter([{ path: '/e/:empresaId/excepciones', element: <PaginaExcepciones /> }], { initialEntries: [ruta] });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <RouterProvider router={router} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { peticiones, router };
}

const resoluciones = (peticiones: Peticion[]) => peticiones.filter((p) => p.metodo === 'POST');

/** Las filas de la lista (sus hijos directos: dentro de cada una hay otras listas, como la de motivos). */
const filasDe = (lista: HTMLElement) => Array.from(lista.children) as HTMLElement[];

beforeEach(() => {
  // Solo el reloj de Date (cuánto llevan esperando las filas); user-event sigue con temporizadores reales.
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(AHORA);
  permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.configuracionLeer]);
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.useRealTimers();
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.setState({ actual: null });
});

describe('Excepciones de la revisión automática', () => {
  it('sin expedientes:leer dice que no hay acceso y no pide nada', () => {
    permisos.scopes = new Set();
    const { peticiones } = montar();
    expect(screen.getByRole('alert')).toHaveTextContent('Excepciones');
    expect(peticiones).toHaveLength(0);
  });

  it('muestra las pendientes: tercero, folio, documento y versión, área, tipo, motivos, confianza y cuánto llevan esperando', async () => {
    montar();
    const lista = await screen.findByRole('list', { name: 'Excepciones pendientes' });
    const filas = filasDe(lista);
    expect(filas).toHaveLength(2);
    // Con pocas filas se dibujan todas, sin lista virtual.
    expect(lista.querySelector('[data-index]')).toBeNull();

    const excepcion = within(filas[0]!);
    expect(excepcion.getByRole('heading', { name: 'Aceros del Norte S.A.S.' })).toBeInTheDocument();
    expect(excepcion.getByText('NIT 900123456')).toBeInTheDocument();
    expect(excepcion.getByText('Excepción')).toBeInTheDocument();
    expect(excepcion.getByText('NORTE-E-2026-000001')).toBeInTheDocument();
    expect(excepcion.getByText(/^· Proveedor · Registro Único Tributario · versión 2 · Área: Compras · Confianza 62\s?%$/)).toBeInTheDocument();
    const motivos = within(excepcion.getByRole('list', { name: 'Motivos' }));
    expect(motivos.getAllByRole('listitem').map((m) => m.textContent)).toEqual(['Confianza baja', 'Señales de manipulación']);
    expect(excepcion.getByText('Esperando anteayer')).toBeInTheDocument();
    expect(excepcion.getByRole('link', { name: /Abrir el expediente NORTE-E-2026-000001/ })).toHaveAttribute('href', '/e/emp/expedientes/e1');
    // Una excepción se decide en el expediente: sin Confirmar ni Revertir.
    expect(excepcion.queryByRole('button', { name: /Confirmar/ })).not.toBeInTheDocument();

    const muestreo = within(filas[1]!);
    expect(muestreo.getByText('Muestreo')).toBeInTheDocument();
    expect(muestreo.getByText('Aprobado por el sistema')).toBeInTheDocument();
    expect(muestreo.getByText(/Confianza 97\s?%/)).toBeInTheDocument();
    expect(muestreo.getByRole('button', { name: /Confirmar la aprobación de Certificado/ })).toBeInTheDocument();
    expect(muestreo.getByRole('button', { name: /Revertir la aprobación/ })).toBeInTheDocument();
    expect(muestreo.queryByRole('list', { name: 'Motivos' })).not.toBeInTheDocument();

    // El contador de la pestaña dice cuántas esperan a una persona.
    expect(screen.getByRole('tab', { name: 'Pendientes (2)' })).toHaveAttribute('aria-selected', 'true');
  });

  it('filtra por tipo y lo deja en la URL; quitar el filtro vuelve a todas', async () => {
    const usuario = userEvent.setup();
    const { router } = montar();
    await screen.findByRole('list', { name: 'Excepciones pendientes' });

    await usuario.click(screen.getByRole('button', { name: /Tipo/ }));
    await usuario.click(screen.getByRole('menuitem', { name: 'Muestreo' }));
    await waitFor(() => expect(router.state.location.search).toBe('?tipo=muestreo'));
    const filas = filasDe(screen.getByRole('list', { name: 'Excepciones pendientes' }));
    expect(filas).toHaveLength(1);
    expect(filas[0]).toHaveTextContent('Ladrillera del Valle S.A.S.');

    await usuario.click(screen.getByRole('button', { name: 'Quitar el filtro' }));
    await waitFor(() => expect(router.state.location.search).toBe(''));
    expect(filasDe(screen.getByRole('list', { name: 'Excepciones pendientes' }))).toHaveLength(2);
  });

  it('un filtro sin filas lo dice y ofrece quitarlo', async () => {
    const usuario = userEvent.setup();
    montar({ pendientes: [EXCEPCION], ruta: '/e/emp/excepciones?tipo=muestreo' });
    expect(await screen.findByText('Ninguna excepción de ese tipo')).toBeInTheDocument();
    await usuario.click(screen.getAllByRole('button', { name: 'Quitar el filtro' }).at(-1)!);
    expect(await screen.findByRole('list', { name: 'Excepciones pendientes' })).toBeInTheDocument();
  });

  it('Confirmar es un clic: manda la decisión con el área y avisa', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = montar();
    await usuario.click(await screen.findByRole('button', { name: /Confirmar la aprobación de Certificado/ }));

    await waitFor(() => expect(resoluciones(peticiones)).toHaveLength(1));
    const [peticion] = resoluciones(peticiones);
    expect(peticion?.url.pathname).toBe('/api/v1/expedientes/e2/requisitos/r-camara/revision-automatica/resolver');
    expect(peticion?.cuerpo).toEqual({ areaId: 'a-compras', decision: 'Confirmar', justificacion: null });
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Confirmaste la aprobación automática de Certificado de existencia y representación legal.'));
  });

  it('Revertir abre un diálogo que avisa qué pasa y no deja enviar sin justificación', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = montar();
    await usuario.click(await screen.findByRole('button', { name: /Revertir la aprobación/ }));

    const dialogo = await screen.findByRole('dialog', { name: /Revertir la aprobación automática/ });
    expect(within(dialogo).getByText(/NORTE-E-2026-000002 · Ladrillera del Valle S.A.S./)).toBeInTheDocument();
    expect(within(dialogo).getByText(/Se anula el veredicto del sistema sobre Certificado de existencia y representación legal en Compras y el documento vuelve a revisión humana/)).toBeInTheDocument();
    const enviar = within(dialogo).getByRole('button', { name: 'Revertir aprobación' });
    expect(enviar).toBeDisabled();
    expect(within(dialogo).getByText('Escribe la justificación')).toBeInTheDocument();

    await usuario.type(within(dialogo).getByRole('textbox', { name: /Justificación/ }), 'El certificado está vencido.');
    expect(enviar).toBeEnabled();
    await usuario.click(enviar);

    await waitFor(() => expect(resoluciones(peticiones)).toHaveLength(1));
    expect(resoluciones(peticiones)[0]?.cuerpo).toEqual({ areaId: 'a-compras', decision: 'Revertir', justificacion: 'El certificado está vencido.' });
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(useAvisos.getState().actual?.mensaje).toBe('Revertiste la aprobación: Certificado de existencia y representación legal vuelve a revisión humana.');
  });

  it('si otra persona ya la resolvió, el diálogo lo explica y conserva lo escrito', async () => {
    const usuario = userEvent.setup();
    montar({
      resolver: () =>
        json({ type: 'about:blank', title: 'Conflicto', status: 409, detail: 'Esa aprobación por muestreo ya se resolvió.', codigo: 'revision-ya-resuelta' }, 409),
    });
    await usuario.click(await screen.findByRole('button', { name: /Revertir la aprobación/ }));
    const dialogo = await screen.findByRole('dialog');
    await usuario.type(within(dialogo).getByRole('textbox', { name: /Justificación/ }), 'No me convence.');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Revertir aprobación' }));

    expect(await within(dialogo).findByText('Otra persona ya resolvió esa aprobación. Recargamos la lista.')).toBeInTheDocument();
    expect(within(dialogo).getByRole('textbox', { name: /Justificación/ })).toHaveValue('No me convence.');
  });

  it('con el expediente ya cerrado solo se puede confirmar', async () => {
    montar({ pendientes: [{ ...MUESTREO, estadoExpediente: 'Aprobado' }] });
    expect(await screen.findByRole('button', { name: /Confirmar la aprobación/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Revertir la aprobación/ })).not.toBeInTheDocument();
  });

  it('sin expedientes:revisar solo se puede abrir el expediente', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    montar();
    const lista = await screen.findByRole('list', { name: 'Excepciones pendientes' });
    expect(within(lista).getAllByRole('link', { name: /Abrir el expediente/ })).toHaveLength(2);
    expect(within(lista).queryByRole('button', { name: /Confirmar/ })).not.toBeInTheDocument();
    expect(within(lista).queryByRole('button', { name: /Revertir/ })).not.toBeInTheDocument();
  });

  it('quien no es miembro del área no confirma y la fila lo dice', async () => {
    montar({ areas: [{ ...AREAS[0]!, miembros: ['u-9'] }] });
    const lista = await screen.findByRole('list', { name: 'Excepciones pendientes' });
    expect(await within(lista).findByText('Solo la confirman los miembros del área.')).toBeInTheDocument();
    expect(within(lista).queryByRole('button', { name: /Confirmar/ })).not.toBeInTheDocument();
  });

  it('Resueltas: muestra quién confirmó o revirtió y cuándo, y las excepciones que ya no esperan a nadie', async () => {
    const usuario = userEvent.setup();
    montar({
      resueltas: [
        { ...MUESTREO, resuelta: true, resolucion: 'Confirmada', resueltaPor: 'u-2', resueltaEn: '2026-11-11T13:00:00Z' },
        { ...EXCEPCION, resuelta: true },
      ],
    });
    await usuario.click(await screen.findByRole('tab', { name: 'Resueltas' }));

    const lista = await screen.findByRole('list', { name: 'Excepciones resueltas' });
    expect(await within(lista).findByText(/Confirmada por Ana Gómez · /)).toBeInTheDocument();
    expect(within(lista).getByText('Resuelta: ya no espera a nadie')).toBeInTheDocument();
    // Lo resuelto no se vuelve a decidir: solo se abre el expediente.
    expect(within(lista).queryByRole('button', { name: /Confirmar/ })).not.toBeInTheDocument();
    expect(within(lista).getAllByRole('link', { name: /Abrir el expediente/ })).toHaveLength(2);
  });

  it('sin pendientes explica cuándo aparecerán; sin resueltas, también', async () => {
    const usuario = userEvent.setup();
    montar({ pendientes: [], resueltas: [] });
    expect(await screen.findByText('No hay excepciones pendientes')).toBeInTheDocument();
    expect(screen.getByText(/Cuando la IA no pueda aprobar un documento/)).toBeInTheDocument();
    await usuario.click(screen.getByRole('tab', { name: /Resueltas/ }));
    expect(await screen.findByText('Aún no hay excepciones resueltas')).toBeInTheDocument();
  });

  it('con 200 filas avisa que son las más antiguas y, con más de 50, usa la lista virtual', async () => {
    // jsdom no calcula el diseño: se simulan las medidas para que el virtualizador dibuje solo las filas visibles.
    simularDisenoDeLista({ altoFila: 132 });
    const muchas = Array.from({ length: 200 }, (_, i) => fila({ expedienteId: `e${String(i)}`, requisitoId: `r${String(i)}`, folio: `NORTE-E-2026-${String(i).padStart(6, '0')}` }));
    montar({ pendientes: muchas });
    expect(await screen.findByText('Mostramos las 200 más antiguas. Al resolver estas aparecen las siguientes.')).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Pendientes (200)' })).toBeInTheDocument();
    // Con más de 50 filas se usa la lista virtual (cada fila lleva su índice).
    const lista = screen.getByRole('list', { name: 'Excepciones pendientes' });
    expect(lista.querySelectorAll('[data-index]').length).toBeGreaterThan(0);
    expect(lista.querySelectorAll('[data-index]').length).toBeLessThan(30);
  });
});
