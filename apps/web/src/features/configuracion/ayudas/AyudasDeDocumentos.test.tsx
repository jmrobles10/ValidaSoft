import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type AyudaDeDocumentoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { useAvisos } from '../../../shared/useAvisos';
import { PaginaAyudasDeDocumentos } from './PaginaAyudasDeDocumentos';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

// Las interacciones con diálogos de MUI en jsdom tardan segundos y, con el equipo cargado, pueden pasar de los 20 s globales.
vi.setConfig({ testTimeout: 60_000 });

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

const RUT: AyudaDeDocumentoDto = {
  tipoDocumentoId: 't-rut',
  codigo: 'RUT_DIAN',
  nombre: 'RUT',
  ayudaDelGrupo: 'Sube el RUT vigente, descargado de la DIAN.',
  ayudaDeLaEmpresa: 'Constructora Norte exige el RUT del último año.',
  ayudaEfectiva: 'Constructora Norte exige el RUT del último año.',
  actualizadaEn: '2026-11-10T12:00:00Z',
};
const CAMARA: AyudaDeDocumentoDto = {
  tipoDocumentoId: 't-camara',
  codigo: 'CAMARA_COMERCIO',
  nombre: 'Cámara de comercio',
  ayudaDelGrupo: 'Con máximo 30 días de expedida.',
  ayudaDeLaEmpresa: null,
  ayudaEfectiva: 'Con máximo 30 días de expedida.',
  actualizadaEn: null,
};
const SEGURIDAD: AyudaDeDocumentoDto = { tipoDocumentoId: 't-seg', codigo: 'SEGURIDAD', nombre: 'Seguridad social', ayudaDelGrupo: null, ayudaDeLaEmpresa: null, ayudaEfectiva: null, actualizadaEn: null };

interface Peticion {
  metodo: string;
  ruta: string;
  cuerpo: unknown;
}

function montar(responderPut: (cuerpo: unknown) => Response = () => json(RUT), lista: AyudaDeDocumentoDto[] = [RUT, CAMARA, SEGURIDAD]) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    obtenerToken: () => 'tok',
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const texto = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const ruta = texto.split('?')[0] ?? texto;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      peticiones.push({ metodo, ruta, cuerpo });
      if (metodo === 'PUT') return Promise.resolve(responderPut(cuerpo));
      return Promise.resolve(json(lista));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>
        <PaginaAyudasDeDocumentos />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

const conPermisos = (...scopes: string[]) => (permisos.scopes = new Set(scopes));
const EDITOR = [SCOPES.configuracionLeer, SCOPES.configuracionEscribir];

async function filaDe(nombre: string) {
  const fila = (await screen.findByText(nombre)).closest('tr');
  if (!fila) throw new Error(`sin fila para ${nombre}`);
  return fila;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
  useAvisos.getState().cerrar();
});

