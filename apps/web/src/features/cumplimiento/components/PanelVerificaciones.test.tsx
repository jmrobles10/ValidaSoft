import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { expediente } from '../../expedientes/datosPrueba';
import { coincidencia, estudio, listas } from '../datosPrueba';
import { PanelVerificaciones } from './PanelVerificaciones';

const OFICIAL = new Set([SCOPES.expedientesLeer, SCOPES.verificacionesEscribir, SCOPES.cumplimientoDecidir, SCOPES.documentosDescargar]);

function montar(datos: ExpedienteDto, scopes: ReadonlySet<string> = OFICIAL) {
  const fetch = vi.fn((_entrada: RequestInfo | URL, _init?: RequestInit) =>
    Promise.resolve(new Response(JSON.stringify(datos), { status: 200, headers: { 'Content-Type': 'application/json' } })),
  );
  configurarCliente({ fetch });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <PanelVerificaciones expediente={datos} empresaId="emp" scopes={scopes} nombres={new Map([['u-ofi', 'Carlos Cumplimiento']])} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return fetch;
}

afterEach(() => reiniciarCliente());

describe('Panel de verificaciones del expediente (H7)', () => {
  it('muestra la coincidencia de la persona relacionada y pide justificación para descartarla', async () => {
    const datos = { ...expediente('EnRevision'), verificaciones: [listas(), estudio()], esperandoVerificaciones: true };
    const fetch = montar(datos);

    const seccion = within(screen.getByRole('region', { name: 'Listas restrictivas' }));
    expect(seccion.getByText('Coincidencia por analizar')).toBeInTheDocument();
    const fila = within(seccion.getByRole('listitem', { name: 'Ana Coincidencia · OFAC (SDN)' }));
    expect(fila.getByText(/Representante legal · CC 1020304050/)).toBeInTheDocument();

    await userEvent.click(fila.getByRole('button', { name: 'Descartar' }));
    const dialogo = within(screen.getByRole('dialog'));
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar decisión' }));
    expect(dialogo.getByText('Escribe la justificación.')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();

    await userEvent.type(dialogo.getByLabelText(/Justificación/), 'Homónimo: otra cédula.');
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar decisión' }));
    const [entrada, init] = fetch.mock.calls[0] ?? [];
    expect(String(entrada)).toContain('/api/v1/expedientes/e1/verificaciones/ListasRestrictivas/coincidencias/c1/resolver');
    expect(JSON.parse(String(init?.body))).toEqual({ decision: 'Descartada', justificacion: 'Homónimo: otra cédula.' });
  });

  it('sin cumplimiento:decidir no ofrece resolver ni registrar el estudio; una decisión heredada dice hasta cuándo vale', () => {
    const resuelta = coincidencia({ decision: 'Descartada', heredada: true, vigenteHasta: '2027-11-10T14:00:00Z', justificacion: 'Homónimo.' });
    montar({ ...expediente('EnRevision'), verificaciones: [listas({ estado: 'Satisfecha', coincidencias: [resuelta] }), estudio()] }, new Set([SCOPES.expedientesLeer]));

    expect(screen.queryByRole('button', { name: 'Descartar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Registrar estudio' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Reconsultar/ })).not.toBeInTheDocument();
    expect(screen.getByText(/Decisión de una consulta anterior, vigente hasta el 10\/11\/2027/)).toBeInTheDocument();
    expect(screen.getByText('Falta el estudio del oficial de cumplimiento.')).toBeInTheDocument();
  });

  it('el estudio exige debida diligencia ampliada con riesgo alto antes de enviarlo', async () => {
    const fetch = montar({ ...expediente('EnRevision'), verificaciones: [estudio()] });
    await userEvent.click(screen.getByRole('button', { name: 'Registrar estudio' }));
    const dialogo = within(screen.getByRole('dialog'));
    await userEvent.click(dialogo.getByRole('button', { name: '3 · Alto' }));
    await userEvent.click(dialogo.getByRole('combobox', { name: 'Concepto' }));
    await userEvent.click(screen.getByRole('option', { name: 'Favorable' }));
    await userEvent.click(dialogo.getByRole('button', { name: 'Registrar estudio' }));
    expect(dialogo.getByText('Con riesgo alto la debida diligencia debe ser ampliada.')).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('un expediente sin verificaciones no muestra el panel', () => {
    montar({ ...expediente('EnRevision'), verificaciones: [] });
    expect(screen.queryByRole('region', { name: 'Verificaciones' })).not.toBeInTheDocument();
  });
});
