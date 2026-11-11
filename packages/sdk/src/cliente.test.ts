import { createPublicKey, generateKeyPairSync, verify } from 'node:crypto';
import { describe, expect, it } from 'vitest';
import { crearFirmanteDeAserciones } from './asercion';
import { ClienteValidasoft, ErrorValidasoft } from './cliente';

const LLAVE = `vsk_test_acme_${'0123456789abcdef'.repeat(4)}`;
const EMPRESA = '0195f1c2-7d3e-7a00-8000-0000000000e1';

interface Recibida {
  url: string;
  metodo: string;
  cabeceras: Record<string, string>;
  cuerpo: unknown;
}

/** `fetch` falso: responde en orden y guarda las peticiones. */
function servidorFalso(respuestas: (() => Response)[]) {
  const recibidas: Recibida[] = [];
  const falso: typeof fetch = (entrada, init) => {
    recibidas.push({
      url: typeof entrada === 'string' ? entrada : entrada instanceof URL ? entrada.href : entrada.url,
      metodo: init?.method ?? 'GET',
      cabeceras: { ...(init?.headers as Record<string, string>) },
      cuerpo: typeof init?.body === 'string' ? JSON.parse(init.body) : undefined,
    });
    const siguiente = respuestas.shift();
    return Promise.resolve(siguiente ? siguiente() : new Response(null, { status: 500 }));
  };
  return { falso, recibidas };
}

const json = (cuerpo: unknown, estado = 200, cabeceras: Record<string, string> = {}) =>
  () => new Response(JSON.stringify(cuerpo), { status: estado, headers: { 'Content-Type': 'application/json', ...cabeceras } });

const consulta = {
  certificado: true,
  folio: 'N1-C-2026-000001-1',
  estado: 'Vigente',
  estadoVinculo: 'Activo',
  vinculoId: 'v1',
  terceroId: 't1',
  vigenteHasta: '2027-04-30',
  revalidacionPendiente: [],
  documentosVencidos: [],
  bloqueado: false,
  motivoBloqueo: null,
  cuentaDesignada: { numeroEnmascarado: '****6578', banco: 'Bancolombia' },
  urlVerificacion: 'https://valida.acme.co/verificar/N1',
  legacy: null,
  fuenteRespaldo: false,
};

function cliente(respuestas: (() => Response)[], extra: Partial<ConstructorParameters<typeof ClienteValidasoft>[0]> = {}) {
  const { falso, recibidas } = servidorFalso(respuestas);
  return { cliente: new ClienteValidasoft({ urlBase: 'https://valida.acme.co/', apiKey: LLAVE, empresaId: EMPRESA, fetch: falso, esperaMs: 1, ...extra }), recibidas };
}

