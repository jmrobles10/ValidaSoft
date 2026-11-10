import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type ExpedienteDto, type MotivoTipificadoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { area, expediente, requisito, requisitoSeccion } from '../datosPrueba';
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

function abrir(dialogo: DialogoDeArea, requisitos = [requisito(), CAMARA]) {
  const fetch = vi.fn((_url: RequestInfo | URL, _init?: RequestInit) => new Promise<Response>(() => undefined));
  configurarCliente({ fetch });
  const datos: ExpedienteDto = { ...expediente('EnRevision'), requisitos };
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

describe('DialogoDecisionArea: aprobar con una discrepancia crítica en el cruce (F5)', () => {
  const cruce = (discrepanciaCritica: boolean) => ({ estado: 'Cruzado', discrepanciaCritica, cruces: [], sinCruce: [] });
  const GENERAL = (discrepanciaCritica: boolean) => requisitoSeccion({ id: 'r-general' }, { cruce: cruce(discrepanciaCritica) });
  const POR_DECIDIR = [{ requisitoId: 'r-general', versionVigente: 3 }, { requisitoId: 'r-rut', versionVigente: 1 }];

  it('pide la justificación, no deja enviar sin ella y la manda junto con las versiones', async () => {
    const usuario = userEvent.setup();
    const fetch = abrir({ tipo: 'aprobarArea', area: area(), documentos: POR_DECIDIR }, [GENERAL(true), requisito()]);

    expect(screen.getByRole('alert')).toHaveTextContent('difiere en campos críticos de Información general');
    const boton = screen.getByRole('button', { name: 'Aprobar área (2 documentos)' });
    expect(boton).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Escribe la justificación de la aprobación');

    const campo = screen.getByRole('textbox', { name: /Justificación de la aprobación/ });
    await usuario.type(campo, 'corto');
    expect(boton).toBeDisabled();

    await usuario.clear(campo);
    await usuario.type(campo, 'La razón social cambió por fusión.');
    expect(boton).toBeEnabled();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();

    await usuario.click(boton);
    const [, init] = fetch.mock.calls[0] ?? [];
    expect(JSON.parse(typeof init?.body === 'string' ? init.body : '{}')).toEqual({
      veredicto: 'Aprobado',
      versiones: { 'r-general': 3, 'r-rut': 1 },
      justificacion: 'La razón social cambió por fusión.',
    });
  });

  it('sin diferencias críticas no pide justificación ni la envía', async () => {
    const usuario = userEvent.setup();
    const fetch = abrir({ tipo: 'aprobarArea', area: area(), documentos: POR_DECIDIR }, [GENERAL(false), requisito()]);

    expect(screen.queryByRole('textbox', { name: /Justificación/ })).not.toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Aprobar área (2 documentos)' }));
    const [, init] = fetch.mock.calls[0] ?? [];
    expect(JSON.parse(typeof init?.body === 'string' ? init.body : '{}')).toEqual({ veredicto: 'Aprobado', versiones: { 'r-general': 3, 'r-rut': 1 } });
  });

  it('un documento que respalda secciones con una diferencia crítica también la pide', () => {
    abrir({ tipo: 'aprobarArea', area: area(), documentos: [{ requisitoId: 'r-rut', versionVigente: 1 }] }, [
      requisito({ soporteDeSecciones: { sinAnalisis: false, discrepanciaCritica: true, secciones: [] } }),
    ]);
    expect(screen.getByRole('textbox', { name: /Justificación de la aprobación/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Aprobar área (1 documento)' })).toBeDisabled();
  });

  it('al observar no se pide justificación: el cruce solo manda al aprobar', () => {
    abrir({ tipo: 'observarArea', area: area() }, [GENERAL(true)]);
    expect(screen.queryByRole('textbox', { name: /Justificación de la aprobación/ })).not.toBeInTheDocument();
  });
});
