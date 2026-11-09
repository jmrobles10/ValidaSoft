import { canjearEnlace, esErrorApi, esErrorDeRed, type OtpRequeridoDto, type SesionTerceroDto } from '@validasoft/api-client';

/** Lo que devuelve el canje: la sesión, o que el tipo de vínculo exige un código y ya salió al correo. */
export type ResultadoCanje = { tipo: 'sesion'; sesion: SesionTerceroDto } | { tipo: 'otp'; expira: string };

export function interpretarCanje(respuesta: SesionTerceroDto | OtpRequeridoDto): ResultadoCanje {
  return 'accessToken' in respuesta ? { tipo: 'sesion', sesion: respuesta } : { tipo: 'otp', expira: respuesta.expira };
}

/**
 * El enlace es de un solo uso: si el efecto corre dos veces (StrictMode, una navegación que vuelve) el segundo canje
 * respondería 410 `enlace-usado`. Por eso el canje de cada token se hace una sola vez y se reutiliza la promesa.
 */
const canjes = new Map<string, Promise<ResultadoCanje>>();

export function canjearUnaVez(token: string): Promise<ResultadoCanje> {
  let canje = canjes.get(token);
  if (!canje) {
    canje = canjearEnlace(token, { token: null, empresa: null }).then(interpretarCanje);
    canjes.set(token, canje);
    // Un fallo (sin red, 429) se puede reintentar: no se guarda.
    void canje.catch(() => {
      canjes.delete(token);
    });
  }
  return canje;
}

/** Solo pruebas. */
export function olvidarCanjes(): void {
  canjes.clear();
}

/** Por qué el enlace no sirve, con su pantalla propia. */
export type FalloEnlace = 'vencido' | 'usado' | 'no-encontrado' | 'otp-vencido' | 'otp-bloqueado' | 'demasiadas-peticiones' | 'red' | 'otro';

export function falloDeEnlace(error: unknown): FalloEnlace {
  if (esErrorDeRed(error)) return 'red';
  if (!esErrorApi(error)) return 'otro';
  switch (error.codigo) {
    case 'enlace-vencido':
      return 'vencido';
    case 'enlace-usado':
      return 'usado';
    case 'enlace-no-encontrado':
      return 'no-encontrado';
    case 'otp-vencido':
      return 'otp-vencido';
    case 'otp-bloqueado':
      return 'otp-bloqueado';
    case 'demasiadas-peticiones':
      return 'demasiadas-peticiones';
    default:
      if (error.estado === 410) return 'vencido';
      if (error.estado === 404) return 'no-encontrado';
      return 'otro';
  }
}

/** Intentos de código que quedan (`otp-invalido` los trae como extensión, IN-12); `null` si no aplica. */
export function intentosRestantesDe(error: unknown): number | null {
  if (!esErrorApi(error) || error.codigo !== 'otp-invalido') return null;
  const valor = Number(error.extensiones.intentosRestantes);
  return Number.isFinite(valor) ? valor : null;
}

/** Solo dígitos y como mucho 6: el código se pega o se escribe en el celular. */
export function normalizarCodigo(texto: string): string {
  return texto.replace(/\D/g, '').slice(0, 6);
}

/**
 * A dónde ir con la sesión recién abierta: si el enlace es de un expediente (`expedienteOrigen`, p. ej. el del correo
 * de observación), a esa revisión; si la sesión trae un solo vínculo, a su invitación (allí ve los textos o, si ya
 * aceptó, pasa a su revisión); con varios, a "Mis empresas".
 */
export function destinoTrasSesion(sesion: SesionTerceroDto): string {
  if (sesion.expedienteOrigen) return `/expediente/${sesion.expedienteOrigen}`;
  const [unico, ...otros] = sesion.vinculos;
  if (unico && otros.length === 0) return `/empresas/${unico.id}`;
  return '/empresas';
}
