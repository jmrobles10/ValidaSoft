import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { configurarCliente, reiniciarCliente, SCOPES, type FichaTerceroDto, type SeccionFichaDto, type VinculoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter, Route, Routes } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaFichaTercero } from '../PaginaFichaTercero';
import { CERTIFICADO, CUENTA, DOCUMENTO } from '../pruebas/datos';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({
  useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [{ id: 'emp', nombre: 'Constructora Norte' }] }),
}));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

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

function seccion(cambios: Partial<SeccionFichaDto>): SeccionFichaDto {
  return { seccion: 'FICHA_GENERAL', nombre: 'Información general', periodo: null, version: 2, datos: {}, soportes: [], completitud: { completa: true, faltantes: [], reglaVersion: 1 }, huella: 'h', origen: 'Tercero', fecha: '2026-11-02T15:00:00Z', versiones: 2, esquemaVersion: 1, ...cambios };
}

const SECCIONES: SeccionFichaDto[] = [
  seccion({
    datos: { razon_social: 'Ferretería El Tornillo S.A.S.', tipo_persona: 'Juridica', pais: 'CO', certificado_iso: true, anio_inicio: 2004, direccion_estructurada: { tipo_via: 'Calle', numero_via: '45' } },
    // d1 es el RUT compartido con el vínculo; d9 no lo puede ver la empresa.
    soportes: [{ documentoId: 'd1', version: 2, tipoDocumento: 'RUT' }, { documentoId: 'd9', version: 1, tipoDocumento: 'SOPORTE_FICHA' }],
  }),
  seccion({
    seccion: 'FICHA_LEGAL',
    nombre: 'Información legal',
    version: 1,
    datos: { personas: [{ rol: 'RepresentanteLegal', tipo_identificacion: 'CC', numero: '1020304050', nombres: 'Ana', apellidos: 'Gómez', porcentaje: 12.5, pep: false }] },
    completitud: { completa: false, faltantes: [{ campo: null, regla: 'socio-requerido', mensaje: 'Registra al menos un socio o accionista.' }], reglaVersion: 1 },
  }),
  seccion({ seccion: 'FICHA_FINANCIERA', nombre: 'Información financiera', periodo: 2025, datos: { periodo: 2025, fecha_corte: '2025-12-31', activos: 1500000.5, moneda_extranjera: { realiza: true, tipos: ['Importaciones'], paises: ['US'] } } }),
];

function montar(ficha: FichaTerceroDto) {
  configurarCliente({ fetch: vi.fn(() => Promise.resolve(new Response(JSON.stringify(ficha), { status: 200, headers: { 'Content-Type': 'application/json' } }))) });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={['/e/emp/terceros/t1']}>
          <Routes>
            <Route path="/e/:empresaId/terceros/:terceroId" element={<PaginaFichaTercero />} />
          </Routes>
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
}

function ficha(secciones: SeccionFichaDto[] | null): FichaTerceroDto {
  return {
    tercero: {
      id: 't1',
      identificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456', digitoVerificacion: '8' },
      tipoPersona: 'Juridica',
      nombre: 'Ferretería El Tornillo',
      nombreComercial: null,
      contactos: [],
      esExtranjero: false,
      creadoEn: '2026-08-01T15:00:00Z',
    },
    vinculos: [{ vinculo: VINCULO, certificado: CERTIFICADO, cuentaDesignada: CUENTA, documentos: [DOCUMENTO] }],
    otrasEmpresasDelGrupo: [],
    secciones,
  };
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
});

describe('Tercero 360: información del tercero', () => {
  it('muestra una tarjeta por sección con estado, versión, datos legibles y soportes', async () => {
    permisos.scopes = new Set([SCOPES.tercerosLeer, SCOPES.vinculosLeer, SCOPES.documentosLeer]);
    montar(ficha(SECCIONES));

    const bloque = await screen.findByRole('region', { name: 'Información del tercero' });
    const general = within(bloque).getByRole('region', { name: 'Información general' });
    expect(general).toHaveTextContent('Completa');
    expect(general).toHaveTextContent('Versión 2 · 02/11/2026');
    expect(general).toHaveTextContent('Razón socialFerretería El Tornillo S.A.S.');
    expect(general).toHaveTextContent('Tipo de personaPersona jurídica');
    expect(general).toHaveTextContent('Certificado ISOSí');
    expect(general).toHaveTextContent('Año de inicio2004');
    expect(within(general).getByRole('region', { name: 'Dirección por partes' })).toHaveTextContent('Tipo de víaCalle');
    // El RUT del vínculo se abre en el vínculo; el otro soporte no lo puede ver la empresa.
    expect(within(general).getByRole('link', { name: 'Ver el soporte RUT en el vínculo' })).toHaveAttribute('href', '/e/emp/vinculos/v1');
    expect(general).toHaveTextContent('Soporte del tercero · versión 1');

    const legal = within(bloque).getByRole('region', { name: 'Información legal' });
    expect(legal).toHaveTextContent('Falta 1 dato');
    expect(legal).toHaveTextContent('Registra al menos un socio o accionista.');
    const persona = within(legal).getByRole('listitem', { name: 'Ana Gómez · Representante legal' });
    expect(persona).toHaveTextContent('Participación12,5 %');
    expect(persona).toHaveTextContent('PEPNo');
    expect(legal).toHaveTextContent('Sin soporte.');

    const financiera = within(bloque).getByRole('region', { name: 'Información financiera · 2025' });
    expect(financiera).toHaveTextContent('Fecha de corte31/12/2025');
    expect(financiera.textContent).toMatch(/Activos\$\s?1\.500\.000,5/);
    expect(within(financiera).getByRole('region', { name: 'Moneda extranjera' })).toHaveTextContent('Tipos de operaciónImportaciones');
    expect(within(financiera).getByRole('region', { name: 'Moneda extranjera' })).toHaveTextContent('Estados Unidos (US)');
  });

  it('sin secciones diligenciadas lo dice', async () => {
    permisos.scopes = new Set([SCOPES.tercerosLeer, SCOPES.vinculosLeer]);
    montar(ficha([]));
    expect(await screen.findByText('El tercero aún no ha diligenciado su información.')).toBeInTheDocument();
  });
});
