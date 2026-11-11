import type { TFunction } from 'i18next';
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
