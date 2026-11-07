import type { AreaDeRequisitoDto, AreaExpedienteDto, ExpedienteDto, RequisitoExpedienteDto, VeredictoEmitidoDto } from '@validasoft/api-client';

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
