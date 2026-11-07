import { clienteValidasoft, type SesionTerceroDto } from '@validasoft/api-client';

/**
 * Reingreso del tercero (rama `feature/H2-reingreso`):
 * - `POST /public/v1/acceso { correo }` → 202 siempre, con el mismo mensaje exista o no el correo.
 * - Al canjear el enlace o el OTP, la API deja la cookie `vs_tercero` (HttpOnly, SameSite=Lax, Path=/public/v1/sesion,
 *   8 h como máximo). `POST /public/v1/sesion/renovar` la cambia por un access nuevo de 30 min (y la rota);
 *   `POST /public/v1/sesion/cerrar` la borra. Ambas exigen `X-Validasoft-Cliente: public` (403 `cliente-invalido`).
 *
 * El navegador manda la cookie solo (mismo origen); por eso estas llamadas van sin `Authorization`.
 */
export const CABECERA_CLIENTE = { 'X-Validasoft-Cliente': 'public' } as const;

export interface AccesoSolicitadoDto {
  mensaje: string;
}

export function solicitarAcceso(correo: string): Promise<AccesoSolicitadoDto> {
  return clienteValidasoft<AccesoSolicitadoDto>({ url: '/public/v1/acceso', method: 'POST', data: { correo: correo.trim() } }, { token: null, empresa: null });
}

export function renovarSesion(): Promise<SesionTerceroDto> {
  return clienteValidasoft<SesionTerceroDto>({ url: '/public/v1/sesion/renovar', method: 'POST' }, { token: null, empresa: null, headers: CABECERA_CLIENTE });
}

/** 204 sin cuerpo. */
export async function cerrarSesionEnServidor(): Promise<void> {
  await clienteValidasoft<unknown>({ url: '/public/v1/sesion/cerrar', method: 'POST' }, { token: null, empresa: null, headers: CABECERA_CLIENTE });
}

/** Rutas de la sesión: un 401 en ellas no se reintenta renovando (sería un ciclo). */
export function esRutaDeSesion(url: string): boolean {
  return /\/public\/v1\/(sesion\/|enlaces\/|acceso)/.test(url);
}
