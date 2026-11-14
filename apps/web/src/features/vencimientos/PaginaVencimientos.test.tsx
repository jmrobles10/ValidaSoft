import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type RecordatorioEnviadoDto, type VencimientoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../shared/useAvisos';
import { PaginaVencimientos } from './PaginaVencimientos';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const TERCERO = { id: 't1', nombre: 'Ferretería El Tornillo', identificacion: 'NIT 900123456' };
const OTRO = { id: 't2', nombre: 'Aceros del Norte', identificacion: 'NIT 800555111' };

function vencimiento(parcial: Partial<VencimientoDto>): VencimientoDto {
  return {
    vinculoId: 'v1',
    documentoId: 'd1',
    tercero: TERCERO,
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    documento: 'RUT',
    version: 2,
    vigenteHasta: '2026-11-19',
    diasRestantes: 10,
    estado: 'PorVencer',
    base: 'FechaExpedicion',
    finDeGracia: null,
    permiteRatificar: false,
    ultimoRecordatorio: null,
    ...parcial,
  };
}

const FILAS: VencimientoDto[] = [
  vencimiento({ documentoId: 'd0', documento: 'Cámara de comercio', vigenteHasta: '2026-11-01', diasRestantes: -8, estado: 'Vencida', base: 'FechaCarga' }),
  vencimiento({ vinculoId: 'v2', tercero: OTRO, documentoId: 'd3', documento: 'Póliza', vigenteHasta: '2026-11-06', diasRestantes: -3, estado: 'EnGracia', finDeGracia: '2026-11-21', base: 'VencimientoPropio' }),
  vencimiento({ documentoId: 'd4', documento: 'Certificado bancario', vigenteHasta: '2026-11-09', diasRestantes: 0, estado: 'PorVencer', base: 'Ratificacion', permiteRatificar: true }),
  vencimiento({}),
];

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

function UbicacionActual() {
  return <p>{`Ubicación: ${useLocation().pathname}`}</p>;
}

/** API falsa: el GET devuelve `filas`; el POST de recordar, `recordatorio`. Guarda cada petición. */
function montar(filas: VencimientoDto[], recordatorio: RecordatorioEnviadoDto = { correos: 1, documentos: 1, omitidos: 0 }, ruta = '/e/emp/vencimientos') {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      peticiones.push({ url, metodo, cuerpo });
      if (url.startsWith('/api/v1/vencimientos/excel')) {
        return Promise.resolve(new Response(new Uint8Array([1, 2, 3]), { headers: { 'Content-Disposition': 'attachment; filename=vencimientos-norte.xlsx' } }));
      }
      return Promise.resolve(json(metodo === 'POST' ? recordatorio : filas));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <Routes>
            <Route path="/e/:empresaId/vencimientos" element={<PaginaVencimientos />} />
            <Route path="/e/:empresaId/vinculos/:vinculoId" element={<UbicacionActual />} />
            <Route path="/e/:empresaId/vencimientos/campanias" element={<UbicacionActual />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.getState().cerrar();
  vi.restoreAllMocks();
});

