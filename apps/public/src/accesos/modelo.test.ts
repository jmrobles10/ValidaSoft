import type { AccesoSolicitadoDto, ContactoDelTerceroDto, SesionActivaDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { decididas, estadoDelContacto, listaDeEmpresas, nombreDelTercero, ordenarContactos, ordenarSesiones, porDecidir, porDecidirPorMi, puedoDecidir, reconocerDispositivo } from './modelo';

function solicitud(cambios: Partial<AccesoSolicitadoDto> = {}): AccesoSolicitadoDto {
  return {
    id: 'a1',
    correo: 'intruso@empresa-a.co',
    estado: 'Pendiente',
    desde: '2026-11-12T15:00:00Z',
    empresas: [{ empresa: 'Empresa A', fecha: '2026-11-12T15:00:00Z' }],
    decididoPor: null,
    decididoEn: null,
    motivo: null,
    ...cambios,
  };
}

describe('accesos por aprobar', () => {
  it('separa lo que falta decidir de lo decidido, y lo decidido más reciente primero', () => {
    const lista = [
      solicitud({ id: 'p' }),
      solicitud({ id: 'v', estado: 'Aprobada', decididoEn: '2026-11-10T10:00:00Z' }),
      solicitud({ id: 'n', estado: 'Rechazada', decididoEn: '2026-11-13T10:00:00Z' }),
    ];

    expect(porDecidir(lista).map((s) => s.id)).toEqual(['p']);
    expect(decididas(lista).map((s) => s.id)).toEqual(['n', 'v']);
  });

  it('nombra a las empresas que invitaron sin repetir ninguna', () => {
    const e = (empresa: string) => ({ empresa, fecha: '2026-11-12T15:00:00Z' });

    expect(listaDeEmpresas([])).toBe('');
    expect(listaDeEmpresas([e('Norte')])).toBe('Norte');
    expect(listaDeEmpresas([e('Norte'), e('Sur'), e('Norte')])).toBe('Norte y Sur');
    expect(listaDeEmpresas([e('Norte'), e('Sur'), e('Oriente')])).toBe('Norte, Sur y Oriente');
  });

  it('del tercero solo se dice el nombre con las letras tapadas; sin él, un texto neutro', () => {
    expect(nombreDelTercero('P••••• d•• N•••', 'este tercero')).toBe('P••••• d•• N•••');
    expect(nombreDelTercero(null, 'este tercero')).toBe('este tercero');
    expect(nombreDelTercero('  ', 'este tercero')).toBe('este tercero');
  });
});

describe('sesiones del tercero', () => {
  it('reconoce el navegador y el sistema en el User-Agent', () => {
    expect(reconocerDispositivo('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36')).toEqual({
      navegador: 'Chrome',
      sistema: 'Windows',
    });
    expect(reconocerDispositivo('Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 Chrome/130.0 Safari/537.36 Edg/130.0')).toEqual({ navegador: 'Edge', sistema: 'Windows' });
    expect(reconocerDispositivo('Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 Version/17.0 Safari/605.1.15')).toEqual({ navegador: 'Safari', sistema: 'macOS' });
    expect(reconocerDispositivo('Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Version/17.0 Mobile/15E148 Safari/604.1')).toEqual({
      navegador: 'Safari',
      sistema: 'iOS',
    });
    expect(reconocerDispositivo('Mozilla/5.0 (Linux; Android 14) AppleWebKit/537.36 Chrome/130.0 Mobile Safari/537.36')).toEqual({ navegador: 'Chrome', sistema: 'Android' });
    expect(reconocerDispositivo('Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0')).toEqual({ navegador: 'Firefox', sistema: 'Linux' });
    expect(reconocerDispositivo(null)).toEqual({ navegador: null, sistema: null });
    expect(reconocerDispositivo('curl/8.0')).toEqual({ navegador: null, sistema: null });
  });

  it('pone primero la sesión de este navegador y luego las demás, la más reciente primero', () => {
    const sesion = (id: string, creadaEn: string, esActual = false): SesionActivaDto => ({
      id,
      correo: 'a@a.co',
      creadaEn,
      expiraEn: '2026-11-15T00:00:00Z',
      ultimoUsoEn: null,
      dispositivo: null,
      ip: null,
      esActual,
    });

    expect(ordenarSesiones([sesion('vieja', '2026-11-10T00:00:00Z'), sesion('actual', '2026-11-09T00:00:00Z', true), sesion('nueva', '2026-11-12T00:00:00Z')]).map((s) => s.id)).toEqual([
      'actual',
      'nueva',
      'vieja',
    ]);
  });
});

describe('verificación de los contactos', () => {
  const contacto = (cambios: Partial<ContactoDelTerceroDto>): ContactoDelTerceroDto => ({
    nombre: 'Contacto',
    correo: 'a@norte.co',
    rol: 'Principal',
    verificacion: 'SinVerificar',
    medio: null,
    verificadoEn: null,
    esTuCorreo: false,
    desplazado: false,
    ...cambios,
  });

  it('un contacto verificado lo está aunque tenga otra marca; sin verificar, desplazado pesa más que sin verificar', () => {
    expect(estadoDelContacto(contacto({ verificacion: 'Verificado', desplazado: true }))).toBe('Verificado');
    expect(estadoDelContacto(contacto({ desplazado: true }))).toBe('Desplazado');
    expect(estadoDelContacto(contacto({}))).toBe('SinVerificar');
  });

  it('ordena tu correo primero y luego verificados, sin verificar y desplazados, cada grupo por correo', () => {
    const lista = [
      contacto({ correo: 'd@norte.co', desplazado: true }),
      contacto({ correo: 'b@norte.co', verificacion: 'Verificado' }),
      contacto({ correo: 'c@norte.co' }),
      contacto({ correo: 'z@norte.co', esTuCorreo: true }),
      contacto({ correo: 'a@norte.co', verificacion: 'Verificado' }),
    ];

    expect(ordenarContactos(lista).map((c) => c.correo)).toEqual(['z@norte.co', 'a@norte.co', 'b@norte.co', 'c@norte.co', 'd@norte.co']);
  });

  it('solo cuenta como por decidir lo que esta sesión puede decidir; sin el dato, la API manda', () => {
    const lista = [
      solicitud({ id: 'si', puedesDecidir: true }),
      solicitud({ id: 'no', puedesDecidir: false }),
      solicitud({ id: 'sin-dato' }),
      solicitud({ id: 'hecha', estado: 'Aprobada', puedesDecidir: true }),
    ];

    expect(porDecidirPorMi(lista).map((s) => s.id)).toEqual(['si', 'sin-dato']);
    expect(porDecidir(lista).map((s) => s.id)).toEqual(['si', 'no', 'sin-dato']);
    expect(puedoDecidir(solicitud({ puedesDecidir: false }))).toBe(false);
    expect(puedoDecidir(solicitud({}))).toBe(true);
  });
});
