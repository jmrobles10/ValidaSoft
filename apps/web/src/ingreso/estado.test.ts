import { ErrorApi, type PasoDeIngresoDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import {
  codigoDeRecuperacionCompleto,
  ESPERA_REENVIO_S,
  esCorreoPlausible,
  exigeEmpezarDeNuevo,
  intentosRestantesDe,
  leerRetornoDeSso,
  normalizarCodigoDeRecuperacion,
  resolverPaso,
  segundosParaReenviar,
} from './estado';

const desafio = '0d0e5a3a-5f0e-47fa-a0a1-6c1e0f9bd7a1';

function paso(parcial: Partial<PasoDeIngresoDto> & Pick<PasoDeIngresoDto, 'estado'>): PasoDeIngresoDto {
  return { desafio, cuentas: null, opciones: null, sesion: null, ...parcial };
}

describe('resolverPaso', () => {
  it('con sesión, termina el ingreso', () => {
    const sesion = { token: 't', expira: '2026-11-11T10:00:00Z', sesionId: 's', metodo: 'Codigo', sugerirPasskey: true };
    expect(resolverPaso(paso({ estado: 'Sesion', sesion }))).toEqual({ tipo: 'sesion', sesion });
  });

  it('con varias cuentas pide elegir', () => {
    const cuentas = [{ id: 'u:a:1', tipo: 'Usuario', nombre: 'Ana', grupo: 'Norte', exigePasskey: false, tienePasskey: false }];
    expect(resolverPaso(paso({ estado: 'ElegirCuenta', cuentas }))).toEqual({ tipo: 'siguiente', paso: { paso: 'cuenta', desafio, cuentas } });
  });

  it('pide verificar o registrar la passkey con las opciones de WebAuthn', () => {
    const opciones = { challenge: 'abc' };
    expect(resolverPaso(paso({ estado: 'PasskeyRequerida', opciones }))).toEqual({ tipo: 'siguiente', paso: { paso: 'passkey', desafio, opciones } });
    expect(resolverPaso(paso({ estado: 'RegistrarPasskey', opciones }))).toEqual({ tipo: 'siguiente', paso: { paso: 'registrar', desafio, opciones } });
  });

  it('un paso sin lo que necesita, o desconocido, es un error de programación y no se disimula', () => {
    expect(() => resolverPaso(paso({ estado: 'PasskeyRequerida' }))).toThrow(/opciones/);
    expect(() => resolverPaso(paso({ estado: 'Sesion' }))).toThrow(/sin sesión/);
    expect(() => resolverPaso(paso({ estado: 'Otro' }))).toThrow(/desconocido/);
  });
});

describe('errores del código', () => {
  const error = (codigo: string, extensiones: Record<string, unknown> = {}) => new ErrorApi(422, codigo, 'detalle', null, null, {}, extensiones);

  it('lee los intentos que quedan solo del código incorrecto', () => {
    expect(intentosRestantesDe(error('codigo-invalido', { intentosRestantes: 3 }))).toBe(3);
    expect(intentosRestantesDe(error('codigo-invalido', { intentosRestantes: 0 }))).toBe(0);
    expect(intentosRestantesDe(error('codigo-invalido'))).toBeNull();
    expect(intentosRestantesDe(error('otro', { intentosRestantes: 3 }))).toBeNull();
    expect(intentosRestantesDe(new Error('x'))).toBeNull();
  });

  it('el código vencido, bloqueado o un ingreso usado obligan a empezar de nuevo', () => {
    for (const codigo of ['codigo-vencido', 'codigo-bloqueado', 'desafio-usado', 'desafio-vencido', 'desafio-no-encontrado'])
      expect(exigeEmpezarDeNuevo(error(codigo))).toBe(true);
    expect(exigeEmpezarDeNuevo(error('codigo-invalido'))).toBe(false);
    expect(exigeEmpezarDeNuevo(new Error('x'))).toBe(false);
  });
});

describe('correo y reenvío', () => {
  it('reconoce un correo con formato razonable', () => {
    expect(esCorreoPlausible(' ana@aceros.co ')).toBe(true);
    for (const malo of ['', 'ana', 'ana@', '@aceros.co', 'ana@aceros', 'a na@aceros.co', `${'a'.repeat(250)}@x.co`]) expect(esCorreoPlausible(malo)).toBe(false);
  });

  it('cuenta los segundos de espera para pedir otro código', () => {
    expect(segundosParaReenviar(1_000, 1_000)).toBe(ESPERA_REENVIO_S);
    expect(segundosParaReenviar(1_000, 11_500)).toBe(ESPERA_REENVIO_S - 10);
    expect(segundosParaReenviar(1_000, 1_000 + ESPERA_REENVIO_S * 1000)).toBe(0);
    expect(segundosParaReenviar(1_000, 99_000)).toBe(0);
  });
});

describe('retorno del proveedor de identidad (PI-1)', () => {
  it('un retorno bueno trae el desafío para canjear', () => {
    expect(leerRetornoDeSso(`#/ingreso?sso=${desafio}`)).toEqual({ tipo: 'completar', desafio });
    expect(leerRetornoDeSso(`/ingreso?sso=${desafio.replaceAll('-', '')}`)).toEqual({ tipo: 'completar', desafio: desafio.replaceAll('-', '') });
  });

  it('un retorno fallido trae el código del error', () => {
    expect(leerRetornoDeSso('#/ingreso?sso_error=sso-cancelado')).toEqual({ tipo: 'error', codigo: 'sso-cancelado' });
  });

  it('lo que no es un retorno se ignora', () => {
    for (const hash of ['', '#/', '#/ingreso', '#/plataforma/grupos?sso=' + desafio, '#/ingreso?otra=1']) expect(leerRetornoDeSso(hash)).toBeNull();
  });

  it('nunca confía en lo que viene en la dirección: un desafío o un código con forma rara se descarta', () => {
    expect(leerRetornoDeSso('#/ingreso?sso=<script>alert(1)</script>')).toEqual({ tipo: 'error', codigo: 'sso-estado-invalido' });
    expect(leerRetornoDeSso('#/ingreso?sso=123')).toEqual({ tipo: 'error', codigo: 'sso-estado-invalido' });
    expect(leerRetornoDeSso('#/ingreso?sso_error=<b>x</b>')).toEqual({ tipo: 'error', codigo: 'sso-error-interno' });
    expect(leerRetornoDeSso(`#/ingreso?sso_error=${'a'.repeat(80)}`)).toEqual({ tipo: 'error', codigo: 'sso-error-interno' });
  });
});

describe('código de recuperación (PI-18)', () => {
  it('lo que se escribe queda en mayúsculas, con guion después de cinco y sin sobrantes', () => {
    expect(normalizarCodigoDeRecuperacion('ab3cd')).toBe('AB3CD');
    expect(normalizarCodigoDeRecuperacion('ab3cdefgh2')).toBe('AB3CD-EFGH2');
    expect(normalizarCodigoDeRecuperacion(' ab3cd - efgh2 xyz')).toBe('AB3CD-EFGH2');
    expect(normalizarCodigoDeRecuperacion('')).toBe('');
  });

  it('solo está completo con dos grupos de cinco', () => {
    expect(codigoDeRecuperacionCompleto('AB3CD-EFGH2')).toBe(true);
    for (const malo of ['', 'AB3CD', 'AB3CD-EFGH', 'ab3cd-efgh2', 'AB3CDEFGH2']) expect(codigoDeRecuperacionCompleto(malo)).toBe(false);
  });
});
