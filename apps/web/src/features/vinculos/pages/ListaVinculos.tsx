import { Box, Button, Chip, Stack, Typography } from '@mui/material';
import { EstadoVinculo, esErrorApi, SCOPES, type VinculoDto } from '@validasoft/api-client';
import {
  APARIENCIA_ESTADO,
  BarraFiltros,
  CampoBusqueda,
  ChipEstado,
  EstadoVacio,
  FiltroMenu,
  formatearFecha,
  ReferenciaExterna,
  TablaConCursor,
  usePaginacionCursor,
  type ColumnaTabla,
} from '@validasoft/ui';
import { ClipboardCheck, FileUp, Search } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate, useSearchParams } from 'react-router';
import { useSesion } from '../../../sesion/sesionStore';
import { EncabezadoPagina, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { buscarPorReferencia, useTiposVinculo, useVinculos } from '../api';
import { NombreTercero } from '../components/NombreTercero';
import { EN_REVISION, estadoVisibleDelVinculo, parametrosDeEstado, type EstadoVisibleDelVinculo } from '../estadoVisible';
import { interpretarReferencia } from '../referencia';

const TAMANO_PAGINA = 20;
// «En revisión» (VR-1) es una situación del vínculo, no un estado de dominio: va entre «En validación» y «Activo».
const ESTADOS: EstadoVisibleDelVinculo[] = Object.values(EstadoVinculo).flatMap((e) => (e === EstadoVinculo.Activo ? [EN_REVISION, e] : [e]));

/** Bandeja de vínculos de la empresa activa; los filtros viven en la URL. */
export function ListaVinculos() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const sistemaSesion = useSesion((s) => s.sesion?.sistema ?? null);
  const mostrar = useAvisos((s) => s.mostrar);
  const [filtros, setFiltros] = useSearchParams();
  const estado = filtros.get('estado') ?? '';
  const tipoVinculo = filtros.get('tipo') ?? '';
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;
  const [busqueda, setBusqueda] = useState('');
  const [sinCoincidencia, setSinCoincidencia] = useState<string | null>(null);

  useEffect(() => {
    reiniciar();
  }, [estado, tipoVinculo, empresa.id, reiniciar]);

  const consulta = useVinculos(empresa.id, {
    ...parametrosDeEstado(estado),
    tipoVinculo: tipoVinculo || undefined,
    limite: TAMANO_PAGINA,
    cursor: paginacion.cursor ?? undefined,
  });
  const tipos = useTiposVinculo(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const conFicha = permisos.puede(SCOPES.tercerosLeer);

  const columnas = useMemo<ColumnaTabla<VinculoDto>[]>(
    () => [
      { id: 'tercero', encabezado: t('vinculos.columnas.tercero'), celda: (v) => <NombreTercero terceroId={v.terceroId} conFicha={conFicha} /> },
      { id: 'tipo', encabezado: t('vinculos.columnas.tipo'), ancho: 120, celda: (v) => v.tipoVinculo.nombre },
      {
        id: 'estado',
        encabezado: t('vinculos.columnas.estado'),
        ancho: 280,
        celda: (v) => (
          <Stack direction="row" spacing={0.75} sx={{ alignItems: 'center' }}>
            <ChipEstado estado={estadoVisibleDelVinculo(v)} fecha={v.estado === 'Bloqueado' ? v.bloqueo?.desde : v.actualizadoEn} />
            {/* Decisión AI: la invitación espera la aprobación de otra persona; el estado del vínculo no cambia. */}
            {v.invitacionPorAprobar?.estado === 'Pendiente' && <Chip size="small" variant="outlined" color="warning" label={t('vinculos.invitacionPorAprobar')} />}
          </Stack>
        ),
      },
      {
        id: 'referencias',
        encabezado: t('vinculos.columnas.referencias'),
        celda: (v) =>
          v.referencias.length === 0 ? (
            <Typography component="span" sx={{ color: 'text.secondary' }} aria-label={t('vinculos.sinReferencias')}>
              —
            </Typography>
          ) : (
            <Box sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
              {v.referencias.map((r) => (
                <ReferenciaExterna key={`${r.sistema}|${r.id}`} sistema={r.sistema} id={r.id} />
              ))}
            </Box>
          ),
      },
      {
        id: 'creado',
        encabezado: t('vinculos.columnas.creado'),
        ancho: 110,
        celda: (v) => <Typography variant="monoTabla">{formatearFecha(v.creadoEn)}</Typography>,
      },
    ],
    [t, conFicha],
  );

  if (!permisos.puede(SCOPES.vinculosLeer)) return <SinPermiso seccion={t('nav.vinculos')} />;

  const cambiarFiltro = (clave: 'estado' | 'tipo', valor: string) => {
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

  const buscar = async (texto: string) => {
    setSinCoincidencia(null);
    if (!texto) return;
    const referencia = interpretarReferencia(texto, sistemaSesion);
    if (!referencia) {
      mostrar(t('vinculos.buscar.formato'), 'warning');
      return;
    }
    try {
      const vinculo = await buscarPorReferencia(empresa.id, referencia.sistema, referencia.id);
      void navegar(vinculo.id);
    } catch (error) {
      if (esErrorApi(error) && error.esNoEncontrado) setSinCoincidencia(texto);
      else mostrar(mensajeDeError(error, t), 'error');
    }
  };

  const limpiarBusqueda = () => {
    setBusqueda('');
    setSinCoincidencia(null);
  };
  const hayFiltros = Boolean(estado || tipoVinculo);

  return (
    <>
      <EncabezadoPagina
        titulo={t('vinculos.titulo')}
        subtitulo={t('vinculos.subtitulo', { empresa: empresa.nombre })}
        acciones={
          <>
            {/* Decisión AI: solo se ofrece si algún tipo de vínculo exige aprobar la invitación (la campana también lleva a la bandeja). */}
            {tipos.data?.some((tipo) => tipo.requiereAprobacionParaInvitar) && (
              <Button variant="outlined" startIcon={<ClipboardCheck size={16} aria-hidden />} onClick={() => void navegar(`/e/${empresa.id}/invitaciones/por-aprobar`)}>
                {t('invitaciones.porAprobar.irA')}
              </Button>
            )}
            {permisos.puede(SCOPES.vinculosEscribir) && (
              <Button variant="outlined" startIcon={<FileUp size={16} aria-hidden />} onClick={() => void navegar(`/e/${empresa.id}/invitaciones/lotes`)}>
                {t('lotes.irALotes')}
              </Button>
            )}
          </>
        }
      />

      <BarraFiltros>
        <CampoBusqueda
          valor={busqueda}
          onCambiar={(valor) => {
            setBusqueda(valor);
            if (!valor) setSinCoincidencia(null);
          }}
          onBuscar={(valor) => void buscar(valor)}
          placeholder={t('vinculos.buscar.placeholder', { sistema: sistemaSesion ?? 'ERP' })}
        />
        <FiltroMenu
          etiqueta={t('vinculos.filtros.estado')}
          textoTodos={t('comun.todos')}
          valor={estado}
          onCambiar={(v) => cambiarFiltro('estado', v)}
          opciones={ESTADOS.map((e) => ({ valor: e, etiqueta: APARIENCIA_ESTADO[e].etiqueta, contenido: <ChipEstado estado={e} /> }))}
        />
        {tipos.data && (
          <FiltroMenu
            etiqueta={t('vinculos.filtros.tipo')}
            textoTodos={t('comun.todos')}
            valor={tipoVinculo}
            onCambiar={(v) => cambiarFiltro('tipo', v)}
            opciones={tipos.data.map((tipo) => ({ valor: tipo.codigo, etiqueta: tipo.nombre }))}
          />
        )}
        {hayFiltros && (
          <Button variant="text" onClick={() => setFiltros(new URLSearchParams(), { replace: true })}>
            {t('vinculos.filtros.limpiar')}
          </Button>
        )}
      </BarraFiltros>

      <TablaConCursor
        etiqueta={t('vinculos.titulo')}
        columnas={columnas}
        filas={sinCoincidencia ? [] : consulta.data?.datos}
        claveFila={(v) => v.id}
        cargando={!sinCoincidencia && (consulta.isPending || (consulta.isFetching && consulta.isPlaceholderData))}
        error={consulta.isError && !sinCoincidencia ? `${t('vinculos.errorLista')} ${mensajeDeError(consulta.error, t)}` : null}
        onReintentar={() => void consulta.refetch()}
        paginacion={paginacion}
        siguienteCursor={sinCoincidencia ? null : consulta.data?.siguienteCursor}
        onFila={(v) => void navegar(v.id)}
        etiquetaFila={(v) => t('vinculos.abrir', { tercero: v.correoTercero })}
        vacio={
          sinCoincidencia ? (
            <EstadoVacio
              compacto
              icono={Search}
              titulo={t('vinculos.buscar.sinResultado', { referencia: sinCoincidencia })}
              texto={t('vinculos.buscar.sinResultadoAyuda', { sistema: sistemaSesion ?? 'ERP' })}
              accion={
                <Button variant="outlined" onClick={limpiarBusqueda}>
                  {t('vinculos.buscar.limpiar')}
                </Button>
              }
            />
          ) : (
            <EstadoVacio
              compacto
              titulo={hayFiltros ? t('vinculos.vacioFiltros') : t('vinculos.vacio', { empresa: empresa.nombre })}
              texto={hayFiltros ? undefined : t('vinculos.vacioAyuda')}
              accion={
                hayFiltros ? (
                  <Button variant="outlined" onClick={() => setFiltros(new URLSearchParams(), { replace: true })}>
                    {t('vinculos.filtros.limpiar')}
                  </Button>
                ) : undefined
              }
            />
          )
        }
      />
    </>
  );
}
