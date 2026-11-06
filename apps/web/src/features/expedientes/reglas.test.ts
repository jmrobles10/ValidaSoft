import { SCOPES, type AreaExpedienteDto, type ExpedienteDto, type MotivoTipificadoDto, type VeredictoEmitidoDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import {
  accionesDeExpediente,
  areasFrenadas,
  areasQueVuelvenARevisar,
  bloqueoVeredicto,
  dialogoAplica,
  motivosPara,
  puedeAbrirExpediente,
  puedeAnularVeredicto,
  puedeAprobarComoAdministrador,
  puedeRechazar,
  type ContextoRevisor,
} from './reglas';
import { identificacionResumida } from '../vinculos/tercero';

const REVISOR = new Set<string>([SCOPES.expedientesLeer, SCOPES.expedientesRevisar]);
const ADMIN = new Set<string>([SCOPES.expedientesLeer, SCOPES.expedientesRevisar, SCOPES.expedientesAdministrar]);
const AUDITOR = new Set<string>([SCOPES.expedientesLeer]);

function veredicto(cambios: Partial<VeredictoEmitidoDto> = {}): VeredictoEmitidoDto {
  return {
    id: 'v1',
    veredicto: 'Aprobado',
    origen: 'Humano',
    usuario: 'u-1',
    fecha: '2026-11-06T15:00:00Z',
    motivos: [],
    observacion: null,
    justificacion: null,
    vigente: true,
    superado: false,
    anulado: false,
    anuladoPor: null,
    ...cambios,
  };
}

function area(cambios: Partial<AreaExpedienteDto> = {}): AreaExpedienteDto {
  return {
    areaId: 'a-juridica',
    codigo: 'JURIDICA',
    nombre: 'Jurídica',
    estado: 'Pendiente',
    aprobadoresRequeridos: 1,
    aprobaciones: 0,
    aprobacionesFaltantes: 1,
    miembrosPendientes: ['u-1'],
    puedeRechazarExpediente: false,
    automatica: false,
    pendienteDesde: '2026-11-06T14:00:00Z',
    slaVence: null,
    veredictos: [],
    ...cambios,
  };
}

function expediente(estado: ExpedienteDto['estado'], areas: AreaExpedienteDto[] = [area()]): ExpedienteDto {
  return {
    id: 'e1',
    folio: 'NORTE-E-2026-000001',
    empresaId: 'emp',
    vinculoId: 'vin',
    tercero: { id: 't', nombre: 'Aceros del Norte S.A.S.', identificacion: 'NIT 900123456-0' },
    tipoVinculo: { id: 'tp', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    tipo: 'Inicial',
    estado,
    flujo: null,
    politicaDevolucion: 'Inmediata',
    separacionDeFunciones: false,
    etapaActual: 1,
    etapas: [{ orden: 1, estado: 'Activa', activadaEn: null, aprobadaEn: null, areas }],
    reenvios: 0,
    ultimaRespuesta: null,
    rechazo: null,
    motivoAnulacion: null,
    abiertoEn: '2026-11-06T14:00:00Z',
    cerradoEn: null,
    actualizadoEn: '2026-11-06T14:00:00Z',
    version: 3,
  };
}

const miembro = (scopes: ReadonlySet<string> = REVISOR, extra: Partial<ContextoRevisor> = {}): ContextoRevisor => ({ scopes, usuarioId: 'u-1', esMiembro: true, ...extra });

describe('bloqueoVeredicto', () => {
  it('permite decidir a un miembro con el área pendiente y el expediente en revisión', () => {
    expect(bloqueoVeredicto(expediente('EnRevision'), area(), miembro())).toBeNull();
  });

  it('frena a todos mientras el expediente está devuelto al tercero (409 expediente-observado)', () => {
    expect(bloqueoVeredicto(expediente('Observado'), area(), miembro())).toBe('expediente-observado');
  });

  it('explica cada caso: sin permiso, no miembro, ya votó, área en espera, automática, cerrada y vínculo bloqueado', () => {
    const exp = expediente('EnRevision');
    expect(bloqueoVeredicto(exp, area(), miembro(AUDITOR))).toBe('sin-permiso');
    expect(bloqueoVeredicto(exp, area(), miembro(REVISOR, { esMiembro: false }))).toBe('no-es-miembro');
    expect(bloqueoVeredicto(exp, area({ aprobadoresRequeridos: 2, veredictos: [veredicto()] }), miembro())).toBe('ya-voto');
    expect(bloqueoVeredicto(exp, area({ estado: 'Esperando' }), miembro())).toBe('area-esperando');
    expect(bloqueoVeredicto(exp, area({ automatica: true }), miembro())).toBe('area-automatica');
    expect(bloqueoVeredicto(exp, area({ estado: 'Aprobada' }), miembro())).toBe('area-cerrada');
    expect(bloqueoVeredicto(exp, area(), miembro(REVISOR, { vinculoBloqueado: true }))).toBe('vinculo-bloqueado');
    expect(bloqueoVeredicto(expediente('Aprobado'), area(), miembro())).toBe('expediente-cerrado');
  });

  it('un veredicto superado por el reenvío no cuenta como voto (el área reinicia completa)', () => {
    const superado = veredicto({ veredicto: 'Observado', vigente: false, superado: true });
    expect(bloqueoVeredicto(expediente('EnRevision'), area({ veredictos: [superado] }), miembro())).toBeNull();
  });

  it('sin la lista de miembros (null) deja decidir y la API valida', () => {
    expect(bloqueoVeredicto(expediente('EnRevision'), area(), miembro(REVISOR, { esMiembro: null }))).toBeNull();
  });
});

describe('rechazo y acciones de administrador', () => {
  const conRechazo = area({ puedeRechazarExpediente: true });

  it('rechaza solo el área con permiso, también con el expediente devuelto (FL-14)', () => {
    expect(puedeRechazar(expediente('EnRevision', [conRechazo]), conRechazo, miembro())).toBe(true);
    expect(puedeRechazar(expediente('Observado', [conRechazo]), conRechazo, miembro())).toBe(true);
    expect(puedeRechazar(expediente('EnRevision'), area(), miembro())).toBe(false);
    expect(puedeRechazar(expediente('Aprobado', [conRechazo]), conRechazo, miembro())).toBe(false);
    expect(puedeRechazar(expediente('EnRevision', [conRechazo]), conRechazo, miembro(REVISOR, { esMiembro: false }))).toBe(false);
  });

  it('aprueba en nombre del área solo el administrador, con el área pendiente y sin devolución', () => {
    expect(puedeAprobarComoAdministrador(expediente('EnRevision'), area(), miembro(ADMIN))).toBe(true);
    expect(puedeAprobarComoAdministrador(expediente('EnRevision'), area(), miembro(REVISOR))).toBe(false);
    expect(puedeAprobarComoAdministrador(expediente('Observado'), area(), miembro(ADMIN))).toBe(false);
    expect(puedeAprobarComoAdministrador(expediente('EnRevision'), area({ estado: 'Observada' }), miembro(ADMIN))).toBe(false);
  });

  it('anula un veredicto vigente en revisión o devuelto, nunca en un expediente cerrado', () => {
    const observada = area({ estado: 'Observada', veredictos: [veredicto({ veredicto: 'Observado' })] });
    expect(puedeAnularVeredicto(expediente('Observado', [observada]), observada, ADMIN)).toBe(true);
    expect(puedeAnularVeredicto(expediente('Rechazado', [observada]), observada, ADMIN)).toBe(false);
    expect(puedeAnularVeredicto(expediente('EnRevision'), area(), ADMIN)).toBe(false);
    expect(puedeAnularVeredicto(expediente('Observado', [observada]), observada, REVISOR)).toBe(false);
  });

  it('reenvía solo un expediente devuelto y anula cualquiera abierto', () => {
    expect(accionesDeExpediente(expediente('Observado'), ADMIN)).toEqual(['reenviar', 'anular']);
    expect(accionesDeExpediente(expediente('EnRevision'), ADMIN)).toEqual(['anular']);
    expect(accionesDeExpediente(expediente('Aprobado'), ADMIN)).toEqual([]);
    expect(accionesDeExpediente(expediente('Observado'), REVISOR)).toEqual([]);
    expect(accionesDeExpediente(expediente('Observado'), ADMIN, true)).toEqual(['anular']);
  });

  it('abre a mano solo el administrador, sin expediente abierto y con el vínculo Invitado', () => {
    expect(puedeAbrirExpediente({ estado: 'Invitado', expedienteAbiertoId: null }, ADMIN)).toBe(true);
    expect(puedeAbrirExpediente({ estado: 'EnValidacion', expedienteAbiertoId: 'e1' }, ADMIN)).toBe(false);
    expect(puedeAbrirExpediente({ estado: 'Bloqueado' }, ADMIN)).toBe(false);
    expect(puedeAbrirExpediente({ estado: 'Invitado' }, REVISOR)).toBe(false);
  });
});

describe('áreas en paralelo', () => {
  it('con una observación, la otra área queda frenada y solo la que observó vuelve a revisar', () => {
    const compras = area({ areaId: 'a-compras', nombre: 'Compras', estado: 'Observada', veredictos: [veredicto({ veredicto: 'Observado' })] });
    const juridica = area();
    const exp = expediente('Observado', [compras, juridica]);
    expect(areasQueVuelvenARevisar(exp).map((a) => a.nombre)).toEqual(['Compras']);
    expect(areasFrenadas(exp).map((a) => a.nombre)).toEqual(['Jurídica']);
    expect(areasFrenadas(expediente('EnRevision', [compras, juridica]))).toEqual([]);
  });

  it('un diálogo abierto deja de aplicar si el expediente cambió al recargar', () => {
    const contexto = () => miembro(ADMIN);
    expect(dialogoAplica({ tipo: 'veredicto', area: area() }, expediente('EnRevision'), contexto)).toBe(true);
    expect(dialogoAplica({ tipo: 'veredicto', area: area() }, expediente('Observado'), contexto)).toBe(false);
    expect(dialogoAplica({ tipo: 'reenviar' }, expediente('Observado'), contexto)).toBe(true);
    expect(dialogoAplica({ tipo: 'reenviar' }, expediente('EnRevision'), contexto)).toBe(false);
  });
});

describe('motivosPara', () => {
  const motivo = (codigo: string, aplicaA: MotivoTipificadoDto['aplicaA'], cambios: Partial<MotivoTipificadoDto> = {}): MotivoTipificadoDto => ({
    id: codigo,
    codigo,
    nombre: codigo,
    categoria: 'Documento',
    aplicaA,
    textoAlTercero: `Texto de ${codigo}`,
    base: true,
    activo: true,
    ...cambios,
  });

  it('filtra por decisión y estado, con los de la plataforma primero', () => {
    const lista = [
      motivo('propio', 'Ambos', { base: false }),
      motivo('ilegible', 'Observacion'),
      motivo('fraude', 'Rechazo'),
      motivo('inactivo', 'Ambos', { activo: false }),
    ];
    expect(motivosPara(lista, 'Observacion').map((m) => m.codigo)).toEqual(['ilegible', 'propio']);
    expect(motivosPara(lista, 'Rechazo').map((m) => m.codigo)).toEqual(['fraude', 'propio']);
  });
});

describe('identificacionResumida', () => {
  it('convierte la clave PAIS|TIPO|NUMERO del resumen en "NIT 900123456" y deja igual lo ya legible', () => {
    expect(identificacionResumida('CO|NIT|900123456')).toBe('NIT 900123456');
    expect(identificacionResumida('NIT 900123456-8')).toBe('NIT 900123456-8');
  });
});
