import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, SCOPES, type ConfiguracionDeSsoDto, type DominioDeSsoDto } from '@validasoft/api-client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { montarPantalla } from '../../../../pruebas/montar';
import { ProblemaSimulado, simularApi } from '../../../../pruebas/simularApi';
import { PaginaSso } from './PaginaSso';

const permisos = vi.hoisted(() => ({ scopes: new Set<string>() }));
vi.mock('../../../empresas/useEmpresas', () => ({
  useEmpresaActiva: () => ({ empresa: { id: 'emp', nombre: 'Andina' }, empresas: [{ id: 'emp', nombre: 'Andina de Obras S.A.S.' }] }),
}));
vi.mock('../../../../shared/usePermisos', () => ({ usePermisos: () => ({ scopes: permisos.scopes, puede: (s: string) => permisos.scopes.has(s) }) }));

beforeEach(() => {
  permisos.scopes = new Set([SCOPES.usuariosAdministrar]);
});

afterEach(() => reiniciarCliente());

const VERIFICADO: DominioDeSsoDto = { dominio: 'andina.co', verificado: true, token: null, instruccion: null, creadoEn: '2026-11-01T10:00:00Z', verificadoEn: '2026-11-01T11:00:00Z' };
const PENDIENTE: DominioDeSsoDto = {
  dominio: 'andina.com',
  verificado: false,
  token: 'validasoft-verificacion=abc123',
  instruccion: 'publica',
  creadoEn: '2026-11-02T10:00:00Z',
  verificadoEn: null,
};

function configuracion(cambios: Partial<ConfiguracionDeSsoDto> = {}): ConfiguracionDeSsoDto {
  return {
    configurado: true,
    activo: true,
    proveedor: 'oidc',
    nombre: 'Andina',
    autoridad: 'https://login.andina.co/v2.0',
    clientId: 'validasoft-andina',
    secretoConfigurado: true,
    ultimosCuatroSecreto: 'wxyz',
    alcances: ['openid', 'email', 'profile'],
    reclamoDeGrupos: 'groups',
    organizacionWorkOs: null,
    conexionWorkOs: null,
    dominios: [VERIFICADO, PENDIENTE],
    aprovisionamientoJit: true,
    rolesPorOmision: [{ rol: 'Revisor', empresaId: null }],
    reglas: [],
    sincronizarRoles: false,
    modo: 'Complementario',
    urlDeRetorno: 'https://validasoft.co/api/v1/ingreso/sso/retorno',
    workOsDisponible: false,
    actualizadoEn: '2026-11-03T10:00:00Z',
    ...cambios,
  };
}

function montar(config: ConfiguracionDeSsoDto, rutas: Parameters<typeof simularApi>[0] = {}) {
  const peticiones = simularApi({ 'GET /api/v1/configuracion/sso': () => config, ...rutas });
  montarPantalla(<PaginaSso />);
  return { peticiones, usuario: userEvent.setup() };
}

