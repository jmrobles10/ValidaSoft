import {
  confirmarCargaDocumento,
  esErrorApi,
  esErrorDeRed,
  iniciarCargaDocumento,
  type CompartidoDto,
  type InstruccionCargaDto,
  type VersionDocumentoDto,
} from '@validasoft/api-client';

/** Límites de la bóveda (docs/05 §2.9.2, `Validasoft:Boveda`). El servidor los vuelve a validar al confirmar. */
export const TAMANO_MAXIMO_BYTES = 20 * 1024 * 1024;
export const MAXIMO_PAGINAS = 50;
/** Tipos que acepta la bóveda por magic bytes (BO-4). HEIC no: el celular lo entrega como JPEG al tomar la foto. */
export const TIPOS_PERMITIDOS = ['application/pdf', 'image/jpeg', 'image/png', 'image/webp'] as const;
export const ACEPTA_ARCHIVOS = TIPOS_PERMITIDOS.join(',');

/** A qué documento va la versión: un canónico global de la bóveda, o un tipo de la empresa en un vínculo. */
export type DestinoCarga = { tipoCanonico: string } | { tipoDocumentoTenantId: string; vinculoId: string };

export type ProblemaArchivo = { tipo: 'vacio' } | { tipo: 'demasiado-grande'; tamano: number } | { tipo: 'formato'; mime: string };

/** Revisión previa en el navegador, para no subir 30 MB y enterarse al final. */
export function validarArchivo(archivo: Pick<File, 'size' | 'type'>): ProblemaArchivo | null {
  if (archivo.size === 0) return { tipo: 'vacio' };
  if (archivo.size > TAMANO_MAXIMO_BYTES) return { tipo: 'demasiado-grande', tamano: archivo.size };
  if (!(TIPOS_PERMITIDOS as readonly string[]).includes(archivo.type)) return { tipo: 'formato', mime: archivo.type };
  return null;
}

/** SHA-256 en hexadecimal: detecta el duplicado antes de subir y el servidor lo verifica al confirmar. */
export async function sha256Hex(archivo: Blob): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', await archivo.arrayBuffer());
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, '0')).join('');
}

export type ResultadoSubida =
  | { tipo: 'lista'; version: VersionDocumentoDto; compartidos: CompartidoDto[] }
  | { tipo: 'duplicado'; numero: number }
  | { tipo: 'devuelta'; version: VersionDocumentoDto };

export type FaseSubida = 'preparando' | 'subiendo' | 'confirmando';

/** PUT del archivo a la URL prefirmada (directo al almacenamiento: los bytes nunca pasan por la API). */
export type EnviarArchivo = (carga: InstruccionCargaDto, archivo: Blob, alProgreso: (fraccion: number) => void, senal?: AbortSignal) => Promise<void>;

export const enviarConXhr: EnviarArchivo = (carga, archivo, alProgreso, senal) =>
  new Promise((resolver, rechazar) => {
    const xhr = new XMLHttpRequest();
    xhr.open(carga.metodo, carga.url);
    for (const [nombre, valor] of Object.entries(carga.cabeceras)) xhr.setRequestHeader(nombre, valor);
    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) alProgreso(e.loaded / e.total);
    };
    xhr.onload = () => (xhr.status >= 200 && xhr.status < 300 ? resolver() : rechazar(new ErrorSubida(xhr.status)));
    xhr.onerror = () => rechazar(new ErrorSubida(0));
    xhr.onabort = () => rechazar(new DOMException('Subida cancelada', 'AbortError'));
    senal?.addEventListener('abort', () => xhr.abort());
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
  alFase?: (fase: FaseSubida) => void;
  alProgreso?: (fraccion: number) => void;
  enviar?: EnviarArchivo;
  senal?: AbortSignal;
}

/** Los dos pasos de la carga (docs/05 §2.9.2): iniciar → PUT prefirmado → confirmar, con el estado final de la versión. */
export async function subirDocumento({ archivo, destino, alFase, alProgreso, enviar = enviarConXhr, senal }: OpcionesSubida): Promise<ResultadoSubida> {
  alFase?.('preparando');
  const sha256 = await sha256Hex(archivo);
  const inicio = await iniciarCargaDocumento(
    { nombreArchivo: archivo.name, tamanoBytes: archivo.size, tipoMime: archivo.type, sha256, ...destino },
    undefined,
    senal,
  );
  if (inicio.duplicado || !inicio.carga) return { tipo: 'duplicado', numero: inicio.version };

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
