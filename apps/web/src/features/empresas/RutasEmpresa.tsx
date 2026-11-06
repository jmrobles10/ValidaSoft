import { Alert, Box, CircularProgress } from '@mui/material';
import { accedeAEmpresa, configurarCliente } from '@validasoft/api-client';
import { useEffect } from 'react';
import { useTranslation } from 'react-i18next';
import { Navigate, Outlet, useParams } from 'react-router';
import { useSesion } from '../../sesion/sesionStore';
import { ErrorRecuperable } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { usePreferencias } from '../../shared/preferencias';
import { elegirEmpresaInicial, useEmpresas, type ContextoEmpresa } from './useEmpresas';

export function RedireccionInicial() {
  const { t } = useTranslation();
  const consulta = useEmpresas();
  const sesion = useSesion((s) => s.sesion);
  const sugerida = useSesion((s) => s.empresaSugerida);
  const ultima = usePreferencias((s) => (sesion ? s.ultimaEmpresa[sesion.usuarioId] : undefined));

  if (consulta.isPending) return <CircularProgress sx={{ m: 4 }} aria-label={t('comun.cargando')} />;
  if (consulta.isError) return <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />;

  const destino = elegirEmpresaInicial(consulta.data, sugerida, ultima);
  if (!destino) return <Alert severity="info">{t('empresas.sinEmpresas')}</Alert>;
  return <Navigate to={`/e/${destino}`} replace />;
}

/** Rama `/e/:empresaId`: valida la empresa de la URL y la fija como `X-Empresa` del cliente. */
export function EmpresaDeLaRuta() {
  const { t } = useTranslation();
  const { empresaId = '' } = useParams();
  const consulta = useEmpresas();
  const sesion = useSesion((s) => s.sesion);
  const recordarEmpresa = usePreferencias((s) => s.recordarEmpresa);
  const empresa = consulta.data?.find((e) => e.id === empresaId);

  useEffect(() => {
    if (!empresa || !sesion) return;
    configurarCliente({ obtenerEmpresa: () => empresa.id });
    recordarEmpresa(sesion.usuarioId, empresa.id);
  }, [empresa, sesion, recordarEmpresa]);

  if (consulta.isPending) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', p: 6 }}>
        <CircularProgress aria-label={t('comun.cargando')} />
      </Box>
    );
  }
  if (consulta.isError) return <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />;
  if (!empresa || (sesion && !accedeAEmpresa(sesion, empresa.id))) return <Alert severity="warning">{t('empresas.noEncontrada')}</Alert>;

  return <Outlet context={{ empresa, empresas: consulta.data } satisfies ContextoEmpresa} />;
}
