import { Alert, AlertTitle, Box, Button, Checkbox, Tooltip, Typography } from '@mui/material';
import { SCOPES, type FalloDeLoteDto, type RevalidacionDto } from '@validasoft/api-client';
import { BarraFiltros, ChipEstado, EstadoVacio, FiltroMenu, formatearFecha, TablaConCursor, usePaginacionCursor, type ColumnaTabla } from '@validasoft/ui';
import { CheckCheck } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { DialogoConfirmacion, EncabezadoPagina, SinPermiso } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useAreas } from '../configuracion/areas/api';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { TerceroEnLinea } from '../expedientes/componentes';
import { useAprobarRevalidaciones, useRevalidaciones } from './api';
import { claveRevalidacion, esRevalidacionDeSeccion, lote, seleccionables, tonoPlazo } from './modelo';

const TAMANO_PAGINA = 25;
/** Plazo de la revalidación (RV-4, `Revalidacion.DiasDePlazo`). */
const DIAS_DE_PLAZO = 15;

/**
 * Bandeja de revalidaciones (RV-5): documentos que el tercero actualizó después de que la empresa los aprobó, con la
 * versión aceptada, la nueva, la comparación de la bóveda y el plazo. Las que no cambiaron se aprueban en lote (A-18).
 */
export function PaginaRevalidaciones() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.expedientesLeer);
  const puedeRevisar = permisos.puede(SCOPES.expedientesRevisar);
  const mostrar = useAvisos((s) => s.mostrar);
  const [filtros, setFiltros] = useSearchParams();
  const area = filtros.get('area') ?? '';
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;
  const [seleccion, setSeleccion] = useState<ReadonlySet<string>>(new Set());
  const [confirmando, setConfirmando] = useState(false);
  const [fallidas, setFallidas] = useState<readonly FalloDeLoteDto[]>([]);

  useEffect(() => {
    reiniciar();
    setSeleccion(new Set());
  }, [area, empresa.id, reiniciar]);

  const consulta = useRevalidaciones(empresa.id, { area: area || undefined, limite: TAMANO_PAGINA, cursor: paginacion.cursor ?? undefined }, puede);
  const areas = useAreas(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const aprobar = useAprobarRevalidaciones(empresa.id);
  const filas = useMemo(() => consulta.data?.datos ?? [], [consulta.data]);
  const aprobables = seleccionables(filas);
  const porAprobar = lote(filas, seleccion);
  const hayAprobables = aprobables.length > 0;

  const alternar = (f: RevalidacionDto) =>
    setSeleccion((previa) => {
      const nueva = new Set(previa);
      const clave = claveRevalidacion(f);
      if (nueva.has(clave)) nueva.delete(clave);
      else nueva.add(clave);
      return nueva;
    });

  const columnas = useMemo<ColumnaTabla<RevalidacionDto>[]>(
    () => [
      ...(puedeRevisar && hayAprobables
        ? [
            {
              id: 'seleccion',
              encabezado: t('revalidaciones.columnas.seleccion'),
              encabezadoOculto: true,
              ancho: 44,
              celda: (f: RevalidacionDto) => (
                <Checkbox
                  size="small"
                  checked={seleccion.has(claveRevalidacion(f))}
                  disabled={!f.aprobableEnLote}
                  onClick={(e) => e.stopPropagation()}
                  onChange={() => alternar(f)}
                  slotProps={{ input: { 'aria-label': t('revalidaciones.seleccionar', { documento: f.tipoDocumento, tercero: f.tercero.nombre }) } }}
                  sx={{ p: 0.5 }}
                />
              ),
            },
          ]
        : []),
      { id: 'tercero', encabezado: t('revalidaciones.columnas.tercero'), celda: (f) => <TerceroEnLinea tercero={f.tercero} /> },
      {
        id: 'documento',
        encabezado: t('revalidaciones.columnas.documento'),
        ancho: 200,
        // Una sección de la información (F2) se ve como un documento, con el nombre de la sección.
        celda: (f) =>
          esRevalidacionDeSeccion(f) ? (
            <Box>
              <Typography component="span" variant="body2" sx={{ display: 'block' }}>
                {f.tipoDocumento}
              </Typography>
              <Typography component="span" variant="caption" sx={{ display: 'block', color: 'text.secondary' }}>
                {t('fichaEmpresa.revalidacion.seccion')}
              </Typography>
            </Box>
          ) : (
            f.tipoDocumento
          ),
      },
      {
        id: 'versiones',
        encabezado: t('revalidaciones.columnas.versiones'),
        ancho: 110,
        celda: (f) =>
          f.versionAceptada ? (
            <Typography variant="monoTabla" aria-label={t('revalidaciones.versionesAccesible', { antes: String(f.versionAceptada.numero), actual: String(f.versionActual.numero) })}>
              {t('revalidaciones.versiones', { antes: String(f.versionAceptada.numero), actual: String(f.versionActual.numero) })}
            </Typography>
          ) : (
            <Typography variant="monoTabla">{t('revalidaciones.soloActual', { actual: String(f.versionActual.numero) })}</Typography>
          ),
      },
      {
        id: 'comparacion',
        encabezado: t('revalidaciones.columnas.comparacion'),
        ancho: 130,
        celda: (f) =>
          esRevalidacionDeSeccion(f) ? (
            <Tooltip title={t('fichaEmpresa.revalidacion.cambiosAyuda')}>
              <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
                {t('fichaEmpresa.revalidacion.cambios')}
              </Typography>
            </Tooltip>
          ) : (
            <Tooltip title={t(`revalidaciones.comparacionAyuda.${f.comparacion as 'Distinta'}`, { defaultValue: '' })}>
              <Typography component="span" variant="body2" color={f.aprobableEnLote ? 'text.primary' : 'text.secondary'}>
                {t(`revalidaciones.comparaciones.${f.comparacion as 'Distinta'}`, { defaultValue: f.comparacion })}
              </Typography>
            </Tooltip>
          ),
      },
      {
        id: 'plazo',
        encabezado: t('revalidaciones.columnas.plazo'),
        ancho: 190,
        celda: (f) => {
          const tono = tonoPlazo(f);
          if (tono === 'bloqueante') return <ChipEstado estado="ConPendientes" bloqueante etiqueta={t('revalidaciones.bloquea')} fecha={f.plazoHasta} />;
          return (
            <Tooltip title={t('revalidaciones.hasta', { fecha: formatearFecha(f.plazoHasta) })}>
              <Typography component="span" variant="monoTabla" color={tono === 'urgente' ? 'warning.main' : 'text.primary'}>
                {t('revalidaciones.quedan', { count: f.diasRestantes })}
              </Typography>
            </Tooltip>
          );
        },
      },
      { id: 'folio', encabezado: t('revalidaciones.columnas.folio'), ancho: 170, celda: (f) => <Typography variant="monoTabla">{f.folio}</Typography> },
    ],
    [t, seleccion, puedeRevisar, hayAprobables],
  );

  if (!puede) return <SinPermiso seccion={t('nav.revalidaciones')} />;

  const cambiarArea = (valor: string) =>
    setFiltros(valor ? new URLSearchParams({ area: valor }) : new URLSearchParams(), { replace: true });
  const confirmar = () =>
    aprobar.mutate(
      { revalidaciones: porAprobar },
      {
        onSuccess: (r) => {
          if (r.aprobadas > 0) mostrar(t('revalidaciones.aprobadas', { count: r.aprobadas }));
          setFallidas(r.fallidas);
          setSeleccion(new Set());
        },
        onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
        onSettled: () => setConfirmando(false),
      },
    );
  const nombreDe = (f: FalloDeLoteDto) => filas.find((x) => claveRevalidacion(x) === claveRevalidacion(f));

  return (
    <>
      <EncabezadoPagina
        titulo={t('revalidaciones.titulo')}
        subtitulo={t('revalidaciones.subtitulo', { empresa: empresa.nombre, dias: String(DIAS_DE_PLAZO) })}
        acciones={
          puedeRevisar && aprobables.length > 0 ? (
            <Box sx={{ display: 'flex', gap: 1 }}>
              <Button variant="outlined" onClick={() => setSeleccion(porAprobar.length > 0 ? new Set() : new Set(aprobables.map(claveRevalidacion)))}>
                {porAprobar.length > 0 ? t('revalidaciones.quitarSeleccion') : t('revalidaciones.seleccionarTodas')}
              </Button>
              <Button variant="contained" startIcon={<CheckCheck size={16} aria-hidden />} disabled={porAprobar.length === 0} onClick={() => setConfirmando(true)}>
                {t('revalidaciones.aprobar', { count: porAprobar.length })}
              </Button>
            </Box>
          ) : undefined
        }
      />

      {fallidas.length > 0 && (
        <Alert severity="warning" onClose={() => setFallidas([])} sx={{ mb: 1.5 }}>
          <AlertTitle>{t('revalidaciones.fallidasTitulo', { count: fallidas.length })}</AlertTitle>
          {fallidas.map((f) => (
            <Box key={claveRevalidacion(f)}>
              {nombreDe(f) ? `${nombreDe(f)?.tipoDocumento ?? ''} · ${nombreDe(f)?.tercero.nombre ?? ''}: ` : ''}
              {t(`errores.${f.codigo as 'revalidacion-con-cambios'}`, { defaultValue: f.detalle })}
            </Box>
          ))}
        </Alert>
      )}
      {puedeRevisar && filas.length > 0 && aprobables.length === 0 && (
        <Alert severity="info" sx={{ mb: 1.5 }}>
          {t('revalidaciones.sinLote')}
        </Alert>
      )}

      {areas.data && (
        <BarraFiltros>
          <FiltroMenu
            etiqueta={t('revalidaciones.filtros.area')}
            textoTodos={t('comun.todas')}
            valor={area}
            onCambiar={cambiarArea}
            opciones={areas.data.filter((a) => a.activa).map((a) => ({ valor: a.id, etiqueta: a.nombre }))}
          />
          {area && (
            <Button variant="text" onClick={() => cambiarArea('')}>
              {t('revalidaciones.filtros.limpiar')}
            </Button>
          )}
        </BarraFiltros>
      )}

      <TablaConCursor
        etiqueta={t('revalidaciones.titulo')}
        columnas={columnas}
        filas={consulta.data?.datos}
        claveFila={claveRevalidacion}
        cargando={consulta.isPending || (consulta.isFetching && consulta.isPlaceholderData)}
        error={consulta.isError ? `${t('revalidaciones.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
        onReintentar={() => void consulta.refetch()}
        paginacion={paginacion}
        siguienteCursor={consulta.data?.siguienteCursor}
        onFila={(f) => void navegar(`/e/${empresa.id}/expedientes/${f.expedienteId}`)}
        etiquetaFila={(f) => t('revalidaciones.abrir', { folio: f.folio, documento: f.tipoDocumento, tercero: f.tercero.nombre })}
        vacio={
          <EstadoVacio
            compacto
            titulo={area ? t('revalidaciones.vacioFiltros') : t('revalidaciones.vacio')}
            texto={area ? undefined : t('revalidaciones.vacioAyuda')}
          />
        }
      />

      {confirmando && (
        <DialogoConfirmacion
          tono="primary"
          titulo={t('revalidaciones.confirmarTitulo', { count: porAprobar.length })}
          mensaje={t('revalidaciones.confirmarTexto')}
          confirmar={t('revalidaciones.aprobar', { count: porAprobar.length })}
          ocupado={aprobar.isPending}
          onConfirmar={confirmar}
          onCerrar={() => setConfirmando(false)}
        />
      )}
    </>
  );
}
