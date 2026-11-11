import { Box, Button, Checkbox, Typography } from '@mui/material';
import { EstadoAreaExpediente, SCOPES, type FilaBandejaDto } from '@validasoft/api-client';
import {
  BarraFiltros,
  EstadoVacio,
  FiltroMenu,
  formatearFecha,
  TablaConCursor,
  usePaginacionCursor,
  type ColumnaTabla,
} from '@validasoft/ui';
import { FolderClock } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { EncabezadoPagina, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAreas } from '../../configuracion/areas/api';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { AccionesRecordarEnLote } from '../../recordatorios/AccionesRecordarEnLote';
import { esObservado, observadosDe } from '../../recordatorios/modelo';
import { useAperturasPendientes, useBandeja } from '../api';
import { Aprobaciones, ChipEstadoArea, TerceroEnLinea } from '../componentes';
import { DrawerAperturas } from '../components/DrawerAperturas';
import { IndicadorSla } from '../components/IndicadorSla';

const TAMANO_PAGINA = 25;
const ESTADOS_AREA = Object.values(EstadoAreaExpediente);

/**
 * Bandeja de revisión (`GET /bandeja`): una fila por expediente × área. Un usuario delegado solo ve las áreas donde
 * es miembro (FL-8); por defecto se ocultan las áreas cuya etapa aún no empieza. Filtros en la URL.
 */
