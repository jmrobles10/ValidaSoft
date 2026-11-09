import type {
  AreaDeRequisitoDto,
  AreaExpedienteDto,
  DatoDeclaradoDto,
  ExpedienteDto,
  RequisitoExpedienteDto,
  VeredictoEmitidoDto,
  VigenciaDto,
} from '@validasoft/api-client';

/** Datos de prueba de expedientes (solo para las pruebas). */
export function veredicto(cambios: Partial<VeredictoEmitidoDto> = {}): VeredictoEmitidoDto {
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

export function area(cambios: Partial<AreaExpedienteDto> = {}): AreaExpedienteDto {
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

export function expediente(estado: ExpedienteDto['estado'], areas: AreaExpedienteDto[] = [area()]): ExpedienteDto {
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

export function areaDeRequisito(cambios: Partial<AreaDeRequisitoDto> = {}): AreaDeRequisitoDto {
  return { areaId: 'a-juridica', codigo: 'JURIDICA', estado: 'Pendiente', aprobadoresRequeridos: 1, aprobaciones: 0, veredictos: [], ...cambios };
}

export function requisito(cambios: Partial<RequisitoExpedienteDto> = {}): RequisitoExpedienteDto {
  return {
    id: 'r-rut',
    tipoDocumento: { id: 'td-rut', codigo: 'RUT', nombre: 'Registro Único Tributario', tipoCanonico: 'RUT', ambito: 'Global' },
    obligatorio: true,
    estado: 'EnRevision',
    versionActual: { documentoId: 'doc-rut', n: 1, sha256: 'abc', cargadaEn: '2026-11-06T14:00:00Z' },
    reenvios: 0,
    exencion: null,
    areas: [areaDeRequisito()],
    ...cambios,
  };
}

/** Vigencia de una versión (TD-6): por defecto vigente hasta el 30/11/2026, calculada desde la expedición declarada. */
export function vigencia(cambios: Partial<VigenciaDto> = {}): VigenciaDto {
  return {
    vigenteHasta: '2026-11-30',
    base: 'FechaExpedicion',
    estado: 'Vigente',
    diasAviso: [15],
    diasGracia: 0,
    finDeGracia: null,
    vencidaEn: null,
    ...cambios,
  };
}

/** Datos que declaró el tercero, uno de cada tipo (valores normalizados como los entrega la API). */
export const DATOS_DECLARADOS: DatoDeclaradoDto[] = [
  { clave: 'fecha_expedicion', etiqueta: 'Fecha de expedición', tipo: 'Fecha', valor: '2026-11-01' },
  { clave: 'activos', etiqueta: 'Activos totales', tipo: 'Numero', valor: '1234567.5' },
  { clave: 'gran_contribuyente', etiqueta: '¿Es gran contribuyente?', tipo: 'SiNo', valor: 'true' },
  { clave: 'regimen', etiqueta: 'Régimen', tipo: 'Lista', valor: 'Común' },
];

/** Requisito cubierto por una versión de solo formulario (sin archivo: su descarga responde 409 `version-sin-archivo`). */
export function requisitoFormulario(cambios: Partial<RequisitoExpedienteDto> = {}): RequisitoExpedienteDto {
  return requisito({
    id: 'r-sarlaft',
    tipoDocumento: { id: 'td-sarlaft', codigo: 'SARLAFT', nombre: 'Formulario SARLAFT', tipoCanonico: null, ambito: 'PorVinculo', modo: 'Formulario', campos: [], archivo: null, tienePlantilla: false },
    versionActual: { documentoId: 'doc-sarlaft', n: 2, sha256: 'def', cargadaEn: '2026-11-07T14:00:00Z', datosDeclarados: DATOS_DECLARADOS, vigencia: vigencia(), estado: 'Actual', clase: 'Formulario' },
    ...cambios,
  });
}
