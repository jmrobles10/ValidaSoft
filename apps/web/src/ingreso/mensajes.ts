import type { TFunction } from 'i18next';
import { esCO } from '../i18n/es-CO';
import { mensajeDeError } from '../shared/errores';
import { intentosRestantesDe } from './estado';
import { esErrorDePasskey } from './passkeys';

/** Texto de un fallo del ingreso: lo del navegador (passkey), los intentos que quedan del código o el mensaje de la API. */
export function mensajeDeIngreso(error: unknown, t: TFunction): string {
  if (esErrorDePasskey(error)) {
    switch (error.causa) {
      case 'cancelada':
        return t('ingreso.passkeyCancelada');
      case 'no-soportada':
        return t('ingreso.passkeyNoSoportada');
      case 'ya-registrada':
        return t('ingreso.passkeyYaRegistrada');
      default:
        return t('ingreso.passkeyFallo');
    }
  }

  const intentos = intentosRestantesDe(error);
  if (intentos !== null) return t('ingreso.intentos', { count: intentos });
  return mensajeDeError(error, t);
}

/** Texto de un código de SSO que dejó el servidor en la dirección (`sso_error`): el de su error o, si no lo conocemos, uno general con el siguiente paso. */
export function mensajeDeCodigoDeSso(codigo: string, t: TFunction): string {
  return codigo in esCO.errores ? t(`errores.${codigo as keyof typeof esCO.errores}`) : t('ingreso.sso.errorGenerico');
}