describe('Vencimientos', () => {
  it('sin documentos:leer no muestra la tabla', () => {
    montar(FILAS);
    expect(screen.queryByRole('table')).not.toBeInTheDocument();
  });

  it('muestra cada documento con su estado, los días en palabras y la base; pide 30 días por defecto', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer]);
    const peticiones = montar(FILAS);

    const fila = (await screen.findByText('Póliza')).closest('tr');
    if (!fila) throw new Error('sin fila');
    expect(within(fila).getByText('En gracia')).toBeInTheDocument();
    expect(within(fila).getByText('Venció hace 3 días')).toBeInTheDocument();
    expect(within(fila).getByText('Vencimiento del documento')).toBeInTheDocument();
    expect(within(fila).getByText('06/11/2026')).toBeInTheDocument();
    expect(screen.getByText('Venció hace 8 días')).toBeInTheDocument();
    expect(screen.getByText('Vence hoy')).toBeInTheDocument();
    expect(screen.getByText('Vence en 10 días')).toBeInTheDocument();
    expect(screen.getByText('Vencida')).toBeInTheDocument();
    expect(screen.getByText('Ratificación del tercero')).toBeInTheDocument();
    expect(screen.getAllByText('Ratificable', { selector: 'span' })).toHaveLength(1);
    expect(peticiones[0]).toMatchObject({ url: '/api/v1/vencimientos?dias=30', metodo: 'GET' });
    // Sin vinculos:escribir no se ofrece recordar ni hay casillas.
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Recordar al tercero/ })).not.toBeInTheDocument();
  });

  it('con expedientes:leer ofrece ir a las campañas de renovación; sin él no', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer, SCOPES.expedientesLeer]);
    const usuario = userEvent.setup();
    montar(FILAS);

    await usuario.click(await screen.findByRole('button', { name: 'Campañas de renovación' }));
    expect(await screen.findByText('Ubicación: /e/emp/vencimientos/campanias')).toBeInTheDocument();
  });

  it('sin expedientes:leer no hay botón de campañas', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer]);
    montar(FILAS);

    await screen.findByText('Póliza');
    expect(screen.queryByRole('button', { name: 'Campañas de renovación' })).not.toBeInTheDocument();
  });

  it('el horizonte de la URL viaja en la consulta y el vacío lo nombra', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer]);
    const peticiones = montar([], undefined, '/e/emp/vencimientos?dias=15');
    expect(await screen.findByText('No hay documentos que venzan en los próximos 15 días.')).toBeInTheDocument();
    expect(peticiones[0]?.url).toBe('/api/v1/vencimientos?dias=15');
  });

  it('al hacer clic en una fila va al vínculo', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer]);
    const usuario = userEvent.setup();
    montar(FILAS);
    await usuario.click(await screen.findByText('Póliza'));
    expect(await screen.findByText('Ubicación: /e/emp/vinculos/v2')).toBeInTheDocument();
  });

  it('selecciona y recuerda: envía el POST con los documentos elegidos y avisa los omitidos', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer, SCOPES.vinculosEscribir]);
    const usuario = userEvent.setup();
    const peticiones = montar(FILAS, { correos: 2, documentos: 2, omitidos: 1 });

    await usuario.click(await screen.findByRole('checkbox', { name: 'Seleccionar Póliza de Aceros del Norte' }));
    await usuario.click(screen.getByRole('checkbox', { name: 'Seleccionar Cámara de comercio de Ferretería El Tornillo' }));
    await usuario.click(screen.getByRole('checkbox', { name: 'Seleccionar RUT de Ferretería El Tornillo' }));
    // Marcar la casilla no navega al vínculo.
    expect(screen.queryByText(/Ubicación:/)).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Recordar al tercero (3)' }));
    const dialogo = await screen.findByRole('dialog');
    expect(within(dialogo).getByText('¿Recordarle 3 documentos a los terceros?')).toBeInTheDocument();
    // Antes de enviar dice a cuántos terceros les llega correo (uno por vínculo: dos de los tres documentos son del mismo).
    expect(within(dialogo).getByText(/Les llega un correo a 2 terceros/)).toBeInTheDocument();
    expect(within(dialogo).getByText(/a lo sumo una vez por día/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Enviar recordatorio' }));

    await waitFor(() =>
      expect(peticiones).toContainEqual({
        url: '/api/v1/vencimientos/recordar',
        metodo: 'POST',
        cuerpo: {
          vencimientos: [
            { vinculoId: 'v1', documentoId: 'd0' },
            { vinculoId: 'v2', documentoId: 'd3' },
            { vinculoId: 'v1', documentoId: 'd1' },
          ],
        },
      }),
    );
    await waitFor(() =>
      expect(useAvisos.getState().actual).toMatchObject({
        mensaje: 'Enviaste 2 recordatorios a 2 terceros. 1 se omitió (ya se recordó hoy o el vínculo no tiene correo).',
        severidad: 'success',
      }),
    );
    expect(await screen.findByRole('button', { name: 'Recordar al tercero' })).toBeDisabled();
  });

  it('descarga el Excel con los filtros y el nombre que da la API', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer]);
    const usuario = userEvent.setup();
    const crear = vi.fn(() => 'blob:vencimientos');
    Object.assign(URL, { createObjectURL: crear, revokeObjectURL: vi.fn() });
    const clic = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => undefined);
    const peticiones = montar(FILAS, undefined, '/e/emp/vencimientos?estado=Vencida');

    await screen.findByText('Póliza');
    await usuario.click(screen.getByRole('button', { name: 'Descargar Excel' }));

    await waitFor(() => expect(clic).toHaveBeenCalled());
    expect(peticiones).toContainEqual({ url: '/api/v1/vencimientos/excel?dias=30&estado=Vencida', metodo: 'GET', cuerpo: null });
    expect(crear).toHaveBeenCalled();
    expect((clic.mock.contexts[0] as HTMLAnchorElement).download).toBe('vencimientos-norte.xlsx');
  });
});

