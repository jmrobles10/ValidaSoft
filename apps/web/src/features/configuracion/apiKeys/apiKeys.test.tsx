import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, type ApiKeyDto, type EmpresaDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { useState, type ReactNode } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { DialogoApiKey } from './DialogoApiKey';
import { DialogoClaveCreada } from './DialogoClaveCreada';
import { borradorApiKeyNuevo, cuerpoCrearApiKey, estadoApiKey, finDelDia, textoAlcance, TODO_EL_GRUPO, validarApiKey } from './modelo';
import { alternarGrupo, claveDeScope, resumirScopes, SCOPES_DEL_TENANT, scopesDeGrupo } from './scopes';

/** `Permisos.DelTenant` (src/Validasoft.Aplicacion/Comun/Permisos.cs): lo único que la API acepta en una llave. */
const DEL_TENANT = [
  'empresas:administrar', 'api-keys:administrar', 'sistemas:administrar', 'usuarios:administrar', 'configuracion:leer', 'configuracion:escribir',
  'terceros:leer', 'terceros:escribir', 'vinculos:leer', 'vinculos:escribir', 'vinculos:bloquear', 'cuentas:aprobar', 'auditoria:leer',
  'flujos:leer', 'flujos:escribir', 'expedientes:leer', 'expedientes:revisar', 'expedientes:administrar', 'documentos:leer', 'documentos:descargar',
  'certificados:leer', 'eventos:leer', 'webhooks:administrar', 'verificaciones:escribir', 'cumplimiento:decidir',
];

const EMPRESAS = [
  { id: 'e1', nombre: 'Constructora Norte', activa: true },
  { id: 'e2', nombre: 'Logística Sur', activa: true },
] as unknown as EmpresaDto[];

const LLAVE: ApiKeyDto = {
  id: 'k1',
  prefijo: '3f9a12bc',
  nombre: 'SRM producción',
  scopes: ['vinculos:leer', 'vinculos:escribir'],
  empresas: ['e2'],
  expiraEn: null,
  creadaEn: '2026-11-08T15:00:00Z',
  sandbox: false,
  valor: 'vsk_live_grupo_3f9a12bc0000000000000000000000000000000000000000000000000000abcd',
  sistemaId: null,
};

const json = (cuerpo: unknown, status = 200) => new Response(JSON.stringify(cuerpo), { status, headers: { 'Content-Type': 'application/json' } });

