import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { configurarCliente, reiniciarCliente, type AccesoSolicitadoDto, type SesionTerceroDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { MemoryRouter } from 'react-router';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useSesionTercero } from '../sesion/sesionTercero';
import { AvisoDeAccesoAislado, AvisoDeAccesosPorAprobar } from './AvisosDeAcceso';

const json = (estado: number, cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

function sesion(cambios: Partial<SesionTerceroDto> = {}): SesionTerceroDto {
  return { accessToken: 't', expira: '2099-01-01T00:00:00Z', terceroId: 'ter', otpVerificado: false, vinculos: [], aislada: false, terceroEnmascarado: null, ...cambios };
}

const SOLICITUD: AccesoSolicitadoDto = {
  id: 'a1',
  correo: 'compras@empresa-a.co',
  estado: 'Pendiente',
  desde: '2026-11-12T15:00:00Z',
  empresas: [{ empresa: 'Empresa A', fecha: '2026-11-12T15:00:00Z' }],
  decididoPor: null,
  decididoEn: null,
  motivo: null,
};

function montar(ui: React.ReactNode, ruta = '/empresas', solicitudes: AccesoSolicitadoDto[] = [SOLICITUD]) {
  const pedidas: string[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL) => {
      pedidas.push(new URL(entrada instanceof Request ? entrada.url : entrada.toString(), 'http://localhost').pathname);
      return Promise.resolve(json(200, solicitudes));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light', { app: 'public' })}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <MemoryRouter initialEntries={[ruta]}>{ui}</MemoryRouter>
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return pedidas;
}

afterEach(() => {
  reiniciarCliente();
  useSesionTercero.setState({ estado: 'sin-sesion', sesion: null });
});

describe('aviso del acceso aislado', () => {
  it('quien entra con un correo que aún no es contacto lee que su acceso espera la aprobación y que solo ve lo de su relación, con el nombre del tercero tapado', () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion({ aislada: true, terceroEnmascarado: 'P••••• d•• N•••' }) });
    montar(<AvisoDeAccesoAislado />);

    const aviso = screen.getByRole('status');
    expect(aviso).toHaveTextContent('Tu acceso está pendiente de aprobación');
    expect(aviso).toHaveTextContent('Tu acceso está pendiente de que un contacto de P••••• d•• N••• lo apruebe.');
    expect(aviso).toHaveTextContent('solo ves y subes lo de tu relación con la empresa que te invitó');
    expect(aviso).toHaveTextContent('no ves la bóveda ni la información de P••••• d•• N•••');
  });

  it('sin el nombre del tercero dice «este tercero»', () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion({ aislada: true }) });
    montar(<AvisoDeAccesoAislado />);
    expect(screen.getByRole('status')).toHaveTextContent('pendiente de que un contacto de este tercero lo apruebe');
  });

  it('con acceso completo no muestra nada', () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion() });
    montar(<AvisoDeAccesoAislado />);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});

describe('aviso de accesos por aprobar', () => {
  it('quien ya es contacto ve cuántos correos esperan su decisión y el enlace para revisarlos', async () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion() });
    montar(<AvisoDeAccesosPorAprobar />);

    expect(await screen.findByText('Tienes 1 acceso por aprobar')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Revisar' })).toHaveAttribute('href', '/accesos');
  });

  it('con varios correos por decidir lo dice en plural', async () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion() });
    montar(<AvisoDeAccesosPorAprobar />, '/empresas', [SOLICITUD, { ...SOLICITUD, id: 'a2', correo: 'otro@empresa-b.co' }]);
    expect(await screen.findByText('Tienes 2 accesos por aprobar')).toBeInTheDocument();
  });

  it('a un contacto sin verificar no le avisa de accesos por aprobar: no es quien los decide', async () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion() });
    const pedidas = montar(<AvisoDeAccesosPorAprobar />, '/empresas', [{ ...SOLICITUD, puedesDecidir: false, decideLaPlataforma: true }]);
    await vi.waitFor(() => expect(pedidas).toContain('/public/v1/accesos-solicitados'));
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('no se muestra con las solicitudes ya decididas', async () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion() });
    const pedidas = montar(<AvisoDeAccesosPorAprobar />, '/empresas', [{ ...SOLICITUD, estado: 'Aprobada' }]);
    await vi.waitFor(() => expect(pedidas).toContain('/public/v1/accesos-solicitados'));
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('en la propia pantalla de accesos no lo repite', async () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion() });
    const pedidas = montar(<AvisoDeAccesosPorAprobar />, '/accesos');
    await vi.waitFor(() => expect(pedidas).toContain('/public/v1/accesos-solicitados'));
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('una sesión aislada ni siquiera pregunta por las solicitudes (la API respondería 403)', async () => {
    useSesionTercero.setState({ estado: 'activa', sesion: sesion({ aislada: true }) });
    const pedidas = montar(<AvisoDeAccesosPorAprobar />);
    await new Promise((r) => setTimeout(r, 50));
    expect(pedidas).toEqual([]);
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });
});
