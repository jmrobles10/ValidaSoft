import { Stack } from '@mui/material';
import { SCOPES } from '@validasoft/api-client';
import { EsqueletoTarjeta } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useMatrizDeRiesgo } from './api';
import { EditorMatriz } from './EditorMatriz';

/**
 * Matriz de riesgo de la empresa activa (CU-17): factores con pesos y opciones puntuadas, límites entre niveles y lo que exige
 * cada nivel. Sin `configuracion:escribir` se ve pero no se cambia. Si la empresa aún no tiene una, se ofrece la plantilla.
 */
export function PaginaMatrizRiesgo() {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puedeLeer = permisos.puede(SCOPES.configuracionLeer);
  const consulta = useMatrizDeRiesgo(empresa.id, puedeLeer);

  if (!puedeLeer) return <SinPermiso seccion={t('nav.matriz-riesgo')} />;

  if (!consulta.data) {
    return (
      <>
        <EncabezadoPagina titulo={t('matrizRiesgo.titulo')} subtitulo={t('matrizRiesgo.subtitulo', { empresa: empresa.nombre })} />
        {consulta.isError ? (
          <ErrorRecuperable mensaje={`${t('matrizRiesgo.error')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />
        ) : (
          <Stack spacing={2} role="status" aria-busy aria-label={t('matrizRiesgo.titulo')}>
            <EsqueletoTarjeta lineas={2} />
            <EsqueletoTarjeta lineas={6} alto={260} />
            <EsqueletoTarjeta lineas={4} />
          </Stack>
        )}
      </>
    );
  }

  return (
    <EditorMatriz
      key={`${String(consulta.data.version)}-${String(consulta.data.esPorDefecto)}`}
      empresaId={empresa.id}
      nombreEmpresa={empresa.nombre}
      matriz={consulta.data}
      puedeEscribir={permisos.puede(SCOPES.configuracionEscribir)}
    />
  );
}