function montar(ui: ReactNode, responder: (url: string, metodo: string) => Response) {
  const peticiones: { url: string; metodo: string; cuerpo: unknown }[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url;
      const metodo = init?.method ?? 'GET';
      const cuerpo: unknown = typeof init?.body === 'string' ? JSON.parse(init.body) : null;
      if (metodo !== 'GET') peticiones.push({ url, metodo, cuerpo });
      return Promise.resolve(responder(url, metodo));
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

describe('permisos de una API key', () => {
  it('ofrece exactamente los scopes de Permisos.DelTenant, cada uno con su texto', () => {
    expect([...SCOPES_DEL_TENANT].sort()).toEqual([...DEL_TENANT].sort());
    expect(SCOPES_DEL_TENANT).not.toContain('plataforma:tenants');
    expect(claveDeScope('vinculos:escribir')).toBe('vinculosEscribir');
    expect(claveDeScope('api-keys:administrar')).toBe('apiKeysAdministrar');
    expect(claveDeScope('plataforma:tenants')).toBeNull();
  });

  it('resume por grupo y reconoce «todos los permisos»', () => {
    expect(resumirScopes(DEL_TENANT).todos).toBe(true);
    const resumen = resumirScopes(['vinculos:leer', 'vinculos:escribir', 'eventos:leer', 'eventos:leer', 'nuevo:scope']);
    expect(resumen.todos).toBe(false);
    expect(resumen.grupos).toEqual([
      { grupo: 'vinculos', cuantos: 2, total: 6 },
      { grupo: 'integracion', cuantos: 1, total: 2 },
    ]);
    expect(resumen.desconocidos).toEqual(['nuevo:scope']);
  });

  it('marcar un grupo agrega los que faltan; con el grupo completo, lo desmarca', () => {
    const integracion = scopesDeGrupo('integracion');
    expect(alternarGrupo(['eventos:leer', 'vinculos:leer'], 'integracion')).toEqual(['eventos:leer', 'vinculos:leer', 'webhooks:administrar']);
    expect(alternarGrupo([...integracion, 'vinculos:leer'], 'integracion')).toEqual(['vinculos:leer']);
  });
});

describe('modelo de API keys', () => {
  const ahora = new Date('2026-11-09T15:00:00Z');

  it('valida como la API: nombre, al menos un permiso, empresas y expiración futura', () => {
    expect(validarApiKey(borradorApiKeyNuevo(), ahora)).toEqual(['nombre-requerido', 'scopes-requeridos']);
    const base = { ...borradorApiKeyNuevo(), nombre: 'SRM', scopes: ['vinculos:leer'] };
    expect(validarApiKey(base, ahora)).toEqual([]);
    expect(validarApiKey({ ...base, empresas: [] }, ahora)).toEqual(['empresas-requeridas']);
    expect(validarApiKey({ ...base, expira: '2026-11-08' }, ahora)).toEqual(['expiracion-invalida']);
    // Hoy todavía vale: vence al final del día en Bogotá.
    expect(validarApiKey({ ...base, expira: '2026-11-09' }, ahora)).toEqual([]);
  });

  it('arma el cuerpo: todo el grupo viaja como null, la expiración al final del día y los scopes en orden estable', () => {
    expect(finDelDia('2026-12-31')).toBe('2027-01-01T04:59:59.000Z');
    expect(finDelDia('31/12/2026')).toBeNull();
    const cuerpo = cuerpoCrearApiKey({ nombre: '  SRM producción ', scopes: ['vinculos:escribir', 'empresas:administrar'], empresas: [TODO_EL_GRUPO], expira: '2026-12-31', sistemaId: '' });
    expect(cuerpo).toEqual({ nombre: 'SRM producción', scopes: ['empresas:administrar', 'vinculos:escribir'], empresas: null, expiraEn: '2027-01-01T04:59:59.000Z', sistemaId: null });
    expect(cuerpoCrearApiKey({ ...borradorApiKeyNuevo(), nombre: 'x', scopes: ['vinculos:leer'], empresas: ['e2'], sistemaId: 's1' })).toMatchObject({ empresas: ['e2'], expiraEn: null, sistemaId: 's1' });
  });

  it('estado y alcance visibles', () => {
    expect(estadoApiKey({ expiraEn: null }, ahora)).toBe('vigente');
    expect(estadoApiKey({ expiraEn: '2026-11-01T00:00:00Z' }, ahora)).toBe('vencida');
    expect(textoAlcance({ empresas: ['*'] }, EMPRESAS, 'Todo el grupo')).toBe('Todo el grupo');
    expect(textoAlcance({ empresas: ['e1', 'e2'] }, EMPRESAS, 'Todo el grupo')).toBe('Constructora Norte, Logística Sur');
  });
});

/** Como en la página: al crear, el formulario cede el paso a la clave; al cerrarla, la clave se descarta. */
function CrearConClave() {
  const [creada, setCreada] = useState<ApiKeyDto | null>(null);
  const [cerrada, setCerrada] = useState(false);
  if (cerrada) return <p>Cerrado</p>;
  return creada?.valor ? (
    <DialogoClaveCreada llave={creada} valor={creada.valor} empresas={EMPRESAS} onCerrar={() => setCerrada(true)} />
  ) : (
    <DialogoApiKey claveIdempotencia="idem-1" empresas={EMPRESAS} sistemas={[]} onCerrar={() => undefined} onCreada={setCreada} />
  );
}

describe('crear una API key', () => {
  it('pide nombre y permisos, manda el alcance elegido y muestra la clave completa una sola vez', async () => {
    const usuario = userEvent.setup();
    // Los textos se pegan: teclear sobre los diálogos de MUI pasaba del tiempo en la suite completa.
    const escribir = async (campo: HTMLElement, texto: string) => {
      await usuario.click(campo);
      await usuario.paste(texto);
    };
    const peticiones = montar(<CrearConClave />, () => json(LLAVE, 201));
    const crear = screen.getByRole('button', { name: 'Crear API key' });
    expect(crear).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent('Escribe el nombre.');

    await escribir(screen.getByRole('textbox', { name: /Nombre/ }), 'SRM producción');
    expect(screen.getByRole('status')).toHaveTextContent('Marca al menos un permiso.');
    await usuario.click(screen.getByRole('checkbox', { name: /Leer vínculos/ }));
    await usuario.click(screen.getByRole('checkbox', { name: /Crear vínculos, invitar/ }));
    await usuario.click(screen.getByRole('radio', { name: 'Solo algunas empresas' }));
    expect(screen.getByRole('status')).toHaveTextContent('Elige al menos una empresa.');
    await usuario.click(screen.getByRole('checkbox', { name: 'Logística Sur' }));

    await waitFor(() => expect(crear).toBeEnabled());
    await usuario.click(crear);

    expect(await screen.findByText(LLAVE.valor ?? '')).toBeInTheDocument();
    expect(screen.getByText(/no la volveremos a mostrar/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Copiar clave' })).toBeInTheDocument();
    expect(peticiones).toEqual([
      {
        url: '/api/v1/api-keys',
        metodo: 'POST',
        cuerpo: { nombre: 'SRM producción', scopes: ['vinculos:leer', 'vinculos:escribir'], empresas: ['e2'], expiraEn: null, sistemaId: null },
      },
    ]);

    await usuario.click(screen.getByRole('button', { name: 'Listo, ya la guardé' }));
    expect(screen.queryByText(LLAVE.valor ?? '')).not.toBeInTheDocument();
    expect(screen.getByText('Cerrado')).toBeInTheDocument();
  }, 20_000);

  it('el diálogo de la clave no se cierra con Escape: solo con «Listo»', async () => {
    const usuario = userEvent.setup();
    const onCerrar = vi.fn();
    montar(<DialogoClaveCreada llave={LLAVE} valor="vsk_live_grupo_abc" empresas={EMPRESAS} onCerrar={onCerrar} />, () => json({}));
    await usuario.keyboard('{Escape}');
    expect(onCerrar).not.toHaveBeenCalled();
    expect(screen.getByText('vsk_live_grupo_abc')).toBeInTheDocument();
    expect(screen.getByText('Logística Sur')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Listo, ya la guardé' }));
    expect(onCerrar).toHaveBeenCalledOnce();
  });
});
