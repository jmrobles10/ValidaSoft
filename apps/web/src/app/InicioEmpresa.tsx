import { SCOPES } from '@validasoft/api-client';
import { Navigate } from 'react-router';
import { useEmpresaActiva } from '../features/empresas/useEmpresas';
import { usePermisos } from '../shared/usePermisos';

/** Primera pantalla de la empresa: el inicio para quien lee expedientes; si no, los vínculos. */
export function InicioEmpresa() {
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  return <Navigate to={permisos.puede(SCOPES.expedientesLeer) ? 'inicio' : 'vinculos'} replace />;
}
