import type { ConfiguracionDeSsoDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { aRolSso, cuerpoDeSso, dominiosVerificados, formularioDesde, problemasDeSso, secretoParaEnviar, secretoValido, sinCambiosDeSso } from './modeloSso';

const EMPRESA = '6f3f1f6e-9d1a-4c0e-8a53-0d0f2b0e1f11';

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
    dominios: [
      { dominio: 'andina.co', verificado: true, token: null, instruccion: null, creadoEn: '2026-11-01T10:00:00Z', verificadoEn: '2026-11-01T11:00:00Z' },
      { dominio: 'andina.com', verificado: false, token: 'validasoft-verificacion=abc', instruccion: 'publica', creadoEn: '2026-11-02T10:00:00Z', verificadoEn: null },
    ],
    aprovisionamientoJit: true,
    rolesPorOmision: [{ rol: 'Revisor', empresaId: null }],
    reglas: [{ grupo: 'Oficiales', rol: 'OficialCumplimiento', empresaId: EMPRESA }],
    sincronizarRoles: true,
    modo: 'Complementario',
    urlDeRetorno: 'https://validasoft.co/api/v1/ingreso/sso/retorno',
    workOsDisponible: false,
    actualizadoEn: '2026-11-03T10:00:00Z',
    ...cambios,
  };
}

describe('formulario del SSO', () => {
  it('toma lo guardado y deja las empresas vacías como «todo el grupo»', () => {
    const f = formularioDesde(configuracion());
    expect(f).toMatchObject({ activo: true, proveedor: 'oidc', nombre: 'Andina', modo: 'Complementario', sincronizarRoles: true });
    expect(f.rolesPorOmision).toEqual([{ rol: 'Revisor', empresaId: '' }]);
    expect(f.reglas).toEqual([{ grupo: 'Oficiales', rol: 'OficialCumplimiento', empresaId: EMPRESA }]);
  });

  it('un proveedor o un modo desconocido cae en el más conservador', () => {
    const f = formularioDesde(configuracion({ proveedor: 'saml', modo: 'otro', reclamoDeGrupos: '' }));
    expect(f.proveedor).toBe('oidc');
    expect(f.modo).toBe('Complementario');
    expect(f.reclamoDeGrupos).toBe('groups');
  });

  it('solo los dominios verificados cuentan para activarlo', () => {
    expect(dominiosVerificados(configuracion())).toEqual(['andina.co']);
    expect(dominiosVerificados(configuracion({ dominios: [] }))).toEqual([]);
  });

  it('un rol desconocido de un selector se vuelve Revisor', () => {
    expect(aRolSso('Auditor')).toBe('Auditor');
    expect(aRolSso('Superusuario')).toBe('Revisor');
  });
});

describe('cuerpo de PUT /configuracion/sso', () => {
  it('con OIDC envía emisor y client id, sin los datos de WorkOS, y el secreto como se pidió', () => {
    const f = formularioDesde(configuracion());
    const conservar = cuerpoDeSso(f, secretoParaEnviar('conservar', ''));
    expect(conservar).toMatchObject({ proveedor: 'oidc', autoridad: 'https://login.andina.co/v2.0', clientId: 'validasoft-andina', organizacionWorkOs: null, conexionWorkOs: null });
    expect(conservar.secretoDelCliente).toBeNull();
    expect(cuerpoDeSso(f, secretoParaEnviar('cambiar', ' nuevo-secreto-1 ')).secretoDelCliente).toBe('nuevo-secreto-1');
    expect(cuerpoDeSso(f, secretoParaEnviar('quitar', '')).secretoDelCliente).toBe('');
  });

  it('con WorkOS envía la organización o la conexión y nunca el emisor ni el secreto', () => {
    const f = { ...formularioDesde(configuracion()), proveedor: 'workos' as const, organizacionWorkOs: ' org_01 ', conexionWorkOs: '' };
    expect(cuerpoDeSso(f, 'no-debe-viajar')).toMatchObject({ proveedor: 'workos', autoridad: null, clientId: null, secretoDelCliente: null, organizacionWorkOs: 'org_01', conexionWorkOs: null });
  });

  it('las reglas sin grupo no viajan y las empresas vacías van como nulas', () => {
    const f = { ...formularioDesde(configuracion()), reglas: [{ grupo: ' Oficiales ', rol: 'Auditor' as const, empresaId: '' }, { grupo: '  ', rol: 'Auditor' as const, empresaId: '' }] };
    expect(cuerpoDeSso(f, null).reglas).toEqual([{ grupo: 'Oficiales', rol: 'Auditor', empresaId: null }]);
    expect(cuerpoDeSso(f, null).rolesPorOmision).toEqual([{ rol: 'Revisor', empresaId: null }]);
  });
});

