/**
 * Errores tipados del cliente. La API responde ProblemDetails RFC 9457 con `codigo` estable y `traceId`
 * (docs/05 §3.3); aquí se convierten en `ErrorApi` para que la UI decida por código, nunca por texto.
 */

/** Códigos que la UI distingue hoy. La lista no es cerrada: el backend puede agregar otros. */
export type CodigoErrorConocido =
  | 'version-desactualizada'
  | 'estado-invalido'
  | 'estado-terminal'
  | 'ya-bloqueado'
  | 'no-bloqueado'
  | 'invitacion-ya-aceptada'
  | 'motivo-requerido'
  | 'justificacion-requerida'
  | 'fecha-invalida'
  | 'vinculo-no-encontrado'
  | 'vinculo-duplicado'
  | 'referencia-duplicada'
  | 'empresa-no-encontrada'
  | 'alcance-insuficiente'
  | 'usuario-duplicado'
  | 'usuario-no-encontrado'
  | 'sistema-duplicado'
  | 'area-duplicada'
  | 'miembros-invalidos'
  | 'idempotency-key-requerida'
  | 'clave-idempotencia-reutilizada'
  | 'usuario-delegado-requerido'
  | 'asercion-invalida'
  | 'asercion-firma-invalida'
  | 'asercion-vencida'
  | 'asercion-vigencia-excesiva'
  | 'asercion-incompleta'
  | 'llave-sin-sistema'
  | 'sistema-inactivo'
  | 'reautenticacion-requerida'
  | 'no-autenticado'
  | 'prohibido'
  | 'no-encontrado'
  | 'peticion-invalida'
  | 'error-interno';

/** Un código conocido o cualquier otro texto que envíe el backend. */
export type CodigoError = CodigoErrorConocido | (string & Record<never, never>);

/** Cuerpo ProblemDetails tal como lo serializa ASP.NET Core con las extensiones de Validasoft. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  codigo?: string;
  traceId?: string;
  /** Errores por campo de la validación de ASP.NET (`errors`). */
  errors?: Record<string, string[]>;
}

export class ErrorApi extends Error {
  override readonly name = 'ErrorApi';

  constructor(
    readonly estado: number,
    readonly codigo: CodigoError,
    readonly detalle: string,
    readonly traceId: string | null,
    readonly correlationId: string | null,
    readonly errores: Readonly<Record<string, readonly string[]>> = {},
    /** Extensiones propias del caso (p. ej. `intentosRestantes` en `otp-invalido`, IN-12). */
    readonly extensiones: Readonly<Record<string, unknown>> = {},
  ) {
    super(`${codigo} (${estado}): ${detalle}`);
  }

  /** La versión que se envió en If-Match ya no es la actual: hay que recargar (412). */
  get esConflictoDeVersion(): boolean {
    return this.estado === 412 || this.codigo === 'version-desactualizada';
  }

  /** La sesión venció o la credencial no es válida (401). */
  get esNoAutenticado(): boolean {
    return this.estado === 401;
  }

  get esProhibido(): boolean {
    return this.estado === 403;
  }

  get esNoEncontrado(): boolean {
    return this.estado === 404;
  }

  get esValidacion(): boolean {
    return this.estado === 400 || this.estado === 422;
  }

  get esConflicto(): boolean {
    return this.estado === 409;
  }
}

/** La petición no llegó a la API (sin red, CORS, servidor caído) o se canceló. */
export class ErrorDeRed extends Error {
  override readonly name = 'ErrorDeRed';

  constructor(
    mensaje: string,
    readonly causa: unknown,
  ) {
    super(mensaje);
  }
}

export function esErrorApi(error: unknown): error is ErrorApi {
  return error instanceof ErrorApi;
}

export function esErrorDeRed(error: unknown): error is ErrorDeRed {
  return error instanceof ErrorDeRed;
}

/** Código por defecto cuando la respuesta de error no trae `codigo` (p. ej. un 401 del esquema de autenticación). */
function codigoPorEstado(estado: number): CodigoErrorConocido {
  switch (estado) {
    case 400:
      return 'peticion-invalida';
    case 401:
      return 'no-autenticado';
    case 403:
      return 'prohibido';
    case 404:
      return 'no-encontrado';
    case 412:
      return 'version-desactualizada';
    default:
      return 'error-interno';
  }
}

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

function texto(valor: unknown): string | undefined {
  return typeof valor === 'string' && valor.length > 0 ? valor : undefined;
}

function erroresPorCampo(valor: unknown, lista?: unknown): Record<string, string[]> {
  const resultado: Record<string, string[]> = {};
  if (esObjeto(valor)) {
    for (const [campo, mensajes] of Object.entries(valor)) {
      if (Array.isArray(mensajes)) resultado[campo] = mensajes.filter((m): m is string => typeof m === 'string');
    }
  }
  // Extensión `errores: [{ campo, mensaje }]` (p. ej. la ficha del tercero, FM-19): todos los errores, no solo el primero.
  if (Array.isArray(lista)) {
    for (const item of lista) {
      if (!esObjeto(item)) continue;
      const campo = texto(item.campo);
      const mensaje = texto(item.mensaje);
      if (campo && mensaje) (resultado[campo] ??= []).push(mensaje);
    }
  }
  return resultado;
}

/** Interpreta un cuerpo de error (ProblemDetails o cualquier otra cosa) y arma el `ErrorApi`. */
export function errorDesdeProblema(estado: number, cuerpo: unknown, correlationId: string | null): ErrorApi {
  const problema = esObjeto(cuerpo) ? cuerpo : {};
  const codigo = texto(problema.codigo) ?? codigoPorEstado(estado);
  const detalle =
    texto(problema.detail) ?? texto(problema.title) ?? (typeof cuerpo === 'string' && cuerpo ? cuerpo : `HTTP ${estado}`);
  return new ErrorApi(
    estado,
    codigo,
    detalle,
    texto(problema.traceId) ?? null,
    correlationId,
    erroresPorCampo(problema.errors, problema.errores),
    extensionesDe(problema),
  );
}

const CAMPOS_ESTANDAR = new Set(['type', 'title', 'status', 'detail', 'instance', 'codigo', 'traceId', 'errors', 'errores']);

/** Lo que el ProblemDetails trae además de los campos estándar. */
function extensionesDe(problema: Record<string, unknown>): Record<string, unknown> {
  return Object.fromEntries(Object.entries(problema).filter(([clave]) => !CAMPOS_ESTANDAR.has(clave)));
}
