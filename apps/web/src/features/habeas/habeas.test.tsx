import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type SolicitudHabeasDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import '../../i18n';
import { PaginaHabeasData } from '../configuracion/habeas/PaginaHabeasData';
import { PaginaHabeasPlataforma } from '../plataforma/PaginaHabeasPlataforma';
import { chipDeSolicitud, comoAccion, comoAlcance, comoTipo, cuerpoDeRespuesta, estadoDePlazo, validarRespuesta } from './modelo';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const SOLICITUD: SolicitudHabeasDto = {
  id: 's1',
  radicado: 'HD-2026-000001',
  tipo: 'Consulta',
  alcance: null,
  estado: 'Recibida',
  resultado: null,
  destino: { empresaId: 'emp', nombre: 'Constructora Norte' },
  recibidaEn: '2026-11-02T15:00:00Z',
  venceEl: '2026-11-16',
  diasHabiles: 10,
  diasHabilesRestantes: 3,
  plazo: 'EnPlazo',
  dentroDelPlazo: null,
  resueltaEn: null,
  detalle: null,
  respuesta: null,
  contenidoSuprimido: false,
  constanciaHash: null,
  titular: { nombre: 'Ana Gómez', identificacion: 'CC 52.123.456', correo: 'ana@aceros.co' },
};
const VENCIDA: SolicitudHabeasDto = { ...SOLICITUD, id: 's2', radicado: 'HD-2026-000002', tipo: 'Correccion', diasHabiles: 15, diasHabilesRestantes: -2, plazo: 'Vencida', detalle: 'Mi razón social cambió.' };
const SUPRESION: SolicitudHabeasDto = { ...SOLICITUD, id: 's3', radicado: 'HD-2026-000003', tipo: 'Supresion', alcance: 'Todo', destino: { empresaId: null, nombre: 'Validasoft' } };

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

interface Peticion {
  url: URL;
  metodo: string;
  cuerpo: unknown;
}

function montar(ui: ReactNode, lista: SolicitudHabeasDto[]) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url, 'http://localhost');
      const metodo = init?.method ?? 'GET';
      peticiones.push({ url, metodo, cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null });
      if (url.pathname.endsWith('/resumen')) return Promise.resolve(json({ abiertas: lista.length, porVencer: 0, vencidas: lista.filter((s) => s.plazo === 'Vencida').length, resueltas: 4 }));
      if (url.pathname.endsWith('/plan'))
        return Promise.resolve(
          json({
            solicitudId: 's3',
            alcance: 'Todo',
            suprimir: 5,
            retener: 2,
            anonimizar: 1,
            elementos: [
              { tipo: 'Documento', referencia: 'd1', descripcion: 'RUT (versión 1)', accion: 'Retener', baseLegal: 'Código de Comercio, art. 28', retenidaHasta: '2036-11-11' },
              { tipo: 'Documento', referencia: 'd2', descripcion: 'Cédula', accion: 'Suprimir', baseLegal: null, retenidaHasta: null },
            ],
          }),
        );
      if (metodo === 'POST') return Promise.resolve(json({ ...lista[0], estado: 'Resuelta', resultado: 'Atendida' }));
      return Promise.resolve(json({ datos: lista, siguienteCursor: null }));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>{ui}</QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
});

