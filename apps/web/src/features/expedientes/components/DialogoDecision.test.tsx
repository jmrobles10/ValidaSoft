import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, getObtenerExpedienteQueryKey, reiniciarCliente, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { conEmpresa } from '../../../shared/consultas';
import { useAvisos } from '../../../shared/useAvisos';
import { useExpediente } from '../api';
import { area, areaDeRequisito, expediente, requisito, requisitoSeccion, veredicto } from '../datosPrueba';
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
function preparar({ inicial, dialogo, respuesta, recarga, cerrarDesmonta = true, alAbrir = inicial }: { inicial: ExpedienteDto; dialogo: Exclude<DialogoExpediente, DialogoDeArea>; respuesta: () => Response; recarga: ExpedienteDto | null; cerrarDesmonta?: boolean; alAbrir?: ExpedienteDto }) {
  // El detalle vuelve a pedir el expediente al abrirse: antes de decidir, el servidor devuelve `alAbrir` (lo mismo que
  // había en caché, salvo que otra persona lo haya cambiado).
  let decidido = false;
  const fetch = vi.fn((_url: RequestInfo | URL, init?: RequestInit) => {
    if ((init?.method ?? 'GET') === 'GET') {
      if (!decidido) return Promise.resolve(json(200, alAbrir));
      return recarga ? Promise.resolve(json(200, recarga)) : new Promise<Response>(() => undefined);
    }
    decidido = true;
    return Promise.resolve(respuesta());
  });
  configurarCliente({ fetch });
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
  return { cliente, onCerrar, fetch };
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
    // Otra área lo observó: el servidor ya lo tiene devuelto y el refresco al abrir lo trae.
    preparar({ inicial: enRevision, alAbrir: expediente('Observado', [pendiente]), dialogo: { tipo: 'veredicto', area: pendiente }, respuesta: () => json(200, aprobado), recarga: null });
    expect(screen.queryByText(YA_NO_APLICA)).not.toBeInTheDocument();

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

describe('DialogoDecision: nada se envía con el formulario incompleto', () => {
  it('eximir queda deshabilitado hasta escribir la justificación, y dice qué falta', async () => {
    const usuario = userEvent.setup();
    preparar({ inicial: { ...expediente('EnRevision'), requisitos: [requisito()] }, dialogo: { tipo: 'eximir', requisito: requisito() }, respuesta: () => json(200, expediente('EnRevision')), recarga: null });

    const boton = screen.getByRole('button', { name: 'Eximir documento' });
    expect(boton).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Falta llenar «Justificación».');
    await usuario.type(screen.getByRole('textbox', { name: /Justificación/ }), 'Lo exime la política de la empresa');
    expect(boton).toBeEnabled();
    expect(screen.queryByRole('status')).not.toBeInTheDocument();
  });

  it('el veredicto «Observado» pide al menos un motivo; «Aprobado» se envía sin más', async () => {
    const usuario = userEvent.setup();
    preparar({ inicial: expediente('EnRevision'), dialogo: { tipo: 'veredicto', area: area() }, respuesta: () => json(200, expediente('EnRevision')), recarga: null });

    expect(screen.getByRole('button', { name: /Aprobar/ })).toBeEnabled();
    await usuario.click(screen.getByRole('radio', { name: /Observar/ }));
    expect(screen.getByRole('button', { name: /Observar/ })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Elige al menos un motivo.');
  });
});

describe('DialogoDecision: aprobar un requisito con una discrepancia crítica en el cruce (F5)', () => {
  const critico = { estado: 'Cruzado', discrepanciaCritica: true, cruces: [], sinCruce: [] };
  const general = requisitoSeccion({ id: 'r-general', areas: [areaDeRequisito()] }, { cruce: critico });
  const decision = { tipo: 'veredictoRequisito' as const, requisito: general, areaRequisito: general.areas[0]!, nombreArea: 'Jurídica' };

  function abrirConDecision(dialogo: typeof decision, requisitoDelExpediente = general) {
    return preparar({
      inicial: { ...expediente('EnRevision'), requisitos: [requisitoDelExpediente] },
      dialogo,
      respuesta: () => json(200, expediente('EnRevision')),
      recarga: null,
    });
  }

  it('«Aprobado» pide la justificación (al menos 10 caracteres) y la envía', async () => {
    const usuario = userEvent.setup();
    const { fetch: peticiones } = abrirConDecision(decision);

    const confirmar = await screen.findByRole('button', { name: 'Aprobar' });
    expect(confirmar).toBeDisabled();
    expect(screen.getByRole('alert')).toHaveTextContent('difiere en campos críticos de Información general');
    expect(screen.getByRole('status')).toHaveTextContent('Escribe la justificación de la aprobación');

    await usuario.type(screen.getByRole('textbox', { name: /Justificación de la aprobación/ }), 'corto');
    expect(confirmar).toBeDisabled();
    await usuario.type(screen.getByRole('textbox', { name: /Justificación de la aprobación/ }), ' pero verificado con la Cámara.');
    expect(confirmar).toBeEnabled();

    await usuario.click(confirmar);
    await waitFor(() => {
      const envio = peticiones.mock.calls.map(([, init]) => init).find((init) => init?.method === 'POST');
      expect(JSON.parse(typeof envio?.body === 'string' ? envio.body : '{}')).toMatchObject({ veredicto: 'Aprobado', sobreVersion: 3, justificacion: 'corto pero verificado con la Cámara.' });
    });
  });

  it('al observar no se pide la justificación: el cruce solo manda al aprobar', async () => {
    const usuario = userEvent.setup();
    abrirConDecision(decision);

    expect(await screen.findByRole('textbox', { name: /Justificación de la aprobación/ })).toBeInTheDocument();
    await usuario.click(screen.getByRole('radio', { name: /Observar/ }));
    expect(screen.queryByRole('textbox', { name: /Justificación de la aprobación/ })).not.toBeInTheDocument();
  });

  it('sin diferencias críticas «Aprobado» se envía sin justificación', async () => {
    abrirConDecision({ ...decision, requisito: { ...general, seccion: { ...general.seccion!, cruce: { ...critico, discrepanciaCritica: false } } } }, {
      ...general,
      seccion: { ...general.seccion!, cruce: { ...critico, discrepanciaCritica: false } },
    });
    expect(await screen.findByRole('button', { name: 'Aprobar' })).toBeEnabled();
    expect(screen.queryByRole('textbox', { name: /Justificación de la aprobación/ })).not.toBeInTheDocument();
  });
});

describe('DialogoDecision: desaprobar (DS-1, DS-2)', () => {
  const dosEtapas = (estado: 'EnRevision' | 'Aprobado'): ExpedienteDto => ({
    ...expediente(estado),
    etapaActual: 2,
    etapas: [
      { orden: 1, estado: 'Aprobada', activadaEn: null, aprobadaEn: null, areas: [area({ areaId: 'a-compras', nombre: 'Compras', estado: 'Aprobada', veredictos: [veredicto()] })] },
      { orden: 2, estado: estado === 'Aprobado' ? 'Aprobada' : 'Activa', activadaEn: null, aprobadaEn: null, areas: [area({ areaId: 'a-cumplimiento', nombre: 'Cumplimiento' })] },
    ],
  });

  /** El cuerpo y la ruta de la decisión (el POST), no las lecturas. */
  const posts = (fetch: { mock: { calls: readonly (readonly [RequestInfo | URL, RequestInit?])[] } }) =>
    fetch.mock.calls
      .filter(([, init]) => init?.method === 'POST')
      .map(([url, init]) => ({
        url: typeof url === 'string' ? url : url instanceof URL ? url.href : url.url,
        cuerpo: (typeof init?.body === 'string' ? JSON.parse(init.body) : null) as unknown,
      }));

  it('reabrir una etapa exige la justificación y la envía a la etapa elegida', async () => {
    const usuario = userEvent.setup();
    const inicial = dosEtapas('EnRevision');
    const { fetch, onCerrar } = preparar({
      inicial,
      dialogo: { tipo: 'reabrirEtapa', etapa: inicial.etapas[0]! },
      respuesta: () => json(200, inicial),
      recarga: null,
    });

    expect(screen.getByRole('heading', { name: /Reabrir la etapa 1/ })).toBeInTheDocument();
    const boton = screen.getByRole('button', { name: 'Reabrir etapa' });
    expect(boton).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Falta llenar «Justificación».');

    await usuario.type(screen.getByRole('textbox', { name: /Justificación/ }), 'Faltó revisar la garantía');
    await usuario.click(boton);

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(posts(fetch)).toEqual([{ url: '/api/v1/expedientes/e1/etapas/1/reabrir', cuerpo: { justificacion: 'Faltó revisar la garantía' } }]);
  });

  it('desaprobar un expediente aprobado avisa lo que pasa con el vínculo y el certificado y deja elegir desde qué etapa', async () => {
    const usuario = userEvent.setup();
    const inicial = dosEtapas('Aprobado');
    const { fetch, onCerrar } = preparar({ inicial, dialogo: { tipo: 'desaprobar' }, respuesta: () => json(200, dosEtapas('EnRevision')), recarga: null });

    expect(screen.getByText(/su certificado se revoca/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Desaprobar expediente' })).toBeDisabled();

    await usuario.type(screen.getByRole('textbox', { name: /Justificación/ }), 'Apareció una inhabilidad');
    await usuario.click(screen.getByRole('combobox', { name: /Se revisa de nuevo desde/ }));
    await usuario.click(screen.getByRole('option', { name: /La primera etapa/ }));
    await usuario.click(screen.getByRole('button', { name: 'Desaprobar expediente' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(posts(fetch)).toEqual([{ url: '/api/v1/expedientes/e1/desaprobar', cuerpo: { justificacion: 'Apareció una inhabilidad', etapa: 1 } }]);
  });

  it('sin elegir etapa se desaprueba desde la última', async () => {
    const usuario = userEvent.setup();
    const { fetch, onCerrar } = preparar({ inicial: dosEtapas('Aprobado'), dialogo: { tipo: 'desaprobar' }, respuesta: () => json(200, dosEtapas('EnRevision')), recarga: null });

    await usuario.type(screen.getByRole('textbox', { name: /Justificación/ }), 'Revisar de nuevo');
    await usuario.click(screen.getByRole('button', { name: 'Desaprobar expediente' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(posts(fetch)).toEqual([{ url: '/api/v1/expedientes/e1/desaprobar', cuerpo: { justificacion: 'Revisar de nuevo', etapa: null } }]);
  });

  it('volver a la fase inicial envía la justificación y deja de aplicar si el expediente ya está al comienzo', async () => {
    const usuario = userEvent.setup();
    const inicial = dosEtapas('EnRevision');
    const { fetch, onCerrar } = preparar({ inicial, dialogo: { tipo: 'faseInicial' }, respuesta: () => json(200, inicial), recarga: null });

    await usuario.type(screen.getByRole('textbox', { name: /Justificación/ }), 'Cambió la política');
    await usuario.click(screen.getByRole('button', { name: 'Volver a la fase inicial' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(posts(fetch)).toEqual([{ url: '/api/v1/expedientes/e1/fase-inicial', cuerpo: { justificacion: 'Cambió la política' } }]);
  });

  it('si otra persona ya devolvió el expediente al comienzo, lo explica y no deja enviar', async () => {
    preparar({ inicial: dosEtapas('EnRevision'), alAbrir: expediente('EnRevision'), dialogo: { tipo: 'faseInicial' }, respuesta: () => json(200, expediente('EnRevision')), recarga: null });

    expect(await screen.findByText(YA_NO_APLICA)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Volver a la fase inicial' })).toBeDisabled();
  });
});
