import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type ExpedientePublicoDto, type TipoDocumentoRequisitoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PaginaSubir } from './PaginaSubir';

const json = (cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: 200, headers: { 'Content-Type': 'application/json' } });

function expediente(tipoDocumento: TipoDocumentoRequisitoDto): ExpedientePublicoDto {
  return {
    id: 'e1',
    folio: 'F-1',
    empresa: { id: 'emp', nombre: 'Constructora Norte', logoUrl: null },
    tipoVinculo: { id: 'tv', codigo: 'CLIENTE', nombre: 'Cliente' },
    estado: 'Abierto',
    observaciones: [],
    puedeReenviar: false,
    reenvios: 0,
    actualizadoEn: '2026-11-08T15:00:00Z',
    requisitos: [{ id: 'r1', tipoDocumento, obligatorio: true, estado: 'Pendiente', versionActual: null, observaciones: [] }],
  };
}

const TIPO: TipoDocumentoRequisitoDto = {
  id: 't1',
  codigo: 'CERT_NORTE',
  nombre: 'Certificado Norte',
  tipoCanonico: null,
  ambito: 'PorVinculo',
  modo: 'ArchivoYFormulario',
  campos: [{ clave: 'fecha_expedicion', etiqueta: 'Fecha de expedición', tipo: 'Fecha', obligatorio: true }],
  archivo: { formatos: ['application/pdf'], tamanoMaximoBytes: 5 * 1024 * 1024, paginasMaximas: 10, descripcion: 'Certificado Norte admite solo PDF de hasta 5 MB' },
  tienePlantilla: true,
  textoAyuda: 'Firmado por el representante legal.',
};

function montar(tipo: TipoDocumentoRequisitoDto) {
  configurarCliente({
    fetch: vi.fn((url: RequestInfo | URL) => {
      const ruta = typeof url === 'string' ? url : url instanceof URL ? url.href : url.url;
      return Promise.resolve(json(ruta.includes('/expedientes/') ? expediente(tipo) : { documentos: [] }));
    }),
  });
  const ruta = `/boveda/subir?nombre=${encodeURIComponent(tipo.nombre)}&volver=%2Fexpediente%2Fe1&expediente=e1&requisito=r1&tipoDocumentoTenantId=t1&vinculoId=v1`;
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>
          <PaginaSubir />
        </MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
}

afterEach(() => reiniciarCliente());

describe('subir lo que pide un requisito', () => {
  it('con archivo y formulario: reglas, plantilla y campos antes del archivo; el selector solo acepta PDF', async () => {
    const usuario = userEvent.setup();
    montar(TIPO);

    expect(await screen.findByText('Solo PDF de hasta 5 MB y 10 páginas')).toBeInTheDocument();
    expect(screen.getByText('Firmado por el representante legal.')).toBeInTheDocument();
    expect(screen.getByText('Descarga el formato, llénalo, fírmalo y súbelo.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Descargar formato' })).toBeEnabled();
    expect(screen.getByTestId('entrada-archivo')).toHaveAttribute('accept', 'application/pdf,.pdf');
    // Solo PDF: no se ofrece tomar una foto.
    expect(screen.queryByRole('button', { name: 'Tomar foto' })).not.toBeInTheDocument();

    const elegir = screen.getByRole('button', { name: 'Elegir archivo' });
    expect(elegir).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Para subir el archivo, llena: Fecha de expedición.');

    await usuario.type(screen.getByLabelText(/Fecha de expedición/), '2026-10-01');
    expect(elegir).toBeEnabled();
  });

  it('solo formulario: se llena aquí mismo, sin zona de archivo', async () => {
    montar({ ...TIPO, modo: 'Formulario', archivo: null, tienePlantilla: false, campos: [{ clave: 'banco', etiqueta: 'Banco', tipo: 'Lista', opciones: ['Uno', 'Dos'], obligatorio: true }] });

    expect(await screen.findByRole('heading', { name: 'Llenar Certificado Norte' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Enviar datos' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Elegir archivo' })).not.toBeInTheDocument();
  });
});
