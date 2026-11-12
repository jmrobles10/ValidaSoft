import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type CampaniaDto, type FilaCampaniaDto, type VistaPreviaCampaniaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../shared/useAvisos';
import { DetalleCampania } from './DetalleCampania';
import { PaginaCampanias } from './PaginaCampanias';
import { PaginaNuevaCampania } from './PaginaNuevaCampania';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

const FILTRO = { diasParaVencer: 30, tipoVinculoId: null, estados: ['Activo', 'PorVencer', 'Vencido'] };

function vistaPrevia(cambios: Partial<VistaPreviaCampaniaDto> = {}): VistaPreviaCampaniaDto {
  const filas = [
    { n: 1, vinculoId: 'v1', tercero: { id: 't1', nombre: 'Aceros del Norte', identificacion: 'CO|NIT|900123456' }, tipoVinculo: { id: 'tv1', codigo: 'PROVEEDOR', nombre: 'Proveedor' }, estadoVinculo: 'PorVencer', venceEn: '2026-11-30', accion: 'Abrir', motivo: null, conCorreo: true },
    { n: 2, vinculoId: 'v2', tercero: { id: 't2', nombre: 'Ferretería El Tornillo', identificacion: 'CO|NIT|800555111' }, tipoVinculo: { id: 'tv1', codigo: 'PROVEEDOR', nombre: 'Proveedor' }, estadoVinculo: 'Vencido', venceEn: '2026-11-01', accion: 'Recordar', motivo: null, conCorreo: true },
    { n: 3, vinculoId: 'v3', tercero: { id: 't3', nombre: 'Cementos Andes', identificacion: 'CO|NIT|901000222' }, tipoVinculo: { id: 'tv1', codigo: 'PROVEEDOR', nombre: 'Proveedor' }, estadoVinculo: 'Activo', venceEn: '2026-12-15', accion: 'Omitir', motivo: 'Tiene un expediente de actualización abierto: se renueva cuando se cierre.', conCorreo: false },
  ];
  return { filtro: FILTRO, total: 3, abririan: 1, recordarian: 1, omitidos: 1, conCorreo: 2, truncada: false, filas, ...cambios };
}

function campania(cambios: Partial<CampaniaDto> = {}): CampaniaDto {
  return {
    id: 'c1',
    nombre: 'Renovación de noviembre',
    filtro: FILTRO,
    estado: 'Procesando',
    totales: { filas: 10, procesadas: 4, pendientes: 6, exitosas: 3, fallidas: 0, omitidas: 1 },
    creadaPor: 'Usuario:u1',
    creadaEn: '2026-11-10T12:00:00Z',
    actualizadaEn: '2026-11-10T12:00:00Z',
    terminadaEn: null,
    reintentos: 0,
    version: 3,
    ...cambios,
  };
}

const FILA: FilaCampaniaDto = {
  n: 1, vinculoId: 'v1', terceroId: 't1', tercero: 'Aceros del Norte', identificacion: 'CO|NIT|900123456', tipoVinculo: 'Proveedor', estadoVinculo: 'PorVencer', venceEn: '2026-11-30',
  estado: 'Exitosa', accion: 'RenovacionAbierta', detalle: null, expedienteId: 'e1', folio: 'NOR-2026-00007', errores: [], intentos: 1, procesadaEn: '2026-11-10T12:00:01Z',
};

function UbicacionActual() {
  return <p>{`Ubicación: ${useLocation().pathname}`}</p>;
}

interface Peticion {
  url: string;
  metodo: string;
  cuerpo: unknown;
}

function montar(ruta: string, responder: (url: string, metodo: string) => Response) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = init?.method ?? 'GET';
      peticiones.push({ url, metodo, cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : null });
      return Promise.resolve(responder(url, metodo));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/e/:empresaId/vencimientos/campanias" element={<PaginaCampanias />} />
            <Route path="/e/:empresaId/vencimientos/campanias/nueva" element={<PaginaNuevaCampania />} />
            <Route path="/e/:empresaId/vencimientos/campanias/:campaniaId" element={<DetalleCampania />} />
            <Route path="*" element={<UbicacionActual />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

const envios = (peticiones: Peticion[], url: string) => peticiones.filter((p) => p.metodo === 'POST' && p.url === url);

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.getState().cerrar();
  vi.restoreAllMocks();
});

