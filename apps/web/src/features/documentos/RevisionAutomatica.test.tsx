import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type DocumentoVistaEmpresaDto, type HechosDto, type HechosIaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { PanelRequisitos } from '../expedientes/components/PanelRequisitos';
import { expediente, requisito } from '../expedientes/datosPrueba';
import type { ContextoRevisor } from '../expedientes/reglas';
import { RevisionAutomatica } from './RevisionAutomatica';
import { VisorDocumento } from './VisorDocumento';

const IA: HechosIaDto = {
  modelo: 'gpt-6.1-terra',
  escalado: true,
  confianza: '0.72',
  legible: true,
  tipoDetectado: 'CAMARA_COMERCIO',
  campos: {
    razonSocial: 'Constructora Norte S.A.S.',
    nit: '900123456',
    fechaExpedicion: '2026-11-01',
    codigoCiiu: '4111',
    representantesLegales: [
      { nombre: 'Ana Gómez', identificacion: '52123456', cargo: 'Principal' },
      { nombre: 'Luis Pérez', identificacion: null, cargo: 'Suplente' },
    ],
  },
  alertas: ['La fecha del sello no coincide con la de expedición.'],
  observaciones: 'El certificado tiene 14 páginas.',
  paginasEnviadas: 10,
  paginasOmitidas: 4,
  fecha: '2026-11-07T15:00:00Z',
};

const BASE: HechosDto = {
  conIa: true,
  hashVerificado: true,
  antivirus: 'Limpio',
  mimeReal: 'application/pdf',
  paginas: 14,
  comparacion: 'SinCambios',
  camposCambiados: [],
  camposCriticosCambiados: [],
  pendienteRevisionHumana: true,
  ia: IA,
};

const SIN_IA: HechosDto = { ...BASE, conIa: false, ia: null };

function montar(contenido: ReactNode, respuesta?: unknown) {
  const fetch = vi.fn((_entrada: RequestInfo | URL) => (respuesta === undefined ? new Promise<Response>(() => undefined) : Promise.resolve(new Response(JSON.stringify(respuesta), { status: 200, headers: { 'Content-Type': 'application/json' } }))));
  configurarCliente({ fetch });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{contenido}</QueryClientProvider>
    </ThemeProvider>,
  );
  return fetch;
}

afterEach(() => reiniciarCliente());

