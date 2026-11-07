import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, getObtenerExpedienteQueryKey, reiniciarCliente, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { conEmpresa } from '../../../shared/consultas';
import { useAvisos } from '../../../shared/useAvisos';
import { useExpediente } from '../api';
import { area, expediente, veredicto } from '../datosPrueba';
import { dialogoAplica, type ContextoRevisor, type DialogoDeArea, type DialogoExpediente } from '../reglas';
import { DialogoDecision } from './DialogoDecision';

const EMPRESA = 'emp';
const CLAVE = conEmpresa(getObtenerExpedienteQueryKey('e1'), EMPRESA);
const CONTEXTO: ContextoRevisor = { scopes: new Set([SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.expedientesAdministrar]), usuarioId: 'u-1', esMiembro: true };

const json = (estado: number, cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

/** Igual que el detalle: el diálogo lee el expediente de la caché y `aplica` se recalcula con cada versión. */
function Detalle({ dialogo, cerrarDesmonta, onCerrar }: { dialogo: Exclude<DialogoExpediente, DialogoDeArea>; cerrarDesmonta: boolean; onCerrar: () => void }) {
  const consulta = useExpediente(EMPRESA, 'e1');
  const [abierto, setAbierto] = useState(true);
  if (!consulta.data || !abierto) return null;
  return (
    <DialogoDecision
      dialogo={dialogo}
      expediente={consulta.data}
      empresaId={EMPRESA}
      motivos={[]}
      aplica={dialogoAplica(dialogo, consulta.data, () => CONTEXTO)}
      onCerrar={() => {
        onCerrar();
        if (cerrarDesmonta) setAbierto(false);
      }}
    />
  );
}

/**
 * `respuesta` contesta la decisión (POST); la recarga del expediente (GET) contesta `recarga`, o nunca si es `null`
 * (una red lenta: el diálogo no debe esperarla).
 */
function preparar({ inicial, dialogo, respuesta, recarga, cerrarDesmonta = true }: { inicial: ExpedienteDto; dialogo: Exclude<DialogoExpediente, DialogoDeArea>; respuesta: () => Response; recarga: ExpedienteDto | null; cerrarDesmonta?: boolean }) {
  configurarCliente({
    fetch: vi.fn((_url: RequestInfo | URL, init?: RequestInit) => {
      if ((init?.method ?? 'GET') === 'GET') return recarga ? Promise.resolve(json(200, recarga)) : new Promise<Response>(() => undefined);
      return Promise.resolve(respuesta());
    }),
  });
  const cliente = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, retry: false } } });
  cliente.setQueryData(CLAVE, inicial);
  const onCerrar = vi.fn();
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={cliente}>
        <Detalle dialogo={dialogo} cerrarDesmonta={cerrarDesmonta} onCerrar={onCerrar} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { cliente, onCerrar };
}

const YA_NO_APLICA = /El expediente cambió y esta decisión ya no aplica/;

afterEach(() => {
  reiniciarCliente();
  useAvisos.getState().cerrar();
});

describe('DialogoDecision', () => {
  const pendiente = area();
  const enRevision = expediente('EnRevision', [pendiente]);
  const aprobado = expediente('Aprobado', [area({ estado: 'Aprobada', aprobaciones: 1, aprobacionesFaltantes: 0, miembrosPendientes: [], veredictos: [veredicto()] })]);

  it('al aprobar se cierra con la respuesta, sin esperar la recarga, y confirma que el expediente quedó aprobado', async () => {
    const usuario = userEvent.setup();
    const { onCerrar } = preparar({ inicial: enRevision, dialogo: { tipo: 'veredicto', area: pendiente }, respuesta: () => json(200, aprobado), recarga: null });

    await usuario.click(screen.getByRole('button', { name: 'Aprobar' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(useAvisos.getState().actual?.mensaje).toBe('Expediente NORTE-E-2026-000001 aprobado: ya no falta ninguna área.');
  });

  it('el cambio que produce la propia decisión no muestra «ya no aplica»', async () => {
    const usuario = userEvent.setup();
    // El diálogo sigue montado tras onCerrar: así se ve qué pinta con el expediente ya aprobado en la caché.
    const { onCerrar } = preparar({ inicial: enRevision, dialogo: { tipo: 'veredicto', area: pendiente }, respuesta: () => json(200, aprobado), recarga: aprobado, cerrarDesmonta: false });

    await usuario.click(screen.getByRole('button', { name: 'Aprobar' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(screen.queryByText(YA_NO_APLICA)).not.toBeInTheDocument();
  });

  it('si otra persona cambió el expediente antes de enviar, lo explica y no deja confirmar', async () => {
    const { cliente } = preparar({ inicial: enRevision, dialogo: { tipo: 'veredicto', area: pendiente }, respuesta: () => json(200, aprobado), recarga: null });
    expect(screen.queryByText(YA_NO_APLICA)).not.toBeInTheDocument();

    act(() => {
      cliente.setQueryData(CLAVE, expediente('Observado', [pendiente]));
    });

    expect(await screen.findByText(YA_NO_APLICA)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Aprobar' })).toBeDisabled();
  });

  it('ante un 409 deja el diálogo abierto con el mensaje del código', async () => {
    const usuario = userEvent.setup();
    const { onCerrar } = preparar({
      inicial: enRevision,
      dialogo: { tipo: 'veredicto', area: pendiente },
      respuesta: () => json(409, { codigo: 'area-ya-se-pronuncio', detail: 'El área ya se pronunció.' }),
      recarga: null,
    });

    await usuario.click(screen.getByRole('button', { name: 'Aprobar' }));

    expect(await screen.findByText(/El área ya se pronunció en esta ronda/)).toBeInTheDocument();
    expect(onCerrar).not.toHaveBeenCalled();
  });

  it('el reenvío también se cierra con la respuesta aunque el expediente deje de estar observado', async () => {
    const usuario = userEvent.setup();
    const observado = expediente('Observado', [area({ estado: 'Observada', veredictos: [veredicto({ veredicto: 'Observado' })] })]);
    const { onCerrar } = preparar({ inicial: observado, dialogo: { tipo: 'reenviar' }, respuesta: () => json(200, enRevision), recarga: enRevision, cerrarDesmonta: false });

    await usuario.click(screen.getByRole('button', { name: 'Reenviar' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(screen.queryByText(YA_NO_APLICA)).not.toBeInTheDocument();
    expect(useAvisos.getState().actual?.mensaje).toBe('Expediente reenviado; vuelven a revisar las áreas que observaron.');
  });
});
