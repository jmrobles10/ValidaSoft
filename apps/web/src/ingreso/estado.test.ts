import { ErrorApi, type PasoDeIngresoDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { ESPERA_REENVIO_S, esCorreoPlausible, exigeEmpezarDeNuevo, intentosRestantesDe, resolverPaso, segundosParaReenviar } from './estado';

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
