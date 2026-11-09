import { ThemeProvider } from '@mui/material';
import { render, screen, within } from '@testing-library/react';
import type { DocumentoBovedaDto, VigenciaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { AvisoConsolidado } from './AvisoConsolidado';

const vigencia = (estado: string, vigenteHasta: string, finDeGracia: string | null = null): VigenciaDto => ({
  vigenteHasta,
  base: 'FechaExpedicion',
  estado,
  diasAviso: [30],
  diasGracia: 15,
  finDeGracia,
  vencidaEn: null,
});

const documento = (id: string, nombre: string, empresas: [string, VigenciaDto][]): DocumentoBovedaDto => ({
  id,
  tipo: { codigo: id.toUpperCase(), nombre, tipoDocumentoTenantId: null },
  ambito: 'Global',
  clase: 'Archivo',
  vinculoId: null,
  versionActual: null,
  versiones: 1,
  porEmpresa: empresas.map(([empresa, v]) => ({
    vinculoId: `v-${empresa}`,
    empresa: { id: `e-${empresa}`, nombre: empresa, logoUrl: null },
    compartido: true,
    version: 1,
    versionActualCompartida: true,
    compartidaEn: '2026-10-01T12:00:00Z',
    vigencia: v,
  })),
});

function montar(documentos: DocumentoBovedaDto[]) {
  return render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <MemoryRouter>
        <AvisoConsolidado documentos={documentos} />
      </MemoryRouter>
    </ThemeProvider>,
  );
}

describe('bloque «Por renovar» de la bóveda', () => {
  it('lista primero lo que vence antes, con qué empresas queda al día y el enlace para subir la versión nueva', () => {
    montar([
      documento('camara', 'Cámara de comercio', [
        ['Constructora Norte', vigencia('PorVencer', '2026-11-30')],
        ['Constructora Sur', vigencia('Vigente', '2026-12-30')],
      ]),
      documento('rut', 'Registro Único Tributario', [['Constructora Norte', vigencia('EnGracia', '2026-11-01', '2026-11-16')]]),
      documento('cedula', 'Cédula', [['Constructora Norte', vigencia('Vigente', '2027-06-01')]]),
    ]);

    const bloque = screen.getByRole('region', { name: 'Por renovar' });
    const [primera, segunda, ...resto] = within(bloque).getAllByRole('listitem');
    expect(resto).toHaveLength(0);
    if (!primera || !segunda) throw new Error('Faltan líneas');
    const lineas = [primera, segunda] as const;
    expect(lineas[0]).toHaveTextContent('Registro Único Tributario: está en el periodo de gracia con Constructora Norte hasta el 16/11/2026.');
    expect(lineas[0]).toHaveTextContent('En periodo de gracia');
    expect(lineas[1]).toHaveTextContent(
      'Cámara de comercio: vence para Constructora Norte el 30/11/2026 y para Constructora Sur el 30/12/2026. Renueva antes del 30/11 y quedas al día con las dos.',
    );
    expect(within(lineas[1]).getByRole('link', { name: 'Subir versión nueva: Cámara de comercio' })).toHaveAttribute(
      'href',
      '/boveda/subir?documento=camara&tipo=CAMARA',
    );
    expect(screen.queryByText(/Cédula/)).not.toBeInTheDocument();
  });

  it('no se muestra si no hay nada que renovar', () => {
    const { container } = montar([documento('rut', 'Registro Único Tributario', [['Constructora Norte', vigencia('Vigente', '2027-06-01')]])]);
    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByRole('region')).not.toBeInTheDocument();
  });
});
