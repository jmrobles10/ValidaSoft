import { SCOPES } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, SinPermiso } from '../../../shared/componentes';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { ListaDeSolicitudesHabeas } from '../../habeas/ListaDeSolicitudesHabeas';

/**
 * Habeas data de la empresa (Ley 1581, OD-1, OD-3): las solicitudes de consulta, corrección o supresión que los terceros le dirigen
 * desde su portal. La empresa es la responsable de los datos de sus vínculos: responde dentro del plazo de la ley (10 días hábiles las
 * consultas, 15 los reclamos) y su respuesta llega al titular con una constancia. Requiere `supresiones:resolver`.
 */
export function PaginaHabeasData() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const puede = usePermisos(empresa.id).puede(SCOPES.supresionesResolver);

  if (!puede) return <SinPermiso seccion={t('nav.habeas-data')} />;

  return (
    <>
      <EncabezadoPagina titulo={t('habeas.titulo')} subtitulo={t('habeas.subtituloEmpresa')} />
      <ListaDeSolicitudesHabeas origen={{ tipo: 'empresa', empresaId: empresa.id }} />
    </>
  );
}
