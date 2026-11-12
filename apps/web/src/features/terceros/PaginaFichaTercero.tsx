import { Box, Button, Skeleton, Stack, Typography } from '@mui/material';
import { esErrorApi, SCOPES, type FichaTerceroDto } from '@validasoft/api-client';
import { EsqueletoTarjeta, PaginaEstado } from '@validasoft/ui';
import { SearchX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams } from 'react-router';
import { useScrollPropio } from '../../sesion/alturaFija';
import { EncabezadoPagina, EnlaceVolver, ErrorRecuperable, SinPermiso, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { identificacionDe } from '../vinculos/tercero';
import { useFichaTercero } from './api';
import { DatosTercero, Mono } from './DatosTercero';
import { InformacionTercero } from './ficha/InformacionTercero';
import { textoTipoPersona } from './modelo';
import { OtrasEmpresas } from './OtrasEmpresas';
import { TarjetaVinculoFicha } from './TarjetaVinculoFicha';

const REJILLA = { display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', lg: 'minmax(0,1.7fr) minmax(0,1fr)' }, gap: 2, alignItems: 'start' } as const;

/**
 * Ficha del tercero (Tercero 360, docs/06 §`/terceros/:id`): su identificación y contactos, cada vínculo con la empresa
 * activa (certificado, cuenta designada y documentos compartidos), la información que diligenció (ficha maestra, docs/14)
 * y, de las otras empresas del grupo, solo que hay vínculo.
 */
export function PaginaFichaTercero() {
  const { t } = useTranslation();
  useScrollPropio();
  const { terceroId = '' } = useParams();
  const { empresa, empresas } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.tercerosLeer);
  const consulta = useFichaTercero(empresa.id, terceroId, puede);

  if (!puede) return <SinPermiso seccion={t('terceros.ficha.titulo')} />;

  // No hay lista de terceros: se vuelve a Vínculos (desde /e/:empresaId/terceros/:terceroId, «../..» es la empresa).
  const volver = <EnlaceVolver a="../../vinculos">{t('nav.vinculos')}</EnlaceVolver>;

  if (consulta.isPending) {
    return (
      <Box aria-busy>
        {volver}
        <Skeleton variant="rounded" width={320} height={28} sx={{ mb: 1 }} />
        <Skeleton variant="rounded" width={220} height={14} sx={{ mb: 3 }} />
        <Box sx={REJILLA}>
          <EsqueletoTarjeta alto={320} />
          <EsqueletoTarjeta alto={220} />
        </Box>
      </Box>
    );
  }
  if (consulta.isError) {
    if (esErrorApi(consulta.error) && consulta.error.esNoEncontrado) {
      return (
        <PaginaEstado
          icono={SearchX}
          titulo={t('terceros.ficha.noEncontrado')}
          texto={t('terceros.ficha.noEncontradoAyuda')}
          acciones={
            <Button variant="outlined" component={RouterLink} to={`/e/${empresa.id}/vinculos`}>
              {t('terceros.ficha.irAVinculos')}
            </Button>
          }
        />
      );
    }
    return (
      <>
        {volver}
        <ErrorRecuperable mensaje={`${t('terceros.ficha.errorCarga')} ${mensajeDeError(consulta.error, t)}`} onReintentar={() => void consulta.refetch()} />
      </>
    );
  }

  const { tercero, vinculos, otrasEmpresasDelGrupo, secciones } = consulta.data;

  return (
    <>
      <EncabezadoPagina
        antes={volver}
        titulo={tercero.nombre}
        tituloCompleto={tercero.nombre}
        subtitulo={
          <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', mt: 0.25, fontSize: 13, flexWrap: 'wrap' }}>
            <Mono>{identificacionDe(tercero)}</Mono>
            <span aria-hidden>·</span>
            <span>{textoTipoPersona(tercero.tipoPersona, t)}</span>
          </Stack>
        }
      />
      <Box sx={REJILLA}>
        <Stack spacing={3} sx={{ minWidth: 0 }}>
          <VinculosDeLaFicha empresaId={empresa.id} empresaNombre={empresa.nombre} vinculos={vinculos} puedeVer={permisos.puede(SCOPES.vinculosLeer)} />
          {secciones && <InformacionTercero empresaId={empresa.id} terceroId={tercero.id} secciones={secciones} vinculos={vinculos} />}
        </Stack>
        <Stack spacing={2} sx={{ minWidth: 0 }}>
          <DatosTercero tercero={tercero} />
          <OtrasEmpresas otras={otrasEmpresasDelGrupo} empresas={empresas} />
        </Stack>
      </Box>
    </>
  );
}

interface VinculosDeLaFichaProps {
  empresaId: string;
  empresaNombre: string;
  vinculos: FichaTerceroDto['vinculos'];
  puedeVer: boolean;
}

/** Sin `vinculos:leer` la API no devuelve los vínculos: se dice que falta el permiso, no que no haya. */
function VinculosDeLaFicha({ empresaId, empresaNombre, vinculos, puedeVer }: VinculosDeLaFichaProps) {
  const { t } = useTranslation();
  const titulo = t('terceros.ficha.vinculos.titulo', { empresa: empresaNombre });

  if (!puedeVer) {
    return (
      <Tarjeta titulo={titulo}>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('terceros.ficha.vinculos.sinPermiso')}
        </Typography>
      </Tarjeta>
    );
  }

  return (
    <Stack component="section" aria-label={titulo} spacing={1.5} sx={{ minWidth: 0 }}>
      <Typography variant="h4" component="h2">
        {titulo}
      </Typography>
      {vinculos.map((v) => (
        <TarjetaVinculoFicha key={v.vinculo.id} empresaId={empresaId} datos={v} />
      ))}
    </Stack>
  );
}
