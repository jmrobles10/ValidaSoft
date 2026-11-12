import type { SolicitudHabeasDto } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { comoAlcance, comoTipo } from './modelo';

/** «Supresión de datos · Solo documentos»: el derecho que pidió el titular y, en la supresión, hasta dónde. Un valor que esta versión no conoce se muestra tal cual. */
export function NombreDeDerecho({ solicitud }: { solicitud: Pick<SolicitudHabeasDto, 'tipo' | 'alcance'> }) {
  const { t } = useTranslation();
  const tipo = comoTipo(solicitud.tipo);
  const alcance = comoAlcance(solicitud.alcance);
  return (
    <>
      {tipo ? t(`habeas.tipos.${tipo}`) : solicitud.tipo}
      {alcance ? ` · ${t(`habeas.alcances.${alcance}`)}` : ''}
    </>
  );
}
