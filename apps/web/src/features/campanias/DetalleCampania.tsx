import { Box, Button, Stack } from '@mui/material';
import { esErrorApi, SCOPES, type CampaniaDto } from '@validasoft/api-client';
import { BarraFiltros, Dato, EsqueletoTarjeta, EstadoVacio, FiltroMenu, formatearFecha, PaginaEstado, RejillaDatos, TablaConCursor, usePaginacionCursor } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { Ban, Download, Pause, Play, RotateCcw, SearchX } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';
import { DialogoConfirmacion, EncabezadoPagina, EnlaceVolver, ErrorRecuperable, SinPermiso, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { guardarArchivo } from '../vencimientos/api';
import { useTiposVinculo } from '../vinculos/api';
import { useAccionCampania, useCampania, useDescargarInformeCampania, useFilasCampania } from './api';
import { AvanceCampania, ChipCampania } from './componentes';
import { ESTADOS_FILA, useColumnasFilasCampania } from './columnasFilas';
import { accionesDeCampania, cuantosAfecta, resumenDeFiltro, type AccionDeCampania } from './modelo';

const EXITO: Record<AccionDeCampania, 'pausado' | 'reanudado' | 'cancelado' | 'reintentado'> = {
  pausar: 'pausado',
  reanudar: 'reanudado',
  cancelar: 'cancelado',
  reintentar: 'reintentado',
};

/** El código de «sin filas fallidas» se comparte con los lotes; aquí se explica en términos de campaña. */
function errorDeAccion(error: unknown, t: TFunction): string {
  return esErrorApi(error) && error.codigo === 'sin-filas-fallidas' ? t('campanias.detalle.sinFallidas') : mensajeDeError(error, t);
}

/**
 * Detalle de la campaña: avance en vivo, totales, pausar/reanudar/cancelar y reintento de las fallidas (cada una con su
 * confirmación que dice qué pasará y a quién), filas por estado e informe en Excel.
 */
export function DetalleCampania() {
  const { t } = useTranslation();
  const { campaniaId = '' } = useParams();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const mostrar = useAvisos((s) => s.mostrar);
  const puede = permisos.puede(SCOPES.expedientesLeer);
  const consulta = useCampania(empresa.id, campaniaId, puede);
  const accion = useAccionCampania(empresa.id);
  const descargar = useDescargarInformeCampania(empresa.id);
  const tipos = useTiposVinculo(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const [estado, setEstado] = useState('');
  const [confirmando, setConfirmando] = useState<AccionDeCampania | null>(null);
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;
  useEffect(() => {
    reiniciar();
  }, [estado, reiniciar]);
  const filas = useFilasCampania(empresa.id, consulta.data, { estado: estado || undefined, limite: 50, cursor: paginacion.cursor ?? undefined });

  const columnas = useColumnasFilasCampania(empresa.id);

  if (!puede) return <SinPermiso seccion={t('campanias.titulo')} />;
  const volver = <EnlaceVolver a={`/e/${empresa.id}/vencimientos/campanias`}>{t('campanias.titulo')}</EnlaceVolver>;
  if (consulta.isPending) return <EsqueletoTarjeta alto={240} />;
  if (consulta.isError) {
    return esErrorApi(consulta.error) && consulta.error.esNoEncontrado ? (
      <PaginaEstado icono={SearchX} titulo={t('campanias.detalle.noEncontrado')} texto={t('campanias.detalle.noEncontradoAyuda')} acciones={volver} />
    ) : (
      <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
    );
  }

  const campania: CampaniaDto = consulta.data;
  const acciones = permisos.puede(SCOPES.expedientesAdministrar) ? accionesDeCampania(campania) : [];
  const ejecutar = (nombre: AccionDeCampania) =>
    accion.mutate(
      { accion: nombre, id: campania.id },
      {
        onSuccess: () => mostrar(t(`campanias.detalle.${EXITO[nombre]}`), 'success'),
        onError: (error) => mostrar(errorDeAccion(error, t), 'error'),
        onSettled: () => setConfirmando(null),
      },
    );
  const informe = () =>
    descargar.mutate(campania.id, {
      onSuccess: (a) => guardarArchivo(a, t('campanias.detalle.informeNombre')),
      onError: (error) => mostrar(`${t('campanias.detalle.informeError')} ${mensajeDeError(error, t)}`, 'error'),
    });
  const boton = (nombre: AccionDeCampania) => ({ disabled: accion.isPending, onClick: () => setConfirmando(nombre) }) as const;

  return (
    <>
      <EncabezadoPagina
        antes={volver}
        titulo={campania.nombre}
        subtitulo={<ChipCampania estado={campania.estado} />}
        acciones={
          <>
            {acciones.includes('pausar') && (
              <Button variant="outlined" startIcon={<Pause size={16} aria-hidden />} {...boton('pausar')}>
                {t('campanias.detalle.pausar')}
              </Button>
            )}
            {acciones.includes('reanudar') && (
              <Button variant="contained" startIcon={<Play size={16} aria-hidden />} {...boton('reanudar')}>
                {t('campanias.detalle.reanudar')}
              </Button>
            )}
            {acciones.includes('reintentar') && (
              <Button variant="contained" startIcon={<RotateCcw size={16} aria-hidden />} {...boton('reintentar')}>
                {t('campanias.detalle.reintentar', { count: campania.totales.fallidas })}
              </Button>
            )}
            {acciones.includes('cancelar') && (
              <Button variant="outlined" color="error" startIcon={<Ban size={16} aria-hidden />} {...boton('cancelar')}>
                {t('campanias.detalle.cancelar')}
              </Button>
            )}
            <Button variant="outlined" startIcon={<Download size={16} aria-hidden />} disabled={descargar.isPending} onClick={informe}>
              {t('campanias.detalle.informe')}
            </Button>
          </>
        }
      />
      <Stack spacing={2}>
        <Tarjeta>
          <Box sx={{ mb: 2 }}>
            <AvanceCampania campania={campania} ancho="min(480px, 60vw)" />
          </Box>
          <RejillaDatos>
            <Dato etiqueta={t('campanias.detalle.totales.exitosas')}>{campania.totales.exitosas}</Dato>
            <Dato etiqueta={t('campanias.detalle.totales.fallidas')}>{campania.totales.fallidas}</Dato>
            <Dato etiqueta={t('campanias.detalle.totales.pendientes')}>{campania.totales.pendientes}</Dato>
            <Dato etiqueta={t('campanias.detalle.totales.omitidas')}>{campania.totales.omitidas}</Dato>
            <Dato etiqueta={t('campanias.detalle.filtro')}>
              {resumenDeFiltro(campania.filtro, tipos.data?.find((x) => x.id === campania.filtro.tipoVinculoId)?.nombre ?? null, t)}
            </Dato>
            <Dato etiqueta={t('campanias.columnas.creada')}>{t('campanias.detalle.creada', { fecha: formatearFecha(campania.creadaEn) })}</Dato>
          </RejillaDatos>
        </Tarjeta>
        <BarraFiltros>
          <FiltroMenu
            etiqueta={t('campanias.detalle.filtroEstado')}
            textoTodos={t('comun.todos')}
            valor={estado}
            onCambiar={setEstado}
            opciones={ESTADOS_FILA.map((e) => ({ valor: e, etiqueta: t(`campanias.detalle.estadosFila.${e}`) }))}
          />
        </BarraFiltros>
        <TablaConCursor
          etiqueta={t('campanias.detalle.filas')}
          columnas={columnas}
          filas={filas.data?.datos}
          claveFila={(f) => String(f.n)}
          filasAltas
          cargando={filas.isPending}
          error={filas.isError ? `${t('campanias.detalle.errorFilas')} ${mensajeDeError(filas.error, t)}` : null}
          onReintentar={() => void filas.refetch()}
          paginacion={paginacion}
          siguienteCursor={filas.data?.siguienteCursor}
          vacio={<EstadoVacio compacto titulo={t('campanias.detalle.sinFilas')} />}
        />
      </Stack>
      {confirmando && (
        <DialogoConfirmacion
          tono={confirmando === 'cancelar' ? 'error' : 'primary'}
          titulo={t(`campanias.detalle.confirmar.${confirmando}.titulo`)}
          mensaje={t(`campanias.detalle.confirmar.${confirmando}.texto`, { count: cuantosAfecta(confirmando, campania.totales) })}
          confirmar={t(`campanias.detalle.confirmar.${confirmando}.confirmar`)}
          ocupado={accion.isPending}
          onConfirmar={() => ejecutar(confirmando)}
          onCerrar={() => setConfirmando(null)}
        />
      )}
    </>
  );
}
