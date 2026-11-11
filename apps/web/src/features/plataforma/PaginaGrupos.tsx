import { Box, Button, Skeleton, Stack, Typography } from '@mui/material';
import type { GrupoDto } from '@validasoft/api-client';
import { BarraFiltros, CampoBusqueda, EstadoVacio, TablaEsqueleto } from '@validasoft/ui';
import { Building2, Plus, SearchX } from 'lucide-react';
import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useSearchParams } from 'react-router';
import { EncabezadoPagina, ErrorRecuperable } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { ListaVirtual } from '../../shared/ListaVirtual';
import { useBusquedaEnUrl } from '../../shared/useBusquedaEnUrl';
import { useGrupos } from './api';
import { ALTO_ESTIMADO_GRUPO, ESPERA_BUSQUEDA_GRUPOS_MS } from './modelo';
import { TarjetaGrupo } from './TarjetaGrupo';

const SEPARACION_PX = 16;
const claveDeGrupo = (g: GrupoDto) => g.tenant.id;

/** Lo que se ve mientras llegan los grupos: una tarjeta con su título y la tabla de sus empresas (como la real). */
function EsqueletoGrupo() {
  return (
    <Box sx={{ py: 1.5, px: 2, border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.md)}px`, bgcolor: 'background.paper' }}>
      <Skeleton variant="rounded" width={220} height={16} sx={{ mb: 1.5, maxWidth: '60%' }} />
      <TablaEsqueleto columnas={5} filas={2} />
    </Box>
  );
}

/** Los grupos (tenants) con sus empresas y el estado del plan de IA de cada una: scroll infinito y buscador (PT-9, PT-11). */
export function PaginaGrupos() {
  const { t } = useTranslation();
  const [parametros, setParametros] = useSearchParams();
  const q = (parametros.get('q') ?? '').trim();
  const busqueda = useBusquedaEnUrl(ESPERA_BUSQUEDA_GRUPOS_MS);
  const consulta = useGrupos(q);
  const [desplegados, setDesplegados] = useState<ReadonlySet<string>>(new Set());

  // Una página nueva puede repetir un grupo si se creó otro mientras se desplazaba (el cursor es por posición).
  const grupos = useMemo(() => {
    const vistos = new Set<string>();
    return (consulta.data?.pages ?? []).flatMap((p) => p.datos).filter((g) => !vistos.has(g.tenant.id) && vistos.add(g.tenant.id));
  }, [consulta.data]);
  const alternar = useCallback(
    (id: string) =>
      setDesplegados((previos) => {
        const nuevos = new Set(previos);
        if (!nuevos.delete(id)) nuevos.add(id);
        return nuevos;
      }),
    [],
  );
  const limpiar = () => setParametros(new URLSearchParams(), { replace: true });
  const { fetchNextPage } = consulta;
  const cargarMas = useCallback(() => void fetchNextPage(), [fetchNextPage]);

  return (
    <>
      <EncabezadoPagina
        titulo={t('plataforma.grupos.titulo')}
        subtitulo={t('plataforma.grupos.subtitulo')}
        acciones={
          <Button component={RouterLink} to="/plataforma/grupos/nuevo" variant="contained" startIcon={<Plus size={16} aria-hidden />}>
            {t('plataforma.grupos.nuevo')}
          </Button>
        }
      />
      <BarraFiltros>
        <CampoBusqueda valor={busqueda.texto} onCambiar={busqueda.setTexto} placeholder={t('plataforma.grupos.buscar')} ancho={380} />
      </BarraFiltros>
      {consulta.isError && !consulta.data && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && (
        <Stack spacing={`${String(SEPARACION_PX)}px`} role="status" aria-busy="true" aria-label={t('plataforma.grupos.cargando')}>
          {[0, 1, 2].map((i) => (
            <EsqueletoGrupo key={i} />
          ))}
        </Stack>
      )}
      {consulta.isSuccess && grupos.length === 0 && (
        <EstadoVacio
          icono={q ? SearchX : Building2}
          titulo={q ? t('plataforma.grupos.vacioBusqueda', { q }) : t('plataforma.grupos.vacio')}
          texto={q ? t('plataforma.grupos.vacioBusquedaAyuda') : t('plataforma.grupos.vacioAyuda')}
          accion={
            q ? (
              <Button variant="outlined" onClick={limpiar}>
                {t('plataforma.grupos.limpiarBusqueda')}
              </Button>
            ) : undefined
          }
        />
      )}
      {grupos.length > 0 && (
        <Box aria-busy={consulta.isPlaceholderData} sx={{ opacity: consulta.isPlaceholderData ? 0.6 : 1, transition: 'opacity 120ms' }}>
          <ListaVirtual
            filas={grupos}
            claveFila={claveDeGrupo}
            altoEstimado={ALTO_ESTIMADO_GRUPO}
            separacion={SEPARACION_PX}
            etiqueta={t('plataforma.grupos.lista')}
            renderFila={(g) => <TarjetaGrupo grupo={g} expandido={desplegados.has(g.tenant.id)} onAlternar={() => alternar(g.tenant.id)} />}
            hayMas={consulta.hasNextPage}
            cargandoMas={consulta.isFetchingNextPage}
            errorAlCargarMas={consulta.isFetchNextPageError}
            alCargarMas={cargarMas}
            textoCargandoMas={t('plataforma.grupos.cargandoMas')}
            esqueletoCargandoMas={<EsqueletoGrupo />}
            textoErrorMas={t('plataforma.grupos.errorMas')}
            textoReintentar={t('comun.reintentar')}
          />
          {!consulta.hasNextPage && (
            <Typography variant="caption" role="status" sx={{ display: 'block', textAlign: 'center', color: 'text.secondary', pt: 2 }}>
              {t('plataforma.grupos.fin', { count: grupos.length })}
            </Typography>
          )}
        </Box>
      )}
    </>
  );
}
