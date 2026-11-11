import type {
  AdjuntoDeCumplimientoDto,
  CoincidenciaDto,
  FilaTerceroSagrilafDto,
  InformeSagrilaftDto,
  MatrizDeRiesgoDto,
  ResultadoDeMatrizDto,
  VerificacionDto,
} from '@validasoft/api-client';

/** Datos de prueba de las verificaciones (H7). */
export function coincidencia(parcial: Partial<CoincidenciaDto> = {}): CoincidenciaDto {
  return {
    id: 'c1',
    persona: 'Ana Coincidencia',
    rol: 'RepresentanteLegal',
    identificacion: 'CC 1020304050',
    lista: 'OFAC (SDN)',
    nombreEncontrado: 'ANA COINCIDENCIA',
    detalle: null,
    puntaje: 100,
    decision: null,
    justificacion: null,
    resueltaPor: null,
    resueltaEn: null,
    vigenteHasta: null,
    heredada: false,
    ...parcial,
  };
}

export function listas(parcial: Partial<VerificacionDto> = {}): VerificacionDto {
  return {
    tipo: 'ListasRestrictivas',
    estado: 'CoincidenciaPorAnalizar',
    bloqueante: true,
    requeridaPorElFlujo: true,
    proveedor: 'Simulado',
    resultado: 'Coincidencias',
    referenciaExterna: 'sim-1',
    registradaEn: '2026-11-10T14:00:00Z',
    registradaPor: 'sistema',
    vigenteHasta: '2027-11-10T14:00:00Z',
    evidencia: { nombreArchivo: 'listas.pdf', tipoContenido: 'application/pdf', tamano: 1000, sha256: 'a'.repeat(64), delProveedor: false, sha256Respuesta: null },
    personas: [
      { rol: 'Tercero', nombre: 'Aceros del Norte S.A.S.', identificacion: 'NIT 900123456' },
      { rol: 'RepresentanteLegal', nombre: 'Ana Coincidencia', identificacion: 'CC 1020304050' },
    ],
    coincidencias: [coincidencia()],
    estudio: null,
    anteriores: [],
    ultimoError: null,
    reintentarDesde: null,
    vigenciaDias: null,
    frecuenciaReconsultaMeses: null,
    ...parcial,
  };
}

export function estudio(parcial: Partial<VerificacionDto> = {}): VerificacionDto {
  return {
    ...listas(),
    tipo: 'EstudioDeCumplimiento',
    estado: 'Pendiente',
    proveedor: null,
    resultado: null,
    referenciaExterna: null,
    registradaEn: null,
    registradaPor: null,
    vigenteHasta: null,
    evidencia: null,
    personas: [],
    coincidencias: [],
    ...parcial,
  };
}

// ── Matriz de riesgo, archivos del oficial e informe SAGRILAFT (H7 completo) ───────────────────────────────────────

/** La plantilla que ofrece el servidor a una empresa sin matriz (`MatrizDeRiesgoEmpresa.PorDefecto`), con tres factores para abreviar. */
export function matrizDePrueba(parcial: Partial<MatrizDeRiesgoDto> = {}): MatrizDeRiesgoDto {
  return {
    activa: false,
    esPorDefecto: true,
    version: 0,
    actualizadaEn: null,
    limiteBajo: 34,
    limiteMedio: 67,
    niveles: [
      { nivel: 1, nombre: 'Bajo', debidaDiligencia: 'Simple', mesesRevision: 24 },
      { nivel: 2, nombre: 'Medio', debidaDiligencia: 'Ordinaria', mesesRevision: 12 },
      { nivel: 3, nombre: 'Alto', debidaDiligencia: 'Ampliada', mesesRevision: 6 },
    ],
    factores: [
      {
        codigo: 'jurisdiccion',
        nombre: 'Jurisdicción del tercero',
        origen: 'Automatico',
        peso: 15,
        ayuda: 'País de la identificación del tercero.',
        opciones: [
          { codigo: 'nacional', nombre: 'Colombia', puntaje: 0 },
          { codigo: 'extranjero', nombre: 'Otro país', puntaje: 70 },
        ],
      },
      {
        codigo: 'coincidencias-listas',
        nombre: 'Coincidencias en listas restrictivas',
        origen: 'Automatico',
        peso: 30,
        opciones: [
          { codigo: 'sin-coincidencias', nombre: 'Sin coincidencias', puntaje: 0 },
          { codigo: 'descartadas', nombre: 'Coincidencias descartadas', puntaje: 30 },
          { codigo: 'por-analizar', nombre: 'Coincidencias por analizar', puntaje: 70, nivelMinimo: 2 },
          { codigo: 'confirmadas', nombre: 'Coincidencia confirmada', puntaje: 100, nivelMinimo: 3 },
        ],
      },
      {
        codigo: 'sector',
        nombre: 'Sector o actividad económica',
        origen: 'Manual',
        peso: 55,
        ayuda: 'Lo valora el oficial según la actividad del tercero.',
        opciones: [
          { codigo: 'bajo', nombre: 'Sector de bajo riesgo', puntaje: 0 },
          { codigo: 'medio', nombre: 'Sector de riesgo medio', puntaje: 50 },
          { codigo: 'alto', nombre: 'Sector de alto riesgo', puntaje: 100 },
        ],
      },
    ],
    ...parcial,
  };
}

