import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type FichaAceptadaDelVinculoDto, type SeccionAceptadaVistaDto, type VinculoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaFichaAceptada } from './PaginaFichaAceptada';
import { resumirFichaAceptada } from './resumen';
import { TarjetaFichaAceptadaEnVinculo } from './TarjetaFichaAceptadaEnVinculo';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const TODOS = [SCOPES.tercerosLeer, SCOPES.vinculosLeer];

const VINCULO = {
  id: 'v1',
  empresaId: 'emp',
  terceroId: 't1',
  tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
  flujoId: null,
  estado: 'Activo',
  atributos: {},
  correoTercero: 'compras@tornillo.co',
  invitacion: null,
  invitacionAceptadaEn: '2026-09-01T15:00:00Z',
  requisitosCubribles: null,
  bloqueo: null,
  referencias: [],
  origen: 'Invitacion',
  creadoEn: '2026-08-01T15:00:00Z',
  actualizadoEn: '2026-10-01T15:00:00Z',
  version: 3,
} satisfies VinculoDto;

const TERCERO = {
  id: 't1',
  identificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456', digitoVerificacion: '8' },
  tipoPersona: 'Juridica',
  nombre: 'Ferretería El Tornillo',
  nombreComercial: null,
  contactos: [],
  esExtranjero: false,
  creadoEn: '2026-08-01T15:00:00Z',
};

const RUT = { documentoId: 'd1', version: 2, tipoDocumento: 'RUT', nombre: 'Registro Único Tributario', retirado: false };

/** General aceptada (v2, con el RUT) y, aparte, una versión 3 del tercero que cambió la razón social con el mismo RUT (sin soporte). */
const GENERAL: SeccionAceptadaVistaDto = {
  seccion: 'FICHA_GENERAL',
  nombre: 'Información general',
  periodo: null,
  aceptada: {
    version: 2,
    aceptadaEn: '2026-10-20T15:00:00Z',
    expedienteId: 'e1',
    folio: 'EXP-0007',
    origen: 'Expediente',
    datos: { razon_social: 'Ferretería El Tornillo S.A.S.', pais: 'CO' },
    soportes: [RUT],
  },
  cambioPosterior: {
    version: 3,
    fecha: '2026-11-05T15:00:00Z',
    cambios: [{ campo: 'razon_social', etiqueta: 'Razón social', anterior: 'Ferretería El Tornillo S.A.S.', nuevo: 'Ferretería El Tornillo y Cía. S.A.S.', tipoCambio: 'Modificado' }],
    soportes: [RUT],
    sinSoporte: { critico: true, cambios: [{ campo: 'razon_social', etiqueta: 'Razón social', anterior: 'Ferretería El Tornillo S.A.S.', nuevo: 'Ferretería El Tornillo y Cía. S.A.S.', tipoCambio: 'Modificado' }] },
    revalidacion: { expedienteId: 'e2', folio: 'EXP-0009', estado: 'EnRevision', version: 3, plazoHasta: '2026-11-20T15:00:00Z' },
  },
};

/** Tributaria aceptada (v1), sin soporte y sin nada posterior; y la financiera 2025 importada del sistema de origen. */
const TRIBUTARIA: SeccionAceptadaVistaDto = {
  seccion: 'FICHA_TRIBUTARIA',
  nombre: 'Información tributaria',
  periodo: null,
  aceptada: { version: 1, aceptadaEn: '2026-10-01T15:00:00Z', expedienteId: 'e1', folio: 'EXP-0007', origen: 'Expediente', datos: { responsabilidades: ['48'] }, soportes: [] },
  cambioPosterior: null,
};

const FINANCIERA: SeccionAceptadaVistaDto = {
  seccion: 'FICHA_FINANCIERA',
  nombre: 'Información financiera',
  periodo: 2025,
  aceptada: { version: 1, aceptadaEn: '2026-09-02T15:00:00Z', expedienteId: null, folio: null, origen: 'Importacion', datos: { periodo: 2025, activos: 100 }, soportes: [{ ...RUT, retirado: true }] },
  cambioPosterior: null,
};

const ficha = (secciones: SeccionAceptadaVistaDto[]): FichaAceptadaDelVinculoDto => ({ vinculoId: 'v1', terceroId: 't1', tipoPersona: 'Juridica', secciones });

const respuesta = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': estado === 200 ? 'application/json' : 'application/problem+json' } });

