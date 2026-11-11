import { Alert, AlertTitle, Box, Button, Skeleton, Stack, Typography } from '@mui/material';
import { esErrorApi, SCOPES } from '@validasoft/api-client';
import { EsqueletoTarjeta, EstadoVacio, PaginaEstado } from '@validasoft/ui';
import { SearchX } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams } from 'react-router';
import { EncabezadoPagina, EnlaceVolver, ErrorRecuperable, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useTercero, useVinculo } from '../../vinculos/api';
import { identificacionDe } from '../../vinculos/tercero';
import { Mono } from '../DatosTercero';
import { useFichaAceptada } from './api';
import { resumirFichaAceptada } from './resumen';
import { TarjetaSeccionAceptada } from './TarjetaSeccionAceptada';

const REJILLA = { display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', lg: 'minmax(0,1.7fr) minmax(0,1fr)' }, gap: 2, alignItems: 'start' } as const;

/**
 * Ficha aceptada de un vínculo (FD-3, `/e/:empresaId/vinculos/:vinculoId/ficha`): lo que **esta** empresa aceptó del tercero, por
 * sección, con su versión y su soporte, y aparte lo que el tercero guardó después y aún no se revisa. Un aviso agrupado dice cuántas
 * secciones tienen una versión nueva pendiente; a la derecha (debajo en pantallas angostas), el resumen. Solo lectura: se acepta u observa
 * en el expediente. Sin scroll propio: no tiene encabezado que fijar, así que fluye con el alto de la página (embebida, el iframe crece con ella).
 */
export function PaginaFichaAceptada() {
  const { t } = useTranslation();
  const { vinculoId = '' } = useParams();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.vinculosLeer) && permisos.puede(SCOPES.tercerosLeer);
  const vinculo = useVinculo(empresa.id, vinculoId);
  const ficha = useFichaAceptada(empresa.id, vinculoId, puede);
  const tercero = useTercero(vinculo.data?.terceroId ?? '', Boolean(vinculo.data));

  if (!puede) return <SinPermiso seccion={t('fichaAceptada.titulo')} />;

  const volver = <EnlaceVolver a="..">{t('fichaAceptada.volver')}</EnlaceVolver>;

  if (ficha.isPending || vinculo.isPending) {
    return (
      <Box aria-busy>
        {volver}
        <Skeleton variant="rounded" width={320} height={28} sx={{ mb: 1 }} />
        <Skeleton variant="rounded" width={220} height={14} sx={{ mb: 3 }} />
        <Box sx={REJILLA}>
          <Stack spacing={2}>
            <EsqueletoTarjeta alto={220} />
            <EsqueletoTarjeta alto={220} />
          </Stack>
          <EsqueletoTarjeta alto={160} />
        </Box>
      </Box>
    );
  }
  const error = ficha.isError ? ficha.error : vinculo.isError ? vinculo.error : null;
  if (error) {
    if (esErrorApi(error) && error.esNoEncontrado) {
      return <PaginaEstado icono={SearchX} titulo={t('fichaAceptada.noEncontrado')} texto={t('fichaAceptada.noEncontradoAyuda')} acciones={volver} />;
    }
    return (
      <>
        {volver}
        <ErrorRecuperable mensaje={`${t('fichaAceptada.errorCarga')} ${mensajeDeError(error, t)}`} onReintentar={() => void (ficha.isError ? ficha.refetch() : vinculo.refetch())} />
      </>
    );
  }
  if (!ficha.data || !vinculo.data) return null;

  const datos = ficha.data;
  const resumen = resumirFichaAceptada(datos);

  return (
    <>
      <EncabezadoPagina
        antes={volver}
        titulo={tercero.data?.nombre ?? t('fichaAceptada.titulo')}
        tituloCompleto={tercero.data?.nombre}
        subtitulo={
          <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', mt: 0.25, fontSize: 13, flexWrap: 'wrap' }}>
            {tercero.data && <Mono>{identificacionDe(tercero.data)}</Mono>}
            {tercero.data && <span aria-hidden>·</span>}
            <span>{vinculo.data.tipoVinculo.nombre}</span>
            <span aria-hidden>·</span>
            <span>{t('fichaAceptada.titulo')}</span>
          </Stack>
        }
      />
      {resumen.pendientes > 0 && (
        <Alert severity="warning" sx={{ mb: 2 }}>
          <AlertTitle>{t('fichaAceptada.aviso.titulo', { count: resumen.pendientes })}</AlertTitle>
          {t('fichaAceptada.aviso.texto')}
        </Alert>
      )}
      <Box sx={REJILLA}>
        <Stack component="section" aria-label={t('fichaAceptada.titulo')} spacing={1.5} sx={{ minWidth: 0 }}>
          {datos.secciones.length === 0 ? (
            <EstadoVacio titulo={t('fichaAceptada.vacia.titulo')} texto={t('fichaAceptada.vacia.texto')} />
          ) : (
            datos.secciones.map((s) => <TarjetaSeccionAceptada key={`${s.seccion}-${String(s.periodo)}`} empresaId={empresa.id} seccion={s} />)
          )}
        </Stack>
        <Tarjeta titulo={t('fichaAceptada.resumen.titulo')} etiqueta={t('fichaAceptada.resumen.titulo')}>
          <Stack spacing={1}>
            <Typography variant="body2">{t('fichaAceptada.resumen.aceptadas', { count: resumen.aceptadas })}</Typography>
            <Typography variant="body2" sx={{ color: resumen.pendientes > 0 ? 'estado.advertencia.texto' : 'text.secondary' }}>
              {resumen.pendientes > 0 ? t('fichaAceptada.resumen.pendientes', { count: resumen.pendientes }) : t('fichaAceptada.resumen.alDia')}
            </Typography>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {t('fichaAceptada.resumen.ayuda')}
            </Typography>
            <Box>
              <Button component={RouterLink} to={`/e/${empresa.id}/terceros/${datos.terceroId}`} variant="outlined" size="small">
                {t('fichaAceptada.resumen.verTercero')}
              </Button>
            </Box>
          </Stack>
        </Tarjeta>
      </Box>
    </>
  );
}
