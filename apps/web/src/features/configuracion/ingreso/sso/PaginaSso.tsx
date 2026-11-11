import { Skeleton, Stack } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { getObtenerConfiguracionSsoQueryOptions, SCOPES } from '@validasoft/api-client';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable, SinPermiso } from '../../../../shared/componentes';
import { mensajeDeError } from '../../../../shared/errores';
import { usePermisos } from '../../../../shared/usePermisos';
import { useEmpresaActiva } from '../../../empresas/useEmpresas';
import { FormularioDeSso } from './FormularioDeSso';

/** Configuración › Inicio de sesión único (PI-1…PI-4): el proveedor de identidad del cliente, sus dominios y cómo se crean y se les dan roles a las personas. */
export function PaginaSso() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.usuariosAdministrar);
  const consulta = useQuery({ ...getObtenerConfiguracionSsoQueryOptions({ request: { empresa: null } }), enabled: puede });

  if (!puede) return <SinPermiso seccion={t('nav.sso')} />;
  if (consulta.isError) return <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />;
  if (!consulta.data)
    return (
      <Stack spacing={2} role="status" aria-busy aria-label={t('comun.cargando')}>
        <Skeleton variant="rounded" width={260} height={28} />
        <Skeleton variant="rounded" height={220} />
        <Skeleton variant="rounded" height={160} />
      </Stack>
    );

  // El formulario guarda lo que se escribe y se rehace solo al guardar (con lo que devolvió el servidor): agregar o verificar un dominio no lo reinicia.
  return <FormularioDeSso inicial={consulta.data} />;
}
