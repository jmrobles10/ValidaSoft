import { ThemeProvider } from '@mui/material';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { configurarCliente, reiniciarCliente, SCOPES, type DominioDeCorreoDto } from '@validasoft/api-client';
import { createValidasoftTheme } from '@validasoft/ui';
import { afterEach, describe, expect, it, vi } from 'vitest';
import '../../../i18n';
import {
  borradorDesdeDominio,
  chipDeDominio,
  comoFinalidad,
  cuerpoDominio,
  direccionDeVistaPrevia,
  estadoDelDominio,
  hayCambiosDeDominio,
  normalizarDominio,
  registrosPendientes,
  validarDominio,
} from './modelo';
import { PaginaDominioCorreo } from './PaginaDominioCorreo';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../empresas/useEmpresas', () => ({ useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Constructora Norte' }, empresas: [] }) }));
vi.mock('../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

const PLATAFORMA = 'Validasoft <no-responder@validasoft.co>';

const SIN_DOMINIO: DominioDeCorreoDto = {
  empresaId: 'emp',
  configurado: false,
  dominio: null,
  parteLocal: 'no-responder',
  nombreRemitente: null,
  estado: null,
  registros: [],
  verificacion: [],
  verificadoEn: null,
  ultimaVerificacionEn: null,
  remitenteEnUso: PLATAFORMA,
  remitenteDeLaPlataforma: PLATAFORMA,
  simulado: true,
};

const PENDIENTE: DominioDeCorreoDto = {
  ...SIN_DOMINIO,
  configurado: true,
  dominio: 'proveedores.norte.co',
  estado: 'Pendiente',
  registros: [
    { finalidad: 'Spf', tipo: 'TXT', nombre: 'proveedores.norte.co', valor: 'v=spf1 include:_spf.validasoft.co ~all' },
    { finalidad: 'Dkim', tipo: 'CNAME', nombre: 'vs1._domainkey.proveedores.norte.co', valor: 'vs1.dkim.validasoft.co' },
    { finalidad: 'Dmarc', tipo: 'TXT', nombre: '_dmarc.proveedores.norte.co', valor: 'v=DMARC1; p=none; rua=mailto:dmarc@validasoft.co' },
  ],
};

const FALLIDO: DominioDeCorreoDto = {
  ...PENDIENTE,
  estado: 'Fallido',
  verificacion: [
    { finalidad: 'Spf', correcto: true, encontrado: 'v=spf1 include:_spf.validasoft.co ~all' },
    { finalidad: 'Dkim', correcto: false, encontrado: null },
    { finalidad: 'Dmarc', correcto: false, encontrado: 'v=DMARC1; p=reject' },
  ],
  ultimaVerificacionEn: '2026-11-11T12:00:00Z',
};

const VERIFICADO: DominioDeCorreoDto = {
  ...PENDIENTE,
  estado: 'Verificado',
  verificacion: PENDIENTE.registros.map((r) => ({ finalidad: r.finalidad, correcto: true, encontrado: r.valor })),
  verificadoEn: '2026-11-11T12:00:00Z',
  ultimaVerificacionEn: '2026-11-11T12:00:00Z',
  remitenteEnUso: 'Constructora Norte <no-responder@proveedores.norte.co>',
};

const json = (cuerpo: unknown, estado = 200) => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json' } });

interface Peticion {
  url: URL;
  metodo: string;
  cuerpo: unknown;
}

function montar(inicial: DominioDeCorreoDto, alEscribir: (metodo: string) => DominioDeCorreoDto = () => inicial) {
  const peticiones: Peticion[] = [];
  configurarCliente({
    fetch: vi.fn((entrada: RequestInfo | URL, init?: RequestInit) => {
      const url = new URL(typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url, 'http://localhost');
      const metodo = init?.method ?? 'GET';
      peticiones.push({ url, metodo, cuerpo: typeof init?.body === 'string' ? (JSON.parse(init.body) as unknown) : null });
      if (metodo === 'DELETE') return Promise.resolve(new Response(null, { status: 204 }));
      return Promise.resolve(json(metodo === 'GET' ? inicial : alEscribir(metodo)));
    }),
  });
  render(
    <ThemeProvider theme={createValidasoftTheme('light')}>
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <PaginaDominioCorreo />
      </QueryClientProvider>
    </ThemeProvider>,
  );
  return peticiones;
}

afterEach(() => {
  reiniciarCliente();
  permisos.scopes = new Set();
});

describe('modelo del dominio de correo', () => {
  it('normaliza como el servidor: minúsculas, sin arroba inicial ni punto final', () => {
    expect(normalizarDominio('  @Proveedores.Norte.CO. ')).toBe('proveedores.norte.co');
  });

  it('pide un dominio público con punto, una parte local válida y un nombre sin < ni >', () => {
    const base = { dominio: 'proveedores.norte.co', parteLocal: '', nombre: '' };
    expect(validarDominio(base)).toEqual([]);
    expect(validarDominio({ ...base, dominio: 'http://norte.co' })).toEqual(['dominio-invalido']);
    expect(validarDominio({ ...base, dominio: 'localhost' })).toEqual(['dominio-invalido']);
    expect(validarDominio({ ...base, dominio: 'norte' })).toEqual(['dominio-invalido']);
    expect(validarDominio({ ...base, parteLocal: 'a b' })).toEqual(['parte-local-invalida']);
    expect(validarDominio({ ...base, nombre: 'Norte <x>' })).toEqual(['nombre-invalido']);
    expect(validarDominio({ ...base, nombre: 'x'.repeat(81) })).toEqual(['nombre-invalido']);
  });

  it('arma el cuerpo con vacíos como nulos y muestra la dirección antes de guardar', () => {
    expect(cuerpoDominio({ dominio: ' Proveedores.Norte.co ', parteLocal: ' ', nombre: ' ' })).toEqual({ dominio: 'proveedores.norte.co', parteLocal: null, nombreRemitente: null });
    expect(direccionDeVistaPrevia({ dominio: 'norte.co', parteLocal: '', nombre: '' })).toBe('no-responder@norte.co');
    expect(direccionDeVistaPrevia({ dominio: 'norte.co', parteLocal: 'Proveedores', nombre: '' })).toBe('proveedores@norte.co');
    expect(direccionDeVistaPrevia({ dominio: 'norte', parteLocal: '', nombre: '' })).toBeNull();
  });

  it('distingue sin dominio, pendiente, verificado y fallido, y cuenta los registros que faltan', () => {
    expect(estadoDelDominio(SIN_DOMINIO)).toBe('sin-dominio');
    expect(estadoDelDominio(PENDIENTE)).toBe('Pendiente');
    expect(estadoDelDominio(FALLIDO)).toBe('Fallido');
    expect(estadoDelDominio(VERIFICADO)).toBe('Verificado');
    expect(chipDeDominio('Verificado')).toBe('Aprobado');
    expect(registrosPendientes(PENDIENTE)).toBe(3);
    expect(registrosPendientes(FALLIDO)).toBe(2);
    expect(registrosPendientes(VERIFICADO)).toBe(0);
    expect(comoFinalidad('Dkim')).toBe('Dkim');
    expect(comoFinalidad('Otra')).toBeNull();
  });

  it('solo hay cambios si el dominio, la parte local o el nombre difieren de lo guardado', () => {
    const base = borradorDesdeDominio(PENDIENTE);
    expect(hayCambiosDeDominio(base, PENDIENTE)).toBe(false);
    expect(hayCambiosDeDominio({ ...base, parteLocal: 'no-responder' }, PENDIENTE)).toBe(false);
    expect(hayCambiosDeDominio({ ...base, nombre: 'Norte' }, PENDIENTE)).toBe(true);
    expect(hayCambiosDeDominio({ ...base, dominio: 'otro.norte.co' }, PENDIENTE)).toBe(true);
  });
});

describe('Dominio de correo de la empresa', () => {
  it('sin empresas:administrar dice que no hay acceso y no pide nada', () => {
    const peticiones = montar(SIN_DOMINIO);
    expect(screen.getByRole('alert')).toHaveTextContent('Dominio de correo');
    expect(peticiones).toHaveLength(0);
  });

  it('sin dominio dice que el correo sale con el remitente de la plataforma y registra el dominio normalizado', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.empresasAdministrar]);
    const peticiones = montar(SIN_DOMINIO, () => PENDIENTE);

    expect(await screen.findByText(/sigue saliendo desde Validasoft <no-responder@validasoft.co>/)).toBeInTheDocument();
    expect(screen.getByText(/Verificación simulada/)).toBeInTheDocument();
    const registrar = screen.getByRole('button', { name: 'Registrar el dominio' });
    expect(registrar).toBeDisabled();
    expect(screen.queryByRole('heading', { name: 'Registros DNS' })).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('textbox', { name: 'Dominio' }));
    await usuario.paste('Proveedores.Norte.co');
    expect(screen.getByText('Constructora Norte <no-responder@proveedores.norte.co>')).toBeInTheDocument();
    await waitFor(() => expect(registrar).toBeEnabled());
    await usuario.click(registrar);

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    const envio = peticiones.find((p) => p.metodo === 'PUT');
    expect(envio?.url.pathname).toBe('/api/v1/empresas/emp/dominio-correo');
    expect(envio?.cuerpo).toEqual({ dominio: 'proveedores.norte.co', parteLocal: null, nombreRemitente: null });
  });

  it('un dominio mal escrito no se envía y dice cómo escribirlo', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.empresasAdministrar]);
    const peticiones = montar(SIN_DOMINIO);

    await usuario.click(await screen.findByRole('textbox', { name: 'Dominio' }));
    await usuario.paste('http://norte');
    await usuario.click(screen.getByRole('button', { name: 'Registrar el dominio' }));

    expect((await screen.findAllByText(/Escribe un dominio como proveedores.miempresa.com/)).length).toBeGreaterThan(0);
    expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(false);
  });

  it('muestra los tres registros con su valor y lo que halló la verificación en cada uno', async () => {
    permisos.scopes = new Set([SCOPES.empresasAdministrar]);
    montar(FALLIDO);

    const lista = await screen.findByRole('list', { name: 'Registros DNS' });
    const registros = within(lista).getAllByRole('listitem');
    expect(registros).toHaveLength(3);
    expect(within(registros[0]!).getByText('SPF')).toBeInTheDocument();
    expect(within(registros[0]!).getByText('Correcto')).toBeInTheDocument();
    expect(within(registros[1]!).getByText('vs1.dkim.validasoft.co')).toBeInTheDocument();
    expect(within(registros[1]!).getByText('No encontrado')).toBeInTheDocument();
    expect(within(registros[1]!).getByText('No encontramos este registro en el DNS.')).toBeInTheDocument();
    expect(within(registros[2]!).getByText('Distinto')).toBeInTheDocument();
    expect(within(registros[2]!).getByText('Encontramos otro valor: v=DMARC1; p=reject')).toBeInTheDocument();
    expect(screen.getByText(/Faltan 2 registros/)).toBeInTheDocument();
    expect(screen.getByText('Falta algún registro')).toBeInTheDocument();
  });

  it('verificar ahora pide la verificación y, verificado, dice que el correo ya sale desde el remitente propio', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.empresasAdministrar]);
    const peticiones = montar(PENDIENTE, () => VERIFICADO);

    expect(await screen.findByText('Pendiente de verificar')).toBeInTheDocument();
    expect(screen.getByText('Aún no se ha verificado.')).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Verificar ahora' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'POST')?.url.pathname).toBe('/api/v1/empresas/emp/dominio-correo/verificar');
  });

  it('verificado muestra el remitente propio; quitarlo dice antes que el correo vuelve a salir desde la plataforma', async () => {
    const usuario = userEvent.setup();
    permisos.scopes = new Set([SCOPES.empresasAdministrar]);
    const peticiones = montar(VERIFICADO);

    expect(await screen.findByText('Constructora Norte <no-responder@proveedores.norte.co>')).toBeInTheDocument();
    expect(screen.getByText(/Es tu remitente propio/)).toBeInTheDocument();
    expect(screen.queryByText(/sigue saliendo desde/)).not.toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Quitar el dominio' }));
    const dialogo = await screen.findByRole('dialog', { name: /Quitar proveedores.norte.co/ });
    expect(within(dialogo).getByText(/vuelve a salir desde Validasoft <no-responder@validasoft.co>/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Quitar el dominio' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'DELETE')).toBe(true));
    expect(peticiones.find((p) => p.metodo === 'DELETE')?.url.pathname).toBe('/api/v1/empresas/emp/dominio-correo');
  });
});