describe('nueva campaña de renovación', () => {
  const TIPOS = [{ id: 'tv1', codigo: 'PROVEEDOR', nombre: 'Proveedor', atributosPermitidos: [], exigeCuentaDesignada: false, activo: true }];
  const API = (vista: VistaPreviaCampaniaDto = vistaPrevia()) => (url: string, metodo: string) => {
    if (url === '/api/v1/campanias/vista-previa') return json(vista);
    if (url === '/api/v1/campanias' && metodo === 'POST') return json(campania(), 202);
    if (url.startsWith('/api/v1/tipos-vinculo')) return json(TIPOS);
    if (url.includes('/filas')) return json({ datos: [], siguienteCursor: null });
    return json(campania());
  };

  it('sin expedientes:administrar no deja crear', () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    montar('/e/emp/vencimientos/campanias/nueva', API());
    expect(screen.queryByRole('button', { name: 'Ver a quiénes afecta' })).not.toBeInTheDocument();
    expect(screen.getByText('Esta sección es de Campañas de renovación')).toBeInTheDocument();
  });

  it('la vista previa dice a quiénes afecta y no crea nada hasta confirmar el aviso', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesAdministrar, SCOPES.configuracionLeer]);
    const usuario = userEvent.setup();
    const peticiones = montar('/e/emp/vencimientos/campanias/nueva', API());

    // Filtro: 45 días, solo Proveedor y sin los vencidos.
    await usuario.clear(await screen.findByLabelText('Vencen en (días)'));
    await usuario.type(screen.getByLabelText('Vencen en (días)'), '45');
    await usuario.click(screen.getByRole('combobox', { name: 'Tipo de vínculo' }));
    await usuario.click(await screen.findByRole('option', { name: 'Proveedor' }));
    await usuario.click(screen.getByRole('button', { name: 'Vencido' }));
    expect(screen.getByRole('button', { name: 'Vencido' })).toHaveAttribute('aria-pressed', 'false');
    // Antes de pedir la vista previa no hay nada que confirmar.
    expect(screen.queryByRole('button', { name: 'Abrir renovaciones' })).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Ver a quiénes afecta' }));
    await waitFor(() => expect(envios(peticiones, '/api/v1/campanias/vista-previa')).toHaveLength(1));
    expect(envios(peticiones, '/api/v1/campanias/vista-previa')[0]?.cuerpo).toEqual({ diasParaVencer: 45, tipoVinculoId: 'tv1', estados: ['Activo', 'PorVencer'] });

    // Tarjetas con los totales y la tabla con la acción y el motivo de omisión de cada vínculo.
    expect(await screen.findByText('Se les abre la renovación')).toBeInTheDocument();
    expect(screen.getByText('Se les recuerda la renovación abierta')).toBeInTheDocument();
    expect(screen.getByText('Se omiten')).toBeInTheDocument();
    expect(screen.getByText('Reciben correo')).toBeInTheDocument();
    const tabla = screen.getByRole('table', { name: '2. Revisa y confirma' });
    expect(within(tabla).getByText('Aceros del Norte')).toBeInTheDocument();
    expect(within(tabla).getByText('NIT 900123456')).toBeInTheDocument();
    expect(within(tabla).getByText('30/11/2026')).toBeInTheDocument();
    expect(within(tabla).getByText('Tiene un expediente de actualización abierto: se renueva cuando se cierre.')).toBeInTheDocument();

    // «Abrir renovaciones» solo abre el aviso: no se ha creado nada.
    await usuario.click(screen.getByRole('button', { name: 'Abrir renovaciones' }));
    const aviso = await screen.findByRole('dialog', { name: /¿Abrir las renovaciones\?/ });
    expect(within(aviso).getByText('Se abrirá la renovación a 1 tercero.')).toBeInTheDocument();
    expect(within(aviso).getByText('Se le recordará la renovación que ya tiene abierta a 1 tercero.')).toBeInTheDocument();
    expect(within(aviso).getByText('1 vínculo se omite: no se le envía nada.')).toBeInTheDocument();
    expect(within(aviso).getByText('A 2 terceros les llega un correo, y a todos un aviso en su campana.')).toBeInTheDocument();
    expect(within(aviso).getByText(/segundo plano/)).toBeInTheDocument();
    expect(within(aviso).getByText(/pausarla o cancelarla/)).toBeInTheDocument();
    expect(envios(peticiones, '/api/v1/campanias')).toHaveLength(0);

    // Cancelar el aviso tampoco crea nada.
    await usuario.click(within(aviso).getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(envios(peticiones, '/api/v1/campanias')).toHaveLength(0);

    // Confirmar lo crea con el filtro de la vista previa y va a la campaña.
    await usuario.type(screen.getByLabelText('Nombre de la campaña'), 'Noviembre');
    await usuario.click(screen.getByRole('button', { name: 'Abrir renovaciones' }));
    await usuario.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Confirmar y abrir' }));
    await waitFor(() => expect(envios(peticiones, '/api/v1/campanias')).toHaveLength(1));
    expect(envios(peticiones, '/api/v1/campanias')[0]?.cuerpo).toEqual({ filtro: FILTRO, nombre: 'Noviembre' });
    expect(await screen.findByRole('heading', { name: 'Renovación de noviembre' })).toBeInTheDocument();
  });

  it('cambiar el filtro descarta la vista previa, y sin nada que hacer no deja abrir renovaciones', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesAdministrar]);
    const usuario = userEvent.setup();
    montar('/e/emp/vencimientos/campanias/nueva', API(vistaPrevia({ abririan: 0, recordarian: 0, omitidos: 3, conCorreo: 0 })));

    // Sin configuracion:leer no se ofrece elegir el tipo de vínculo.
    expect(screen.queryByRole('combobox', { name: 'Tipo de vínculo' })).not.toBeInTheDocument();
    await usuario.click(await screen.findByRole('button', { name: 'Ver a quiénes afecta' }));
    expect(await screen.findByText(/Ningún vínculo con este filtro necesita/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Abrir renovaciones' })).toBeDisabled();

    await usuario.click(screen.getByRole('button', { name: 'Activo' }));
    expect(screen.queryByText('Se omiten')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Abrir renovaciones' })).not.toBeInTheDocument();
  });

  it('con los días inválidos o sin estados no deja pedir la vista previa', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesAdministrar]);
    const usuario = userEvent.setup();
    montar('/e/emp/vencimientos/campanias/nueva', API());

    const boton = await screen.findByRole('button', { name: 'Ver a quiénes afecta' });
    await usuario.clear(screen.getByLabelText('Vencen en (días)'));
    expect(boton).toBeDisabled();
    await usuario.type(screen.getByLabelText('Vencen en (días)'), '400');
    expect(screen.getByText('Escribe un número de 0 a 365.')).toBeInTheDocument();
    expect(boton).toBeDisabled();
    await usuario.clear(screen.getByLabelText('Vencen en (días)'));
    await usuario.type(screen.getByLabelText('Vencen en (días)'), '30');
    for (const estado of ['Activo', 'Por vencer', 'Vencido']) await usuario.click(screen.getByRole('button', { name: estado }));
    expect(screen.getByText('Elige al menos un estado.')).toBeInTheDocument();
    expect(boton).toBeDisabled();
  });
});

