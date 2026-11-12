import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, SCOPES, type MonitoreoListasDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { coincidencia, listas } from '../datosPrueba';
import { problema, renderizar, simularApi, type LlamadaApi } from '../pruebasApi';
import { TarjetaMonitoreoListas } from './TarjetaMonitoreoListas';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

// Las interacciones con diálogos de MUI en jsdom tardan segundos y, con el equipo cargado, pueden pasar de los 20 s globales.
vi.setConfig({ testTimeout: 60_000 });

const OFICIAL = [SCOPES.expedientesLeer, SCOPES.cumplimientoDecidir, SCOPES.documentosDescargar];
const V1 = '/api/v1/vinculos/v1/monitoreo-listas';

function monitoreo(parcial: Partial<MonitoreoListasDto> = {}): MonitoreoListasDto {
  return {
    vinculoId: 'v1',
    expedienteId: 'e1',
    folio: 'N0001-E-2026-000001',
    frecuenciaDias: 30,
    proximaConsulta: '2026-12-12T14:00:00Z',
    listas: listas({ registradaPor: 'sistema', registradaEn: '2026-11-12T14:00:00Z' }),
    ...parcial,
  };
}

function montar(datos: MonitoreoListasDto | Response, scopes: readonly string[] = OFICIAL, manejadores: Record<string, (llamada: LlamadaApi) => unknown> = {}): LlamadaApi[] {
  permisos.scopes = new Set(scopes);
  const llamadas = simularApi({ [`GET ${V1}`]: () => datos, [`GET /api/v1/usuarios/resumen`]: () => [], ...manejadores });
  renderizar(<TarjetaMonitoreoListas vinculoId="v1" empresaId="emp" />);
  return llamadas;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  vi.unstubAllGlobals();
});

describe('Monitoreo de listas del vínculo (VE-12)', () => {
  it('muestra la última consulta, la próxima y la coincidencia nueva; descartarla pide justificación y llama al monitoreo, no al expediente', async () => {
    const llamadas = montar(monitoreo(), OFICIAL, { [`POST ${V1}/coincidencias/c1/resolver`]: () => monitoreo({ listas: listas({ estado: 'Satisfecha' }) }) });

    const tarjeta = within(await screen.findByRole('region', { name: 'Monitoreo de listas' }));
    expect(tarjeta.getByText(/El vínculo activo se reconsulta cada 30 días/)).toBeInTheDocument();
    expect(tarjeta.getByText('Coincidencia por analizar')).toBeInTheDocument();
    expect(tarjeta.getByText(/· Sistema$/)).toBeInTheDocument();
    expect(tarjeta.getByText('12/12/2026')).toBeInTheDocument();
    const fila = within(tarjeta.getByRole('listitem', { name: 'Ana Coincidencia · OFAC (SDN)' }));

    await userEvent.click(fila.getByRole('button', { name: 'Descartar' }));
    const dialogo = within(await screen.findByRole('dialog'));
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar decisión' }));
    expect(dialogo.getByText('Escribe la justificación.')).toBeInTheDocument();
    expect(llamadas.some((l) => l.metodo === 'POST')).toBe(false);

    await userEvent.type(dialogo.getByLabelText(/Justificación/), 'Homónimo: otra cédula.');
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar decisión' }));
    const resolver = llamadas.find((l) => l.ruta === `${V1}/coincidencias/c1/resolver`);
    expect(resolver?.cuerpo).toEqual({ decision: 'Descartada', justificacion: 'Homónimo: otra cédula.' });
  });

  it('al confirmar una coincidencia avisa que el vínculo sigue activo hasta que un administrador decida', async () => {
    let datos = monitoreo();
    montar(datos, OFICIAL, {
      [`GET ${V1}`]: () => datos,
      [`POST ${V1}/coincidencias/c1/resolver`]: () => {
        datos = monitoreo({ listas: listas({ estado: 'Fallida', coincidencias: [coincidencia({ decision: 'Confirmada', justificacion: 'Es la misma persona.', resueltaPor: 'sistema' })] }) });
        return datos;
      },
    });

    const tarjeta = within(await screen.findByRole('region', { name: 'Monitoreo de listas' }));
    expect(tarjeta.queryByText(/sigue activo hasta que un administrador/)).not.toBeInTheDocument();
    await userEvent.click(tarjeta.getByRole('button', { name: 'Confirmar' }));
    const dialogo = within(await screen.findByRole('dialog'));
    await userEvent.type(dialogo.getByLabelText(/Justificación/), 'Es la misma persona.');
    await userEvent.click(dialogo.getByRole('button', { name: 'Guardar decisión' }));

    expect(await tarjeta.findByText(/el vínculo sigue activo hasta que un administrador decida si lo bloquea o lo desvincula/)).toBeInTheDocument();
    expect(tarjeta.queryByRole('button', { name: 'Confirmar' })).not.toBeInTheDocument();
  });

  it('sin cumplimiento:decidir se ve pero no se decide; un monitoreo detenido lo dice', async () => {
    montar(monitoreo({ proximaConsulta: null }), [SCOPES.expedientesLeer]);

    const tarjeta = within(await screen.findByRole('region', { name: 'Monitoreo de listas' }));
    expect(tarjeta.getByText('Detenido (el vínculo no está activo).')).toBeInTheDocument();
    expect(tarjeta.queryByRole('button', { name: 'Descartar' })).not.toBeInTheDocument();
    expect(tarjeta.queryByRole('button', { name: 'Confirmar' })).not.toBeInTheDocument();
    expect(tarjeta.queryByRole('button', { name: 'Evidencia (PDF)' })).not.toBeInTheDocument();
  });

  it('un vínculo sin monitoreo no muestra la tarjeta', async () => {
    const llamadas = montar(problema(404, 'monitoreo-no-encontrado'));
    await vi.waitFor(() => expect(llamadas.some((l) => l.ruta === V1)).toBe(true));
    await vi.waitFor(() => expect(screen.queryByRole('region', { name: 'Monitoreo de listas' })).not.toBeInTheDocument());
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