describe('Vencimientos y exención del vínculo (EX-6)', () => {
  const EXENTA: VencimientoDto = vencimiento({
    documentoId: 'd5',
    documento: 'Póliza de cumplimiento',
    exencion: { id: 'ex1', todos: false, justificacion: 'La renueva el área legal.', hasta: '2026-12-31' },
    tipoDocumentoTenantId: 'tipo-poliza',
  });
  const NORMAL: VencimientoDto = vencimiento({ vinculoId: 'v2', tercero: OTRO, documentoId: 'd6', documento: 'RUT', tipoDocumentoTenantId: 'tipo-rut' });

  it('marca con «Exento» la fila que el vínculo tiene eximida y no marca las demás', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer]);
    montar([EXENTA, NORMAL]);

    const fila = (await screen.findByText('Póliza de cumplimiento')).closest('tr');
    const otra = screen.getByText('RUT').closest('tr');
    if (!fila || !otra) throw new Error('sin fila');
    expect(within(fila).getByText('Exento')).toBeInTheDocument();
    expect(within(otra).queryByText('Exento')).not.toBeInTheDocument();
  });

  it('el filtro de exención viaja en la consulta y se lee de la URL', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer]);
    const peticiones = montar([EXENTA], undefined, '/e/emp/vencimientos?exento=true');

    await screen.findByText('Póliza de cumplimiento');
    expect(peticiones[0]?.url).toBe('/api/v1/vencimientos?dias=30&exento=true');
  });

  it('elegir «No exentos» en el filtro pide solo esos y se puede limpiar', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer]);
    const usuario = userEvent.setup();
    const peticiones = montar([NORMAL]);

    await screen.findByText('RUT');
    await usuario.click(screen.getByRole('button', { name: /Exención/ }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'No exentos' }));

    await waitFor(() => expect(peticiones.map((p) => p.url)).toContain('/api/v1/vencimientos?dias=30&exento=false'));
    await usuario.click(await screen.findByRole('button', { name: 'Limpiar filtros' }));
    await waitFor(() => expect(peticiones.at(-1)?.url).toBe('/api/v1/vencimientos?dias=30'));
  });

  it('sin vinculos:eximir la fila no ofrece eximir ni revocar', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer]);
    montar([EXENTA, NORMAL]);

    await screen.findByText('RUT');
    expect(screen.queryByRole('button', { name: /Eximir/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Revocar/ })).not.toBeInTheDocument();
  });

  it('con vinculos:eximir eximir desde la fila abre el diálogo del tercero y la acción no navega al vínculo', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer, SCOPES.vinculosEximir]);
    const usuario = userEvent.setup();
    const peticiones = montar([EXENTA, NORMAL]);

    await usuario.click(await screen.findByRole('button', { name: 'Eximir RUT de Aceros del Norte' }));

    const dialogo = await screen.findByRole('dialog', { name: /Eximir al vínculo/ });
    expect(within(dialogo).getByText('Aceros del Norte')).toBeInTheDocument();
    expect(screen.queryByText(/Ubicación:/)).not.toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('radio', { name: /De todo/ }));
    await usuario.type(within(dialogo).getByLabelText(/Justificación/), 'Cliente estratégico.');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Eximir' }));

    await waitFor(() =>
      expect(peticiones).toContainEqual({
        url: '/api/v1/vinculos/v2/exenciones',
        metodo: 'POST',
        cuerpo: { todos: true, objetivos: null, justificacion: 'Cliente estratégico.', hasta: null },
      }),
    );
  });

  it('la fila exenta ofrece revocar, con su justificación obligatoria', async () => {
    permisos.scopes = new Set([SCOPES.documentosLeer, SCOPES.vinculosEximir]);
    const usuario = userEvent.setup();
    const peticiones = montar([EXENTA]);

    await usuario.click(await screen.findByRole('button', { name: 'Revocar la exención de Póliza de cumplimiento' }));

    const dialogo = await screen.findByRole('dialog', { name: /Revocar la exención/ });
    expect(within(dialogo).getByRole('button', { name: 'Revocar exención' })).toBeDisabled();
    await usuario.type(within(dialogo).getByLabelText(/Justificación/), 'Ya no aplica.');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Revocar exención' }));

    await waitFor(() =>
      expect(peticiones).toContainEqual({ url: '/api/v1/vinculos/v1/exenciones/ex1/revocar', metodo: 'POST', cuerpo: { justificacion: 'Ya no aplica.' } }),
    );
  });
});
