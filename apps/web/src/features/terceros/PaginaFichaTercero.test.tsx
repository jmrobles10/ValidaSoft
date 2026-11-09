import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type FichaTerceroDto, type VinculoDto, type VinculoEnFichaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaFichaTercero } from './PaginaFichaTercero';
import { CERTIFICADO, CUENTA, DOCUMENTO } from './pruebas/datos';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({
  useEmpresaActiva: () => ({
    empresa: { id: 'emp', nombre: 'Constructora Norte' },
    empresas: [
      { id: 'emp', nombre: 'Constructora Norte' },
      { id: 'EMP-SUR', nombre: 'Constructora Sur' },
    ],
  }),
}));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const TODOS = [SCOPES.tercerosLeer, SCOPES.vinculosLeer, SCOPES.certificadosLeer, SCOPES.documentosLeer];

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

const EN_FICHA: VinculoEnFichaDto = { vinculo: VINCULO, certificado: CERTIFICADO, cuentaDesignada: CUENTA, documentos: [DOCUMENTO] };

function ficha(parcial: Partial<FichaTerceroDto> = {}): FichaTerceroDto {
  return {
    tercero: {
      id: 't1',
      identificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456', digitoVerificacion: '8' },
      tipoPersona: 'Juridica',
      nombre: 'Ferretería El Tornillo',
      nombreComercial: null,
      contactos: [{ nombre: 'Ana Gómez', correo: 'ana@tornillo.co', telefono: '3001234567', rol: 'Representante legal' }],
      esExtranjero: false,
      creadoEn: '2026-08-01T15:00:00Z',
    },
    vinculos: [EN_FICHA],
    otrasEmpresasDelGrupo: [
      { empresaId: 'emp-sur', estado: 'Bloqueado' },
      { empresaId: 'emp-oculta', estado: 'Activo' },
    ],
    ...parcial,
  };
}

const respuesta = (estado: number, cuerpo: unknown) =>
  new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': estado === 200 ? 'application/json' : 'application/problem+json' } });

function UbicacionActual() {
  return <p>{`Ubicación: ${useLocation().pathname}`}</p>;
}

