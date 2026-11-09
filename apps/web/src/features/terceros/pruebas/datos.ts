import type { CertificadoDelVinculoDto, CertificadoDto, CuentaDesignadaDelVinculoDto, DocumentoVistaEmpresaDto } from '@validasoft/api-client';

/** Datos de prueba de la ficha del tercero: un vínculo con certificado vigente, cuenta designada y un documento. */

const ACTUAL = {
  id: 'c1',
  folio: 'VS-2026-000001',
  folioRaiz: 'VS-2026-000001',
  emision: 1,
  esLaActual: true,
  estado: 'Vigente',
  bloqueante: false,
  vinculoId: 'v1',
  expedienteId: 'x1',
  expedienteFolio: 'VS-E-2026-000001',
  emitidoEn: '2026-10-01T15:00:00Z',
  vigenteHasta: '2026-11-30',
  pendientes: [],
  suspension: null,
  revocacion: null,
  pdf: null,
} as unknown as CertificadoDto;

export const CERTIFICADO: CertificadoDelVinculoDto = { vinculoId: 'v1', actual: ACTUAL, emisiones: [] };

export const CUENTA: CuentaDesignadaDelVinculoDto = {
  actual: {
    cuentaId: 'cta1',
    banco: 'Bancolombia',
    tipoCuenta: 'Ahorros',
    numeroEnmascarado: '****6789',
    numero: null,
    titular: 'Ferretería El Tornillo S.A.S.',
    identificacionTitular: '900123456',
    moneda: 'COP',
    estado: 'Aprobada',
    propuestaEn: '2026-09-01T15:00:00Z',
    aprobaciones: [],
    aprobadaEn: '2026-09-02T15:00:00Z',
    motivoRechazo: null,
  },
  propuesta: null,
  rechazada: null,
  aprobadoresRequeridos: 2,
  puedesAprobar: false,
};

export const DOCUMENTO: DocumentoVistaEmpresaDto = {
  documentoId: 'd1',
  tipo: { codigo: 'RUT', nombre: 'RUT', tipoDocumentoTenantId: null },
  ambito: 'Global',
  versionActual: 2,
  versionesCompartidas: [
    {
      documentoId: 'd1',
      n: 2,
      estado: 'Actual',
      cargadaEn: '2026-09-10T15:00:00Z',
      cargadaPor: 'tercero',
      clase: 'Archivo',
      nombreOriginal: 'rut.pdf',
      mime: 'application/pdf',
      tamanoBytes: 1024,
      paginas: 1,
      sha256: 'abcdef0123456789',
      motivoDevolucion: null,
      detalleDevolucion: null,
      hechos: null,
      vigencia: { vigenteHasta: '2027-09-10', base: 'FechaCarga', estado: 'Vigente', diasAviso: [30], diasGracia: 0, finDeGracia: null, vencidaEn: null },
    },
  ],
};