describe('Revisión automática de una versión', () => {
  it('muestra confianza, escalamiento, tipo detectado distinto, alertas, campos y páginas omitidas', () => {
    montar(<RevisionAutomatica hechos={BASE} tipoEsperado={{ codigo: 'RUT', nombre: 'Registro Único Tributario' }} />);
    const panel = within(screen.getByRole('region', { name: 'Revisión automática' }));

    // 0,72 queda bajo el umbral (0,85): chip en ámbar.
    expect(panel.getByText(/Confianza 72\s?%/).closest('.MuiChip-root')).toHaveClass('MuiChip-colorWarning');
    expect(panel.getByText('Revisado por un modelo de mayor capacidad')).toBeInTheDocument();
    expect(panel.getByText(/parece ser «Certificado de existencia y representación legal», no «Registro Único Tributario»/)).toBeInTheDocument();
    expect(panel.getByText('Posibles señales de alteración')).toBeInTheDocument();
    expect(panel.getByText('La fecha del sello no coincide con la de expedición.')).toBeInTheDocument();

    const datos = within(screen.getByLabelText('Datos extraídos'));
    expect(datos.getByText('Razón social')).toBeInTheDocument();
    expect(datos.getByText('Constructora Norte S.A.S.')).toBeInTheDocument();
    expect(datos.getByText('NIT')).toBeInTheDocument();
    expect(datos.getByText('01/11/2026')).toBeInTheDocument();
    expect(datos.getByText('Codigo ciiu')).toBeInTheDocument();
    expect(datos.getByText('Representantes legales')).toBeInTheDocument();
    expect(datos.getByText('Ana Gómez')).toBeInTheDocument();
    expect(datos.getByText('Suplente')).toBeInTheDocument();

    expect(panel.getByText('Se analizaron las primeras 10 páginas.')).toBeInTheDocument();
    expect(panel.getByText('Nota del análisis: El certificado tiene 14 páginas.')).toBeInTheDocument();
    // El modelo va solo en el texto pequeño.
    expect(panel.getByText('Analizado el 07/11/2026 · gpt-6.1-terra')).toBeInTheDocument();
  });

  it('con confianza alta y el tipo esperado no alerta', () => {
    montar(<RevisionAutomatica hechos={{ ...BASE, ia: { ...IA, confianza: 0.93, escalado: false, alertas: [], paginasOmitidas: 0 } }} tipoEsperado={{ codigo: 'CAMARA_COMERCIO', nombre: 'Cámara de comercio' }} />);
    expect(screen.getByText(/Confianza 93\s?%/).closest('.MuiChip-root')).toHaveClass('MuiChip-colorSuccess');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.queryByText('Revisado por un modelo de mayor capacidad')).not.toBeInTheDocument();
    expect(screen.queryByText(/Se analiz/)).not.toBeInTheDocument();
  });

  it('sin análisis pide revisarla a mano', () => {
    montar(<RevisionAutomatica hechos={SIN_IA} />);
    expect(screen.getByText('Sin análisis automático: revísalo manualmente.')).toBeInTheDocument();
    expect(screen.queryByRole('region', { name: 'Revisión automática' })).not.toBeInTheDocument();
  });

  it('el visor la muestra en el panel al lado del archivo', () => {
    const documento = { documentoId: 'd1', tipo: 'RUT', n: 2, cargadaEn: '2026-11-06T14:00:00Z', hechos: SIN_IA, tipoEsperado: { codigo: 'RUT', nombre: 'RUT' } };
    montar(<VisorDocumento empresaId="emp" vinculoId="vin" documento={documento} onCerrar={() => undefined} />);
    expect(within(screen.getByRole('dialog')).getByText('Sin análisis automático: revísalo manualmente.')).toBeInTheDocument();
  });

  it('en el expediente toma los hechos de los documentos del vínculo y limita los campos', async () => {
    const documentos: DocumentoVistaEmpresaDto[] = [
      {
        documentoId: 'doc-rut',
        tipo: { codigo: 'RUT', nombre: 'Registro Único Tributario', tipoDocumentoTenantId: null },
        ambito: 'Global',
        versionActual: 1,
        versionesCompartidas: [
          {
            documentoId: 'doc-rut',
            n: 1,
            estado: 'Actual',
            cargadaEn: '2026-11-06T14:00:00Z',
            cargadaPor: 'tercero',
            clase: 'Archivo',
            nombreOriginal: 'rut.pdf',
            mime: 'application/pdf',
            tamanoBytes: 1024,
            paginas: 1,
            sha256: 'abc',
            motivoDevolucion: null,
            detalleDevolucion: null,
            hechos: { ...BASE, ia: { ...IA, tipoDetectado: 'RUT', alertas: [], campos: { a: '1', b: '2', c: '3', d: '4', e: '5', f: '6', g: '7' } } },
          },
        ],
      },
    ];
    const contexto: ContextoRevisor = { scopes: new Set([SCOPES.expedientesLeer, SCOPES.documentosLeer]), usuarioId: 'u-1', esMiembro: true };
    const fetch = montar(
      <PanelRequisitos expediente={{ ...expediente('EnRevision'), requisitos: [requisito()] }} contexto={() => contexto} nombres={new Map()} onAccion={() => undefined} />,
      documentos,
    );

    const tarjeta = within(await screen.findByRole('region', { name: 'Revisión automática' }));
    const entrada = fetch.mock.calls[0]?.[0];
    expect(typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada?.url).toContain('/api/v1/vinculos/vin/documentos');
    expect(tarjeta.queryByRole('alert')).not.toBeInTheDocument();
    expect(tarjeta.queryByText('G')).not.toBeInTheDocument();
    await userEvent.setup().click(tarjeta.getByRole('button', { name: 'Ver 1 dato más' }));
    await waitFor(() => expect(tarjeta.getByText('G')).toBeInTheDocument());
  });
});
