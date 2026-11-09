import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type EmpresaDto, type WebhookDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { useState, type ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DetalleWebhook } from './DetalleWebhook';
import { DialogoSecreto } from './DialogoSecreto';
import { DrawerWebhook } from './DrawerWebhook';
import { agruparTipos, alternarTipo, estadoWebhook, TODOS, validarUrl, validarWebhook } from './modelo';

const CATALOGO = [
  { tipo: 'vinculo.activado', descripcion: 'El vínculo quedó Activo.' },
  { tipo: 'expediente.aprobado', descripcion: 'El expediente se aprobó.' },
  { tipo: 'certificado.emitido', descripcion: 'Se emitió el certificado.' },
];
const EMPRESAS = [{ id: 'e1', nombre: 'Constructora Norte' }, { id: 'e2', nombre: 'Logística Sur' }] as unknown as EmpresaDto[];

const WEBHOOK: WebhookDto = {
  id: 'w1',
  sistemaId: 's1',
  sistema: 'SRM',
  url: 'https://srm.cliente.co/validasoft',
  descripcion: 'SRM producción',
  tipos: [TODOS],
  empresas: [TODOS],
  version: 1,
  activa: true,
  fallosSeguidos: 0,
  deshabilitadaEn: null,
  causaDeshabilitacion: null,
  creadaEn: '2026-11-08T15:00:00Z',
  ultimaEntregaEn: null,
  secretoAnteriorVence: null,
};

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

function montar(ui: ReactNode, responder: (url: string, metodo: string, cuerpo: unknown) => Response) {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      if (metodo !== 'GET') peticiones.push({ url, metodo, cuerpo });
      return Promise.resolve(responder(url, metodo, cuerpo));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{ui}</QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => reiniciarCliente());

describe('modelo de webhooks', () => {
  it('valida la URL como la API: https, o http solo hacia localhost', () => {
    expect(validarUrl('https://srm.cliente.co/hook')).toBeNull();
    expect(validarUrl('http://localhost:9000/hook')).toBeNull();
    expect(validarUrl('http://srm.cliente.co/hook')).toBe('url-no-permitida');
    expect(validarUrl('ftp://srm.cliente.co')).toBe('url-no-permitida');
    expect(validarUrl('no es una url')).toBe('url-invalida');
    expect(validarUrl('https://ana:clave@srm.cliente.co')).toBe('url-invalida');
    expect(validarUrl('  ')).toBe('url-requerida');
    expect(validarWebhook({ url: 'https://a.co', descripcion: '', tipos: [], empresas: [TODOS] })).toEqual(['tipos-requeridos']);
  });

  it('agrupa el catálogo por prefijo y desmarcar un tipo con «todos» deja los demás explícitos', () => {
    expect(agruparTipos(CATALOGO).map((g) => g.grupo)).toEqual(['vinculo', 'expediente', 'certificado']);
    expect(alternarTipo([TODOS], 'expediente.aprobado', CATALOGO.map((c) => c.tipo))).toEqual(['vinculo.activado', 'certificado.emitido']);
    expect(alternarTipo(['vinculo.activado'], 'certificado.emitido', [])).toEqual(['vinculo.activado', 'certificado.emitido']);
  });

  it('estado visible: activa, con fallos, deshabilitada por fallos o desactivada', () => {
    expect(estadoWebhook({ activa: true, fallosSeguidos: 0, causaDeshabilitacion: null })).toBe('activa');
    expect(estadoWebhook({ activa: true, fallosSeguidos: 3, causaDeshabilitacion: null })).toBe('conFallos');
    expect(estadoWebhook({ activa: false, fallosSeguidos: 20, causaDeshabilitacion: 'fallos-consecutivos' })).toBe('deshabilitadaPorFallos');
    expect(estadoWebhook({ activa: false, fallosSeguidos: 0, causaDeshabilitacion: 'manual' })).toBe('desactivada');
  });
});