describe('Configuración › Inicio de sesión único (PI-1…PI-5)', () => {
  it('sin el permiso de administrar usuarios no muestra nada de la configuración', () => {
    permisos.scopes = new Set();
    const { peticiones } = montar(configuracion());
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(peticiones).toEqual([]);
  });

  it('muestra lo guardado y nunca el secreto: solo con qué cuatro caracteres termina', async () => {
    montar(configuracion());

    expect(await screen.findByRole('heading', { name: 'Inicio de sesión único' })).toBeInTheDocument();
    expect(screen.getByLabelText('Emisor (issuer)')).toHaveValue('https://login.andina.co/v2.0');
    expect(screen.getByLabelText('Client ID')).toHaveValue('validasoft-andina');
    expect(screen.getByText('Secreto guardado, termina en wxyz')).toBeInTheDocument();
    expect(screen.queryByLabelText('Secreto del cliente')).not.toBeInTheDocument();
    expect(screen.getByText('https://validasoft.co/api/v1/ingreso/sso/retorno')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();
  });

  it('guardar sin tocar el secreto lo conserva (nulo), y un cambio habilita el botón', async () => {
    const { peticiones, usuario } = montar(configuracion(), { 'PUT /api/v1/configuracion/sso': () => configuracion({ nombre: 'Andina Grupo' }) });
    const nombre = await screen.findByLabelText('Nombre del botón');
    await usuario.clear(nombre);
    await usuario.type(nombre, 'Andina Grupo');
    expect(screen.getByText('Cambios sin guardar')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    const cuerpo = peticiones.find((p) => p.metodo === 'PUT')?.cuerpo as Record<string, unknown>;
    expect(cuerpo).toMatchObject({ proveedor: 'oidc', nombre: 'Andina Grupo', autoridad: 'https://login.andina.co/v2.0', clientId: 'validasoft-andina', modo: 'Complementario' });
    expect(cuerpo.secretoDelCliente).toBeNull();
  });

  it('cambiar el secreto lo envía una sola vez y exige 8 caracteres sin espacios', async () => {
    const { peticiones, usuario } = montar(configuracion(), { 'PUT /api/v1/configuracion/sso': () => configuracion() });
    await usuario.click(await screen.findByRole('button', { name: 'Cambiar secreto' }));
    const campo = screen.getByLabelText('Secreto del cliente');
    expect(campo).toHaveAttribute('type', 'password');

    await usuario.type(campo, 'corto');
    expect(screen.getByText('Debe tener al menos 8 caracteres y no llevar espacios.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();

    await usuario.type(campo, '-secreto-nuevo');
    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    expect((peticiones.find((p) => p.metodo === 'PUT')?.cuerpo as Record<string, unknown>).secretoDelCliente).toBe('corto-secreto-nuevo');
  });

  it('quitar el secreto envía una cadena vacía', async () => {
    const { peticiones, usuario } = montar(configuracion(), { 'PUT /api/v1/configuracion/sso': () => configuracion() });
    await usuario.click(await screen.findByRole('button', { name: 'Quitar' }));
    expect(screen.getByText('Se quitará el secreto guardado al guardar.')).toBeInTheDocument();

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    expect((peticiones.find((p) => p.metodo === 'PUT')?.cuerpo as Record<string, unknown>).secretoDelCliente).toBe('');
  });

  it('activarlo sin un dominio verificado lo dice en un solo aviso y no deja guardar', async () => {
    const { usuario } = montar(configuracion({ activo: false, dominios: [PENDIENTE] }));
    const interruptor = await screen.findByRole('switch', { name: 'Activar el inicio de sesión único' });

    await usuario.click(interruptor);

    const aviso = await screen.findByText('Antes de guardar');
    expect(within(aviso.closest('[role="alert"]')!).getByText('Verifica al menos un dominio de correo antes de activar el inicio de sesión.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Guardar' })).toBeDisabled();
  });

  it('el modo exclusivo avisa que hay que probar la conexión antes', async () => {
    const { usuario } = montar(configuracion());
    await usuario.click(await screen.findByRole('combobox', { name: 'Convivencia con el código del correo' }));
    await usuario.click(await screen.findByRole('option', { name: 'Exclusivo' }));

    expect(await screen.findByText(/prueba la conexión con una cuenta tuya/)).toBeInTheDocument();
  });

  it('probar la conexión usa lo guardado: con cambios sin guardar no se ofrece', async () => {
    const { peticiones, usuario } = montar(configuracion(), { 'POST /api/v1/configuracion/sso/probar': { correcto: true, mensaje: 'El proveedor respondió.', emisor: 'https://login.andina.co/v2.0', puntoDeAutorizacion: 'https://login.andina.co/authorize' } });
    const probar = await screen.findByRole('button', { name: 'Probar la conexión' });
    expect(probar).toBeEnabled();

    await usuario.click(probar);
    expect(await screen.findByText(/El proveedor respondió\./)).toBeInTheDocument();
    expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta.endsWith('/sso/probar'))).toBe(true);

    await usuario.type(screen.getByLabelText('Client ID'), 'x');
    expect(screen.getByRole('button', { name: 'Probar la conexión' })).toBeDisabled();
    expect(screen.getByText('Guarda los cambios para poder probar la conexión.')).toBeInTheDocument();
  });

  it('una prueba que falla muestra el mensaje del servidor como advertencia', async () => {
    const { usuario } = montar(configuracion(), { 'POST /api/v1/configuracion/sso/probar': { correcto: false, mensaje: 'El proveedor no respondió.', emisor: null, puntoDeAutorizacion: null } });
    await usuario.click(await screen.findByRole('button', { name: 'Probar la conexión' }));
    expect(await screen.findByText('El proveedor no respondió.')).toBeInTheDocument();
  });

  it('con WorkOS pide la organización o la conexión y no el emisor ni el secreto', async () => {
    const { usuario } = montar(configuracion({ workOsDisponible: true }));
    await usuario.click(await screen.findByRole('combobox', { name: 'Proveedor' }));
    await usuario.click(await screen.findByRole('option', { name: 'WorkOS' }));

    expect(await screen.findByLabelText('Organización de WorkOS')).toBeInTheDocument();
    expect(screen.getByLabelText('Conexión de WorkOS')).toBeInTheDocument();
    expect(screen.queryByLabelText('Emisor (issuer)')).not.toBeInTheDocument();
    expect(screen.getByText(/Indica la organización \(org_…\)/)).toBeInTheDocument();
  });

  it('WorkOS no se puede elegir si la instalación no lo tiene configurado', async () => {
    const { usuario } = montar(configuracion({ workOsDisponible: false }));
    await usuario.click(await screen.findByRole('combobox', { name: 'Proveedor' }));
    expect(await screen.findByRole('option', { name: 'WorkOS' })).toHaveAttribute('aria-disabled', 'true');
  });

  it('las reglas de grupos se agregan con su rol y viajan al guardar', async () => {
    const { peticiones, usuario } = montar(configuracion(), { 'PUT /api/v1/configuracion/sso': () => configuracion() });
    await usuario.click(await screen.findByRole('button', { name: 'Agregar una regla' }));
    await usuario.type(screen.getByLabelText('Grupo del proveedor'), 'Oficiales');
    await usuario.click(screen.getByRole('combobox', { name: 'Recibe el rol' }));
    await usuario.click(await screen.findByRole('option', { name: 'Oficial de cumplimiento' }));

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'PUT')).toBe(true));
    expect((peticiones.find((p) => p.metodo === 'PUT')?.cuerpo as Record<string, unknown>).reglas).toEqual([{ grupo: 'Oficiales', rol: 'OficialCumplimiento', empresaId: null }]);
  });

  it('un error de la API al guardar se muestra y no pierde lo escrito', async () => {
    const { usuario } = montar(configuracion(), { 'PUT /api/v1/configuracion/sso': new ProblemaSimulado(422, 'autoridad-invalida', 'Escribe el emisor del proveedor.') });
    const nombre = await screen.findByLabelText('Nombre del botón');
    await usuario.clear(nombre);
    await usuario.type(nombre, 'Otro nombre');

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }));

    expect(await screen.findByText('Escribe el emisor del proveedor.')).toBeInTheDocument();
    expect(screen.getByLabelText('Nombre del botón')).toHaveValue('Otro nombre');
  });
});

