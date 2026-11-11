import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import { configurarCliente, reiniciarCliente, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { expediente, requisito } from '../datosPrueba';
import type { ContextoRevisor } from '../reglas';
import { PanelRequisitos } from './PanelRequisitos';

const CONTEXTO: ContextoRevisor = { scopes: new Set([SCOPES.expedientesLeer]), usuarioId: 'u-1', esMiembro: true };

const conRetiro = requisito({
  versionActual: { documentoId: 'doc-rut', n: 7, sha256: 'abc', cargadaEn: '2026-11-06T14:00:00Z', clase: 'Archivo' },
  retiro: { numero: 8, motivo: 'Subí el RUT de otra empresa', fecha: '2026-11-11T11:59:00Z' },
});
const sinRetiro = requisito({ versionActual: { documentoId: 'doc-rut', n: 7, sha256: 'abc', cargadaEn: '2026-11-06T14:00:00Z', clase: 'Archivo' } });

function montar(datos: ExpedienteDto) {
  configurarCliente({ fetch: vi.fn(() => new Promise<Response>(() => undefined)) });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient()}>
        <PanelRequisitos expediente={datos} contexto={() => CONTEXTO} nombres={new Map()} onAccion={() => undefined} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
}

afterEach(() => reiniciarCliente());

describe('«Documentos que pide el flujo»: el tercero retiró la versión (RT-3)', () => {
  it('la revalidación dice qué versión se retiró, por qué y cuál vuelve a revisarse', () => {
    montar({ ...expediente('EnRevision'), tipo: 'Actualizacion', requisitos: [conRetiro] });

    const tarjeta = within(screen.getByRole('heading', { name: 'Registro Único Tributario' }).closest('li') as HTMLElement);
    expect(tarjeta.getByRole('alert')).toHaveTextContent('El tercero retiró la versión 8 (motivo: Subí el RUT de otra empresa). Vuelve la versión 7: revísala de nuevo.');
  });

  it('un documento sin retiro no trae ese aviso', () => {
    montar({ ...expediente('EnRevision'), requisitos: [sinRetiro] });

    expect(screen.queryByText(/El tercero retiró/)).not.toBeInTheDocument();
  });
});
