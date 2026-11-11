import { useQuery } from '@tanstack/react-query';
import { getListarVinculosPublicosQueryOptions } from '@validasoft/api-client';
import { Navigate, useParams } from 'react-router';
import { useSesionTercero } from '../sesion/sesionTercero';
import { EsperandoSesion } from './EsperandoSesion';
import { esComponente, rutaDelComponente } from './embebido';
import { MODO_EMBEBIDO } from './modo';

/**
 * `/embed/:componente` (bóveda, expediente o ficha): la entrada de un sistema integrador. Espera la sesión que el sistema entrega por
 * `postMessage` (el puente del Shell la recibe) y, con ella, lleva a la pantalla del componente. Fuera de un iframe permitido no
 * muestra nada del tercero.
 */
export function PaginaEmbebida() {
  const { componente } = useParams();
  const sesion = useSesionTercero((s) => s.sesion);
  const activa = useSesionTercero((s) => s.estado === 'activa');
  const vinculos = useQuery({ ...getListarVinculosPublicosQueryOptions(), enabled: activa });

  if (!MODO_EMBEBIDO || !esComponente(componente) || !activa || !sesion || vinculos.isPending) return <EsperandoSesion />;

  const vinculo = sesion.vinculos[0];
  const expedienteId = vinculos.data?.find((v) => v.id === vinculo?.id)?.expedienteId ?? null;
  return <Navigate to={rutaDelComponente(componente, sesion, expedienteId)} replace />;
}