export function Bandeja() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const [filtros, setFiltros] = useSearchParams();
  const area = filtros.get('area') ?? '';
  const estado = filtros.get('estado') ?? '';
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;
  const [verAperturas, setVerAperturas] = useState(false);
  const esAdministrador = permisos.puede(SCOPES.expedientesAdministrar);
  const puedeRecordar = permisos.puede(SCOPES.vinculosEscribir);
  // Expedientes (no filas: una fila por área) elegidos para recordarles sus observaciones al tercero (NT-2).
  const [seleccion, setSeleccion] = useState<ReadonlySet<string>>(new Set());

  useEffect(() => {
    reiniciar();
    setSeleccion(new Set());
  }, [area, estado, empresa.id, reiniciar]);

  const consulta = useBandeja(empresa.id, { area: area || undefined, estado: estado || undefined, limite: TAMANO_PAGINA, cursor: paginacion.cursor ?? undefined });
  const areas = useAreas(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const aperturas = useAperturasPendientes(empresa.id, esAdministrador);

  const columnas = useMemo<ColumnaTabla<FilaBandejaDto>[]>(
    () => [
      ...(puedeRecordar
        ? [
            {
              id: 'seleccion',
              encabezado: t('bandeja.columnas.seleccion'),
              encabezadoOculto: true,
              ancho: 44,
              celda: (f: FilaBandejaDto) =>
                esObservado(f) ? (
                  <Checkbox
                    size="small"
                    checked={seleccion.has(f.expedienteId)}
                    onClick={(e) => e.stopPropagation()}
                    onChange={() =>
                      setSeleccion((previa) => {
                        const nueva = new Set(previa);
                        if (nueva.has(f.expedienteId)) nueva.delete(f.expedienteId);
                        else nueva.add(f.expedienteId);
                        return nueva;
                      })
                    }
                    slotProps={{ input: { 'aria-label': t('recordatorios.lote.seleccionar', { folio: f.folio, tercero: f.tercero.nombre }) } }}
                    sx={{ p: 0.5 }}
                  />
                ) : null,
            },
          ]
        : []),
      { id: 'folio', encabezado: t('bandeja.columnas.folio'), ancho: 170, celda: (f) => <Typography variant="monoTabla">{f.folio}</Typography> },
      { id: 'tercero', encabezado: t('bandeja.columnas.tercero'), celda: (f) => <TerceroEnLinea tercero={f.tercero} /> },
      { id: 'tipo', encabezado: t('bandeja.columnas.tipo'), ancho: 110, celda: (f) => f.tipoVinculo.nombre },
      { id: 'area', encabezado: t('bandeja.columnas.area'), ancho: 140, celda: (f) => f.areaNombre },
      {
        id: 'estado',
        encabezado: t('bandeja.columnas.estado'),
        ancho: 220,
        celda: (f) => (
          <Box sx={{ display: 'inline-flex', alignItems: 'center', gap: 1 }}>
            <ChipEstadoArea estado={f.estadoArea} desde={f.desde} />
            {f.estadoExpediente === 'Observado' && f.estadoArea !== 'Observada' && (
              <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12, whiteSpace: 'nowrap' }}>
                {t('bandeja.devuelto')}
              </Typography>
            )}
            {f.desaprobado && (
              <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12, whiteSpace: 'nowrap' }}>
                {t('bandeja.reabierto')}
              </Typography>
            )}
          </Box>
        ),
      },
      {
        // Plazo de revisión del área (OP-12): a tiempo, por vencer o vencido, y cuándo vence; vacío si el área no tiene plazo.
        id: 'plazo',
        encabezado: t('sla.indicador.columna'),
        ancho: 250,
        celda: (f) => <IndicadorSla sla={f.sla} vence={f.slaVence} />,
      },
      {
        id: 'aprobaciones',
        encabezado: t('bandeja.columnas.aprobaciones'),
        ancho: 130,
        celda: (f) => <Aprobaciones aprobaciones={f.aprobaciones} requeridas={f.aprobadoresRequeridos} />,
      },
      {
        // Documentos que el área aún debe decidir (flujos con requisitos, RQ-4); «—» en un flujo sin requisitos.
        id: 'documentos',
        encabezado: t('bandeja.columnas.documentos'),
        ancho: 110,
        celda: (f) =>
          (f.requisitosPendientes ?? 0) > 0 ? (
            <Typography variant="monoTabla" aria-label={t('bandeja.documentosAccesible', { count: f.requisitosPendientes ?? 0 })}>
              {t('bandeja.documentos', { count: f.requisitosPendientes ?? 0 })}
            </Typography>
          ) : (
            <Typography variant="monoTabla" sx={{ color: 'text.secondary' }}>
              —
            </Typography>
          ),
      },
      {
        id: 'desde',
        encabezado: t('bandeja.columnas.desde'),
        ancho: 100,
        celda: (f) => <Typography variant="monoTabla">{f.desde ? formatearFecha(f.desde) : '—'}</Typography>,
      },
    ],
    [t, puedeRecordar, seleccion],
  );

  if (!permisos.puede(SCOPES.expedientesLeer)) return <SinPermiso seccion={t('nav.bandeja')} />;

  const cambiarFiltro = (clave: 'area' | 'estado', valor: string) => {
    setFiltros(
      (previos) => {
        const nuevos = new URLSearchParams(previos);
        if (valor) nuevos.set(clave, valor);
        else nuevos.delete(clave);
        return nuevos;
      },
      { replace: true },
    );
  };
  const hayFiltros = Boolean(area || estado);
  const limpiar = () => setFiltros(new URLSearchParams(), { replace: true });
  const pendientes = aperturas.data?.length ?? 0;
  const observadosEnPagina = observadosDe(consulta.data?.datos ?? []);

  return (
    <>
      <EncabezadoPagina
        titulo={t('bandeja.titulo')}
        subtitulo={t('bandeja.subtitulo', { empresa: empresa.nombre })}
        acciones={
          <>
            {puedeRecordar && observadosEnPagina.length > 0 && (
              <Button variant="outlined" onClick={() => setSeleccion(seleccion.size > 0 ? new Set() : new Set(observadosEnPagina))}>
                {seleccion.size > 0 ? t('recordatorios.lote.quitarSeleccion') : t('recordatorios.lote.seleccionarObservados')}
              </Button>
            )}
            {puedeRecordar && (
              <AccionesRecordarEnLote
                empresaId={empresa.id}
                empresaNombre={empresa.nombre}
                seleccion={seleccion}
                areaId={area}
                areaNombre={areas.data?.find((a) => a.id === area)?.nombre}
                onTerminado={() => setSeleccion(new Set())}
              />
            )}
            {esAdministrador && pendientes > 0 && (
              <Button variant="outlined" color="warning" startIcon={<FolderClock size={16} aria-hidden />} onClick={() => setVerAperturas(true)}>
                {t('bandeja.aperturas.boton', { count: pendientes })}
              </Button>
            )}
          </>
        }
      />

      <BarraFiltros>
        {areas.data && (
          <FiltroMenu
            etiqueta={t('bandeja.filtros.area')}
            textoTodos={t('comun.todas')}
            valor={area}
            onCambiar={(v) => cambiarFiltro('area', v)}
            opciones={areas.data.filter((a) => a.activa).map((a) => ({ valor: a.id, etiqueta: a.nombre }))}
          />
        )}
        <FiltroMenu
          etiqueta={t('bandeja.filtros.estado')}
          textoTodos={t('comun.todos')}
          valor={estado}
          onCambiar={(v) => cambiarFiltro('estado', v)}
          opciones={ESTADOS_AREA.map((e) => ({ valor: e, etiqueta: t(`bandeja.estadosArea.${e}`), contenido: <ChipEstadoArea estado={e} /> }))}
        />
        {hayFiltros && (
          <Button variant="text" onClick={limpiar}>
            {t('bandeja.filtros.limpiar')}
          </Button>
        )}
      </BarraFiltros>

      <TablaConCursor
        etiqueta={t('bandeja.titulo')}
        columnas={columnas}
        filas={consulta.data?.datos}
        claveFila={(f) => `${f.expedienteId}|${f.areaId}`}
        cargando={consulta.isPending || (consulta.isFetching && consulta.isPlaceholderData)}
        error={consulta.isError ? `${t('bandeja.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
        onReintentar={() => void consulta.refetch()}
        paginacion={paginacion}
        siguienteCursor={consulta.data?.siguienteCursor}
        onFila={(f) => void navegar(`/e/${empresa.id}/expedientes/${f.expedienteId}`)}
        etiquetaFila={(f) => t('bandeja.abrir', { folio: f.folio, tercero: f.tercero.nombre, area: f.areaNombre })}
        vacio={
          <EstadoVacio
            compacto
            titulo={hayFiltros ? t('bandeja.vacioFiltros') : t('bandeja.vacio')}
            texto={hayFiltros ? undefined : t('bandeja.vacioAyuda')}
            accion={
              hayFiltros ? (
                <Button variant="outlined" onClick={limpiar}>
                  {t('bandeja.filtros.limpiar')}
                </Button>
              ) : undefined
            }
          />
        }
      />

      {verAperturas && <DrawerAperturas empresaId={empresa.id} vinculos={aperturas.data ?? []} onCerrar={() => setVerAperturas(false)} />}
    </>
  );
}
