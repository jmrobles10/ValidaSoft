import { ErrorApi } from '@validasoft/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { peticionDesde, resolverReautenticacion, rutaDeMiCuenta, solicitarReautenticacion, useReautenticacion } from './reautenticacion';

function error(extensiones: Record<string, unknown> = { accion: 'rotar-api-key', minutos: 5, tienePasskey: true }): ErrorApi {
  return new ErrorApi(403, 'reautenticacion-requerida', 'Confirma con tu passkey', null, null, {}, extensiones);
}

afterEach(() => {
  resolverReautenticacion(false);
});

describe('reautenticación para acciones sensibles', () => {
  it('lee la acción, los minutos y si hay passkey de las extensiones del error', () => {
    expect(peticionDesde(error())).toEqual({ accion: 'rotar-api-key', minutos: 5, tienePasskey: true });
    expect(peticionDesde(error({ accion: 'cambiar-roles', minutos: 10, tienePasskey: false }))).toEqual({ accion: 'cambiar-roles', minutos: 10, tienePasskey: false });
  });

  it('con extensiones raras usa valores seguros: sin acción, 5 minutos y con passkey', () => {
    expect(peticionDesde(error({ accion: 7, minutos: -1 }))).toEqual({ accion: '', minutos: 5, tienePasskey: true });
    expect(peticionDesde(error({}))).toEqual({ accion: '', minutos: 5, tienePasskey: true });
  });

  it('abre un solo diálogo: la segunda petición espera la misma confirmación', async () => {
    const primera = solicitarReautenticacion(error());
    const segunda = solicitarReautenticacion(error({ accion: 'crear-api-key', minutos: 5, tienePasskey: true }));

    expect(segunda).toBe(primera);
    expect(useReautenticacion.getState().peticion?.accion).toBe('rotar-api-key');

    resolverReautenticacion(true);
    await expect(primera).resolves.toBe(true);
    await expect(segunda).resolves.toBe(true);
    expect(useReautenticacion.getState().peticion).toBeNull();
  });

  it('cancelar responde falso y deja abrir uno nuevo después', async () => {
    const cancelada = solicitarReautenticacion(error());
    resolverReautenticacion(false);
    await expect(cancelada).resolves.toBe(false);

    const nueva = solicitarReautenticacion(error());
    expect(nueva).not.toBe(cancelada);
    expect(useReautenticacion.getState().peticion).not.toBeNull();
    resolverReautenticacion(true);
    await expect(nueva).resolves.toBe(true);
  });

  it('resolver sin diálogo abierto no falla', () => {
    expect(() => resolverReautenticacion(true)).not.toThrow();
  });
});

describe('ruta de Mi cuenta', () => {
  it('en la consola va a la de Plataforma y en una empresa, a la de esa empresa', () => {
    expect(rutaDeMiCuenta('#/plataforma/operadores')).toBe('#/plataforma/mi-cuenta');
    expect(rutaDeMiCuenta('#/e/6f3f1f6e/areas?q=x')).toBe('#/e/6f3f1f6e/mi-cuenta');
    expect(rutaDeMiCuenta('#/')).toBe('#/');
    expect(rutaDeMiCuenta('')).toBe('#/');
  });
});