describe('modelo del habeas data', () => {
  it('pide una respuesta de 10 a 4.000 caracteres y no deja pasar una base legal larguísima', () => {
    expect(validarRespuesta({ decision: 'Atender', respuesta: '   ', baseLegal: '' })).toEqual(['respuesta-corta']);
    expect(validarRespuesta({ decision: 'Atender', respuesta: 'Listo, ya está.', baseLegal: '' })).toEqual([]);
    expect(validarRespuesta({ decision: 'Atender', respuesta: 'x'.repeat(4001), baseLegal: '' })).toEqual(['respuesta-larga']);
    expect(validarRespuesta({ decision: 'Rechazar', respuesta: 'No procede.', baseLegal: 'y'.repeat(501) })).toEqual(['base-legal-larga']);
  });

  it('la base legal solo viaja al rechazar', () => {
    expect(cuerpoDeRespuesta({ decision: 'Atender', respuesta: ' Hecho. Revisa tu portal. ', baseLegal: 'Ley 1581' })).toEqual({
      decision: 'Atender',
      respuesta: 'Hecho. Revisa tu portal.',
      baseLegal: null,
    });
    expect(cuerpoDeRespuesta({ decision: 'Rechazar', respuesta: 'No procede por deber legal.', baseLegal: ' Código de Comercio, art. 28 ' })).toEqual({
      decision: 'Rechazar',
      respuesta: 'No procede por deber legal.',
      baseLegal: 'Código de Comercio, art. 28',
    });
  });

  it('el chip dice cómo va el plazo y, si ya se respondió, el resultado', () => {
    expect(chipDeSolicitud(SOLICITUD)).toEqual({ estado: 'EnRevision', clave: 'enPlazo' });
    expect(chipDeSolicitud({ ...SOLICITUD, plazo: 'PorVencer' })).toEqual({ estado: 'PorVencer', clave: 'porVencer' });
    expect(chipDeSolicitud(VENCIDA)).toEqual({ estado: 'Vencido', clave: 'vencida' });
    expect(chipDeSolicitud({ ...SOLICITUD, estado: 'Resuelta', resultado: 'Parcial' })).toEqual({ estado: 'Aprobado', clave: 'parcial' });
    expect(chipDeSolicitud({ ...SOLICITUD, estado: 'Resuelta', resultado: 'Rechazada' })).toEqual({ estado: 'Rechazado', clave: 'rechazada' });
    expect(estadoDePlazo({ plazo: 'algo-nuevo' })).toBe('EnPlazo');
  });

  it('reconoce los valores que conoce y deja pasar el resto', () => {
    expect(comoTipo('Supresion')).toBe('Supresion');
    expect(comoTipo('Portabilidad')).toBeNull();
    expect(comoAlcance('Documentos')).toBe('Documentos');
    expect(comoAlcance(null)).toBeNull();
    expect(comoAccion('Retener')).toBe('Retener');
    expect(comoAccion('Archivar')).toBeNull();
  });
});