describe('detalle de la campaña', () => {
  const API = (estado: CampaniaDto['estado'], totales: Partial<CampaniaDto['totales']> = {}) => (url: string, metodo: string) => {
    if (metodo === 'POST') return json(campania({ estado: 'Pausada' }));
    if (url.includes('/filas')) return json({ datos: [FILA], siguienteCursor: null });
    return json(campania({ estado, totales: { filas: 10, procesadas: 4, pendientes: 6, exitosas: 3, fallidas: 0, omitidas: 1, ...totales } }));
  };

  it('en proceso muestra el avance y las filas, y ofrece pausar y cancelar (cada una con su aviso)', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesAdministrar]);
    const usuario = userEvent.setup();
    const peticiones = montar('/e/emp/vencimientos/campanias/c1', API('Procesando'));

    expect(await screen.findByText('4 de 10')).toBeInTheDocument();
    expect(await screen.findByRole('link', { name: 'Renovación abierta · NOR-2026-00007' })).toHaveAttribute('href', '/e/emp/expedientes/e1');
    expect(screen.getByRole('link', { name: 'Aceros del Norte' })).toHaveAttribute('href', '/e/emp/vinculos/v1');
    expect(screen.getByText('30/11/2026')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reanudar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Reintentar/ })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cancelar campaña' })).toBeInTheDocument();

    // Pausar pide confirmar y dice cuántos vínculos quedan.
    await usuario.click(screen.getByRole('button', { name: 'Pausar' }));
    const dialogo = await screen.findByRole('dialog', { name: '¿Pausar la campaña?' });
    expect(within(dialogo).getByText(/Quedan 6 vínculos por procesar/)).toBeInTheDocument();
    expect(envios(peticiones, '/api/v1/campanias/c1/pausar')).toHaveLength(0);
    await usuario.click(within(dialogo).getByRole('button', { name: 'Pausar campaña' }));
    await waitFor(() => expect(envios(peticiones, '/api/v1/campanias/c1/pausar')).toHaveLength(1));
    await waitFor(() => expect(useAvisos.getState().actual).toMatchObject({ mensaje: 'Campaña pausada.', severidad: 'success' }));
  });

  it('cancelar avisa que lo ya abierto sigue su curso y no hace nada si se descarta', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesAdministrar]);
    const usuario = userEvent.setup();
    const peticiones = montar('/e/emp/vencimientos/campanias/c1', API('Procesando'));

    await usuario.click(await screen.findByRole('button', { name: 'Cancelar campaña' }));
    const dialogo = await screen.findByRole('dialog', { name: '¿Cancelar la campaña?' });
    expect(within(dialogo).getByText(/Lo que ya se abrió sigue su curso/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Cancelar' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(envios(peticiones, '/api/v1/campanias/c1/cancelar')).toHaveLength(0);
  });

  it('pausada se puede reanudar; terminada con errores, reintentar las fallidas', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesAdministrar]);
    const usuario = userEvent.setup();
    const peticiones = montar('/e/emp/vencimientos/campanias/c1', API('Pausada'));

    await usuario.click(await screen.findByRole('button', { name: 'Reanudar' }));
    expect(screen.queryByRole('button', { name: 'Pausar' })).not.toBeInTheDocument();
    const dialogo = await screen.findByRole('dialog', { name: '¿Reanudar la campaña?' });
    expect(within(dialogo).getByText(/recibe su aviso por correo y campana/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Reanudar campaña' }));
    await waitFor(() => expect(envios(peticiones, '/api/v1/campanias/c1/reanudar')).toHaveLength(1));
  });

  it('con errores ofrece reintentar las fallidas y no pausar ni cancelar', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesAdministrar]);
    const usuario = userEvent.setup();
    const peticiones = montar('/e/emp/vencimientos/campanias/c1', API('CompletadaConErrores', { procesadas: 10, pendientes: 0, exitosas: 7, fallidas: 2, omitidas: 1 }));

    await usuario.click(await screen.findByRole('button', { name: 'Reintentar 2 fallidas' }));
    expect(screen.queryByRole('button', { name: 'Pausar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cancelar campaña' })).not.toBeInTheDocument();
    const dialogo = await screen.findByRole('dialog', { name: '¿Reintentar las fallidas?' });
    expect(within(dialogo).getByText(/Se vuelven a procesar 2 vínculos que fallaron/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Reintentar fallidas' }));
    await waitFor(() => expect(envios(peticiones, '/api/v1/campanias/c1/reintentar')).toHaveLength(1));
  });

  it('terminada o cancelada solo deja descargar el informe; sin administrar tampoco hay acciones', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesAdministrar]);
    montar('/e/emp/vencimientos/campanias/c1', API('Cancelada'));
    expect(await screen.findByRole('button', { name: 'Descargar informe' })).toBeInTheDocument();
    for (const nombre of ['Pausar', 'Reanudar', 'Cancelar campaña', /^Reintentar/]) expect(screen.queryByRole('button', { name: nombre })).not.toBeInTheDocument();
  });

  it('sin expedientes:administrar no hay botones de acción aunque esté en proceso', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    montar('/e/emp/vencimientos/campanias/c1', API('Procesando'));
    expect(await screen.findByText('4 de 10')).toBeInTheDocument();
    for (const nombre of ['Pausar', 'Cancelar campaña']) expect(screen.queryByRole('button', { name: nombre })).not.toBeInTheDocument();
  });
});

