import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type ExpedienteDto, type MotivoTipificadoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { area, expediente, requisito } from '../datosPrueba';
import type { DialogoDeArea } from '../reglas';
import { DialogoDecisionArea } from './DialogoDecisionArea';

const MOTIVO: MotivoTipificadoDto = {
  id: 'm1',
  codigo: 'ilegible',
  nombre: 'Ilegible',
  categoria: 'Documento',
  aplicaA: 'Observacion',
  textoAlTercero: 'No se puede leer el documento.',
  base: true,
  activo: true,
};

const CAMARA = requisito({
  id: 'r-camara',
  tipoDocumento: { id: 'td-cc', codigo: 'CAMARA', nombre: 'Cámara de comercio', tipoCanonico: 'CamaraComercio', ambito: 'Global' },
});

function abrir(dialogo: DialogoDeArea) {
  const fetch = vi.fn(() => new Promise<Response>(() => undefined));
  configurarCliente({ fetch });
  const datos: ExpedienteDto = { ...expediente('EnRevision'), requisitos: [requisito(), CAMARA] };
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient()}>
        <DialogoDecisionArea dialogo={dialogo} expediente={datos} empresaId="emp" motivos={[MOTIVO]} aplica onCerrar={() => undefined} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return fetch;
}

afterEach(() => reiniciarCliente());

describe('DialogoDecisionArea: nada se envía con el formulario incompleto', () => {
  it('«Observar» empieza deshabilitado con 0 documentos y dice qué falta', () => {
    const fetch = abrir({ tipo: 'observarArea', area: area() });
    expect(screen.getByRole('button', { name: 'Observar' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Observar 0/ })).not.toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('Marca al menos un documento.');
    expect(fetch).not.toHaveBeenCalled();
  });

  it('se habilita solo cuando cada documento marcado tiene al menos un motivo', async () => {
    const usuario = userEvent.setup();
    abrir({ tipo: 'observarArea', area: area() });

    await usuario.click(screen.getByRole('checkbox', { name: /Registro Único Tributario/ }));
    expect(screen.getByRole('button', { name: 'Observar 1 documento' })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Falta el motivo de 1 documento.');

    await usuario.click(screen.getByRole('checkbox', { name: /Ilegible/ }));
    expect(screen.getByRole('button', { name: 'Observar 1 documento' })).toBeEnabled();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('checkbox', { name: /Cámara de comercio/ }));
    expect(screen.getByRole('button', { name: 'Observar 2 documentos' })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Falta el motivo de 1 documento.');

    // Quitar el único motivo de un documento muestra el error en ese documento (ya lo tocó).
    await usuario.click(screen.getByRole('checkbox', { name: /Cámara de comercio/ }));
    await usuario.click(screen.getByRole('checkbox', { name: /Ilegible/ }));
    expect(screen.getByText('Elige al menos un motivo.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Observar 1 documento' })).toBeDisabled();
  });

  it('«Aprobar área» sin documentos por decidir queda deshabilitado', () => {
    abrir({ tipo: 'aprobarArea', area: area(), documentos: [] });
    expect(screen.getByRole('button', { name: /Aprobar área/ })).toBeDisabled();
  });

  it('«Aprobar área» con documentos por decidir se puede enviar', () => {
    abrir({ tipo: 'aprobarArea', area: area(), documentos: [{ requisitoId: 'r-rut', versionVigente: 1 }] });
    expect(screen.getByRole('button', { name: 'Aprobar área (1 documento)' })).toBeEnabled();
  });
});