describe('Habeas data de la empresa', () => {
  it('sin supresiones:resolver dice que no hay acceso y no pide nada', () => {
    const peticiones = montar(<PaginaHabeasData />, []);
    expect(screen.getByRole('alert')).toHaveTextContent('Habeas data');
    expect(peticiones).toHaveLength(0);
  });

  it('lista las abiertas con su plazo en días hábiles y avisa de las vencidas', async () => {
    permisos.scopes = new Set([SCOPES.supresionesResolver]);
    const peticiones = montar(<PaginaHabeasData />, [VENCIDA, SOLICITUD]);

    expect(await screen.findByText('HD-2026-000001')).toBeInTheDocument();
    const lista = peticiones.find((p) => p.url.pathname === '/api/v1/solicitudes-habeas');
    expect(lista?.url.searchParams.get('estado')).toBe('abiertas');
    expect(screen.getByText(/Hay solicitudes urgentes: 0 por vencer .* y 1 vencidas/)).toBeInTheDocument();
    expect(screen.getByText(/lleva 2 días hábiles de retraso/)).toBeInTheDocument();
    expect(screen.getByText(/quedan 3 días hábiles/)).toBeInTheDocument();
    expect(screen.getAllByText('Ana Gómez')).toHaveLength(2);
    expect(screen.getAllByRole('button', { name: 'Responder' })).toHaveLength(2);
  });

  it('antes de responder dice a quién le llega y no envía sin una respuesta de verdad', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.supresionesResolver]);
    const peticiones = montar(<PaginaHabeasData />, [SOLICITUD]);

    await usuario.click((await screen.findAllByRole('button', { name: 'Responder' }))[0]!);
    const dialogo = await screen.findByRole('dialog', { name: /Responder la solicitud HD-2026-000001/ });
    expect(within(dialogo).getByText(/le llega al titular a ana@aceros.co/)).toBeInTheDocument();
    expect(within(dialogo).queryByRole('textbox', { name: /Base legal/ })).not.toBeInTheDocument();

    await usuario.click(within(dialogo).getByRole('button', { name: 'Responder al titular' }));
    expect(within(dialogo).getByText('Escribe una respuesta de al menos 10 caracteres.')).toBeInTheDocument();
    expect(peticiones.some((p) => p.metodo === 'POST')).toBe(false);

    await usuario.click(within(dialogo).getByRole('textbox', { name: /Respuesta al titular/ }));
    await usuario.paste('Tus datos son los de la ficha: nombre, NIT y contacto.');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Responder al titular' }));

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const envio = peticiones.find((p) => p.metodo === 'POST');
    expect(envio?.url.pathname).toBe('/api/v1/solicitudes-habeas/s1/resolver');
    expect(envio?.cuerpo).toEqual({ decision: 'Atender', respuesta: 'Tus datos son los de la ficha: nombre, NIT y contacto.', baseLegal: null });
  }, 20_000);

  it('al rechazar pide citar la base legal y la envía con la respuesta', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.supresionesResolver]);
    const peticiones = montar(<PaginaHabeasData />, [VENCIDA]);

    await usuario.click((await screen.findAllByRole('button', { name: 'Responder' }))[0]!);
    const dialogo = await screen.findByRole('dialog');
    await usuario.click(within(dialogo).getByRole('radio', { name: 'Rechazar' }));
    await usuario.click(within(dialogo).getByRole('textbox', { name: /Respuesta al titular/ }));
    await usuario.paste('No podemos cambiar la razón social de un certificado ya emitido.');
    await usuario.click(within(dialogo).getByRole('textbox', { name: /Base legal/ }));
    await usuario.paste('Código de Comercio, art. 28');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Responder al titular' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'POST')?.cuerpo).toEqual({
      decision: 'Rechazar',
      respuesta: 'No podemos cambiar la razón social de un certificado ya emitido.',
      baseLegal: 'Código de Comercio, art. 28',
    });
  }, 20_000);
});

describe('Habeas data de la plataforma', () => {
  it('antes de suprimir muestra qué se elimina, qué se conserva por ley y avisa que no se deshace', async () => {
    const usuario = userEvent.setup();
    const peticiones = montar(<PaginaHabeasPlataforma />, [SUPRESION]);

    await usuario.click((await screen.findAllByRole('button', { name: 'Responder' }))[0]!);
    const dialogo = await screen.findByRole('dialog', { name: /Responder la solicitud HD-2026-000003/ });
    expect(await within(dialogo).findByText('Se eliminan')).toBeInTheDocument();
    expect(within(dialogo).getByText('Se conservan')).toBeInTheDocument();
    expect(within(dialogo).getByText('Código de Comercio, art. 28 · hasta el', { exact: false })).toBeInTheDocument();
    expect(within(dialogo).getByText(/lo que se elimina no se puede recuperar/)).toBeInTheDocument();
    expect(within(dialogo).getByRole('button', { name: 'Suprimir y responder' })).toBeInTheDocument();
    expect(peticiones.some((p) => p.url.pathname === '/api/v1/plataforma/solicitudes-habeas/s3/plan')).toBe(true);

    // Rechazar no ejecuta nada: ya no se muestra el plan ni el botón de suprimir.
    await usuario.click(within(dialogo).getByRole('radio', { name: 'Rechazar' }));
    expect(within(dialogo).queryByText('Se eliminan')).not.toBeInTheDocument();
    expect(within(dialogo).getByRole('button', { name: 'Responder al titular' })).toBeInTheDocument();
  }, 20_000);
});