/** API falsa: la ficha (o el 404) y las URL pedidas. */
function montar(resultado: FichaTerceroDto | 404) {
  const urls: string[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      urls.push(typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url);
      if (resultado === 404) return Promise.resolve(respuesta(404, { status: 404, codigo: 'tercero-no-encontrado', detail: 'No existe ese tercero en la empresa.' }));
      return Promise.resolve(respuesta(200, resultado));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={['/e/emp/terceros/t1']}>
          <Routes>
            <Route path="/e/:empresaId/terceros/:terceroId" element={<PaginaFichaTercero />} />
            <Route path="/e/:empresaId/vinculos/*" element={<UbicacionActual />} />
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

describe('Ficha del tercero', () => {
  it('sin terceros:leer no pide la ficha', () => {
    const urls = montar(ficha());
    expect(screen.getByText('Esta sección es de Ficha del tercero')).toBeInTheDocument();
    expect(urls).toHaveLength(0);
  });

  it('muestra identificación, contactos, cada vínculo con sus secciones y las otras empresas', async () => {
    permisos.scopes = new Set(TODOS);
    const usuario = userEvent.setup();
    const urls = montar(ficha());

    expect(await screen.findByRole('heading', { level: 1, name: 'Ferretería El Tornillo' })).toBeInTheDocument();
    expect(urls).toEqual(['/api/v1/terceros/t1/ficha']);
    expect(screen.getAllByText('NIT 900123456-8').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Persona jurídica').length).toBeGreaterThan(0);

    const identificacion = seccion('Identificación y contactos');
    expect(within(identificacion).getByText('Ana Gómez')).toBeInTheDocument();
    expect(within(identificacion).getByText('Representante legal')).toBeInTheDocument();
    expect(within(identificacion).getByText('ana@tornillo.co')).toBeInTheDocument();

    const vinculo = seccion('Proveedor');
    expect(within(vinculo).getByText('Activo')).toBeInTheDocument();
    const certificado = within(vinculo).getByRole('region', { name: 'Certificado' });
    expect(within(certificado).getByText('VS-2026-000001')).toBeInTheDocument();
    expect(within(certificado).getByText('Vigente hasta el 30/11/2026')).toBeInTheDocument();
    const cuenta = within(vinculo).getByRole('region', { name: 'Cuenta designada' });
    expect(within(cuenta).getByText('Bancolombia')).toBeInTheDocument();
    expect(within(cuenta).getByText('****6789')).toBeInTheDocument();
    const documentos = within(vinculo).getByRole('region', { name: 'Documentos compartidos' });
    expect(within(documentos).getByText('RUT')).toBeInTheDocument();
    expect(within(documentos).getByText('Versión 2')).toBeInTheDocument();

    const otras = seccion('Otras empresas del grupo');
    expect(within(otras).getByText('Constructora Sur')).toBeInTheDocument();
    expect(within(otras).getByText('Bloqueado')).toBeInTheDocument();
    expect(within(otras).getByText('Otra empresa del grupo')).toBeInTheDocument();

    await usuario.click(within(vinculo).getByRole('link', { name: 'Ver el vínculo Proveedor' }));
    expect(await screen.findByText('Ubicación: /e/emp/vinculos/v1')).toBeInTheDocument();
  });

  it('certificado y documentos en null se muestran como sin permiso, no como vacíos', async () => {
    permisos.scopes = new Set([SCOPES.tercerosLeer, SCOPES.vinculosLeer]);
    montar(ficha({ vinculos: [{ ...EN_FICHA, certificado: null, documentos: null }] }));

    const vinculo = await screen.findByRole('region', { name: 'Proveedor' });
    expect(within(vinculo).getByText('No tienes permiso para ver los certificados.')).toBeInTheDocument();
    expect(within(vinculo).getByText('No tienes permiso para ver los documentos.')).toBeInTheDocument();
    expect(within(vinculo).queryByText('Aún no se ha emitido.')).not.toBeInTheDocument();
    expect(within(vinculo).queryByText(/no ha compartido documentos/)).not.toBeInTheDocument();
  });

  it('sin certificado emitido, sin cuenta ni documentos lo dice en cada sección', async () => {
    permisos.scopes = new Set(TODOS);
    montar(ficha({ vinculos: [{ ...EN_FICHA, certificado: { vinculoId: 'v1', actual: null, emisiones: [] }, cuentaDesignada: { ...CUENTA, actual: null }, documentos: [] }] }));

    const vinculo = await screen.findByRole('region', { name: 'Proveedor' });
    expect(within(vinculo).getByText('Aún no se ha emitido.')).toBeInTheDocument();
    expect(within(vinculo).getByText('El tercero no tiene una cuenta designada.')).toBeInTheDocument();
    expect(within(vinculo).getByText('El tercero no ha compartido documentos con este vínculo.')).toBeInTheDocument();
  });

  it('sin otras empresas del grupo no muestra la sección', async () => {
    permisos.scopes = new Set(TODOS);
    montar(ficha({ otrasEmpresasDelGrupo: [] }));

    await screen.findByRole('region', { name: 'Proveedor' });
    expect(screen.queryByRole('region', { name: 'Otras empresas del grupo' })).not.toBeInTheDocument();
  });

  it('sin vinculos:leer dice que falta el permiso en lugar de los vínculos', async () => {
    permisos.scopes = new Set([SCOPES.tercerosLeer]);
    montar(ficha({ vinculos: [], otrasEmpresasDelGrupo: [] }));

    expect(await screen.findByText(/No tienes permiso para ver los vínculos de esta empresa/)).toBeInTheDocument();
  });

  it('404: dice que el tercero no existe en esta empresa y ofrece ir a Vínculos', async () => {
    permisos.scopes = new Set(TODOS);
    const usuario = userEvent.setup();
    montar(404);

    expect(await screen.findByText('No existe ese tercero en esta empresa')).toBeInTheDocument();
    await usuario.click(screen.getByRole('link', { name: 'Ir a Vínculos' }));
    expect(await screen.findByText('Ubicación: /e/emp/vinculos')).toBeInTheDocument();
  });
});
