import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../shared/useAvisos';
import { AccionesRecordarEnLote } from './AccionesRecordarEnLote';
import { AvisoObservado } from './AvisoObservado';
import { RecordatorioAutomatico } from './RecordatorioAutomatico';

const json = (cuerpo: unknown, status = 200) =>
  new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' } });

interface Peticion {
  metodo: string;
  url: string;
  cuerpo: unknown;
  empresa: string | null;
}

/** API falsa: `responder` decide cada respuesta (404 por omisión); guarda método, URL, cuerpo y la empresa activa. */
function api(responder: (p: Peticion) => Response | undefined) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const cabeceras = new Headers(init?.headers);
      const peticion: Peticion = {
        metodo: init?.method ?? 'GET',
        url,
        cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null,
        empresa: cabeceras.get('X-Empresa'),
      };
      peticiones.push(peticion);
      return Promise.resolve(responder(peticion) ?? json({ codigo: 'no-encontrado' }, 404));
    }),
  });
  return peticiones;
}

function pintar(contenido: React.ReactNode) {
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <MemoryRouter>{contenido}</MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
}

const expediente = (cambios: Partial<ExpedienteDto> = {}): ExpedienteDto =>
  ({
    id: 'exp-1',
    folio: 'NORTE-E-2026-000001',
    tercero: { id: 't1', nombre: 'Aceros del Norte', identificacion: 'NIT 900123456' },
    estado: 'Observado',
    ...cambios,
  }) as ExpedienteDto;

afterEach(() => {
  reiniciarCliente();
  useAvisos.getState().cerrar();
  vi.restoreAllMocks();
});

describe('«Recordar al tercero» en un expediente observado', () => {
  it('sin permiso de vínculos solo muestra el aviso', () => {
    pintar(<AvisoObservado expediente={expediente()} empresaId="emp" scopes={new Set([SCOPES.expedientesLeer])} texto="Cumplimiento observó." />);
    expect(screen.getByText('Cumplimiento observó.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Recordar al tercero' })).not.toBeInTheDocument();
  });

  it('pide confirmación, recuerda con la empresa activa y lo avisa', async () => {
    const usuario = userEvent.setup();
    const peticiones = api((p) => (p.metodo === 'POST' ? json(expediente()) : undefined));
    pintar(<AvisoObservado expediente={expediente()} empresaId="emp" scopes={new Set([SCOPES.vinculosEscribir])} texto="Cumplimiento observó." />);

    await usuario.click(screen.getByRole('button', { name: 'Recordar al tercero' }));
    const dialogo = await screen.findByRole('dialog', { name: /Recordarle sus observaciones al tercero/ });
    expect(within(dialogo).getByText(/Aceros del Norte · NORTE-E-2026-000001/)).toBeInTheDocument();
    expect(peticiones.filter((p) => p.metodo === 'POST')).toHaveLength(0);
    await usuario.click(within(dialogo).getByRole('button', { name: 'Enviar recordatorio' }));

    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Le recordamos al tercero sus observaciones.'));
    const envio = peticiones.find((p) => p.metodo === 'POST');
    expect(envio?.url).toBe('/api/v1/expedientes/exp-1/recordar-observaciones');
    expect(envio?.empresa).toBe('emp');
  });

  it('si ya se le recordó hoy el botón queda deshabilitado y se muestra el último recordatorio', () => {
    pintar(
      <AvisoObservado
        expediente={expediente({ recordatorioObservaciones: { ultimoEn: new Date().toISOString(), total: 2 } })}
        empresaId="emp"
        scopes={new Set([SCOPES.vinculosEscribir])}
        texto="Cumplimiento observó."
      />,
    );
    expect(screen.getByRole('button', { name: 'Recordar al tercero' })).toBeDisabled();
    expect(screen.getByText(/Último recordatorio: .* \(2 en total\)\./)).toBeInTheDocument();
  });

  it('si el servidor responde que ya se recordó hoy muestra su mensaje', async () => {
    const usuario = userEvent.setup();
    api(() => json({ codigo: 'recordatorio-ya-enviado-hoy', detail: 'Ya se le recordó hoy al tercero: se puede volver a recordar mañana.' }, 409));
    pintar(<AvisoObservado expediente={expediente()} empresaId="emp" scopes={new Set([SCOPES.vinculosEscribir])} texto="Cumplimiento observó." />);

    await usuario.click(screen.getByRole('button', { name: 'Recordar al tercero' }));
    await usuario.click(await screen.findByRole('button', { name: 'Enviar recordatorio' }));
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toMatch(/Ya se le recordó hoy al tercero/));
    expect(useAvisos.getState().actual?.severidad).toBe('error');
  });
});