describe('Dominios del SSO (PI-2)', () => {
  it('un dominio por verificar muestra el texto a publicar y la dirección donde publicarlo', async () => {
    montar(configuracion());

    expect(await screen.findByText('@andina.co')).toBeInTheDocument();
    expect(screen.getByText('Verificado')).toBeInTheDocument();
    expect(screen.getByText('Por verificar')).toBeInTheDocument();
    expect(screen.getByText('validasoft-verificacion=abc123')).toBeInTheDocument();
    expect(screen.getByText(/https:\/\/andina\.com\/\.well-known\/validasoft-verificacion\.txt/)).toBeInTheDocument();
  });

  it('agrega un dominio con POST y vacía el campo', async () => {
    const { peticiones, usuario } = montar(configuracion(), { 'POST /api/v1/configuracion/sso/dominios': () => ({ ...PENDIENTE, dominio: 'nuevo.co' }) });
    const campo = await screen.findByLabelText('Dominio');

    await usuario.type(campo, 'nuevo.co');
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }));

    await waitFor(() => expect(peticiones.find((p) => p.metodo === 'POST' && p.ruta.endsWith('/sso/dominios'))?.cuerpo).toEqual({ dominio: 'nuevo.co' }));
    await waitFor(() => expect(campo).toHaveValue(''));
  });

  it('verifica un dominio pendiente y quita uno', async () => {
    const { peticiones, usuario } = montar(configuracion(), {
      'POST /api/v1/configuracion/sso/dominios/andina.com/verificar': () => ({ ...PENDIENTE, verificado: true }),
      'DELETE /api/v1/configuracion/sso/dominios/andina.com': undefined,
    });

    await usuario.click(await screen.findByRole('button', { name: 'Verificar' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta.endsWith('/andina.com/verificar'))).toBe(true));

    await usuario.click(screen.getByRole('button', { name: 'Quitar @andina.com' }));
    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'DELETE' && p.ruta.endsWith('/dominios/andina.com'))).toBe(true));
  });

  it('si el texto no está publicado muestra por qué no se pudo verificar', async () => {
    const { usuario } = montar(configuracion(), {
      'POST /api/v1/configuracion/sso/dominios/andina.com/verificar': new ProblemaSimulado(422, 'dominio-sin-verificar', 'No encontramos el texto en el sitio.'),
    });
    await usuario.click(await screen.findByRole('button', { name: 'Verificar' }));
    expect(await screen.findByText('No encontramos el texto en el sitio.')).toBeInTheDocument();
  });

  it('agregar el primer dominio de una configuración nueva (que la crea) no borra lo que ya se escribió', async () => {
    let creada = false;
    const vacia = configuracion({ configurado: false, activo: false, nombre: '', autoridad: null, clientId: null, secretoConfigurado: false, ultimosCuatroSecreto: null, dominios: [], aprovisionamientoJit: false, rolesPorOmision: [], actualizadoEn: null });
    const { usuario } = montar(vacia, {
      'GET /api/v1/configuracion/sso': () => (creada ? { ...vacia, configurado: true, dominios: [{ ...PENDIENTE, dominio: 'nuevo.co' }], actualizadoEn: '2026-11-11T10:00:00Z' } : vacia),
      'POST /api/v1/configuracion/sso/dominios': () => {
        creada = true;
        return { ...PENDIENTE, dominio: 'nuevo.co' };
      },
    });
    await usuario.type(await screen.findByLabelText('Nombre del botón'), 'Andina');
    await usuario.type(screen.getByLabelText('Client ID'), 'validasoft-andina');

    await usuario.type(screen.getByLabelText('Dominio'), 'nuevo.co');
    await usuario.click(screen.getByRole('button', { name: 'Agregar' }));

    expect(await screen.findByText('@nuevo.co')).toBeInTheDocument();
    expect(screen.getByLabelText('Nombre del botón')).toHaveValue('Andina');
    expect(screen.getByLabelText('Client ID')).toHaveValue('validasoft-andina');
  });

  it('verificar un dominio no borra lo que se está editando en el formulario', async () => {
    let verificado = false;
    const { usuario } = montar(configuracion(), {
      'GET /api/v1/configuracion/sso': () => configuracion({ dominios: [VERIFICADO, { ...PENDIENTE, verificado }] }),
      'POST /api/v1/configuracion/sso/dominios/andina.com/verificar': () => {
        verificado = true;
        return { ...PENDIENTE, verificado: true };
      },
    });
    const nombre = await screen.findByLabelText('Nombre del botón');
    await usuario.clear(nombre);
    await usuario.type(nombre, 'Sin guardar');

    await usuario.click(screen.getByRole('button', { name: 'Verificar' }));

    await waitFor(() => expect(screen.queryByRole('button', { name: 'Verificar' })).not.toBeInTheDocument());
    expect(screen.getByLabelText('Nombre del botón')).toHaveValue('Sin guardar');
  });
});