/** API falsa: el vínculo, el tercero y la ficha aceptada (o el 404). Devuelve las URL pedidas. */
function montar(resultado: FichaAceptadaDelVinculoDto | 404, elemento: 'pagina' | 'tarjeta' = 'pagina') {
  const urls: string[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      const url = entrada instanceof Request ? entrada.url : entrada.toString();
      urls.push(url);
      if (url.endsWith('/ficha-aceptada')) {
        return Promise.resolve(resultado === 404 ? respuesta(404, { status: 404, codigo: 'vinculo-no-encontrado', detail: 'No existe ese vínculo.' }) : respuesta(200, resultado));
      }
      if (url.endsWith('/api/v1/vinculos/v1')) return Promise.resolve(respuesta(200, VINCULO));
      if (url.endsWith('/api/v1/terceros/t1')) return Promise.resolve(respuesta(200, TERCERO));
      return Promise.resolve(respuesta(404, { status: 404, codigo: 'no-encontrado', detail: url }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={['/e/emp/vinculos/v1/ficha']}>
          <Routes>
            <Route path="/e/:empresaId/vinculos/:vinculoId/ficha" element={elemento === 'pagina' ? <PaginaFichaAceptada /> : <TarjetaFichaAceptadaEnVinculo vinculo={VINCULO} />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return urls;
}

const seccion = (nombre: string) => screen.getByRole('region', { name: nombre });

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  vi.restoreAllMocks();
});

describe('Ficha aceptada del vínculo (FD-3)', () => {
  it('sin terceros:leer y vinculos:leer no pide la ficha', () => {
    const urls = montar(ficha([GENERAL]));
    expect(screen.getByText('Esta sección es de Ficha aceptada')).toBeInTheDocument();
    expect(urls.some((u) => u.endsWith('/ficha-aceptada'))).toBe(false);
  });

  it('separa lo que la empresa aceptó, con su versión y su soporte, de la versión nueva que el tercero guardó después', async () => {
    permisos.scopes = new Set(TODOS);
    const usuario = userEvent.setup();
    montar(ficha([GENERAL, TRIBUTARIA, FINANCIERA]));

    expect(await screen.findByRole('heading', { level: 1, name: 'Ferretería El Tornillo' })).toBeInTheDocument();
    // Un solo aviso agrupado (no uno por sección) dice cuántas tienen una versión nueva pendiente de revisión.
    const avisos = screen.getAllByRole('alert').filter((a) => a.textContent?.includes('versión nueva pendiente de revisión'));
    expect(avisos).toHaveLength(1);
    expect(avisos[0]).toHaveTextContent('1 sección tiene una versión nueva pendiente de revisión');
    expect(avisos[0]).toHaveTextContent('Lo que ves abajo sigue siendo lo que aceptaste');

    // Lo aceptado: versión 2, su fecha, su expediente y el soporte con que se aceptó.
    const general = within(seccion('Información general'));
    expect(general.getByText('Aceptada · versión 2')).toBeInTheDocument();
    expect(general.getByText('Versión nueva pendiente de revisión')).toBeInTheDocument();
    expect(general.getByText('Versión 2 · aceptada el 20/10/2026')).toBeInTheDocument();
    expect(general.getByRole('link', { name: 'Ver el expediente EXP-0007' })).toHaveAttribute('href', '/e/emp/expedientes/e1');
    const soporteAceptado = general.getByRole('region', { name: 'Soporte con que se aceptó' });
    expect(soporteAceptado).toHaveTextContent('Registro Único Tributario · versión 2');

    // Lo nuevo del tercero va en su propio bloque: qué cambió frente a lo aceptado, sin soporte nuevo y su revalidación.
    const posterior = within(general.getByRole('region', { name: 'Versión nueva del tercero, pendiente de revisión' }));
    expect(posterior.getByText('Versión 3 · guardada el 05/11/2026')).toBeInTheDocument();
    expect(posterior.getByRole('alert', { name: 'Cambio sin soporte' })).toHaveTextContent('datos críticos');
    const cambios = posterior.getByRole('table', { name: 'Qué cambió frente a la versión 2 aceptada' });
    expect(cambios).toHaveTextContent('Ferretería El Tornillo S.A.S.');
    expect(cambios).toHaveTextContent('Ferretería El Tornillo y Cía. S.A.S.');
    expect(posterior.getByRole('link', { name: 'Ver la revalidación EXP-0009' })).toHaveAttribute('href', '/e/emp/expedientes/e2');
    // Los datos aceptados siguen escondidos hasta pedirlos y traen los de la versión 2, no los de la nueva.
    expect(general.queryByText('Datos aceptados')).not.toBeInTheDocument();
    await usuario.click(general.getByRole('button', { name: 'Ver los datos aceptados' }));
    expect(await general.findByText('Datos aceptados')).toBeInTheDocument();
    expect(general.getAllByText('Ferretería El Tornillo S.A.S.').length).toBeGreaterThan(0);

    // Una sección sin nada posterior no tiene bloque aparte ni chip de pendiente.
    const tributaria = within(seccion('Información tributaria'));
    expect(tributaria.getByText('Aceptada · versión 1')).toBeInTheDocument();
    expect(tributaria.getByText('Sin soporte.')).toBeInTheDocument();
    expect(tributaria.queryByText('Versión nueva pendiente de revisión')).not.toBeInTheDocument();
    expect(tributaria.queryByRole('region', { name: 'Versión nueva del tercero, pendiente de revisión' })).not.toBeInTheDocument();

    // La aceptación importada no tiene expediente y un soporte retirado por el tercero lo dice.
    const financiera = within(seccion('Información financiera · 2025'));
    expect(financiera.getByText('Importada del sistema de origen')).toBeInTheDocument();
    expect(financiera.getByText('Retirado por el tercero')).toBeInTheDocument();

    // El resumen de la derecha cuenta lo mismo que las tarjetas.
    const resumen = within(seccion('Resumen'));
    expect(resumen.getByText('3 secciones aceptadas')).toBeInTheDocument();
    expect(resumen.getByText('1 con versión nueva pendiente de revisión')).toBeInTheDocument();
    expect(resumen.getByRole('link', { name: 'Ver la información completa del tercero' })).toHaveAttribute('href', '/e/emp/terceros/t1');
  });

  it('sin nada pendiente no muestra el aviso y lo dice en el resumen', async () => {
    permisos.scopes = new Set(TODOS);
    montar(ficha([TRIBUTARIA]));
    expect(await screen.findByRole('heading', { level: 1, name: 'Ferretería El Tornillo' })).toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(within(seccion('Resumen')).getByText('Nada pendiente de revisión.')).toBeInTheDocument();
  });

  it('sin secciones aceptadas dice qué falta para que aparezcan', async () => {
    permisos.scopes = new Set(TODOS);
    montar(ficha([]));
    expect(await screen.findByText('Aún no hay nada aceptado')).toBeInTheDocument();
    expect(screen.getByText(/aparecerá aquí con su versión y su soporte/)).toBeInTheDocument();
  });

  it('un vínculo que no es de la empresa responde con su estado y no con un error', async () => {
    permisos.scopes = new Set(TODOS);
    montar(404);
    expect(await screen.findByText('No encontramos ese vínculo')).toBeInTheDocument();
  });

  it('el resumen cuenta aceptadas y pendientes', () => {
    expect(resumirFichaAceptada(ficha([GENERAL, TRIBUTARIA, FINANCIERA]))).toEqual({ aceptadas: 3, pendientes: 1 });
    expect(resumirFichaAceptada(ficha([]))).toEqual({ aceptadas: 0, pendientes: 0 });
  });
});

describe('Tarjeta de la ficha aceptada en el vínculo', () => {
  it('dice cuántas secciones aceptó la empresa y cuántas esperan revisión, con el enlace a la ficha', async () => {
    permisos.scopes = new Set(TODOS);
    montar(ficha([GENERAL, TRIBUTARIA]), 'tarjeta');
    const tarjeta = within(await screen.findByRole('region', { name: 'Ficha aceptada' }));
    expect(await tarjeta.findByText('2 secciones aceptadas')).toBeInTheDocument();
    expect(tarjeta.getByText('1 con versión nueva pendiente de revisión')).toBeInTheDocument();
    expect(tarjeta.getByRole('link', { name: 'Ver la ficha aceptada del vínculo Proveedor' })).toHaveAttribute('href', '/e/emp/vinculos/v1/ficha');
  });

  it('sin terceros:leer lo dice y no pide nada', () => {
    permisos.scopes = new Set([SCOPES.vinculosLeer]);
    const urls = montar(ficha([]), 'tarjeta');
    expect(screen.getByText('No tienes permiso para ver la información del tercero.')).toBeInTheDocument();
    expect(urls.some((u) => u.endsWith('/ficha-aceptada'))).toBe(false);
  });
});