describe('qué falta antes de guardar', () => {
  const lleno = formularioDesde(configuracion());

  it('un formulario completo con un dominio verificado no tiene problemas', () => {
    expect(problemasDeSso(lleno, true)).toEqual([]);
  });

  it('pide el nombre, el emisor con https y el client id con OIDC', () => {
    expect(problemasDeSso({ ...lleno, nombre: ' ', autoridad: 'login.andina.co', clientId: '' }, true)).toEqual(['nombre', 'autoridad', 'clientId']);
  });

  it('con WorkOS pide la organización o la conexión', () => {
    expect(problemasDeSso({ ...lleno, proveedor: 'workos', organizacionWorkOs: '', conexionWorkOs: '' }, true)).toEqual(['conexionWorkOs']);
    expect(problemasDeSso({ ...lleno, proveedor: 'workos', conexionWorkOs: 'conn_1' }, true)).toEqual([]);
  });

  it('crear personas al entrar exige un rol por omisión o una regla con grupo', () => {
    expect(problemasDeSso({ ...lleno, rolesPorOmision: [], reglas: [] }, true)).toEqual(['rolPorOmision']);
    expect(problemasDeSso({ ...lleno, rolesPorOmision: [], reglas: [{ grupo: 'Todos', rol: 'Revisor', empresaId: '' }] }, true)).toEqual([]);
    expect(problemasDeSso({ ...lleno, aprovisionamientoJit: false, rolesPorOmision: [], reglas: [] }, true)).toEqual([]);
  });

  it('activarlo sin un dominio verificado es un problema; apagado, no', () => {
    expect(problemasDeSso(lleno, false)).toEqual(['dominio']);
    expect(problemasDeSso({ ...lleno, activo: false }, false)).toEqual([]);
  });
});

describe('cambios sin guardar', () => {
  const original = formularioDesde(configuracion());

  it('el orden de las filas no cuenta como cambio', () => {
    const mas = formularioDesde(configuracion({ reglas: [{ grupo: 'A', rol: 'Auditor', empresaId: null }, { grupo: 'B', rol: 'Revisor', empresaId: null }] }));
    const invertido = { ...mas, reglas: [...mas.reglas].reverse() };
    expect(sinCambiosDeSso(invertido, mas, null)).toBe(true);
  });

  it('un secreto nuevo o quitado es un cambio aunque el resto sea igual', () => {
    expect(sinCambiosDeSso(original, original, null)).toBe(true);
    expect(sinCambiosDeSso(original, original, 'nuevo-secreto-1')).toBe(false);
    expect(sinCambiosDeSso(original, original, '')).toBe(false);
  });

  it('una barra final en el emisor no es un cambio', () => {
    expect(sinCambiosDeSso({ ...original, autoridad: `${original.autoridad}/` }, original, null)).toBe(true);
    expect(sinCambiosDeSso({ ...original, modo: 'Exclusivo' }, original, null)).toBe(false);
  });
});

describe('secreto del cliente', () => {
  it('entre 8 y 2000 caracteres y sin espacios', () => {
    expect(secretoValido('12345678')).toBe(true);
    expect(secretoValido('1234567')).toBe(false);
    expect(secretoValido('con espacio-adentro')).toBe(false);
    expect(secretoValido('x'.repeat(2001))).toBe(false);
    expect(secretoValido('  secreto-largo-1  ')).toBe(true);
  });
});
