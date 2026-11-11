import { esErrorApi, type CuentaDeIngresoDto, type PasoDeIngresoDto, type SesionDirectaDto } from '@validasoft/api-client';

/** Opciones de WebAuthn tal como las entrega la API (JSON con los binarios en base64url). */
export type OpcionesJson = Record<string, unknown>;

/** Dónde está la persona en el ingreso: pasos de la pantalla, en el orden en que se recorren. */
export type PasoIngreso =
  | { paso: 'correo' }
  | { paso: 'codigo'; correo: string; desafio: string; expira: Date }
  | { paso: 'cuenta'; desafio: string; cuentas: readonly CuentaDeIngresoDto[] }
  | { paso: 'passkey'; desafio: string; opciones: OpcionesJson }
  | { paso: 'registrar'; desafio: string; opciones: OpcionesJson };

export type ResultadoDePaso = { tipo: 'siguiente'; paso: PasoIngreso } | { tipo: 'sesion'; sesion: SesionDirectaDto };

/** Segundos que se espera para pedir otro código (el servidor limita a cinco por correo y hora). */
export const ESPERA_REENVIO_S = 30;

function opciones(dto: PasoDeIngresoDto): OpcionesJson {
  if (typeof dto.opciones !== 'object' || dto.opciones === null) throw new Error(`El paso ${dto.estado} no trae las opciones de la passkey.`);
  return dto.opciones as OpcionesJson;
}

/** El paso que sigue según la respuesta de la API (`estado`): elegir cuenta, verificar o registrar una passkey, o ya la sesión. */
export function resolverPaso(dto: PasoDeIngresoDto): ResultadoDePaso {
  switch (dto.estado) {
    case 'Sesion':
      if (!dto.sesion) throw new Error('El ingreso terminó sin sesión.');
      return { tipo: 'sesion', sesion: dto.sesion };
    case 'ElegirCuenta':
      return { tipo: 'siguiente', paso: { paso: 'cuenta', desafio: dto.desafio, cuentas: dto.cuentas ?? [] } };
    case 'PasskeyRequerida':
      return { tipo: 'siguiente', paso: { paso: 'passkey', desafio: dto.desafio, opciones: opciones(dto) } };
    case 'RegistrarPasskey':
      return { tipo: 'siguiente', paso: { paso: 'registrar', desafio: dto.desafio, opciones: opciones(dto) } };
    default:
      throw new Error(`Paso de ingreso desconocido: ${dto.estado}.`);
  }
}

/** Intentos que le quedan al código (`codigo-invalido` los trae como extensión); `null` si el error no es de un código incorrecto. */
export function intentosRestantesDe(error: unknown): number | null {
  if (!esErrorApi(error) || error.codigo !== 'codigo-invalido') return null;
  const valor = Number(error.extensiones.intentosRestantes);
  return Number.isFinite(valor) ? valor : null;
}

/** Errores tras los cuales el código o el ingreso ya no sirven y hay que empezar de nuevo (pedir otro código). */
const REINICIAN = new Set(['codigo-vencido', 'codigo-bloqueado', 'desafio-usado', 'desafio-vencido', 'desafio-no-encontrado']);

export function exigeEmpezarDeNuevo(error: unknown): boolean {
  return esErrorApi(error) && REINICIAN.has(error.codigo);
}

/** Formato mínimo de un correo (la API valida de verdad y responde igual exista o no la cuenta). */
export function esCorreoPlausible(texto: string): boolean {
  const correo = texto.trim();
  return correo.length <= 254 && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(correo);
}

/** Segundos que faltan para poder pedir otro código; 0 cuando ya se puede. */
export function segundosParaReenviar(enviadoEn: number, ahora: number): number {
  return Math.max(0, ESPERA_REENVIO_S - Math.floor((ahora - enviadoEn) / 1000));
}
