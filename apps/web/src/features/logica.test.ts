import { ErrorApi, ErrorDeRed, type EventoHistorialDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import { debeReintentar } from '../shared/consultas';
import { mensajeDeError } from '../shared/errores';
import { aFilas, ALCANCE_TENANT, aRoles } from './configuracion/usuarios/roles';
import { elegirEmpresaInicial } from './empresas/useEmpresas';
import { campo, estadoDe, eventoATimeline, tipoNormalizado } from './vinculos/historial';

const t = i18n.t.bind(i18n);

// Forma real de GET /api/v1/vinculos/{id}/historial (datos en PascalCase y enums numéricos, como los guarda Marten).
const historial: EventoHistorialDto[] = [
  {
    version: 1,
    tipo: 'vinculo_creado',
    fecha: '2026-11-06T15:22:55.885452+00:00',
    actorTipo: 'Integracion',
    actorId: 'llave-1',
    sistema: 'DEMO',
    datos: { VinculoId: 'v1', Origen: 3, ReferenciaExterna: { Sistema: 'DEMO', Id: 'PRV-005' }, CorreoInvitacion: 'c@p.co' },
  },
  {
    version: 3,
    tipo: 'vinculo_bloqueado',
    fecha: '2026-11-06T15:22:56.383233+00:00',
    actorTipo: 'Usuario',
    actorId: 'usuario-ana',
    sistema: 'DEMO',
    datos: { EstadoPrevio: 0, Motivo: 'Documentos vencidos.', Hasta: '2026-12-31T23:59:59Z', Fecha: '2026-11-06T15:22:56Z' },
  },
  {
    version: 4,
    tipo: 'vinculo_desbloqueado',
    fecha: '2026-11-06T15:22:56.49863+00:00',
    actorTipo: 'Usuario',
    actorId: 'usuario-ana',
    sistema: 'DEMO',
    datos: { EstadoRestituido: 0, Justificacion: 'Allegó los documentos al día.' },
  },
];

describe('historial del vínculo', () => {
  it('lee campos sin importar mayúsculas y estados numéricos o textuales', () => {
    expect(campo({ Motivo: 'x' }, 'motivo')).toBe('x');
    expect(estadoDe(5)).toBe('Bloqueado');
    expect(estadoDe('activo')).toBe('Activo');
    expect(estadoDe(99)).toBeNull();
    expect(tipoNormalizado('VinculoBloqueado')).toBe('vinculo_bloqueado');
    expect(tipoNormalizado('vinculo_bloqueado')).toBe('vinculo_bloqueado');
  });

  it('arma títulos, actor, antes/después y detalle', () => {
    const [creado, bloqueado, desbloqueado] = historial.map((e) =>
      eventoATimeline(e, { usuarioActual: 'usuario-ana', nombres: new Map([['otro', 'Rubén']]) }, t),
    );

    expect(creado).toMatchObject({ titulo: 'Vínculo creado', detalle: 'Referencia DEMO PRV-005', actor: { tipo: 'integracion', nombre: 'Integración (API key)' } });
    expect(bloqueado).toMatchObject({
      titulo: 'Vínculo bloqueado',
      antes: 'Invitado',
      despues: 'Bloqueado',
      detalle: 'Documentos vencidos. · Hasta el 31/12/2026',
      actor: { nombre: 'Tú', tipo: 'humano' },
      sistema: 'DEMO',
      tono: 'error',
    });
    expect(desbloqueado).toMatchObject({ antes: 'Bloqueado', despues: 'Invitado', detalle: 'Allegó los documentos al día.', tono: 'exito' });
  });

  it('usa el nombre conocido del usuario y tolera tipos nuevos', () => {
    const otro = eventoATimeline({ ...historial[2]!, actorId: 'otro' }, { usuarioActual: null, nombres: new Map([['otro', 'Rubén']]) }, t);
    expect(otro.actor.nombre).toBe('Rubén');
    const nuevo = eventoATimeline({ ...historial[2]!, tipo: 'cuenta_designada_aprobada', datos: {} }, { usuarioActual: null }, t);
    expect(nuevo.titulo).toBe('cuenta designada aprobada');
  });
});

describe('mensajeDeError', () => {
  it('traduce códigos conocidos con siguiente paso', () => {
    const error = new ErrorApi(412, 'version-desactualizada', 'El recurso cambió.', 'tr-1', null);
    expect(mensajeDeError(error, t)).toMatch(/Recargamos los datos/);
  });

  it('usa el detalle de la API para códigos sin texto propio y el traceId en 5xx', () => {
    expect(mensajeDeError(new ErrorApi(422, 'tipo-vinculo-invalido', 'El tipo de vínculo X no existe.', null, null), t)).toBe(
      'El tipo de vínculo X no existe.',
    );
    expect(mensajeDeError(new ErrorApi(500, 'algo-raro', 'boom', 'tr-9', null), t)).toMatch(/tr-9/);
    expect(mensajeDeError(new ErrorDeRed('sin red', null), t)).toMatch(/conexión/);
    expect(mensajeDeError(new Error('x'), t)).toMatch(/inesperado/);
  });
});

describe('roles', () => {
  it('convierte filas a roles sin duplicados y con alcance de tenant', () => {
    const filas = aFilas([
      { rol: 'Administrador', empresaId: null },
      { rol: 'Revisor', empresaId: 'e1' },
    ]);
    expect(filas).toEqual([
      { rol: 'Administrador', alcance: ALCANCE_TENANT },
      { rol: 'Revisor', alcance: 'e1' },
    ]);
    expect(aRoles([...filas, { rol: 'Revisor', alcance: 'e1' }])).toEqual([
      { rol: 'Administrador', empresaId: null },
      { rol: 'Revisor', empresaId: 'e1' },
    ]);
  });
});

describe('empresa inicial', () => {
  const empresas = [{ id: 'e1' }, { id: 'e2' }];
  it('prefiere la sugerida, luego la última y luego la primera', () => {
    expect(elegirEmpresaInicial(empresas, 'e2', 'e1')).toBe('e2');
    expect(elegirEmpresaInicial(empresas, 'otra', 'e2')).toBe('e2');
    expect(elegirEmpresaInicial(empresas, null, undefined)).toBe('e1');
    expect(elegirEmpresaInicial([], 'e1', 'e1')).toBeNull();
  });
});

describe('reintentos de consultas', () => {
  it('no reintenta errores del cliente', () => {
    expect(debeReintentar(0, new ErrorApi(404, 'no-encontrado', '', null, null))).toBe(false);
    expect(debeReintentar(0, new ErrorApi(503, 'error-interno', '', null, null))).toBe(true);
    expect(debeReintentar(2, new ErrorDeRed('x', null))).toBe(false);
  });
});
