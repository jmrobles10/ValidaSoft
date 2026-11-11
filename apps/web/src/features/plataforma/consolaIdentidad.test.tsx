import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { reiniciarCliente, type ClaveDeArranqueDto, type GrupoDto, type OperadorDePlataformaDto, type SistemaIntegradoDto } from '@validasoft/api-client';
import { Route, Routes } from 'react-router';
import { afterEach, describe, expect, it } from 'vitest';
import { montarPantalla } from '../../pruebas/montar';
import { ProblemaSimulado, simularApi } from '../../pruebas/simularApi';
import { PaginaIngresosOperadores } from './PaginaIngresosOperadores';
import { PaginaOperadores } from './PaginaOperadores';
import { PaginaSecretos } from './PaginaSecretos';
import { TarjetaGrupo } from './TarjetaGrupo';

afterEach(() => reiniciarCliente());

// ---------- Ingresos de los operadores (PI-16) ----------

const OPERADORES: OperadorDePlataformaDto[] = [
  { id: 'op1', sistema: 'directo', nombre: 'Camila Admin', correo: 'camila@validasoft.co', activo: true, directo: true, creadoEn: '2026-10-01T10:00:00Z', ultimoAcceso: '2026-11-10T10:00:00Z', passkeys: 2 },
  { id: 'op2', sistema: 'directo', nombre: 'Diego Soporte', correo: 'diego@validasoft.co', activo: false, directo: true, creadoEn: '2026-10-02T10:00:00Z', ultimoAcceso: null, passkeys: 0 },
];

const INGRESOS = {
  datos: [
    { id: 'i1', fecha: '2026-11-10T15:00:00Z', evento: 'IngresoExitoso', exitoso: true, operadorId: 'op1', correo: 'camila@validasoft.co', metodo: 'Passkey', motivo: null, ip: '10.0.0.5', dispositivo: 'Mozilla/5.0 (Windows NT 10.0) Chrome/130.0 Safari/537.36', actorTipo: 'Operador', actorId: 'op1', sesionId: 's1', passkeyId: 'p1' },
    { id: 'i2', fecha: '2026-11-10T14:00:00Z', evento: 'IngresoFallido', exitoso: false, operadorId: 'op2', correo: 'diego@validasoft.co', metodo: 'Codigo', motivo: 'codigo-incorrecto', ip: '10.0.0.9', dispositivo: null, actorTipo: 'Operador', actorId: 'op2', sesionId: null, passkeyId: null },
    { id: 'i3', fecha: '2026-11-10T13:00:00Z', evento: 'EventoQueLaAppNoConoce', exitoso: true, operadorId: 'op1', correo: 'camila@validasoft.co', metodo: null, motivo: null, ip: null, dispositivo: null, actorTipo: 'Operador', actorId: 'op1', sesionId: null, passkeyId: null },
  ],
  siguienteCursor: null,
};

function montarIngresos(ruta = '/plataforma/ingresos', rutas: Parameters<typeof simularApi>[0] = {}) {
  const peticiones = simularApi({
    'GET /api/v1/plataforma/auditoria-ingresos': INGRESOS,
    'GET /api/v1/plataforma/operadores': OPERADORES,
    ...rutas,
  });
  montarPantalla(
    <Routes>
      <Route path="/plataforma/ingresos" element={<PaginaIngresosOperadores />} />
    </Routes>,
    ruta,
  );
  return { peticiones, usuario: userEvent.setup() };
}