describe('cliente de la API', () => {
  it('manda la API key, la empresa y una clave de idempotencia en cada POST', async () => {
    const { cliente: c, recibidas } = cliente([json({ correoDestino: 'p@a.co', expira: '2026-11-12T00:00:00Z', envios: 1 }, 201), json(consulta)]);
    await c.invitaciones.enviar('v1', { correoDestino: 'proveedor@aceros.co' });
    await c.consultas.certificacion({ tipoIdentificacion: 'NIT', numero: '900.123.456-7', tipoVinculo: 'PROVEEDOR' });

    const [post, get] = recibidas;
    expect(post?.url).toBe('https://valida.acme.co/api/v1/vinculos/v1/invitaciones');
    expect(post?.cabeceras['X-Api-Key']).toBe(LLAVE);
    expect(post?.cabeceras['X-Empresa']).toBe(EMPRESA);
    expect(post?.cabeceras['Idempotency-Key']).toMatch(/^[0-9a-f-]{36}$/);
    expect(post?.cuerpo).toEqual({ correoDestino: 'proveedor@aceros.co' });
    expect(get?.url).toBe('https://valida.acme.co/api/v1/consultas/certificacion?pais=CO&tipoIdentificacion=NIT&numero=900.123.456-7&tipoVinculo=PROVEEDOR');
    expect(get?.cabeceras['Idempotency-Key']).toBeUndefined();
  });

  it('la consulta condicional devuelve noModificado con 304 y manda If-None-Match', async () => {
    const { cliente: c, recibidas } = cliente([json(consulta, 200, { ETag: '"c-1"' }), () => new Response(null, { status: 304, headers: { ETag: '"c-1"' } })]);
    const primera = await c.consultas.certificacionDelVinculo('v1');
    expect([primera.noModificado, primera.etag, primera.datos?.cuentaDesignada?.banco]).toEqual([false, '"c-1"', 'Bancolombia']);
    const segunda = await c.consultas.certificacionDelVinculo('v1', primera.etag);
    expect(segunda).toEqual({ datos: null, etag: '"c-1"', noModificado: true });
    expect(recibidas[1]?.cabeceras['If-None-Match']).toBe('"c-1"');
  });

  it('un error llega como ErrorValidasoft con su código y correlación', async () => {
    const { cliente: c } = cliente([
      json({ title: 'No encontrado', detail: 'No existe ese vínculo en la empresa.', codigo: 'vinculo-no-encontrado' }, 404, { 'X-Correlation-Id': 'corr-1' }),
    ]);
    const error = await c.vinculos.obtener('v9').catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ErrorValidasoft);
    expect(error).toMatchObject({ estado: 404, codigo: 'vinculo-no-encontrado', detalle: 'No existe ese vínculo en la empresa.', correlacion: 'corr-1' });
  });

  it('reintenta lo idempotente con la misma clave y no un POST sin clave', async () => {
    const vinculo = { id: 'v1' };
    const { cliente: c, recibidas } = cliente([
      () => new Response(null, { status: 503 }),
      json(consulta),
      () => new Response(null, { status: 502 }),
      json(vinculo, 201),
      () => new Response(null, { status: 503 }),
    ]);
    expect((await c.consultas.certificacionDelVinculo('v1')).datos?.folio).toBe('N1-C-2026-000001-1');
    expect(recibidas).toHaveLength(2);

    await c.vinculos.crear({ tipoVinculo: 'PROVEEDOR', correoInvitacion: 'a@b.co' }, 'srm-sol-7731');
    expect(recibidas.slice(2).map((r) => r.cabeceras['Idempotency-Key'])).toEqual(['srm-sol-7731', 'srm-sol-7731']);

    await expect(c.vinculos.bloquear('v1', { motivo: 'Lista restrictiva' })).rejects.toMatchObject({ estado: 503 });
    expect(recibidas).toHaveLength(5);
  });

  it('recorre los eventos por páginas desde el cursor guardado', async () => {
    const evento = (cursor: string) => ({ id: `e${cursor}`, tipo: 'certificado.emitido', version: 1, ocurridoEn: '', tenantId: 't', empresaId: EMPRESA, cursor, datos: {} });
    const { cliente: c, recibidas } = cliente([
      json({ datos: [evento('11'), evento('12')], cursor: '12', hayMas: true }),
      json({ datos: [evento('15')], cursor: '15', hayMas: false }),
      json({ datos: [], cursor: '0', hayMas: false }),
    ]);
    const cursores: string[] = [];
    for await (const e of c.eventos.recorrer({ desde: '10', tipos: ['certificado.emitido', 'vinculo.bloqueado'], limite: 2 })) cursores.push(e.cursor);
    expect(cursores).toEqual(['11', '12', '15']);
    expect(recibidas.map((r) => new URL(r.url).search)).toEqual([
      '?desde=10&tipos=certificado.emitido%2Cvinculo.bloqueado&limite=2',
      '?desde=12&tipos=certificado.emitido%2Cvinculo.bloqueado&limite=2',
    ]);
    // Con empresaId explícito no va X-Empresa (la API exige que coincidan).
    await c.eventos.listar({ empresaId: EMPRESA });
    expect(recibidas[2]?.cabeceras['X-Empresa']).toBeUndefined();
  });

  it('como usuario firma una aserción nueva por intento y crea la sesión embebida', async () => {
    const { privateKey, publicKey } = generateKeyPairSync('rsa', {
      modulusLength: 2048,
      privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
      publicKeyEncoding: { type: 'spki', format: 'pem' },
    });
    const firmante = crearFirmanteDeAserciones('SRM', privateKey);
    const { cliente: c, recibidas } = cliente([() => new Response(null, { status: 503 }), json({ id: 'x1' }), json({ token: 'token-corto', expira: '2026-11-09T15:30:00Z' })], {
      firmante,
    });
    const laura = c.comoUsuario({ sub: 'srm-laura', nombre: 'Laura', correo: 'laura@constructora.co' });

    await laura.expedientes.obtener('x1');
    const aserciones = recibidas.map((r) => r.cabeceras['X-Usuario-Delegado'] ?? '');
    const jtis = aserciones.map((a) => (JSON.parse(Buffer.from(a.split('.')[1] ?? '', 'base64url').toString()) as { jti: string }).jti);
    expect(new Set(jtis).size).toBe(2);
    for (const a of aserciones) {
      const [cabecera = '', carga = '', firma = ''] = a.split('.');
      expect(verify('RSA-SHA256', Buffer.from(`${cabecera}.${carga}`), createPublicKey(publicKey), Buffer.from(firma, 'base64url'))).toBe(true);
    }

    expect((await laura.sesiones.crear()).token).toBe('token-corto');
    expect(recibidas[2]?.url).toBe('https://valida.acme.co/api/v1/sesiones-usuario');
    expect(recibidas[2]?.cabeceras['X-Empresa']).toBeUndefined();
    await expect(c.sesiones.crear()).rejects.toThrow(/comoUsuario/);
    await expect(cliente([]).cliente.comoUsuario({ sub: 'x', nombre: 'X', correo: 'x@y.co' }).vinculos.obtener('v')).rejects.toThrow(/firmante/);
  });

  it('paraEmpresa cambia X-Empresa y la URL base puede tener ruta', async () => {
    const { cliente: c, recibidas } = cliente([json({ id: 'v1' })], { urlBase: 'https://erp.acme.co/validasoft' });
    await c.paraEmpresa('otra').vinculos.porReferencia('SRM', 'ADP 00123');
    expect(recibidas[0]?.url).toBe('https://erp.acme.co/validasoft/api/v1/vinculos/por-referencia?sistema=SRM&clave=ADP%2000123');
    expect(recibidas[0]?.cabeceras['X-Empresa']).toBe('otra');
  });

  it('el sandbox se consulta sin empresa y su buzón se filtra y se espera', async () => {
    const sandbox = {
      existe: true,
      entorno: 'produccion',
      tenantId: 't1',
      creadoEn: '2026-11-10T00:00:00Z',
      reiniciadoEn: null,
      empresas: [{ id: 'e-prueba', produccionId: EMPRESA, nombre: 'Norte (sandbox)', prefijoFolio: 'TN', activa: true }],
      sistemas: ['SRM'],
      correos: 0,
    };
    const correo = { id: 'c1', para: 'ana@aceros.co', asunto: 'Te invitan', texto: 'https://valida.acme.co/e/abc', html: '<p>x</p>', enlaces: ['https://valida.acme.co/e/abc'], recibidoEn: '2026-11-10T00:00:00Z' };
    const { cliente: c, recibidas } = cliente([
      json(sandbox),
      json(sandbox, 201),
      json(sandbox),
      json({ datos: [], siguienteCursor: null }),
      json({ datos: [correo], siguienteCursor: null }),
      json({ datos: [], siguienteCursor: null }),
      () => new Response(null, { status: 204 }),
    ]);

    expect((await c.sandbox.obtener()).empresas[0]?.id).toBe('e-prueba');
    await c.sandbox.asegurar();
    await c.sandbox.reiniciar();
    expect(recibidas.map((r) => [r.metodo, r.url])).toEqual([
      ['GET', 'https://valida.acme.co/api/v1/sandbox'],
      ['POST', 'https://valida.acme.co/api/v1/sandbox'],
      ['POST', 'https://valida.acme.co/api/v1/sandbox/reiniciar'],
    ]);
    // Son rutas de la credencial, no de una empresa: no llevan X-Empresa.
    expect(recibidas.every((r) => r.cabeceras['X-Empresa'] === undefined)).toBe(true);

    // Sin el correo todavía: la espera consulta de nuevo y lo devuelve cuando llega.
    expect((await c.sandbox.esperarCorreo('ana@aceros.co', { asunto: 'invita', esperaMs: 5_000 })).enlaces).toEqual(['https://valida.acme.co/e/abc']);
    expect(recibidas.at(-1)?.url).toBe('https://valida.acme.co/api/v1/sandbox/correos?para=ana%40aceros.co&asunto=invita&limite=1');

    // Si nunca llega, falla; vaciar el buzón responde 204.
    await expect(c.sandbox.esperarCorreo('nadie@aceros.co', { esperaMs: 0 })).rejects.toThrow(/No llegó al buzón/);
    await c.sandbox.vaciarCorreos();
    expect(recibidas.at(-1)).toMatchObject({ metodo: 'DELETE', url: 'https://valida.acme.co/api/v1/sandbox/correos' });
  });

  it('sin API key o sin URL no se construye', () => {
    expect(() => new ClienteValidasoft({ urlBase: 'https://x.co', apiKey: '' })).toThrow(/API key/);
    expect(() => new ClienteValidasoft({ urlBase: '', apiKey: LLAVE })).toThrow(/URL/);
  });
});

