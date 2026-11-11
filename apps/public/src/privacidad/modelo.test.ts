import type { ExportacionBovedaDto, SolicitudHabeasDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import {
  BORRADOR_VACIO, cuerpoDeSolicitud, DESTINO_PLATAFORMA, diasHabilesDe, enCurso, estadoDePlazo, hayIgualEnTramite, intervaloDeRefresco, ordenarSolicitudes, tamanoLegible,
  validarSolicitud,
} from './modelo';

function solicitud(parcial: Partial<SolicitudHabeasDto>): SolicitudHabeasDto {
  return {
    id: 'sol-1',
    radicado: 'HD-2026-AAAA1111',
    tipo: 'Consulta',
    alcance: null,
    estado: 'Recibida',
    resultado: null,
    destino: { empresaId: null, nombre: 'Validasoft' },
    recibidaEn: '2026-11-11T15:00:00Z',
    venceEl: '2026-11-26',
    diasHabiles: 10,
    diasHabilesRestantes: 10,
    plazo: 'EnPlazo',
    dentroDelPlazo: null,
    resueltaEn: null,
    detalle: null,
    respuesta: null,
    contenidoSuprimido: false,
    constanciaHash: null,
    titular: null,
    ...parcial,
  };
}

describe('plazos de la ley', () => {
  it('la consulta se responde en 10 días hábiles y los reclamos en 15', () => {
    expect(diasHabilesDe('Consulta')).toBe(10);
    expect(diasHabilesDe('Correccion')).toBe(15);
    expect(diasHabilesDe('Supresion')).toBe(15);
  });

  it('el estado del plazo viene del servidor; lo desconocido se trata como en plazo', () => {
    expect(estadoDePlazo({ plazo: 'PorVencer' })).toBe('PorVencer');
    expect(estadoDePlazo({ plazo: 'Vencida' })).toBe('Vencida');
    expect(estadoDePlazo({ plazo: 'Resuelta' })).toBe('Resuelta');
    expect(estadoDePlazo({ plazo: 'otra cosa' })).toBe('EnPlazo');
  });
});

describe('validar la solicitud', () => {
  it('exige elegir qué hacer', () => {
    expect(validarSolicitud(BORRADOR_VACIO)).toEqual(['tipo-requerido']);
  });

  it('la corrección exige contar qué dato corregir (mínimo 10 caracteres)', () => {
    expect(validarSolicitud({ ...BORRADOR_VACIO, tipo: 'Correccion', detalle: 'corto' })).toEqual(['detalle-requerido']);
    expect(validarSolicitud({ ...BORRADOR_VACIO, tipo: 'Correccion', detalle: 'Mi dirección está mal: es la calle 45' })).toEqual([]);
    expect(validarSolicitud({ ...BORRADOR_VACIO, tipo: 'Consulta', detalle: '' })).toEqual([]);
  });

  it('el detalle no pasa de 2.000 caracteres', () => {
    expect(validarSolicitud({ ...BORRADOR_VACIO, tipo: 'Consulta', detalle: 'x'.repeat(2001) })).toEqual(['detalle-largo']);
  });
});

describe('el cuerpo que se envía', () => {
  it('sin empresa va a la plataforma y el alcance solo viaja en la supresión', () => {
    expect(cuerpoDeSolicitud({ tipo: 'Supresion', destino: DESTINO_PLATAFORMA, alcance: 'Documentos', detalle: '  Borren mis documentos  ' })).toEqual({
      tipo: 'Supresion',
      alcance: 'Documentos',
      empresaId: null,
      detalle: 'Borren mis documentos',
    });
    expect(cuerpoDeSolicitud({ tipo: 'Consulta', destino: 'empresa-9', alcance: 'Todo', detalle: '' })).toEqual({ tipo: 'Consulta', alcance: null, empresaId: 'empresa-9', detalle: null });
  });
});

describe('la lista de solicitudes', () => {
  it('las abiertas van primero, la que vence antes arriba, y luego las resueltas, la más reciente primero', () => {
    const lista = [
      solicitud({ id: 'resuelta-vieja', estado: 'Resuelta', recibidaEn: '2026-10-01T00:00:00Z' }),
      solicitud({ id: 'abierta-tarde', venceEl: '2026-12-03' }),
      solicitud({ id: 'resuelta-nueva', estado: 'Resuelta', recibidaEn: '2026-11-05T00:00:00Z' }),
      solicitud({ id: 'abierta-pronto', venceEl: '2026-11-20' }),
    ];
    expect(ordenarSolicitudes(lista).map((s) => s.id)).toEqual(['abierta-pronto', 'abierta-tarde', 'resuelta-nueva', 'resuelta-vieja']);
  });

  it('avisa antes de enviar si ya hay una igual en trámite para ese derecho y destino', () => {
    const lista = [solicitud({ id: 'a', tipo: 'Consulta' }), solicitud({ id: 'b', tipo: 'Supresion', destino: { empresaId: 'e1', nombre: 'Norte' } }), solicitud({ id: 'c', tipo: 'Correccion', estado: 'Resuelta' })];
    expect(hayIgualEnTramite(lista, 'Consulta', DESTINO_PLATAFORMA)?.id).toBe('a');
    expect(hayIgualEnTramite(lista, 'Supresion', 'e1')?.id).toBe('b');
    expect(hayIgualEnTramite(lista, 'Supresion', DESTINO_PLATAFORMA)).toBeNull(); // otro destino
    expect(hayIgualEnTramite(lista, 'Correccion', DESTINO_PLATAFORMA)).toBeNull(); // ya resuelta
    expect(hayIgualEnTramite(lista, null, DESTINO_PLATAFORMA)).toBeNull();
  });
});

describe('exportaciones', () => {
  const exportacion = (estado: string): ExportacionBovedaDto => ({
    id: 'e', estado, solicitadaEn: '2026-11-11T00:00:00Z', terminadaEn: null, expiraEn: null, archivos: 0, bytes: null, sha256: null, hashManifiesto: null, kid: null, codigoDeError: null, descargas: 0,
  });

  it('se refresca cada 3 segundos solo mientras una se prepara', () => {
    expect(enCurso(exportacion('Pendiente'))).toBe(true);
    expect(enCurso(exportacion('Generando'))).toBe(true);
    expect(enCurso(exportacion('Lista'))).toBe(false);
    expect(intervaloDeRefresco([exportacion('Lista'), exportacion('Generando')])).toBe(3000);
    expect(intervaloDeRefresco([exportacion('Lista'), exportacion('Vencida')])).toBe(false);
    expect(intervaloDeRefresco(undefined)).toBe(false);
  });

  it('el tamaño se escribe legible en es-CO', () => {
    expect(tamanoLegible(512)).toBe('512 B');
    expect(tamanoLegible(2048)).toBe('2 KB');
    expect(tamanoLegible(4.5 * 1024 * 1024)).toBe('4,5 MB');
  });
});