describe('Ayuda de documentos', () => {
  it('sin configuracion:leer dice que la sección no es suya y no pide nada', () => {
    const peticiones = montar();
    expect(screen.getByRole('alert')).toHaveTextContent('Ayuda de documentos');
    expect(peticiones).toHaveLength(0);
  });

  it('lista cada documento con la ayuda que ve el tercero y de quién es: propia, del grupo o ninguna', async () => {
    conPermisos(...EDITOR);
    montar();

    const rut = await filaDe('RUT');
    expect(within(rut).getByText('Constructora Norte exige el RUT del último año.')).toBeInTheDocument();
    expect(within(rut).getByText('Propia')).toBeInTheDocument();
    const camara = await filaDe('Cámara de comercio');
    expect(within(camara).getByText('Con máximo 30 días de expedida.')).toBeInTheDocument();
    expect(within(camara).getByText('Del grupo')).toBeInTheDocument();
    const seguridad = await filaDe('Seguridad social');
    expect(within(seguridad).getByText('Sin ayuda')).toBeInTheDocument();
    expect(within(seguridad).getByText('El tercero no ve ninguna ayuda para este documento.')).toBeInTheDocument();
  });

  it('el drawer muestra la del grupo atenuada y de solo lectura, y la de la empresa editable con su contador', async () => {
    const usuario = userEvent.setup();
    conPermisos(...EDITOR);
    montar();
    await usuario.click(within(await filaDe('Cámara de comercio')).getByRole('button', { name: 'Editar la ayuda de Cámara de comercio' }));

    const drawer = await screen.findByRole('dialog', { name: /Ayuda de Cámara de comercio/ });
    expect(within(drawer).getByText('Con máximo 30 días de expedida.')).toBeInTheDocument();
    expect(within(drawer).getByText('Solo lectura: la define el grupo para todas sus empresas.')).toBeInTheDocument();
    const campo = within(drawer).getByRole('textbox', { name: 'Ayuda de Constructora Norte' });
    expect(campo).toBeEnabled();
    expect(campo).toHaveValue('');
    expect(within(drawer).getByText('0 / 2000')).toBeInTheDocument();
    // Sin ayuda propia no hay nada a lo que «volver».
    expect(within(drawer).queryByRole('button', { name: 'Volver a la del grupo' })).not.toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: 'Guardar' })).toBeDisabled();

    await usuario.type(campo, 'Que sea de este año.');
    expect(within(drawer).getByText('20 / 2000')).toBeInTheDocument();
    expect(within(drawer).getByRole('button', { name: 'Guardar' })).toBeEnabled();
  });

  it('guarda la ayuda de la empresa con PUT y lo confirma', async () => {
    const usuario = userEvent.setup();
    conPermisos(...EDITOR);
    const peticiones = montar();
    await usuario.click(within(await filaDe('Cámara de comercio')).getByRole('button', { name: /Editar la ayuda/ }));
    const drawer = await screen.findByRole('dialog', { name: /Ayuda de Cámara de comercio/ });
    await usuario.type(within(drawer).getByRole('textbox', { name: 'Ayuda de Constructora Norte' }), '  Que sea de este año.  ');
    await usuario.click(within(drawer).getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'PUT')).toMatchObject({ ruta: '/api/v1/ayudas-de-documentos/t-camara', cuerpo: { texto: 'Que sea de este año.' } });
    await waitFor(() => expect(screen.queryByRole('dialog', { name: /Ayuda de Cámara de comercio/ })).not.toBeInTheDocument());
    expect(useAvisos.getState().actual?.mensaje).toBe('Ayuda guardada: el tercero la ve desde ahora.');
  });

  it('«Volver a la del grupo» avisa qué pasará antes de borrar la propia', async () => {
    const usuario = userEvent.setup();
    conPermisos(...EDITOR);
    const peticiones = montar();
    await usuario.click(within(await filaDe('RUT')).getByRole('button', { name: /Editar la ayuda/ }));
    const drawer = await screen.findByRole('dialog', { name: /Ayuda de RUT/ });
    expect(within(drawer).getByText('Sube el RUT vigente, descargado de la DIAN.')).toBeInTheDocument();
    expect(within(drawer).getByRole('textbox', { name: 'Ayuda de Constructora Norte' })).toHaveValue('Constructora Norte exige el RUT del último año.');

    await usuario.click(within(drawer).getByRole('button', { name: 'Volver a la del grupo' }));
    const confirmacion = await screen.findByRole('dialog', { name: '¿Volver a la ayuda del grupo?' });
    expect(confirmacion).toHaveTextContent('Se borra el texto de Constructora Norte y los terceros que validen con ella verán la ayuda del grupo para RUT.');
    expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(false);

    await usuario.click(within(confirmacion).getByRole('button', { name: 'Volver a la del grupo' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'PUT')).toMatchObject({ ruta: '/api/v1/ayudas-de-documentos/t-rut', cuerpo: { texto: null } });
    await waitFor(() => expect(useAvisos.getState().actual?.mensaje).toBe('Ahora el tercero ve la ayuda del grupo.'));
  });

  it('el error del servidor se muestra en el drawer y deja lo escrito', async () => {
    const usuario = userEvent.setup();
    conPermisos(...EDITOR);
    montar(() => json({ status: 422, codigo: 'texto-demasiado-largo', title: 'Validación', detail: 'La ayuda admite hasta 2000 caracteres.' }, 422));
    await usuario.click(within(await filaDe('Cámara de comercio')).getByRole('button', { name: /Editar la ayuda/ }));
    const drawer = await screen.findByRole('dialog', { name: /Ayuda de Cámara de comercio/ });
    await usuario.type(within(drawer).getByRole('textbox', { name: 'Ayuda de Constructora Norte' }), 'Texto');
    await usuario.click(within(drawer).getByRole('button', { name: 'Guardar' }));

    expect(await within(drawer).findByText('El texto es demasiado largo; resúmelo.')).toBeInTheDocument();
    expect(within(drawer).getByRole('textbox', { name: 'Ayuda de Constructora Norte' })).toHaveValue('Texto');
  });

  it('sin configuracion:escribir solo se ve: el campo está deshabilitado y no hay botón de guardar', async () => {
    const usuario = userEvent.setup();
    conPermisos(SCOPES.configuracionLeer);
    montar();
    await usuario.click(within(await filaDe('RUT')).getByRole('button', { name: 'Ver la ayuda de RUT' }));
    const drawer = await screen.findByRole('dialog', { name: /Ayuda de RUT/ });
    expect(within(drawer).getByRole('textbox', { name: 'Ayuda de Constructora Norte' })).toBeDisabled();
    expect(within(drawer).queryByRole('button', { name: 'Guardar' })).not.toBeInTheDocument();
    expect(within(drawer).queryByRole('button', { name: 'Volver a la del grupo' })).not.toBeInTheDocument();
    expect(within(drawer).getAllByRole('button', { name: 'Cerrar' })).toHaveLength(2);
  });

  it('el filtro por origen deja solo los documentos con la ayuda propia', async () => {
    const usuario = userEvent.setup();
    conPermisos(...EDITOR);
    montar();
    await filaDe('RUT');
    await usuario.click(screen.getByRole('button', { name: /Ayuda/ }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Propia' }));
    expect(screen.queryByText('Cámara de comercio')).not.toBeInTheDocument();
    expect(screen.getByText('RUT')).toBeInTheDocument();
  });
});
