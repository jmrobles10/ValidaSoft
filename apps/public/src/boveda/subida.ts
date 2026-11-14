import {
  confirmarCargaDocumento,
  esErrorApi,
  esErrorDeRed,
  iniciarCargaDocumento,
  type CompartidoDto,
  type InstruccionCargaDto,
  type VersionDocumentoDto,
} from '@validasoft/api-client';
import { sha256Hex } from '@validasoft/ui';

/** Límites de la bóveda (docs/05 §2.9.2, `Validasoft:Boveda`). El servidor los vuelve a validar al confirmar. */
export const TAMANO_MAXIMO_BYTES = 20 * 1024 * 1024;
export const MAXIMO_PAGINAS = 50;
/** Tipos que acepta la bóveda por magic bytes (BO-4). HEIC no: el celular lo entrega como JPEG al tomar la foto. */
export const TIPOS_PERMITIDOS = ['application/pdf', 'image/jpeg', 'image/png', 'image/webp'] as const;
export const ACEPTA_ARCHIVOS = TIPOS_PERMITIDOS.join(',');

/**
 * A qué documento va la versión: un canónico global de la bóveda, o un tipo de la empresa en un vínculo. En un canónico
 * global, el `vinculoId` es solo contexto: la API aplica las reglas de archivo que configuró esa empresa (TD-1).
 */
export type DestinoCarga = { tipoCanonico: string; vinculoId?: string } | { tipoDocumentoTenantId: string; vinculoId: string };

/** Lo que admite un tipo de documento: formatos (MIME), tamaño y páginas (estas solo se revisan en el servidor). */
export interface ReglasArchivo {
  formatos: readonly string[];
  tamanoMaximoBytes: number;
  paginasMaximas: number;
}

/** Las de la bóveda cuando el tipo no restringe nada (o se sube sin requisito). */
export const REGLAS_GLOBALES: ReglasArchivo = { formatos: TIPOS_PERMITIDOS, tamanoMaximoBytes: TAMANO_MAXIMO_BYTES, paginasMaximas: MAXIMO_PAGINAS };

export type ProblemaArchivo =
  | { tipo: 'vacio' }
  | { tipo: 'demasiado-grande'; tamano: number; maximo: number }
  | { tipo: 'formato'; mime: string; formatos: readonly string[] };

/** Revisión previa en el navegador con las reglas del tipo, para no subir 30 MB y enterarse al final. */
export function validarArchivo(archivo: Pick<File, 'size' | 'type'>, reglas: ReglasArchivo = REGLAS_GLOBALES): ProblemaArchivo | null {
  if (archivo.size === 0) return { tipo: 'vacio' };
  if (!reglas.formatos.includes(archivo.type)) return { tipo: 'formato', mime: archivo.type, formatos: reglas.formatos };
  if (archivo.size > reglas.tamanoMaximoBytes) return { tipo: 'demasiado-grande', tamano: archivo.size, maximo: reglas.tamanoMaximoBytes };
  return null;
}

export type ResultadoSubida =
  | { tipo: 'lista'; version: VersionDocumentoDto; compartidos: CompartidoDto[] }
  | { tipo: 'duplicado'; numero: number }
  | { tipo: 'devuelta'; version: VersionDocumentoDto };

export type FaseSubida = 'preparando' | 'subiendo' | 'confirmando';

/** PUT del archivo a la URL prefirmada (directo al almacenamiento: los bytes nunca pasan por la API). */
export type EnviarArchivo = (carga: InstruccionCargaDto, archivo: Blob, alProgreso: (fraccion: number) => void, senal?: AbortSignal) => Promise<void>;

const subidaCancelada = () => new DOMException('Subida cancelada', 'AbortError');

