import { ErrorApi, SCOPES, type ExpedienteDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { area, areaDeRequisito, expediente, requisito, veredicto } from './datosPrueba';
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

describe('«Aprobar área»: qué documentos y en qué versión', () => {
  it('arma `versiones` con exactamente los documentos por decidir y su versión vigente', () => {
    const exp = conDocumentos('EnRevision');
    const porDecidir = documentosPorDecidir(exp, exp.etapas[0]!.areas[0]!, 'u-1');
    expect(porDecidir).toEqual([
      { requisitoId: 'r-rut', versionVigente: 1 },
      { requisitoId: 'r-camara', versionVigente: 2 },
    ]);
    expect(versionesParaAprobar(porDecidir)).toEqual({ 'r-rut': 1, 'r-camara': 2 });
  });

  it('usa `documentosPorDecidir` de la API cuando viene', () => {
    const delServidor = { ...area(), documentosPorDecidir: [{ requisitoId: 'r-camara', versionVigente: 2 }] };
    expect(documentosPorDecidir(conDocumentos('EnRevision', [rut, camara], [delServidor]), delServidor, 'u-1')).toEqual([{ requisitoId: 'r-camara', versionVigente: 2 }]);
  });

  it('sin la API, deja fuera lo ya decidido, lo que la persona votó en esta versión, lo eximido y lo que no tiene versión', () => {
    const decidido = requisito({ id: 'r-decidido', areas: [areaDeRequisito({ estado: 'Aprobado' })] });
    const yaVote = requisito({ id: 'r-vote', areas: [areaDeRequisito({ aprobadoresRequeridos: 2, veredictos: [veredicto({ usuario: 'u-1', sobreVersion: 1 })] })] });
    const eximido = requisito({ id: 'r-eximido', estado: 'Eximido' });
    const sinVersion = requisito({ id: 'r-sin', versionActual: null });
    const exp = conDocumentos('EnRevision', [rut, decidido, yaVote, eximido, sinVersion]);
    expect(documentosPorDecidir(exp, exp.etapas[0]!.areas[0]!, 'u-1').map((d) => d.requisitoId)).toEqual(['r-rut']);
  });

  it('la aprobación vista deja de aplicar si cambió alguna versión (todo o nada)', () => {
    const exp = conDocumentos('EnRevision');
    const a = exp.etapas[0]!.areas[0]!;
    const dialogo = { tipo: 'aprobarArea' as const, area: a, documentos: documentosPorDecidir(exp, a, 'u-1') };
    expect(dialogoAplica(dialogo, exp, () => miembro())).toBe(true);
    const nueva = conDocumentos('EnRevision', [rut, { ...camara, versionActual: version(3) }]);
    expect(dialogoAplica(dialogo, nueva, () => miembro())).toBe(false);
  });

  it('explica por qué no se puede decidir: devuelto, no miembro o nada pendiente', () => {
    const exp = conDocumentos('EnRevision');
    const a = exp.etapas[0]!.areas[0]!;
    expect(bloqueoDecisionArea(exp, a, miembro())).toBeNull();
    expect(bloqueoDecisionArea(conDocumentos('Observado'), a, miembro())).toBe('expediente-observado');
    expect(bloqueoDecisionArea(exp, a, miembro({ esMiembro: false }))).toBe('no-es-miembro');
    const todoDecidido = conDocumentos('EnRevision', [requisito({ areas: [areaDeRequisito({ estado: 'Aprobado' })] })]);
    expect(bloqueoDecisionArea(todoDecidido, a, miembro())).toBe('nada-por-decidir');
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
  it('version-superada dice qué documentos cambiaron (ids u objetos, en `requisitos` o en `datos.requisitos`)', () => {
    expect(falloDeDecision(new ErrorApi(409, 'version-superada', '', null, null, {}, { requisitos: ['r-camara'] }))).toEqual({ tipo: 'version-superada', requisitos: ['r-camara'] });
    expect(
      falloDeDecision(new ErrorApi(409, 'version-superada', '', null, null, {}, { datos: { requisitos: [{ requisitoId: 'r-rut', versionVigente: 2 }] } })),
    ).toEqual({ tipo: 'version-superada', requisitos: ['r-rut'] });
    expect(falloDeDecision(new ErrorApi(409, 'version-superada', '', null, null))).toEqual({ tipo: 'version-superada', requisitos: [] });
  });

  it('reconoce expediente-observado y area-ya-se-pronuncio; lo demás va al mensaje general', () => {
    expect(falloDeDecision(new ErrorApi(409, 'expediente-observado', '', null, null))).toEqual({ tipo: 'expediente-observado' });
    expect(falloDeDecision(new ErrorApi(409, 'area-ya-se-pronuncio', '', null, null))).toEqual({ tipo: 'area-ya-se-pronuncio' });
    expect(falloDeDecision(new ErrorApi(422, 'versiones-requeridas', '', null, null))).toEqual({ tipo: 'otro' });
  });
});
