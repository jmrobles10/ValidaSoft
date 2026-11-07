import { ErrorApi, type InvitacionPublicaDto, type TextoLegalPublicoDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { claveTexto, consentimientosDe, estadoInvitacion, falloDeAceptacion, marcadosVigentes, puedeAceptar, textosOrdenados } from './textos';

const texto = (tipo: TextoLegalPublicoDto['tipo'], textoVersion = 1): TextoLegalPublicoDto => ({
  tipo,
  textoVersion,
  hashTexto: `h-${tipo}-${String(textoVersion)}`,
  contenido: `Texto de ${tipo}`,
  publicadoEn: '2026-11-06T12:00:00Z',
});

function invitacion(cambios: Partial<InvitacionPublicaDto> = {}, aceptada = false): InvitacionPublicaDto {
  return {
    vinculo: {
      id: 'v1',
      empresa: { id: 'e1', nombre: 'Constructora Norte S.A.S.', logoUrl: null },
      tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
      estado: 'Invitado',
      invitacionAceptada: aceptada,
      invitacionAceptadaEn: aceptada ? '2026-11-06T12:30:00Z' : null,
      expedienteId: null,
    },
    textosPorAceptar: [texto('TerminosEmpresa'), texto('AvisoPlataforma'), texto('AutorizacionEmpresa')],
    textosFaltantes: [],
    puedeAceptar: true,
    ...cambios,
  };
}

describe('qué textos hay que aceptar', () => {
  it('ordena aviso de la plataforma, autorización y términos', () => {
    expect(textosOrdenados(invitacion()).map((t) => t.tipo)).toEqual(['AvisoPlataforma', 'AutorizacionEmpresa', 'TerminosEmpresa']);
  });

  it('distingue aceptada, sin textos de la empresa y por aceptar', () => {
    expect(estadoInvitacion(invitacion({}, true))).toBe('aceptada');
    expect(estadoInvitacion(invitacion({ puedeAceptar: false, textosFaltantes: ['TerminosEmpresa'] }))).toBe('faltan-textos');
    expect(estadoInvitacion(invitacion())).toBe('por-aceptar');
  });

  it('un vínculo bloqueado es una validación suspendida, no «faltan textos» (aunque no pueda aceptar)', () => {
    const bloqueado = invitacion({ puedeAceptar: false, textosFaltantes: [] });
    bloqueado.vinculo = { ...bloqueado.vinculo, estado: 'Bloqueado' };
    expect(estadoInvitacion(bloqueado)).toBe('suspendida');
    expect(puedeAceptar(bloqueado, new Set(bloqueado.textosPorAceptar.map(claveTexto)))).toBe(false);
  });

  it('desvinculado o anulado: la invitación ya no está vigente, también si ya había aceptado', () => {
    for (const estado of ['Desvinculado', 'Anulado'] as const) {
      const base = invitacion({ puedeAceptar: false }, true);
      expect(estadoInvitacion({ ...base, vinculo: { ...base.vinculo, estado } })).toBe('terminada');
    }
  });

  it('«aún no publica sus textos» solo cuando faltan textos; si la API no deja aceptar por otra razón, aviso genérico', () => {
    expect(estadoInvitacion(invitacion({ puedeAceptar: false, textosFaltantes: ['AutorizacionEmpresa'] }))).toBe('faltan-textos');
    expect(estadoInvitacion(invitacion({ puedeAceptar: false, textosFaltantes: [] }))).toBe('no-disponible');
  });

  it('exige marcar cada texto vigente; sin el aviso (ya aceptado antes) bastan los de la empresa', () => {
    const todos = new Set(invitacion().textosPorAceptar.map(claveTexto));
    expect(puedeAceptar(invitacion(), new Set(['AvisoPlataforma:1', 'AutorizacionEmpresa:1']))).toBe(false);
    expect(puedeAceptar(invitacion(), todos)).toBe(true);
    const sinAviso = invitacion({ textosPorAceptar: [texto('AutorizacionEmpresa'), texto('TerminosEmpresa')] });
    expect(puedeAceptar(sinAviso, new Set(['AutorizacionEmpresa:1', 'TerminosEmpresa:1']))).toBe(true);
  });

  it('envía cada texto con la versión exacta que se mostró', () => {
    const marcados = new Set(invitacion().textosPorAceptar.map(claveTexto));
    expect(consentimientosDe(invitacion(), marcados)).toEqual([
      { tipo: 'AvisoPlataforma', textoVersion: 1, aceptado: true },
      { tipo: 'AutorizacionEmpresa', textoVersion: 1, aceptado: true },
      { tipo: 'TerminosEmpresa', textoVersion: 1, aceptado: true },
    ]);
  });

  it('tras texto-desactualizado, el texto que cambió de versión vuelve a quedar sin marcar', () => {
    const marcados = new Set(['AvisoPlataforma:1', 'AutorizacionEmpresa:1', 'TerminosEmpresa:1']);
    const recargada = invitacion({ textosPorAceptar: [texto('AvisoPlataforma'), texto('AutorizacionEmpresa', 2), texto('TerminosEmpresa')] });
    expect([...marcadosVigentes(recargada, marcados)].sort()).toEqual(['AvisoPlataforma:1', 'TerminosEmpresa:1']);
    expect(puedeAceptar(recargada, marcadosVigentes(recargada, marcados))).toBe(false);
  });

  it('reconoce los errores con tratamiento propio', () => {
    const e = (codigo: string) => new ErrorApi(409, codigo, '', null, null);
    expect(falloDeAceptacion(e('texto-desactualizado'))).toBe('texto-desactualizado');
    expect(falloDeAceptacion(e('textos-legales-faltantes'))).toBe('textos-legales-faltantes');
    expect(falloDeAceptacion(new ErrorApi(403, 'otp-requerido', '', null, null))).toBe('otp-requerido');
    expect(falloDeAceptacion(e('invitacion-ya-aceptada'))).toBe('ya-aceptada');
    expect(falloDeAceptacion(e('consentimiento-faltante'))).toBe('otro');
  });
});
