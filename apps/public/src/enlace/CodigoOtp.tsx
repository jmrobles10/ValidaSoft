import { CampoCodigo } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';

/**
 * Seis casillas de un dígito (AceptacionConEvidencia del design system) para el código del enlace: la misma pieza que usa el ingreso directo de la
 * web de empresa (`CampoCodigo`, packages/ui), con los textos de esta app.
 */
export function CodigoOtp({ valor, onCambiar, deshabilitado, error }: { valor: string; onCambiar: (valor: string) => void; deshabilitado?: boolean; error?: boolean }) {
  const { t } = useTranslation();
  return (
    <CampoCodigo
      valor={valor}
      onCambiar={onCambiar}
      deshabilitado={deshabilitado}
      error={error}
      etiqueta={t('enlace.otpCampo')}
      etiquetaDigito={(n) => t('enlace.otpDigito', { n: String(n) })}
    />
  );
}
