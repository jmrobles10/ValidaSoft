import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { DialogoEximir } from './DialogoEximir';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const TIPOS = [
  { id: 'tipo-rut', codigo: 'RUT', nombre: 'Registro Único Tributario', activo: true },
  { id: 'tipo-camara', codigo: 'CAMARA_COMERCIO', nombre: 'Cámara de comercio', activo: true },
  { id: 'tipo-viejo', codigo: 'VIEJO', nombre: 'Documento retirado', activo: false },
];

const json = (estado: number, cuerpo: unknown) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

interface Peticion {
  url: string;
  metodo: string;
  cabeceras: Headers;
  cuerpo: unknown;
}

function montar(respuesta: () => Response, documentoInicial?: string) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      if (url.includes('/tipos-documento')) return Promise.resolve(json(200, TIPOS));
      peticiones.push({ url, metodo: init?.method ?? 'GET', cabeceras: new Headers(init?.headers), cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : null });
      return Promise.resolve(respuesta());
    }),
  });
  const onCerrar = vi.fn();
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <DialogoEximir empresaId="emp" vinculoId="v1" tercero="Ferretería El Tornillo" documentoInicial={documentoInicial} onCerrar={onCerrar} />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return { peticiones, onCerrar };
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.getState().cerrar();
});

describe('DialogoEximir (EX-5)', () => {
  it('pide qué se exime y la justificación: el botón dice qué falta y se habilita al completar', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    const usuario = userEvent.setup();
    const { peticiones } = montar(() => json(201, {}));

    expect(screen.getByText('Ferretería El Tornillo')).toBeInTheDocument();
    const boton = screen.getByRole('button', { name: 'Eximir' });
    expect(boton).toBeDisabled();
    expect(screen.getByText('Elige al menos un documento o una sección, o exime de todo.')).toBeInTheDocument();

    await usuario.click(await screen.findByRole('checkbox', { name: 'Cámara de comercio' }));
    expect(screen.getByText('Escribe la justificación.')).toBeInTheDocument();
    await usuario.type(screen.getByLabelText(/Justificación/), '   ');
    expect(boton).toBeDisabled();
    await usuario.type(screen.getByLabelText(/Justificación/), 'La gestiona el área legal.');
    expect(boton).toBeEnabled();
    expect(peticiones).toHaveLength(0);
  });

  it('ofrece los documentos activos del grupo y las secciones de la ficha', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    montar(() => json(201, {}));

    expect(await screen.findByRole('checkbox', { name: 'Registro Único Tributario' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Cámara de comercio' })).toBeInTheDocument();
    expect(screen.queryByRole('checkbox', { name: 'Documento retirado' })).not.toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Información general' })).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Información financiera' })).toBeInTheDocument();
  });

  it('exime de los documentos y secciones elegidos: la petición lleva la empresa activa y cierra con el aviso', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    const usuario = userEvent.setup();
    const { peticiones, onCerrar } = montar(() => json(201, { id: 'ex1' }));

    await usuario.click(await screen.findByRole('checkbox', { name: 'Registro Único Tributario' }));
    await usuario.click(screen.getByRole('checkbox', { name: 'Información general' }));
    await usuario.type(screen.getByLabelText(/Justificación/), 'La renueva Contabilidad.');
    await usuario.click(screen.getByRole('button', { name: 'Eximir' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(peticiones).toHaveLength(1);
    expect(peticiones[0]).toMatchObject({ url: '/api/v1/vinculos/v1/exenciones', metodo: 'POST' });
    expect(peticiones[0]?.cabeceras.get('X-Empresa')).toBe('emp');
    expect(peticiones[0]?.cuerpo).toEqual({
      todos: false,
      objetivos: [
        { tipo: 'Documento', tipoDocumentoTenantId: 'tipo-rut' },
        { tipo: 'Seccion', seccion: 'FICHA_GENERAL' },
      ],
      justificacion: 'La renueva Contabilidad.',
      hasta: null,
    });
    expect(useAvisos.getState().actual?.mensaje).toBe('Vínculo eximido. Quedó en su historial.');
  });

  it('de todo no pide elegir nada y no manda objetivos', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    const usuario = userEvent.setup();
    const { peticiones, onCerrar } = montar(() => json(201, { id: 'ex1' }));

    await usuario.click(screen.getByRole('radio', { name: /De todo/ }));
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument();
    await usuario.type(screen.getByLabelText(/Justificación/), 'Proveedor estratégico.');
    await usuario.click(screen.getByRole('button', { name: 'Eximir' }));

    await waitFor(() => expect(onCerrar).toHaveBeenCalled());
    expect(peticiones[0]?.cuerpo).toEqual({ todos: true, objetivos: null, justificacion: 'Proveedor estratégico.', hasta: null });
  });

  it('al eximir desde una fila de vencimientos llega con ese documento ya elegido', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    montar(() => json(201, {}), 'tipo-camara');

    expect(await screen.findByRole('checkbox', { name: 'Cámara de comercio' })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'Registro Único Tributario' })).not.toBeChecked();
  });

  it('sin permiso de configuración no lista los documentos y lo dice, pero deja elegir secciones', () => {
    montar(() => json(201, {}));

    expect(screen.getByText(/No se pudo cargar la lista de documentos/)).toBeInTheDocument();
    expect(screen.getByRole('checkbox', { name: 'Información tributaria' })).toBeInTheDocument();
  });

  it('muestra el motivo que da la API (otra exención vigente) y deja el diálogo abierto para corregir', async () => {
    permisos.scopes = new Set([SCOPES.configuracionLeer]);
    const usuario = userEvent.setup();
    const { onCerrar } = montar(() => json(409, { codigo: 'exencion-superpuesta', title: 'exencion-superpuesta', status: 409, detail: 'Ya hay una exención.' }));

    await usuario.click(await screen.findByRole('checkbox', { name: 'Cámara de comercio' }));
    await usuario.type(screen.getByLabelText(/Justificación/), 'Otra vez.');
    await usuario.click(screen.getByRole('button', { name: 'Eximir' }));

    expect(await screen.findByText('Ya hay una exención vigente que cubre algo de esto: revócala antes de crear otra.')).toBeInTheDocument();
    expect(onCerrar).not.toHaveBeenCalled();
  });
});