export const enviarConXhr: EnviarArchivo = (carga, archivo, alProgreso, senal) =>
  new Promise((resolver, rechazar) => {
    // Un `abort` que ya ocurrió no vuelve a avisar: sin esta revisión el PUT saldría igual y pisaría al que lo reemplazó.
    if (senal?.aborted) {
      rechazar(subidaCancelada());
      return;
    }
    const xhr = new XMLHttpRequest();
    const cancelar = () => xhr.abort();
    const terminar = () => senal?.removeEventListener('abort', cancelar);
    xhr.open(carga.metodo, carga.url);
    for (const [nombre, valor] of Object.entries(carga.cabeceras)) xhr.setRequestHeader(nombre, valor);
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) alProgreso(e.loaded / e.total);
    };
    xhr.onload = () => {
      terminar();
      if (xhr.status >= 200 && xhr.status < 300) resolver();
      else rechazar(new ErrorSubida(xhr.status));
    };
    xhr.onerror = () => {
      terminar();
      rechazar(new ErrorSubida(0));
    };
    xhr.onabort = () => {
      terminar();
      rechazar(subidaCancelada());
    };
    senal?.addEventListener('abort', cancelar, { once: true });
    xhr.send(archivo);
  });

/** El almacenamiento rechazó el PUT (0 = sin conexión o CORS). */
export class ErrorSubida extends Error {
  override readonly name = 'ErrorSubida';
  constructor(readonly estado: number) {
    super(`El almacenamiento respondió ${String(estado)}`);
  }
}

export interface OpcionesSubida {
  archivo: File;
  destino: DestinoCarga;
  /** Campos de un tipo `ArchivoYFormulario`: viajan en el paso 1 y la API los valida ahí (TD-4). */
  datos?: Record<string, string | boolean> | null;
  alFase?: (fase: FaseSubida) => void;
  alProgreso?: (fraccion: number) => void;
  enviar?: EnviarArchivo;
  senal?: AbortSignal;
}

/** Los dos pasos de la carga (docs/05 §2.9.2): iniciar → PUT prefirmado → confirmar, con el estado final de la versión. */
export async function subirDocumento({ archivo, destino, datos, alFase, alProgreso, enviar = enviarConXhr, senal }: OpcionesSubida): Promise<ResultadoSubida> {
  senal?.throwIfAborted();
  alFase?.('preparando');
  const sha256 = await sha256Hex(archivo);
  // El hash no se puede cancelar: si mientras tanto la subida se reemplazó o se canceló, no se le pide nada a la API.
  senal?.throwIfAborted();
  const inicio = await iniciarCargaDocumento(
    { nombreArchivo: archivo.name, tamanoBytes: archivo.size, tipoMime: archivo.type, sha256, ...destino, ...(datos ? { datos } : {}) },
    undefined,
    senal,
  );
  if (inicio.duplicado || !inicio.carga) return { tipo: 'duplicado', numero: inicio.version };

  senal?.throwIfAborted();
  alFase?.('subiendo');
  await enviar(inicio.carga, archivo, (f) => alProgreso?.(f), senal);

  alFase?.('confirmando');
  const confirmacion = await confirmarCargaDocumento(inicio.documentoId, inicio.version, undefined, senal);
  if (confirmacion.duplicado) return { tipo: 'duplicado', numero: confirmacion.version.n };
  if (confirmacion.version.estado === 'Devuelta') return { tipo: 'devuelta', version: confirmacion.version };
  return { tipo: 'lista', version: confirmacion.version, compartidos: confirmacion.compartidos };
}

/** Errores de la subida con mensaje y siguiente paso propios. */
export type FalloSubida = 'demasiado-grande' | 'formato' | 'red' | 'vencida' | 'otro';

export function falloDeSubida(error: unknown): FalloSubida {
  if (esErrorDeRed(error) || (error instanceof ErrorSubida && error.estado === 0)) return 'red';
  if (error instanceof ErrorSubida && error.estado === 403) return 'vencida'; // la URL prefirmada venció (15 min)
  if (!esErrorApi(error)) return 'otro';
  if (error.estado === 413 || error.codigo === 'archivo-demasiado-grande') return 'demasiado-grande';
  if (error.estado === 415 || error.codigo === 'tipo-archivo-no-soportado') return 'formato';
  return 'otro';
}

/** "2,4 MB" o "820 KB". */
export function formatearTamano(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toLocaleString('es-CO', { maximumFractionDigits: 1 })} MB`;
  return `${Math.max(1, Math.round(bytes / 1024)).toLocaleString('es-CO')} KB`;
}
