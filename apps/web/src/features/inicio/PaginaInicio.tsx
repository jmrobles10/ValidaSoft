import { SCOPES } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, SinPermiso } from '../../shared/componentes';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { Indicadores } from './Indicadores';
import { LoQueTeToca } from './LoQueTeToca';

/**
 * Inicio de la empresa (IN-1, IN-2): primero lo que le toca a quien entra, con enlace a cada pantalla; debajo, los
 * indicadores de la empresa. Es la primera pantalla para quien lee expedientes.
 */
export function PaginaInicio() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);

  if (!permisos.puede(SCOPES.expedientesLeer)) return <SinPermiso seccion={t('nav.inicio')} />;

  return (
    <>
      <EncabezadoPagina titulo={t('inicio.titulo')} subtitulo={t('inicio.subtitulo', { empresa: empresa.nombre })} />
      <LoQueTeToca empresaId={empresa.id} />
      <Indicadores empresaId={empresa.id} puedeVerVinculos={permisos.puede(SCOPES.vinculosLeer)} />
    </>
  );
}
