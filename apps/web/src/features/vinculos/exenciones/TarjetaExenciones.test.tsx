import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type ExencionDeVinculoDto, type VinculoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { TarjetaExenciones } from './TarjetaExenciones';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const json = (estado: number, cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

const TERCERO = {
  id: 't1',
  identificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456', digitoVerificacion: '8' },
  tipoPersona: 'Juridica',
  nombre: 'Ferretería El Tornillo',
  nombreComercial: null,
  contactos: [],
  esExtranjero: false,
  creadoEn: '2026-11-01T00:00:00Z',
};

function exencion(parcial: Partial<ExencionDeVinculoDto>): ExencionDeVinculoDto {
  return {
    id: 'ex1',
    todos: false,
    objetivos: [{ tipo: 'Documento', nombre: 'Registro Único Tributario' }],
    justificacion: 'La renueva Contabilidad.',
    hasta: null,
    por: '00000000-0000-0000-0000-0000000000a1',
    creadaEn: '2026-11-10T15:00:00Z',
    estado: 'Vigente',
    ...parcial,
  };
}

function vinculo(parcial: Partial<VinculoDto> = {}): VinculoDto {
  return {
    id: 'v1',
    empresaId: 'emp',
    terceroId: 't1',
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    flujoId: null,
    estado: 'Activo',
    atributos: {},
    correoTercero: 'contacto@tornillo.co',
    invitacion: null,
    invitacionAceptadaEn: null,
    requisitosCubribles: null,
    bloqueo: null,
    referencias: [],
    origen: 'Api',
    creadoEn: '2026-11-01T00:00:00Z',
    actualizadoEn: '2026-11-10T15:00:00Z',
    version: 7,
    ...parcial,
  };
}

const EXENCIONES = [
  exencion({ hasta: '2026-12-31' }),
  exencion({ id: 'ex2', estado: 'Revocada', revocadaPor: 'otro', revocadaEn: '2026-11-11T15:00:00Z', justificacionDeRevocacion: 'Ya no aplica.', objetivos: [{ tipo: 'Seccion', nombre: 'Información general' }], justificacion: 'Se actualiza a mano.' }),
  exencion({ id: 'ex3', estado: 'Vencida', hasta: '2026-10-31', todos: true, objetivos: [], justificacion: 'Proveedor estratégico.' }),
];

interface Peticion {
  url: string;
  metodo: string;
  cabeceras: Headers;
  cuerpo: unknown;
}

function montar(v: VinculoDto) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      if (url.includes('/terceros/')) return Promise.resolve(json(200, TERCERO));
      if (url.includes('/usuarios/resumen')) return Promise.resolve(json(200, [{ id: '00000000-0000-0000-0000-0000000000a1', nombre: 'Ana Gómez' }]));
      peticiones.push({ url, metodo: init?.method ?? 'GET', cabeceras: new Headers(init?.headers), cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : null });
      return Promise.resolve(json(200, exencion({ estado: 'Revocada' })));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <TarjetaExenciones vinculo={v} empresaId="emp" />
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

describe('TarjetaExenciones (EX-6)', () => {
  it('lista cada exención con su estado, alcance, justificación, vigencia y quién la hizo, y cómo terminaron las que ya no rigen', async () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer]);
    montar(vinculo({ exenciones: EXENCIONES }));

    const items = await screen.findAllByRole('listitem');
    expect(items).toHaveLength(3);
    const [vigente, revocada, vencida] = items as [HTMLElement, HTMLElement, HTMLElement];
    expect(within(vigente).getByText('Vigente')).toBeInTheDocument();
    expect(within(vigente).getByText('Registro Único Tributario')).toBeInTheDocument();
    expect(within(vigente).getByText('La renueva Contabilidad.')).toBeInTheDocument();
    expect(await within(vigente).findByText(/Hasta el 31\/12\/2026 · Por Ana Gómez el/)).toBeInTheDocument();
    expect(within(revocada).getByText('Revocada')).toBeInTheDocument();
    expect(within(revocada).getByText(/Revocada por una persona el .*: Ya no aplica\./)).toBeInTheDocument();
    expect(within(vencida).getByText('Vencida')).toBeInTheDocument();
    expect(within(vencida).getByText('Todos los requisitos y la vigencia del vínculo')).toBeInTheDocument();
    expect(within(vencida).getByText('Venció el 31/10/2026')).toBeInTheDocument();
    // Solo lectura: nada de eximir ni de revocar.
    expect(screen.queryByRole('button', { name: 'Eximir' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Revocar/ })).not.toBeInTheDocument();
  });

  it('sin exenciones y sin permiso para eximir la tarjeta no aparece', () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer]);
    montar(vinculo());

    expect(screen.queryByText('Exenciones')).not.toBeInTheDocument();
  });

  it('con el permiso y sin exenciones ofrece eximir con su explicación', async () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer, SCOPES.vinculosEximir]);
    const usuario = userEvent.setup();
    montar(vinculo());

    expect(screen.getByText('Este vínculo no tiene exenciones.')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Eximir' }));
    expect(await screen.findByRole('dialog', { name: /Eximir al vínculo/ })).toBeInTheDocument();
    expect(await screen.findByText('Ferretería El Tornillo')).toBeInTheDocument();
  });

  it('un vínculo bloqueado o terminado no ofrece eximir (la API tampoco lo admite)', () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer, SCOPES.vinculosEximir]);
    montar(vinculo({ estado: 'Bloqueado', exenciones: [exencion({})] }));

    expect(screen.queryByRole('button', { name: 'Eximir' })).not.toBeInTheDocument();
    // Pero se puede revocar una exención de un vínculo bloqueado.
    expect(screen.getByRole('button', { name: /Revocar la exención de Registro Único Tributario/ })).toBeInTheDocument();
  });

  it('revoca una exención vigente con su justificación y la API recibe la empresa activa', async () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer, SCOPES.vinculosEximir]);
    const usuario = userEvent.setup();
    const peticiones = montar(vinculo({ exenciones: EXENCIONES }));

    // Solo la vigente se puede revocar.
    const botones = await screen.findAllByRole('button', { name: /Revocar la exención/ });
    expect(botones).toHaveLength(1);
    await usuario.click(botones[0]!);

    const dialogo = await screen.findByRole('dialog', { name: /Revocar la exención/ });
    const confirmar = within(dialogo).getByRole('button', { name: 'Revocar exención' });
    expect(confirmar).toBeDisabled();
    expect(within(dialogo).getByRole('status')).toHaveTextContent('Escribe por qué revocas la exención.');
    await usuario.type(within(dialogo).getByLabelText(/Justificación/), 'Cambió la política.');
    await usuario.click(confirmar);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const peticion = peticiones.find((p) => p.metodo === 'POST');
    expect(peticion?.url).toBe('/api/v1/vinculos/v1/exenciones/ex1/revocar');
    expect(peticion?.cabeceras.get('X-Empresa')).toBe('emp');
    expect(peticion?.cuerpo).toEqual({ justificacion: 'Cambió la política.' });
    expect(useAvisos.getState().actual?.mensaje).toBe('Exención revocada. El vínculo vuelve a regirse por sus vigencias.');
  });
});
