import { Button, Skeleton, Stack, Typography } from '@mui/material';
import { SCOPES, type FichaAceptadaDelVinculoDto, type VinculoDto } from '@validasoft/api-client';
import { ArrowRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';
import { Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useFichaAceptada } from './api';
import { resumirFichaAceptada } from './resumen';

/**
 * En el detalle del vínculo (FD-3): cuántas secciones aceptó la empresa y cuántas tienen una versión nueva del tercero pendiente de
 * revisión, con el enlace a la ficha aceptada. Usa la misma consulta de la página, así que abrirla no vuelve a pedir nada.
 */
export function TarjetaFichaAceptadaEnVinculo({ vinculo }: { vinculo: VinculoDto }) {
  const { t } = useTranslation();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.tercerosLeer);
  const ficha = useFichaAceptada(empresa.id, vinculo.id, puede);
  const tipo = vinculo.tipoVinculo.nombre;

  const acciones = (
    <Button
      component={RouterLink}
      to={`/e/${empresa.id}/vinculos/${vinculo.id}/ficha`}
      variant="text"
      size="small"
      endIcon={<ArrowRight size={16} aria-hidden />}
      aria-label={t('fichaAceptada.tarjeta.verAccesible', { tipo })}
      sx={{ flex: 'none' }}
    >
      {t('fichaAceptada.tarjeta.ver')}
    </Button>
  );

  return (
    <Tarjeta titulo={t('fichaAceptada.tarjeta.titulo')} etiqueta={t('fichaAceptada.tarjeta.titulo')} acciones={puede ? acciones : undefined}>
      {!puede ? (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('fichaAceptada.tarjeta.sinPermiso')}
        </Typography>
      ) : ficha.isPending ? (
        <Skeleton variant="rounded" width={240} height={20} />
      ) : ficha.isError ? (
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {`${t('fichaAceptada.tarjeta.errorCarga')} ${mensajeDeError(ficha.error, t)}`}
        </Typography>
      ) : (
        <Resumen ficha={ficha.data} />
      )}
    </Tarjeta>
  );
}

function Resumen({ ficha }: { ficha: FichaAceptadaDelVinculoDto }) {
  const { t } = useTranslation();
  const resumen = resumirFichaAceptada(ficha);
  if (resumen.aceptadas === 0) {
    return (
      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
        {t('fichaAceptada.tarjeta.nada')}
      </Typography>
    );
  }
  return (
    <Stack spacing={0.25}>
      <Typography variant="body2">{t('fichaAceptada.resumen.aceptadas', { count: resumen.aceptadas })}</Typography>
      <Typography variant="body2" sx={{ color: resumen.pendientes > 0 ? 'estado.advertencia.texto' : 'text.secondary' }}>
        {resumen.pendientes > 0 ? t('fichaAceptada.resumen.pendientes', { count: resumen.pendientes }) : t('fichaAceptada.resumen.alDia')}
      </Typography>
    </Stack>
  );
}