describe('recordatorio en lote desde la bandeja', () => {
  const montarLote = (seleccion: string[], area = '', areaNombre?: string) => {
    const terminado = vi.fn();
    const peticiones = api(() => json({ enviados: 2, omitidos: 1, detalle: [{ expedienteId: 'a', folio: 'A', resultado: 'Enviado' }, { expedienteId: 'b', folio: 'B', resultado: 'Enviado' }, { expedienteId: 'c', folio: 'C', resultado: 'YaRecordadoHoy' }] }));
    pintar(<AccionesRecordarEnLote empresaId="emp" empresaNombre="Constructora Norte" seleccion={new Set(seleccion)} areaId={area} areaNombre={areaNombre} onTerminado={terminado} />);
    return { peticiones, terminado };
  };

  it('con expedientes seleccionados recuerda solo esos, confirma antes y resume el resultado', async () => {
    const usuario = userEvent.setup();
    const { peticiones, terminado } = montarLote(['e1', 'e2']);

    await usuario.click(screen.getByRole('button', { name: 'Recordar (2)' }));
    const dialogo = await screen.findByRole('dialog', { name: '¿Recordar las observaciones de 2 expedientes?' });
    await usuario.click(within(dialogo).getByRole('button', { name: 'Enviar recordatorios' }));

    await waitFor(() => expect(terminado).toHaveBeenCalled());
    expect(useAvisos.getState().actual?.mensaje).toBe('Recordamos a 2 terceros. 1 omitido: 1 ya se les recordó hoy.');
    const envio = peticiones.find((p) => p.metodo === 'POST');
    expect(envio?.url).toBe('/api/v1/expedientes/recordar-observaciones');
    expect(envio?.cuerpo).toEqual({ expedientes: ['e1', 'e2'] });
    expect(envio?.empresa).toBe('emp');
  });

  it('sin selección solo ofrece «Recordar a los observados»; con un área filtrada lo dice y la manda', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = montarLote([], 'area-1', 'Cumplimiento');

    expect(screen.queryByRole('button', { name: /^Recordar \(/ })).not.toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Recordar a los observados' }));
    const dialogo = await screen.findByRole('dialog', { name: '¿Recordar a todos los terceros con observaciones pendientes?' });
    expect(within(dialogo).getByText(/que pasa por el área Cumplimiento/)).toBeInTheDocument();
    expect(within(dialogo).getByText(/A quien ya se le recordó hoy no se le vuelve a enviar/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Enviar recordatorios' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'POST')?.cuerpo).toEqual({ todos: true, area: 'area-1' });
  });

  it('sin filtro de área dice que son todos los de la empresa', async () => {
    const usuario = userEvent.setup();
    const { peticiones } = montarLote([]);

    await usuario.click(screen.getByRole('button', { name: 'Recordar a los observados' }));
    expect(await screen.findByText(/con un expediente devuelto en Constructora Norte/)).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Enviar recordatorios' }));
    await waitFor(() => expect(peticiones.find((p) => p.metodo === 'POST')?.cuerpo).toEqual({ todos: true, area: null }));
  });
});

describe('recordatorio automático en Plantillas', () => {
  it('apagado por omisión: se enciende, se elige cada cuántos días y se guarda con la empresa activa', async () => {
    const usuario = userEvent.setup();
    const peticiones = api((p) => (p.metodo === 'PUT' ? json({ activo: true, cadaDias: 5, actualizadoEn: '2026-11-10T12:00:00Z' }) : json({ activo: false, cadaDias: 3, actualizadoEn: null })));
    pintar(<RecordatorioAutomatico empresaId="emp" puedeEscribir />);

    const interruptor = await screen.findByRole('switch', { name: 'Enviar recordatorios automáticos' });
    expect(interruptor).not.toBeChecked();
    // La tarjeta avisa que cada flujo puede sobrescribirlo (OP-19).
    expect(screen.getByText(/Cada flujo puede sobrescribirlo/)).toBeInTheDocument();
    expect(screen.getByLabelText('Cada (días)')).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();

    await usuario.click(interruptor);
    const dias = screen.getByLabelText('Cada (días)');
    await usuario.clear(dias);
    await usuario.type(dias, '0');
    expect(screen.getByText('Escribe un número entero de 1 a 60.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();
    await usuario.clear(dias);
    await usuario.type(dias, '5');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Guardamos el recordatorio automático.'));
    const guardado = peticiones.find((p) => p.metodo === 'PUT');
    expect(guardado?.url).toBe('/api/v1/configuracion/recordatorio-observaciones');
    expect(guardado?.cuerpo).toEqual({ activo: true, cadaDias: 5 });
    expect(guardado?.empresa).toBe('emp');
  });

  it('sin permiso de escritura se ve pero no se cambia', async () => {
    api(() => json({ activo: true, cadaDias: 2, actualizadoEn: '2026-11-10T12:00:00Z' }));
    pintar(<RecordatorioAutomatico empresaId="emp" puedeEscribir={false} />);

    expect(await screen.findByRole('switch', { name: 'Enviar recordatorios automáticos' })).toBeDisabled();
    expect(screen.getByLabelText('Cada (días)')).toBeDisabled();
    expect(screen.getByText('Solo quien administra la configuración puede cambiarlo.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Guardar' })).not.toBeInTheDocument();
  });
});
