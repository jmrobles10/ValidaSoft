/**
 * Contratos de la API v1 que usa un sistema integrador (docs/05 §4), con los mismos nombres que `Validasoft.Contratos`.
 * JSON en camelCase; fechas en ISO 8601 (`YYYY-MM-DD` las de solo día). Los DTO grandes declaran lo que un integrador
 * suele leer y dejan el resto abierto (`[campo: string]: unknown`).
 */

export interface IdentificacionDto {
  pais: string;
  tipo: string;
  numero: string;
  digitoVerificacion?: string | null;
}

export interface ContactoDto {
  nombre: string;
  correo: string;
  telefono?: string | null;
  rol: string;
}

export interface CrearTerceroDto {
  identificacion: IdentificacionDto;
  tipoPersona: 'Juridica' | 'Natural';
  nombre: string;
  nombreComercial?: string | null;
  contactos?: ContactoDto[] | null;
}

export interface ReferenciaExternaDto {
  sistema: string;
  id: string;
}

/** Alta de vínculo: el tercero por `terceroId` o por sus datos (si ya existe se reutiliza). */
export interface CrearVinculoDto {
  terceroId?: string | null;
  tercero?: CrearTerceroDto | null;
  /** Código del tipo de vínculo (`PROVEEDOR`, `CLIENTE`…). */
  tipoVinculo: string;
  atributos?: Record<string, unknown> | null;
  correoInvitacion: string;
  referenciaExterna?: ReferenciaExternaDto | null;
  enviarInvitacion?: boolean;
  vigenciaInvitacionHoras?: number;
  flujoId?: string | null;
}

export type EstadoVinculo = 'Invitado' | 'EnValidacion' | 'Activo' | 'PorVencer' | 'Vencido' | 'Bloqueado' | 'Desvinculado' | 'Anulado';

export interface EnlaceInvitacionDto {
  correoDestino: string;
  expira: string;
  envios: number;
}

export interface VinculoDto {
  id: string;
  empresaId: string;
  terceroId: string;
  tipoVinculo: { id: string; codigo: string; nombre: string };
  flujoId: string | null;
  estado: EstadoVinculo;
  invitacion: EnlaceInvitacionDto | null;
  invitacionAceptadaEn: string | null;
  referencias: ReferenciaExternaDto[];
  expedienteAbiertoId?: string | null;
  version: number;
  [campo: string]: unknown;
}

export interface InvitacionDto {
  correoDestino?: string | null;
  vigenciaHoras?: number;
}

export interface BloquearVinculoDto {
  motivo: string;
  hasta?: string | null;
}

export interface JustificacionDto {
  justificacion: string;
}

export interface AbrirExpedienteDto {
  flujoId?: string | null;
}

export interface ExpedienteDto {
  id: string;
  folio: string;
  vinculoId: string;
  estado: 'Abierto' | 'EnRevision' | 'Observado' | 'Aprobado' | 'Rechazado' | 'Anulado' | 'Caducado';
  tipo: 'Inicial' | 'Renovacion' | 'Actualizacion';
  version: number;
  [campo: string]: unknown;
}

export type EstadoCertificado = 'Vigente' | 'ConPendientes' | 'Suspendido' | 'Vencido' | 'Revocado' | 'Reemplazado';

export interface CertificadoDto {
  id: string;
  folio: string;
  folioRaiz: string;
  emision: number;
  estado: EstadoCertificado;
  bloqueante: boolean;
  vinculoId: string;
  expedienteFolio: string;
  emitidoEn: string;
  vigenteHasta: string | null;
  huella: string;
  hashContenido: string;
  urlVerificacion: string;
  [campo: string]: unknown;
}

export interface CertificadoDelVinculoDto {
  vinculoId: string;
  actual: CertificadoDto | null;
  emisiones: { folio: string; emision: number; estado: EstadoCertificado; emitidoEn: string; vigenteHasta: string | null; huella: string }[];
}

export interface DescargaDto {
  url: string;
  expira: string;
  nombre: string;
  mime: string;
}

/** Motivo del bloqueo en la consulta de certificación (CN-2). */
export type MotivoBloqueo = 'Bloqueo' | 'PlazoRevalidacion' | 'Vencido' | 'Revocado';

/** `GET /consultas/certificacion` (CN-1): ¿se le puede pagar o contratar a este tercero en esta empresa? */
export interface ConsultaCertificacionDto {
  certificado: boolean;
  folio: string | null;
  estado: Exclude<EstadoCertificado, 'Reemplazado'> | null;
  estadoVinculo: EstadoVinculo | 'SinVinculo';
  vinculoId: string | null;
  terceroId: string | null;
  vigenteHasta: string | null;
  revalidacionPendiente: { tipo: string; plazo: string | null; bloqueante: boolean }[];
  documentosVencidos: string[];
  bloqueado: boolean;
  motivoBloqueo: MotivoBloqueo | null;
  cuentaDesignada: { numeroEnmascarado: string; banco: string } | null;
  urlVerificacion: string | null;
  legacy: { estadoTercero: number } | null;
  /** `false` desde Validasoft; el integrador lo pone en `true` cuando responde desde su copia local. */
  fuenteRespaldo: boolean;
  antiguedadSegundos?: number | null;
}

/** Sobre común de un evento público: el mismo JSON en el webhook y en `GET /eventos`. */
export interface EventoPublicoDto<TDatos = Record<string, unknown>> {
  id: string;
  tipo: string;
  version: number;
  ocurridoEn: string;
  tenantId: string;
  empresaId: string | null;
  /** Posición del evento en el tenant: se guarda y se pasa en `desde` para seguir. */
  cursor: string;
  datos: TDatos;
}

export interface PaginaEventosDto {
  datos: EventoPublicoDto[];
  cursor: string;
  hayMas: boolean;
}

export interface TipoEventoPublicoDto {
  tipo: string;
  descripcion: string;
}

export interface SesionUsuarioDto {
  token: string;
  expira: string;
}