describe('Ingresos de los operadores (PI-16)', () => {
  it('lista los ingresos con evento, resultado, IP y un dispositivo legible', async () => {
    montarIngresos();

    const tabla = await screen.findByRole('table', { name: 'Ingresos de los operadores' });
    expect(await within(tabla).findByText('Ingreso exitoso')).toBeInTheDocument();
    expect(within(tabla).getByText('Ingreso fallido')).toBeInTheDocument();
    expect(within(tabla).getByText('codigo-incorrecto')).toBeInTheDocument();
    expect(within(tabla).getByText('10.0.0.5')).toBeInTheDocument();
    expect(within(tabla).getByText('Chrome en Windows')).toBeInTheDocument();
    expect(within(tabla).getAllByText('Exitoso')).toHaveLength(2);
    expect(within(tabla).getByText('Fallido')).toBeInTheDocument();
    // Un evento nuevo del servidor se muestra tal cual, sin romper la tabla.
    expect(within(tabla).getByText('EventoQueLaAppNoConoce')).toBeInTheDocument();
  });

  it('los filtros de la URL viajan a la API: operador, evento, resultado y fechas como límites del día', async () => {
    const { peticiones } = montarIngresos('/plataforma/ingresos?operador=op2&evento=IngresoFallido&resultado=fallido&desde=2026-11-01&hasta=2026-11-10');

    await screen.findByText('Ingreso fallido');

    const consulta = peticiones.find((p) => p.ruta === '/api/v1/plataforma/auditoria-ingresos');
    const url = new URL(consulta?.url ?? '', 'http://x');
    expect(url.searchParams.get('operador')).toBe('op2');
    expect(url.searchParams.get('evento')).toBe('IngresoFallido');
    expect(url.searchParams.get('exitoso')).toBe('false');
    expect(new Date(url.searchParams.get('desde') ?? '').getTime()).toBeLessThan(new Date(url.searchParams.get('hasta') ?? '').getTime());
    expect(screen.getByRole('button', { name: 'Limpiar filtros' })).toBeInTheDocument();
  });

  it('un rango al revés lo dice y deja limpiar los filtros', async () => {
    const { usuario } = montarIngresos('/plataforma/ingresos?desde=2026-11-10&hasta=2026-11-01');
    expect(await screen.findByRole('alert')).toHaveTextContent('La fecha inicial es posterior a la final.');

    await usuario.click(screen.getByRole('button', { name: 'Limpiar filtros' }));

    await waitFor(() => expect(screen.queryByRole('alert')).not.toBeInTheDocument());
  });

  it('pagina con el cursor de la API', async () => {
    let paginas = 0;
    const { peticiones, usuario } = montarIngresos('/plataforma/ingresos', {
      'GET /api/v1/plataforma/auditoria-ingresos': () => {
        paginas += 1;
        return paginas === 1 ? { datos: INGRESOS.datos.slice(0, 1), siguienteCursor: 'c2' } : { datos: INGRESOS.datos.slice(1, 2), siguienteCursor: null };
      },
    });
    await screen.findByText('Ingreso exitoso');

    await usuario.click(screen.getByRole('button', { name: 'Ver más' }));

    expect(await screen.findByText('Ingreso fallido')).toBeInTheDocument();
    expect(peticiones.filter((p) => p.ruta === '/api/v1/plataforma/auditoria-ingresos').at(-1)?.url).toContain('cursor=c2');
    expect(screen.queryByRole('button', { name: 'Ver más' })).not.toBeInTheDocument();
  });

  it('sin ingresos dice que aún no hay', async () => {
    montarIngresos('/plataforma/ingresos', { 'GET /api/v1/plataforma/auditoria-ingresos': { datos: [], siguienteCursor: null } });
    expect(await screen.findByText('Aún no hay ingresos de operadores')).toBeInTheDocument();
  });
});

// ---------- Clave de arranque (PI-17) ----------

const CLAVE_ACTIVA: ClaveDeArranqueDto = { configurada: true, desactivada: false, desactivadaEn: null, desactivadaPor: null, operadoresConPasskey: 2 };
const SECRETOS = { claveVigente: 'a1b2c3d4', clavesAnteriores: [], porTipo: [{ tipo: 'cuenta-en-eventos', total: 3, vigentes: 3, recifrados: 0, fallidos: 0 }] };

function montarSecretos(clave: ClaveDeArranqueDto, rutas: Parameters<typeof simularApi>[0] = {}) {
  const peticiones = simularApi({ 'GET /api/v1/plataforma/secretos': SECRETOS, 'GET /api/v1/plataforma/clave-de-arranque': () => clave, ...rutas });
  montarPantalla(
    <Routes>
      <Route path="/plataforma/secretos" element={<PaginaSecretos />} />
    </Routes>,
    '/plataforma/secretos',
  );
  return { peticiones, usuario: userEvent.setup() };
}