export function resultadoDeMatriz(parcial: Partial<ResultadoDeMatrizDto> = {}): ResultadoDeMatrizDto {
  return {
    puntaje: 42.5,
    nivel: 2,
    nivelPorPuntaje: 2,
    forzadoPor: null,
    debidaDiligencia: 'Ordinaria',
    mesesRevision: 12,
    versionMatriz: 3,
    desglose: [
      { factor: 'jurisdiccion', factorNombre: 'Jurisdicción del tercero', origen: 'Automatico', peso: 15, opcion: 'nacional', opcionNombre: 'Colombia', puntaje: 0, nivelMinimo: null },
      { factor: 'sector', factorNombre: 'Sector o actividad económica', origen: 'Manual', peso: 55, opcion: 'medio', opcionNombre: 'Sector de riesgo medio', puntaje: 50, nivelMinimo: null },
    ],
    ...parcial,
  };
}

export function adjuntoDePrueba(parcial: Partial<AdjuntoDeCumplimientoDto> = {}): AdjuntoDeCumplimientoDto {
  return {
    id: 'adj-1',
    nombre: 'consulta-infolaft.pdf',
    tipoContenido: 'application/pdf',
    tamano: 204_800,
    sha256: 'b'.repeat(64),
    proposito: 'Estudio',
    subidoPor: 'u-ofi',
    fecha: '2026-11-10T14:00:00Z',
    ...parcial,
  };
}

export function filaSagrilaft(parcial: Partial<FilaTerceroSagrilafDto> = {}): FilaTerceroSagrilafDto {
  return {
    vinculoId: 'v1',
    terceroId: 't1',
    folio: 'EXP-0001',
    tercero: 'Aceros del Norte S.A.S.',
    tipoIdentificacion: 'NIT',
    identificacion: '900123456',
    tipoPersona: 'Juridica',
    tipoVinculo: 'Proveedor',
    pais: 'CO',
    ciudad: 'Medellín',
    correo: null,
    actividad: null,
    estadoVinculo: 'Activo',
    ultimaConsultaListas: '2026-10-01T14:00:00Z',
    proveedorListas: 'Simulado',
    estadoListas: 'Vigente',
    coincidenciasPorAnalizar: 0,
    fechaEstudio: '2026-10-02T14:00:00Z',
    oficial: 'u-ofi',
    nivelRiesgo: 2,
    debidaDiligencia: 'Ordinaria',
    concepto: 'Favorable',
    conflictoDeInteres: false,
    proximaRevision: '2027-10-02T14:00:00Z',
    diasParaRevision: 325,
    estadoDebidaDiligencia: 'Vigente',
    personas: 3,
    pep: 0,
    ...parcial,
  };
}

export function informeSagrilaft(parcial: Partial<InformeSagrilaftDto> = {}): InformeSagrilaftDto {
  return {
    desde: '2025-12-01',
    hasta: '2026-11-11',
    terceros: 3,
    conEstudioVigente: 1,
    estudiosPorVencer: 0,
    estudiosVencidos: 1,
    listasPorVencer: 1,
    listasVencidas: 0,
    sinEstudio: 1,
    riesgo: [
      { nivel: 2, cantidad: 1 },
      { nivel: 3, cantidad: 1 },
    ],
    tendencia: [
      { mes: '2026-09', estudios: 1, consultas: 2 },
      { mes: '2026-10', estudios: 3, consultas: 4 },
      { mes: '2026-11', estudios: 2, consultas: 0 },
    ],
    tiempos: { promedioDiasEnResolver: 3.5, resueltas: 6, abiertas: 2, abiertasMasDe7Dias: 1 },
    filas: [
      filaSagrilaft(),
      filaSagrilaft({
        vinculoId: 'v2',
        terceroId: 't2',
        folio: 'EXP-0002',
        tercero: 'Ferretería Roja S.A.S.',
        nivelRiesgo: 3,
        debidaDiligencia: 'Ampliada',
        estadoDebidaDiligencia: 'Vencida',
        proximaRevision: '2026-11-01T14:00:00Z',
        diasParaRevision: -10,
        estadoListas: 'PorVencer',
        coincidenciasPorAnalizar: 1,
        pep: 1,
      }),
      filaSagrilaft({
        vinculoId: 'v3',
        terceroId: 't3',
        folio: 'EXP-0003',
        tercero: 'Sin Estudio Ltda.',
        nivelRiesgo: null,
        debidaDiligencia: null,
        concepto: null,
        fechaEstudio: null,
        proximaRevision: null,
        diasParaRevision: null,
        estadoDebidaDiligencia: 'SinEstudio',
        estadoListas: 'SinConsulta',
        ultimaConsultaListas: null,
        personas: 0,
      }),
    ],
    ...parcial,
  };
}
