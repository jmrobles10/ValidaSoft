import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import {
  configurarCliente,
  reiniciarCliente,
  type ComparticionPublicaDto,
  type DocumentoBovedaDto,
  type DocumentoDto,
  type VersionDocumentoDto,
  type VigenciaDto,
} from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { TarjetaDocumento } from './TarjetaDocumento';

const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

const vigencia = (vigenteHasta: string, base = 'FechaExpedicion'): VigenciaDto => ({
  vigenteHasta,
  base,
  estado: 'Vigente',
  diasAviso: [30],
  diasGracia: 0,
  finDeGracia: vigenteHasta,
  vencidaEn: null,
});

const version = (n: number, estado: VersionDocumentoDto['estado'], cambios: Partial<VersionDocumentoDto> = {}): VersionDocumentoDto => ({
  documentoId: 'd1',
  n,
  estado,
  cargadaEn: '2026-11-10T15:00:00Z',
  cargadaPor: 'tercero',
  clase: 'Archivo',
  nombreOriginal: `rut-v${String(n)}.pdf`,
  mime: 'application/pdf',
  tamanoBytes: 1000,
  paginas: 1,
  sha256: `sha${String(n)}`,
  motivoDevolucion: null,
  detalleDevolucion: null,
  hechos: null,
  ...cambios,
});

const empresa = (id: string, nombre: string, cambios: Partial<ComparticionPublicaDto>): ComparticionPublicaDto => ({
  vinculoId: `v-${id}`,
  empresa: { id, nombre, logoUrl: null },
  compartido: true,
  version: 7,
  versionActualCompartida: true,
  compartidaEn: '2026-11-10T15:00:00Z',
  vigencia: vigencia('2027-11-09'),
  situacion: 'TieneVigente',
  otraVersion: null,
  ...cambios,
});

/** El caso de la captura del usuario: la 8 se retiró, la vigente es la 7 y cada empresa tiene su vencimiento. */
const DOCUMENTO: DocumentoBovedaDto = {
  id: 'd1',
  tipo: { codigo: 'RUT', nombre: 'Registro Único Tributario', tipoDocumentoTenantId: null },
  ambito: 'Global',
  clase: 'Archivo',
  vinculoId: null,
  versionActual: version(7, 'Actual', { vigencia: vigencia('2027-11-09') }),
  versiones: 3,
  porEmpresa: [
    empresa('norte', 'Constructora Norte S.A.S.', { situacion: 'RevalidaRetirada', otraVersion: 8, vigencia: vigencia('2027-11-10') }),
    empresa('sur', 'Constructora Sur', { situacion: 'Revisando' }),
    empresa('oriente', 'Constructora Oriente', { situacion: 'TieneVigente' }),
    empresa('poniente', 'Constructora Poniente', { compartido: false, version: null, versionActualCompartida: false, vigencia: null, situacion: 'NoRecibe' }),
  ],
};

const COMPLETO: DocumentoDto = {
  id: 'd1',
  terceroId: 't1',
  tipo: DOCUMENTO.tipo,
  clase: 'Archivo',
  ambito: 'Global',
  vinculoId: null,
  versionActual: 7,
  versiones: [version(6, 'Superada'), version(7, 'Actual', { vigencia: vigencia('2027-11-09') }), version(8, 'Retirada')],
  porEmpresa: DOCUMENTO.porEmpresa,
};

function montar(documento: DocumentoBovedaDto = DOCUMENTO) {
  configurarCliente({ fetch: vi.fn(() => Promise.resolve(json(COMPLETO))) });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter>
          <TarjetaDocumento documento={documento} />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
}

afterEach(() => reiniciarCliente());

describe('tarjeta de un documento de la bóveda', () => {
  it('la cabecera dice la versión vigente y cada empresa, qué tiene y qué falta, sin contradecirla', () => {
    montar();

    const tarjeta = screen.getByRole('article', { name: 'Registro Único Tributario' });
    expect(tarjeta).toHaveTextContent('Versión 7 · cargada el 10/11/2026');
    expect(within(tarjeta).getAllByText('Vigente').length).toBeGreaterThan(0);
    expect(tarjeta).toHaveTextContent('Vence el 09/11/2027');

    const filas = within(screen.getByRole('list', { name: 'Con tus empresas' })).getAllByRole('listitem');
    expect(filas[0]).toHaveTextContent('Constructora Norte S.A.S.');
    expect(filas[0]).toHaveTextContent('Aprobó la versión 8, que retiraste. Debe revisar la versión 7: revalidación abierta.');
    expect(filas[1]).toHaveTextContent('Revisando la versión 7.');
    expect(filas[2]).toHaveTextContent('Tiene la versión 7, la vigente.');
    expect(filas[3]).toHaveTextContent('No la recibe: no la exige o no has aceptado su invitación.');

    // Nada de «recibirá la nueva cuando la exija»: era una versión más vieja que la que dice la cabecera.
    expect(tarjeta).not.toHaveTextContent(/recibirá la nueva|cuando la exija|v8/);
  });

  it('el vencimiento de una empresa solo aparece si difiere de la cabecera y dice por qué', () => {
    montar();

    const filas = within(screen.getByRole('list', { name: 'Con tus empresas' })).getAllByRole('listitem');
    expect(filas[0]).toHaveTextContent('Vence el 10/11/2027: su regla de vigencia cuenta desde la fecha de expedición.');
    expect(filas[1]).not.toHaveTextContent(/Vence/);
    expect(filas[2]).not.toHaveTextContent(/Vence/);
    // Y la cabecera explica por qué no todas coinciden.
    expect(screen.getByText('Cada empresa cuenta la vigencia con su propia regla: la fecha de arriba es la más próxima, la de Constructora Sur.')).toBeInTheDocument();
  });

  it('sin fechas distintas no se explica nada de más', () => {
    montar({ ...DOCUMENTO, porEmpresa: DOCUMENTO.porEmpresa.map((c) => ({ ...c, vigencia: c.vigencia ? vigencia('2027-11-09') : null })) });

    expect(screen.queryByText(/Cada empresa cuenta la vigencia/)).not.toBeInTheDocument();
    expect(screen.queryByText(/su regla de vigencia/)).not.toBeInTheDocument();
  });

  it('el listado de versiones usa «Vigente» para la que vale hoy, en vez de «Actual»', async () => {
    const usuario = userEvent.setup();
    montar();

    await usuario.click(screen.getByRole('button', { name: 'Ver versiones (3)' }));

    const items = await screen.findAllByText(/^Versión [678]$/);
    expect(items).toHaveLength(3);
    const de = (n: number) => items.find((i) => i.textContent === `Versión ${String(n)}`)?.closest('li') as HTMLElement;
    expect(de(7)).toHaveTextContent('Vigente');
    expect(de(6)).toHaveTextContent('Superada');
    expect(de(8)).toHaveTextContent('Retirada');
    expect(screen.queryByText('Actual')).not.toBeInTheDocument();
  });
});