describe('lista de campañas', () => {
  it('muestra el estado, el avance y el resultado; crear solo con expedientes:administrar', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer]);
    montar('/e/emp/vencimientos/campanias', () => json({ datos: [campania(), campania({ id: 'c2', nombre: 'Renovación de octubre', estado: 'CompletadaConErrores', totales: { filas: 5, procesadas: 5, pendientes: 0, exitosas: 3, fallidas: 1, omitidas: 1 } })], siguienteCursor: null }));

    expect(await screen.findByText('Renovación de noviembre')).toBeInTheDocument();
    expect(screen.getByText('Procesando')).toBeInTheDocument();
    expect(screen.getByText('Con errores')).toBeInTheDocument();
    expect(screen.getByText('4 de 10')).toBeInTheDocument();
    expect(screen.getByText('3 listas · 1 fallidas · 1 omitidas')).toBeInTheDocument();
    expect(screen.getAllByText('Vencen en 30 días · Todos los tipos de vínculo · Todos los estados').length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Nueva campaña' })).not.toBeInTheDocument();
  });

  it('con expedientes:administrar ofrece una campaña nueva', async () => {
    permisos.scopes = new Set([SCOPES.expedientesLeer, SCOPES.expedientesAdministrar]);
    const usuario = userEvent.setup();
    montar('/e/emp/vencimientos/campanias', () => json({ datos: [], siguienteCursor: null }));

    expect(await screen.findByText('Aún no hay campañas de renovación.')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Nueva campaña' }));
    expect(await screen.findByRole('heading', { name: 'Nueva campaña de renovación' })).toBeInTheDocument();
  });
});