/** Como en la página: al crear, el panel cede el paso al diálogo con el secreto. */
function CrearConSecreto() {
  const [creado, setCreado] = useState<WebhookDto | null>(null);
  return creado?.secreto ? (
    <DialogoSecreto secreto={creado.secreto} anteriorVence={null} onCerrar={() => setCreado(null)} />
  ) : (
    <DrawerWebhook webhook={null} catalogo={CATALOGO} empresas={EMPRESAS} onCerrar={() => undefined} onCreado={setCreado} />
  );
}

describe('crear un webhook', () => {
  it('«Crear webhook» queda deshabilitado hasta tener URL válida; manda tipos y empresas elegidos y muestra el secreto una vez', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar(<CrearConSecreto />, () => json({ ...WEBHOOK, secreto: 'whsec_abc123' }, 201));
    const crear = screen.getByRole('button', { name: 'Crear webhook' });
    expect(crear).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Escribe la dirección.');

    await usuario.type(screen.getByRole('textbox', { name: /Dirección/ }), 'http://srm.cliente.co/hook');
    await usuario.tab();
    expect(screen.getAllByText(/Usa https/).length).toBeGreaterThan(0);
    await usuario.clear(screen.getByRole('textbox', { name: /Dirección/ }));
    await usuario.type(screen.getByRole('textbox', { name: /Dirección/ }), 'https://srm.cliente.co/hook');

    await usuario.click(screen.getByRole('checkbox', { name: /expediente\.aprobado/ }));
    await usuario.click(screen.getByRole('radio', { name: 'Solo algunas empresas' }));
    expect(screen.getByRole('status')).toHaveTextContent('Elige al menos una empresa.');
    await usuario.click(screen.getByRole('checkbox', { name: 'Logística Sur' }));

    await waitFor(() => expect(crear).toBeEnabled());
    await usuario.click(crear);
    expect(await screen.findByText('whsec_abc123')).toBeInTheDocument();
    expect(screen.getByText(/es la única vez que lo mostramos/)).toBeInTheDocument();
    expect(peticiones[0]).toMatchObject({
      url: '/api/v1/webhooks',
      metodo: 'POST',
      cuerpo: { url: 'https://srm.cliente.co/hook', tipos: ['vinculo.activado', 'certificado.emitido'], empresas: ['e2'] },
    });
  }, 20_000);
});

describe('detalle de un webhook', () => {
  it('rotar el secreto pide confirmación y entrega el secreto nuevo; reenviar una entrega llama a la API', async () => {
    const usuario = userEvent.setup();
    const onSecreto = vi.fn();
    const entrega = { id: 'en1', webhookId: 'w1', eventoId: 'ev1', tipoEvento: 'expediente.aprobado', cursor: '1', empresaId: 'e1', estado: 'Fallida', intentos: 6, creadaEn: '2026-11-08T15:00:00Z', entregadaEn: null, proximoIntentoEn: null, historial: [{ numero: 1, intentoId: 'i1', fecha: '2026-11-08T15:00:00Z', codigoHttp: 500, duracionMs: 120, resultado: 'Fallido', error: null, manual: false }] };
    const peticiones = montar(<DetalleWebhook webhook={WEBHOOK} empresas={EMPRESAS} onEditar={() => undefined} onSecreto={onSecreto} />, (url, metodo) => {
      if (url.includes('/entregas') && metodo === 'GET') return json({ datos: [entrega], siguienteCursor: null });
      if (url.endsWith('/reenviar')) return json({ ...entrega, estado: 'Pendiente' }, 202);
      return json({ ...WEBHOOK, secreto: 'whsec_nuevo', secretoAnteriorVence: '2026-11-09T15:00:00Z' });
    });

    expect(await screen.findByText('Fallida')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: /Ver intentos/ }));
    expect(screen.getByText(/Intento 1 .* fallido · HTTP 500 · 120 ms/)).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Reenviar' }));
    await waitFor(() => expect(peticiones.some((p) => p.url === '/api/v1/webhooks/w1/entregas/en1/reenviar')).toBe(true));

    await usuario.click(screen.getByRole('button', { name: 'Rotar secreto' }));
    const dialogo = await screen.findByRole('dialog');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Rotar secreto' }));
    await waitFor(() => expect(onSecreto).toHaveBeenCalledWith(expect.objectContaining({ secreto: 'whsec_nuevo' })));
  }, 20_000);
});
