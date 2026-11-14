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
  /** Con la clave del tercero en el sistema de la API key la creación es idempotente. */
  referenciaExterna?: ReferenciaExternaDto | null;
}

/** Clave de un tercero o un vínculo en un sistema integrador (`id` es la clave externa). */
export interface ReferenciaExternaDto {
  sistema: string;
  id: string;
}

/** Contacto del tercero con una empresa (distinto por empresa); `rol`: Comercial, Pagos, Cumplimiento o Notificaciones. */
export interface ContactoDeEmpresaDto {
  nombre: string;
  cargo?: string | null;
  correo: string;
  telefono?: string | null;
  rol: 'Comercial' | 'Pagos' | 'Cumplimiento' | 'Notificaciones';
}

export interface ActualizarContactosDto {
  contactos: ContactoDeEmpresaDto[];
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
  /** Contactos del tercero con la empresa del vínculo, por rol. */
  contactos?: ContactoDeEmpresaDto[];
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
  /**
   * Solo informativo: el tercero tiene una actualización abierta con la empresa (actualizó información que ya había aprobado y la empresa la
   * revisa). No cambia `estadoVinculo` (sigue `Activo`, `PorVencer` o `Vencido`) ni el certificado, la vigencia o `bloqueado`.
   */
  enRevision?: boolean;
  /**
   * Solo informativo: el certificado sigue vigente y la empresa revisa de nuevo lo que el tercero actualizó («En verificación»). `estado` sigue siendo
   * `ConPendientes`; `bloqueado`, `vigenteHasta` y `revalidacionPendiente` no cambian. Con el plazo de la revalidación vencido o el certificado vencido, es `false`.
   */
  enVerificacion?: boolean;
  /**
   * Solo informativo (EX-8): la empresa tiene una exención vigente del vínculo y eso cambió lo que dicen `bloqueado`, `motivoBloqueo` y `documentosVencidos`: un documento exento
   * que venció no cuenta, así que un certificado que venció solo por él no bloquea. `estado` sigue siendo el del certificado sellado (puede decir `Vencido` con `bloqueado: false`).
   */
  exento?: boolean;
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

/** Datos de una cuenta bancaria designada: el banco sale del catálogo (código de compensación local y SWIFT). */
export interface ContactoDePagosDto {
  nombre: string;
  correo: string;
  telefono?: string | null;
}

/** La ficha del vínculo para un ERP: lo aceptado por la empresa, el representante, la cuenta designada, los contactos y las claves externas. */
export interface FichaDelVinculoDto {
  vinculoId: string;
  empresaId: string;
  terceroId: string;
  referencias: ReferenciaExternaDto[];
  referenciasDelTercero?: ReferenciaExternaDto[];
  contactos?: ContactoDeEmpresaDto[];
  cuentaDesignada?: {
    banco: string;
    bancoCodigo?: string | null;
    bancoSwift?: string | null;
    numeroEnmascarado: string;
    contactoPagos?: ContactoDePagosDto | null;
    [campo: string]: unknown;
  } | null;
  [campo: string]: unknown;
}

export interface TerceroDto {
  id: string;
  identificacion: IdentificacionDto;
  tipoPersona: string;
  nombre: string;
  [campo: string]: unknown;
}

/** Pide una sesión del tercero para un iframe: el vínculo debe llevar la referencia del sistema y `origen` ser uno de los suyos. */
export interface CrearSesionEmbebidaDto {
  vinculoId: string;
  componente: 'boveda' | 'expediente' | 'ficha';
  /** Origen exacto de la página que aloja el iframe (`https://host[:puerto]`). */
  origen: string;
  /** Vigencia en minutos (1 a 15; 10 por omisión). */
  minutos?: number;
}

export interface SesionUsuarioDto {
  token: string;
  expira: string;
}

/** Empresa del sandbox (SB-3): la copia de pruebas de una empresa real. Con una llave de prueba, `X-Empresa` es `id`. */
export interface EmpresaDeSandboxDto {
  id: string;
  produccionId: string;
  nombre: string;
  prefijoFolio: string;
  activa: boolean;
}

/** Estado del sandbox del grupo (`GET /sandbox`). `entorno` es el de la credencial que pregunta. */
export interface SandboxDto {
  existe: boolean;
  entorno: 'produccion' | 'sandbox';
  tenantId: string | null;
  creadoEn: string | null;
  reiniciadoEn: string | null;
  empresas: EmpresaDeSandboxDto[];
  sistemas: string[];
  correos: number;
}

/** Correo que el sandbox habría enviado, retenido en su buzón: `enlaces` trae el de acceso del tercero. */
export interface CorreoDePruebaDto {
  id: string;
  para: string;
  asunto: string;
  texto: string;
  html: string;
  enlaces: string[];
  recibidoEn: string;
}

export interface PaginaDto<T> {
  datos: T[];
  siguienteCursor: string | null;
}