describe('Clave de arranque desde la consola (PI-17)', () => {
  it('muestra que está activa y cuántos operadores ya entran con passkey', async () => {
    montarSecretos(CLAVE_ACTIVA);
    expect(await screen.findByText('Activa')).toBeInTheDocument();
    expect(screen.getByText('2 con passkey')).toBeInTheDocument();
    expect(screen.getByText('Ya hay operadores con passkey: puedes desactivar la clave de arranque.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Desactivar' })).toBeEnabled();
  });

  it('también muestra los números de cuenta del historial entre los secretos en reposo', async () => {
    montarSecretos(CLAVE_ACTIVA);
    expect(await screen.findByText('Números de cuenta en el historial de los vínculos')).toBeInTheDocument();
  });

  it('desactivarla pide confirmar y llama a la API', async () => {
    const { peticiones, usuario } = montarSecretos(CLAVE_ACTIVA, {
      'POST /api/v1/plataforma/clave-de-arranque/desactivar': { ...CLAVE_ACTIVA, desactivada: true, desactivadaEn: '2026-11-11T15:00:00Z', desactivadaPor: 'camila@validasoft.co' },
    });
    await usuario.click(await screen.findByRole('button', { name: 'Desactivar' }));

    const dialogo = await screen.findByRole('dialog', { name: 'Desactivar la clave de arranque' });
    expect(within(dialogo).getByText(/deja de servir en cualquier servidor, de inmediato/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Desactivar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta.endsWith('/clave-de-arranque/desactivar'))).toBe(true));
  });

  it('sin ningún operador con passkey no se puede desactivar: nadie volvería a entrar', async () => {
    montarSecretos({ ...CLAVE_ACTIVA, operadoresConPasskey: 0 });
    expect(await screen.findByText(/Registra una passkey para al menos un operador antes de desactivarla/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Desactivar' })).toBeDisabled();
  });

  it('desactivada ofrece reactivarla para rescatar la consola y dice quién y cuándo', async () => {
    const { peticiones, usuario } = montarSecretos(
      { ...CLAVE_ACTIVA, desactivada: true, desactivadaEn: '2026-11-11T15:00:00Z', desactivadaPor: 'camila@validasoft.co' },
      { 'POST /api/v1/plataforma/clave-de-arranque/reactivar': CLAVE_ACTIVA },
    );

    expect(await screen.findByText('Desactivada')).toBeInTheDocument();
    expect(screen.getByText(/camila@validasoft.co/)).toBeInTheDocument();
    await usuario.click(screen.getByRole('button', { name: 'Reactivar' }));
    const dialogo = await screen.findByRole('dialog', { name: 'Reactivar la clave de arranque' });
    await usuario.click(within(dialogo).getByRole('button', { name: 'Reactivar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta.endsWith('/clave-de-arranque/reactivar'))).toBe(true));
  });

  it('si el servidor no tiene clave de arranque lo dice y no ofrece nada', async () => {
    montarSecretos({ configurada: false, desactivada: false, desactivadaEn: null, desactivadaPor: null, operadoresConPasskey: 2 });
    expect(await screen.findByText('No configurada en el servidor')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Desactivar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reactivar' })).not.toBeInTheDocument();
  });

  it('un 409 de la API al desactivar se muestra', async () => {
    const { usuario } = montarSecretos(CLAVE_ACTIVA, {
      'POST /api/v1/plataforma/clave-de-arranque/desactivar': new ProblemaSimulado(409, 'sin-operadores-con-passkey', 'sin operadores'),
    });
    await usuario.click(await screen.findByRole('button', { name: 'Desactivar' }));
    const dialogo = await screen.findByRole('dialog');
    await usuario.click(within(dialogo).getByRole('button', { name: 'Desactivar' }));

    expect(await screen.findByText('No puedes dejar la consola sin ningún operador con passkey: registra una antes de seguir.')).toBeInTheDocument();
  });
});

// ---------- Operadores: reactivar (PI-7) ----------

describe('Reactivar a un operador (PI-7)', () => {
  it('un operador desactivado ofrece reactivarlo y avisa que vuelve a entrar con una passkey nueva', async () => {
    const peticiones = simularApi({
      'GET /api/v1/plataforma/operadores': OPERADORES,
      'POST /api/v1/plataforma/operadores/op2/reactivar': { ...OPERADORES[1], activo: true },
    });
    montarPantalla(
      <Routes>
        <Route path="/plataforma/operadores" element={<PaginaOperadores />} />
      </Routes>,
      '/plataforma/operadores',
    );
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Acciones de Diego Soporte' }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Reactivar' }));

    const dialogo = await screen.findByRole('dialog', { name: /^Reactivar a Diego Soporte/ });
    expect(within(dialogo).getByText(/registrar una passkey nueva/)).toBeInTheDocument();
    await usuario.click(within(dialogo).getByRole('button', { name: 'Reactivar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta.endsWith('/op2/reactivar'))).toBe(true));
  });

  it('desactivar explica que se revocan las sesiones y las passkeys', async () => {
    simularApi({ 'GET /api/v1/plataforma/operadores': OPERADORES });
    montarPantalla(
      <Routes>
        <Route path="/plataforma/operadores" element={<PaginaOperadores />} />
      </Routes>,
      '/plataforma/operadores',
    );
    const usuario = userEvent.setup();

    await usuario.click(await screen.findByRole('button', { name: 'Acciones de Camila Admin' }));
    await usuario.click(await screen.findByRole('menuitem', { name: 'Desactivar' }));

    const dialogo = await screen.findByRole('dialog', { name: /^Desactivar a Camila Admin/ });
    expect(within(dialogo).getByText(/sus passkeys se revocan y se invalida su token de inmediato/)).toBeInTheDocument();
  });
});

// ---------- Empresas y sistemas de un grupo (PI-7, PI-9) ----------

const GRUPO: GrupoDto = {
  tenant: { id: 't1', slug: 'grupo-andino', nombre: 'Grupo Andino', region: 'co', plan: 'Inicial', zonaHoraria: 'America/Bogota', idioma: 'es-CO', activo: true, creadoEn: '2026-11-09T15:00:00Z' },
  empresas: [],
};

const SISTEMAS: SistemaIntegradoDto[] = [
  { id: 's1', codigo: 'ERP', nombre: 'ERP de Andina', clavePublicaPem: 'x', activo: true, creadoEn: '2026-11-01T10:00:00Z', origenesPermitidos: ['https://erp.andina.co'] },
  { id: 's2', codigo: 'CRM', nombre: 'CRM de Andina', clavePublicaPem: 'x', activo: false, creadoEn: '2026-11-02T10:00:00Z', origenesPermitidos: [] },
];

const NIT = { pais: 'CO', codigo: 'NIT', nombre: 'Número de Identificación Tributaria', aplicaA: 'Ambas', patron: '^[0-9]{6,15}$', tieneDv: true };

function montarGrupo(rutas: Parameters<typeof simularApi>[0] = {}) {
  const peticiones = simularApi({
    'GET /api/v1/catalogos/paises': [{ codigo: 'CO', nombre: 'Colombia', tiposIdentificacion: [NIT] }],
    'GET /api/v1/plataforma/tenants/t1/sistemas': SISTEMAS,
    ...rutas,
  });
  montarPantalla(<TarjetaGrupo grupo={GRUPO} expandido={false} onAlternar={() => undefined} />);
  return { peticiones, usuario: userEvent.setup() };
}

describe('Agregar una empresa a un grupo desde la consola (PI-9)', () => {
  it('crea la empresa en ese grupo con una Idempotency-Key y deja de mostrar el diálogo', async () => {
    const { peticiones, usuario } = montarGrupo({
      'POST /api/v1/plataforma/tenants/t1/empresas': (p) => ({ id: 'e9', activa: true, ...(p.cuerpo as Record<string, unknown>) }),
    });

    await usuario.click(screen.getByRole('button', { name: 'Agregar una empresa a Grupo Andino' }));
    const dialogo = await screen.findByRole('dialog', { name: /Agregar empresa/ });
    expect(within(dialogo).getByText('Para el grupo Grupo Andino')).toBeInTheDocument();

    await usuario.type(within(dialogo).getByRole('textbox', { name: /Número de identificación/ }), '900123456-8');
    await usuario.type(within(dialogo).getByRole('textbox', { name: /Razón social/ }), 'Constructora Norte');
    await usuario.type(within(dialogo).getByRole('textbox', { name: /Prefijo de folio/ }), 'norte');
    const crear = within(dialogo).getByRole('button', { name: 'Agregar empresa' });
    await waitFor(() => expect(crear).toBeEnabled());
    await usuario.click(crear);

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    const peticion = peticiones.find((p) => p.metodo === 'POST');
    expect(peticion?.ruta).toBe('/api/v1/plataforma/tenants/t1/empresas');
    expect(peticion?.cuerpo).toMatchObject({ identificacion: { numero: '900123456-8' }, nombre: 'Constructora Norte', prefijoFolio: 'NORTE' });
    expect(peticion?.cabeceras.get('Idempotency-Key')).toBeTruthy();
  }, 30_000);

  it('un grupo sin empresas ofrece agregarla aquí', () => {
    montarGrupo();
    expect(screen.getByText(/agrégala aquí/)).toBeInTheDocument();
  });
});

describe('Sistemas de un grupo (PI-7)', () => {
  it('lista los sistemas con su estado y deja desactivar uno activo tras confirmar', async () => {
    const { peticiones, usuario } = montarGrupo({ 'POST /api/v1/plataforma/tenants/t1/sistemas/s1/desactivar': { ...SISTEMAS[0], activo: false } });

    await usuario.click(screen.getByRole('button', { name: 'Ver los sistemas de Grupo Andino' }));
    const lista = await screen.findByRole('dialog', { name: /Sistemas integrados/ });
    expect(await within(lista).findByText('ERP de Andina')).toBeInTheDocument();
    expect(within(lista).getByText('CRM de Andina')).toBeInTheDocument();
    expect(within(lista).getByText(/1 origen permitido/)).toBeInTheDocument();

    await usuario.click(within(lista).getByRole('button', { name: 'Desactivar ERP de Andina' }));
    const confirmar = await screen.findByRole('dialog', { name: /^Desactivar ERP de Andina/ });
    expect(within(confirmar).getByText(/sus llaves dejan de servir ahora mismo/)).toBeInTheDocument();
    await usuario.click(within(confirmar).getByRole('button', { name: 'Desactivar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta.endsWith('/sistemas/s1/desactivar'))).toBe(true));
  });

  it('un sistema desactivado se reactiva aclarando que no revive lo anterior', async () => {
    const { peticiones, usuario } = montarGrupo({ 'POST /api/v1/plataforma/tenants/t1/sistemas/s2/reactivar': { ...SISTEMAS[1], activo: true } });

    await usuario.click(screen.getByRole('button', { name: 'Ver los sistemas de Grupo Andino' }));
    const lista = await screen.findByRole('dialog', { name: /Sistemas integrados/ });
    await usuario.click(await within(lista).findByRole('button', { name: 'Reactivar CRM de Andina' }));

    const confirmar = await screen.findByRole('dialog', { name: /^Reactivar CRM de Andina/ });
    expect(within(confirmar).getByText(/Las sesiones y los tokens de antes no se recuperan/)).toBeInTheDocument();
    await usuario.click(within(confirmar).getByRole('button', { name: 'Reactivar' }));

    await waitFor(() => expect(peticiones.some((p) => p.metodo === 'POST' && p.ruta.endsWith('/sistemas/s2/reactivar'))).toBe(true));
  });

  it('el error de la API al cambiar un sistema se muestra', async () => {
    const { usuario } = montarGrupo({ 'POST /api/v1/plataforma/tenants/t1/sistemas/s1/desactivar': new ProblemaSimulado(409, 'sistema-inactivo', 'El sistema ya está desactivado.') });
    await usuario.click(screen.getByRole('button', { name: 'Ver los sistemas de Grupo Andino' }));
    const lista = await screen.findByRole('dialog', { name: /Sistemas integrados/ });
    await usuario.click(await within(lista).findByRole('button', { name: 'Desactivar ERP de Andina' }));
    const confirmar = await screen.findByRole('dialog', { name: /^Desactivar ERP de Andina/ });

    await usuario.click(within(confirmar).getByRole('button', { name: 'Desactivar' }));

    expect(await screen.findByText('El sistema ya está desactivado.')).toBeInTheDocument();
  });
});

