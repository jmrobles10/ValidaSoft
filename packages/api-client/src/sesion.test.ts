import { describe, expect, it } from 'vitest';
import { accionesDisponibles, accionPermitidaPorEstado } from './dominio';
import { accedeAEmpresa, esSesionDePlataforma, esSesionDirecta, leerSesion, msRestantes, puede, scopesEn, TokenInvalido } from './sesion';

function base64Url(texto: string): string {
  return Buffer.from(texto, 'utf8').toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function token(claims: Record<string, unknown>): string {
  return `${base64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))}.${base64Url(JSON.stringify(claims))}.firma`;
}

const base = {
  sub: 'usuario-1',
  exp: 1_900_000_000,
  'validasoft:tenant': 'tenant-1',
  'validasoft:sistema': 'SRM',
  'validasoft:actor-tipo': 'Usuario',
};

describe('leerSesion', () => {
  it('lee scopes de tenant, scopes por empresa y empresas', () => {
    const sesion = leerSesion(
      token({
        ...base,
        scope: ['vinculos:leer', 'terceros:leer'],
        'validasoft:scope-empresa': ['vinculos:escribir|e1', 'vinculos:bloquear|e1', 'vinculos:escribir|e2'],
        'validasoft:empresa': ['e1', 'e2'],
      }),
    );

    expect(sesion.usuarioId).toBe('usuario-1');
    expect(sesion.tenantId).toBe('tenant-1');
    expect(sesion.sistema).toBe('SRM');
    expect(sesion.expira.toISOString()).toBe(new Date(1_900_000_000_000).toISOString());
    expect([...scopesEn(sesion, 'e1')].sort()).toEqual(['terceros:leer', 'vinculos:bloquear', 'vinculos:escribir', 'vinculos:leer']);
    expect(puede(sesion, 'vinculos:bloquear', 'e2')).toBe(false);
    expect(puede(sesion, 'vinculos:escribir', null)).toBe(false);
    expect(accedeAEmpresa(sesion, 'e2')).toBe(true);
    expect(accedeAEmpresa(sesion, 'e3')).toBe(false);
  });

  it('acepta claims únicos como texto y el comodín de empresas', () => {
    const sesion = leerSesion(token({ ...base, scope: 'usuarios:administrar', 'validasoft:empresa': '*' }));
    expect(puede(sesion, 'usuarios:administrar', null)).toBe(true);
    expect(sesion.empresas).toBe('*');
    expect(accedeAEmpresa(sesion, 'cualquiera')).toBe(true);
  });

  it('lee nombres con tildes (UTF-8 en base64url)', () => {
    const sesion = leerSesion(token({ ...base, 'validasoft:sistema': 'Compañía' }));
    expect(sesion.sistema).toBe('Compañía');
  });

  it('reconoce la sesión de ingreso directo por la familia y el método (ING-1)', () => {
    const delegada = leerSesion(token(base));
    expect(esSesionDirecta(delegada)).toBe(false);
    expect(delegada.metodoIngreso).toBeNull();

    const directa = leerSesion(token({ ...base, 'validasoft:sistema': undefined, 'validasoft:sesion-id': 'sesion-1', 'validasoft:metodo': 'Passkey' }));
    expect(esSesionDirecta(directa)).toBe(true);
    expect(directa.sesionId).toBe('sesion-1');
    expect(directa.metodoIngreso).toBe('Passkey');
    expect(directa.sistema).toBeNull();

    // Un método desconocido no se toma por válido.
    expect(leerSesion(token({ ...base, 'validasoft:sesion-id': 's', 'validasoft:metodo': 'Otro' })).metodoIngreso).toBeNull();
  });

  it('calcula el tiempo restante', () => {
    const sesion = leerSesion(token({ ...base, exp: 1_000 }));
    expect(msRestantes(sesion, new Date(400_000))).toBe(600_000);
  });

  it('rechaza tokens mal formados o incompletos', () => {
    expect(() => leerSesion('no-es-jwt')).toThrow(TokenInvalido);
    expect(() => leerSesion(token({ sub: 'x' }))).toThrow(TokenInvalido);
    // Sin tenant solo vale la sesión de Plataforma.
    expect(() => leerSesion(token({ sub: 'x', exp: 1_900_000_000, 'validasoft:actor-tipo': 'Usuario' }))).toThrow(TokenInvalido);
  });

  it('lee la sesión de un operador de Plataforma: sin tenant, con su nombre y solo plataforma:tenants', () => {
    const sesion = leerSesion(
      token({ sub: 'BACKOFFICE:jperez', exp: 1_900_000_000, 'validasoft:actor-tipo': 'Plataforma', name: 'Juana Pérez', scope: 'plataforma:tenants' }),
    );
    expect(sesion.tenantId).toBeNull();
    expect(sesion.nombre).toBe('Juana Pérez');
    expect(esSesionDePlataforma(sesion)).toBe(true);
    expect(esSesionDePlataforma(leerSesion(token({ ...base, scope: 'plataforma:tenants' })))).toBe(false);
  });
});

describe('acciones del vínculo', () => {
  const todos = new Set(['vinculos:escribir', 'vinculos:bloquear']);

  it('ofrece según el estado', () => {
    const nombres = (estado: Parameters<typeof accionesDisponibles>[0]['estado'], aceptada: string | null = null) =>
      accionesDisponibles({ estado, invitacionAceptadaEn: aceptada }, todos).map((a) => a.accion);

    expect(nombres('Invitado')).toEqual(['invitar', 'bloquear', 'desvincular', 'anular']);
    expect(nombres('Invitado', '2026-11-01T00:00:00Z')).toEqual(['bloquear', 'desvincular', 'anular']);
    expect(nombres('Activo')).toEqual(['bloquear', 'desvincular', 'anular']);
    expect(nombres('Bloqueado')).toEqual(['desbloquear', 'desvincular', 'anular']);
    expect(nombres('Desvinculado')).toEqual(['reintegrar']);
    expect(nombres('Anulado')).toEqual([]);
  });

  it('con el acceso del correo rechazado por los contactos del tercero ofrece reinvitar a otro correo aunque ya hubiera aceptado', () => {
    const rechazado = { estado: 'EnValidacion', invitacionAceptadaEn: '2026-11-01T00:00:00Z', accesoDelCorreo: { estado: 'Rechazado', correo: 'a@b.co', desde: '2026-11-01T00:00:00Z', resueltoEn: null } } as const;
    expect(accionesDisponibles(rechazado, todos).map((a) => a.accion)).toEqual(['invitar', 'bloquear', 'desvincular', 'anular']);
    // Pendiente o aprobado, y ya aceptado: no se reinvita; bloqueado o terminal tampoco aunque esté rechazado.
    expect(accionPermitidaPorEstado('invitar', { ...rechazado, accesoDelCorreo: { ...rechazado.accesoDelCorreo, estado: 'Pendiente' } })).toBe(false);
    expect(accionPermitidaPorEstado('invitar', { ...rechazado, accesoDelCorreo: { ...rechazado.accesoDelCorreo, estado: 'Aprobado' } })).toBe(false);
    expect(accionPermitidaPorEstado('invitar', { ...rechazado, estado: 'Bloqueado' })).toBe(false);
    expect(accionPermitidaPorEstado('invitar', { ...rechazado, estado: 'Anulado' })).toBe(false);
  });

  it('con la invitación retenida a la espera de aprobación no se envía ni se reenvía; aprobada o rechazada, las reglas de siempre', () => {
    const retenida = (estado: 'Pendiente' | 'Aprobada' | 'Rechazada') =>
      ({ estado: 'Invitado', invitacionAceptadaEn: null, invitacionPorAprobar: { estado, correoDestino: 'a@b.co', vigenciaHoras: 72, solicitadaEn: '2026-11-14T00:00:00Z', solicitadaPor: 'Usuario:u1' } }) as const;
    expect(accionPermitidaPorEstado('invitar', retenida('Pendiente'))).toBe(false);
    expect(accionesDisponibles(retenida('Pendiente'), todos).map((a) => a.accion)).toEqual(['bloquear', 'desvincular', 'anular']);
    expect(accionPermitidaPorEstado('invitar', retenida('Aprobada'))).toBe(true);
    expect(accionPermitidaPorEstado('invitar', { ...retenida('Aprobada'), invitacionAceptadaEn: '2026-11-15T00:00:00Z' })).toBe(false);
  });

  it('filtra por scopes', () => {
    const soloEscribir = accionesDisponibles({ estado: 'Activo', invitacionAceptadaEn: null }, new Set(['vinculos:escribir']));
    expect(soloEscribir.map((a) => a.accion)).toEqual(['desvincular', 'anular']);
    expect(accionPermitidaPorEstado('bloquear', { estado: 'Bloqueado', invitacionAceptadaEn: null })).toBe(false);
  });
});