describe('integración abierta: referencias, contactos, ficha y sesión embebida', () => {
  it('los terceros se crean y se buscan por la clave del sistema, de forma idempotente', async () => {
    const { cliente: c, recibidas } = cliente([
      json({ id: 't1', nombre: 'Aceros' }, 201),
      json([{ sistema: 'ERPX', id: 'C-1' }], 201),
      json({ id: 't1' }),
      json([{ sistema: 'ERPX', id: 'C-1' }]),
    ]);
    const identificacion = { pais: 'CO', tipo: 'NIT', numero: '900123456' };
    await c.terceros.crear({ identificacion, tipoPersona: 'Juridica', nombre: 'Aceros', referenciaExterna: { sistema: '', id: 'C-1' } }, 'erpx-c-1');
    expect(recibidas[0]).toMatchObject({ metodo: 'POST', url: 'https://valida.acme.co/api/v1/terceros' });
    expect(recibidas[0]?.cabeceras['Idempotency-Key']).toBe('erpx-c-1');
    expect((recibidas[0]?.cuerpo as { referenciaExterna: unknown }).referenciaExterna).toEqual({ sistema: '', id: 'C-1' });

    await c.terceros.agregarReferencia('t1', { id: 'C-1' });
    expect(recibidas[1]).toMatchObject({ metodo: 'POST', url: 'https://valida.acme.co/api/v1/terceros/t1/referencias', cuerpo: { id: 'C-1' } });

    await c.terceros.porReferencia('ERPX', 'C 1');
    expect(recibidas[2]?.url).toBe('https://valida.acme.co/api/v1/terceros/por-referencia?sistema=ERPX&clave=C%201');
    expect(recibidas[2]?.cabeceras['X-Empresa']).toBeUndefined(); // el tercero es del tenant, no de una empresa

    expect(await c.terceros.referencias('t1')).toEqual([{ sistema: 'ERPX', id: 'C-1' }]);
    expect(recibidas[3]?.url).toBe('https://valida.acme.co/api/v1/terceros/t1/referencias');
  });

  it('el vínculo suma referencias, reemplaza contactos por empresa y entrega la ficha para el ERP', async () => {
    const { cliente: c, recibidas } = cliente([json({ id: 'v1' }), json({ id: 'v1', contactos: [] }), json({ vinculoId: 'v1', contactos: [] })]);
    await c.vinculos.agregarReferencia('v1', { id: 'S-1' });
    expect(recibidas[0]).toMatchObject({ metodo: 'POST', url: 'https://valida.acme.co/api/v1/vinculos/v1/referencias', cuerpo: { id: 'S-1' } });

    await c.vinculos.actualizarContactos('v1', [{ nombre: 'Ana', correo: 'ana@aceros.co', rol: 'Pagos' }]);
    expect(recibidas[1]).toMatchObject({
      metodo: 'PUT',
      url: 'https://valida.acme.co/api/v1/vinculos/v1/contactos',
      cuerpo: { contactos: [{ nombre: 'Ana', correo: 'ana@aceros.co', rol: 'Pagos' }] },
    });
    expect(recibidas[1]?.cabeceras['X-Empresa']).toBe(EMPRESA);

    await c.vinculos.ficha('v1');
    expect(recibidas[2]).toMatchObject({ metodo: 'GET', url: 'https://valida.acme.co/api/v1/vinculos/v1/ficha' });
  });

  it('la sesión del tercero se pide con el origen de la página y el componente, sin delegar a una persona', async () => {
    const sesion = { token: 'a.b.c', expira: '2026-11-11T12:10:00Z', url: 'https://valida.acme.co/embed/boveda', origen: 'https://anfitrion.test', componente: 'boveda', vinculoId: 'v1', terceroId: 't1' };
    const { cliente: c, recibidas } = cliente([json(sesion, 201)]);
    expect(await c.sesiones.crearDelTercero({ vinculoId: 'v1', componente: 'boveda', origen: 'https://anfitrion.test', minutos: 5 })).toEqual(sesion);
    expect(recibidas[0]).toMatchObject({
      metodo: 'POST',
      url: 'https://valida.acme.co/api/v1/sesiones-tercero',
      cuerpo: { vinculoId: 'v1', componente: 'boveda', origen: 'https://anfitrion.test', minutos: 5 },
    });
    expect(recibidas[0]?.cabeceras['X-Empresa']).toBe(EMPRESA);
    expect(recibidas[0]?.cabeceras['X-Usuario-Delegado']).toBeUndefined();
  });
});
