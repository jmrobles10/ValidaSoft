import { ErrorApi, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { area, expediente, requisito } from './datosPrueba';
import {
  bloqueoDecisionArea,
  documentosPorDecidir,
  falloDeDecision,
  requisitosParaObservar,
  validarObservacion,
  versionesParaAprobar,
  type MarcasDeObservacion,
} from './decisionArea';
import { dialogoAplica, type ContextoRevisor } from './reglas';

const REVISOR = new Set<string>([SCOPES.expedientesLeer, SCOPES.expedientesRevisar]);
const miembro = (extra: Partial<ContextoRevisor> = {}): ContextoRevisor => ({ scopes: REVISOR, usuarioId: 'u-1', esMiembro: true, ...extra });
const version = (n: number) => ({ documentoId: `doc-${String(n)}`, n, sha256: 'x', cargadaEn: '2026-11-06T14:00:00Z' });

const rut = requisito();
const camara = requisito({
  id: 'r-camara',
  tipoDocumento: { id: 'td-c', codigo: 'CAMARA_COMERCIO', nombre: 'Cámara de comercio', tipoCanonico: 'CAMARA_COMERCIO', ambito: 'Global' },
  versionActual: version(2),
});
const conDocumentos = (estado: ExpedienteDto['estado'], requisitos = [rut, camara], areas = [area()]): ExpedienteDto => ({ ...expediente(estado, areas), requisitos });

const conPorDecidir = (documentos = [{ requisitoId: 'r-rut', versionVigente: 1 }, { requisitoId: 'r-camara', versionVigente: 2 }]) => ({
  ...area(),
  documentosPorDecidir: documentos,
});

describe('«Aprobar área»: qué documentos y en qué versión', () => {
  it('arma `versiones` con exactamente los documentos por decidir que manda la API y su versión vigente', () => {
    const a = conPorDecidir();
    expect(documentosPorDecidir(a)).toEqual([
      { requisitoId: 'r-rut', versionVigente: 1 },
      { requisitoId: 'r-camara', versionVigente: 2 },
    ]);
    expect(versionesParaAprobar(documentosPorDecidir(a))).toEqual({ 'r-rut': 1, 'r-camara': 2 });
    // Fuera de revisión, etapa inactiva o área automática la API lo manda vacío (o no lo manda).
    expect(documentosPorDecidir({ documentosPorDecidir: null })).toEqual([]);
  });

  it('la aprobación vista deja de aplicar si cambió alguna versión (todo o nada)', () => {
    const a = conPorDecidir();
    const exp = conDocumentos('EnRevision', [rut, camara], [a]);
    const dialogo = { tipo: 'aprobarArea' as const, area: a, documentos: documentosPorDecidir(a) };
    expect(dialogoAplica(dialogo, exp, () => miembro())).toBe(true);
    const nueva = conPorDecidir([{ requisitoId: 'r-rut', versionVigente: 1 }, { requisitoId: 'r-camara', versionVigente: 3 }]);
    expect(dialogoAplica(dialogo, conDocumentos('EnRevision', [rut, { ...camara, versionActual: version(3) }], [nueva]), () => miembro())).toBe(false);
  });

  it('explica por qué no se puede decidir: devuelto, no miembro o nada pendiente', () => {
    const a = conPorDecidir();
    const exp = conDocumentos('EnRevision', [rut, camara], [a]);
    expect(bloqueoDecisionArea(exp, a, miembro())).toBeNull();
    expect(bloqueoDecisionArea(conDocumentos('Observado', [rut, camara], [a]), a, miembro())).toBe('expediente-observado');
    expect(bloqueoDecisionArea(exp, a, miembro({ esMiembro: false }))).toBe('no-es-miembro');
    const nada = conPorDecidir([]);
    expect(bloqueoDecisionArea(conDocumentos('EnRevision', [rut, camara], [nada]), nada, miembro())).toBe('nada-por-decidir');
  });
});

describe('«Observar»: validación y cuerpo', () => {
  it('exige al menos un documento y al menos un motivo por documento', () => {
    expect(validarObservacion(new Map())).toEqual([{ tipo: 'sin-documentos' }]);
    const marcas: MarcasDeObservacion = new Map([
      ['r-rut', { motivos: ['ilegible'], observacion: '' }],
      ['r-camara', { motivos: [], observacion: 'Vencida' }],
    ]);
    expect(validarObservacion(marcas)).toEqual([{ tipo: 'sin-motivos', requisitoId: 'r-camara' }]);
  });

  it('cada documento observado va sobre su versión vigente, con motivos y nota (vacía = null)', () => {
    const marcas: MarcasDeObservacion = new Map([
      ['r-rut', { motivos: ['ilegible'], observacion: '  ' }],
      ['r-camara', { motivos: ['vencido', 'incompleto'], observacion: ' Tiene más de 30 días. ' }],
    ]);
    expect(requisitosParaObservar(marcas, conDocumentos('EnRevision'))).toEqual([
      { requisitoId: 'r-rut', sobreVersion: 1, motivos: ['ilegible'], observacion: null },
      { requisitoId: 'r-camara', sobreVersion: 2, motivos: ['vencido', 'incompleto'], observacion: 'Tiene más de 30 días.' },
    ]);
  });
});

describe('errores de la decisión', () => {
  it('version-superada y versiones-requeridas dicen qué documentos cambiaron (`requisitos` en el primer nivel)', () => {
    const requisitos = [{ requisitoId: 'r-camara', versionVigente: 3 }];
    expect(falloDeDecision(new ErrorApi(409, 'version-superada', '', null, null, {}, { requisitos }))).toEqual({ tipo: 'version-superada', requisitos: ['r-camara'] });
    expect(falloDeDecision(new ErrorApi(422, 'versiones-requeridas', '', null, null, {}, { requisitos }))).toEqual({ tipo: 'version-superada', requisitos: ['r-camara'] });
    expect(
      falloDeDecision(new ErrorApi(409, 'version-superada', '', null, null, {}, { datos: { requisitos: [{ requisitoId: 'r-rut', versionVigente: 2 }] } })),
    ).toEqual({ tipo: 'version-superada', requisitos: ['r-rut'] });
    expect(falloDeDecision(new ErrorApi(409, 'version-superada', '', null, null))).toEqual({ tipo: 'version-superada', requisitos: [] });
  });

  it('reconoce expediente-observado, area-ya-se-pronuncio y area-observada; lo demás va al mensaje general', () => {
    expect(falloDeDecision(new ErrorApi(409, 'expediente-observado', '', null, null))).toEqual({ tipo: 'expediente-observado' });
    expect(falloDeDecision(new ErrorApi(409, 'area-ya-se-pronuncio', '', null, null))).toEqual({ tipo: 'area-ya-se-pronuncio' });
    expect(falloDeDecision(new ErrorApi(409, 'area-observada', '', null, null))).toEqual({ tipo: 'area-observada' });
    expect(falloDeDecision(new ErrorApi(422, 'requisito-duplicado', '', null, null))).toEqual({ tipo: 'otro' });
  });
});
