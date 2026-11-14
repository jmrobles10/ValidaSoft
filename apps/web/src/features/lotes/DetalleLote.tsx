import { Alert, Box, Button, Chip, Link, Stack, Typography } from '@mui/material';
import { esErrorApi, SCOPES, type EstadoFilaLote, type FilaLoteDto } from '@validasoft/api-client';
import { BarraFiltros, Dato, EsqueletoTarjeta, EstadoVacio, FiltroMenu, formatearFecha, PaginaEstado, RejillaDatos, TablaConCursor, type ColumnaTabla, usePaginacionCursor } from '@validasoft/ui';
import { Ban, CircleCheck, CircleSlash, Download, Pause, Play, RotateCcw, SearchX } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams } from 'react-router';
import { DialogoConfirmacion, EncabezadoPagina, EnlaceVolver, ErrorRecuperable, SinPermiso, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { usePermisos } from '../../shared/usePermisos';
import { useEmpresaActiva } from '../empresas/useEmpresas';
import { useNombresUsuarios } from '../configuracion/usuarios/api';
import { mensajeDeErrorDeDecision, useDecidirLote } from '../invitaciones/api';
import { DialogoRechazo } from '../invitaciones/DialogoRechazo';
import { guardarArchivo } from '../vencimientos/api';
import { useAccionLote, useDescargarLote, useFilasLote, useLote, type AccionLote } from './api';
import { AvanceLote, ChipLote } from './componentes';
import { accionesDeLote, identificacionLegible } from './modelo';

const ESTADOS_FILA: EstadoFilaLote[] = ['Pendiente', 'Exitosa', 'Fallida', 'Duplicada', 'Invalida'];
const TONO_FILA: Record<EstadoFilaLote, 'default' | 'success' | 'error' | 'warning' | 'info'> = {
  Pendiente: 'info',
  Exitosa: 'success',
  Fallida: 'error',
  Duplicada: 'warning',
  Invalida: 'error',
  Cancelada: 'default',
};

/** Detalle del lote: avance en vivo, totales, pausa/reanudación/cancelación, reintento de las fallidas, filas e informe. */
export function DetalleLote() {
  const { t } = useTranslation();
  const { loteId = '' } = useParams();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const mostrar = useAvisos((s) => s.mostrar);
  const consulta = useLote(empresa.id, loteId);
  const accion = useAccionLote(empresa.id);
  const descargar = useDescargarLote(empresa.id);
  const decidir = useDecidirLote(empresa.id);
  const [estado, setEstado] = useState('');
  const [cancelando, setCancelando] = useState(false);
  const [rechazando, setRechazando] = useState(false);
  const paginacion = usePaginacionCursor();
  const { reiniciar } = paginacion;
  useEffect(() => {
    reiniciar();
  }, [estado, reiniciar]);
  // Quién aprobó el lote (decisión AI): su nombre, si se puede resolver.
  const aprobadoPor = consulta.data?.aprobadoPor;
  const nombres = useNombresUsuarios(empresa.id, useMemo(() => (aprobadoPor?.startsWith('Usuario:') ? [aprobadoPor.slice('Usuario:'.length)] : []), [aprobadoPor]));
  const filas = useFilasLote(empresa.id, consulta.data, { estado: estado || undefined, limite: 50, cursor: paginacion.cursor ?? undefined });

  const columnas = useMemo<ColumnaTabla<FilaLoteDto>[]>(
    () => [
      { id: 'n', encabezado: t('lotes.detalle.columnas.fila'), ancho: 64, celda: (f) => <Typography variant="monoTabla">{f.n}</Typography> },
      { id: 'identificacion', encabezado: t('lotes.detalle.columnas.identificacion'), celda: (f) => identificacionLegible(f.identificacion) },
      { id: 'nombre', encabezado: t('lotes.detalle.columnas.nombre'), celda: (f) => f.nombre ?? '—' },
      { id: 'correo', encabezado: t('lotes.detalle.columnas.correo'), celda: (f) => f.correo ?? '—' },
      {
        id: 'estado',
        encabezado: t('lotes.detalle.columnas.estado'),
        ancho: 130,
        celda: (f) => <Chip size="small" variant="outlined" color={TONO_FILA[f.estado]} label={t(`lotes.detalle.estadosFila.${f.estado}`)} />,
      },
      {
        id: 'detalle',
        encabezado: t('lotes.detalle.columnas.detalle'),
        celda: (f) =>
          f.errores.length > 0 ? (
            <Typography component="span" variant="body2" sx={{ color: 'estado.error.texto' }}>
              {f.errores.map((e) => e.mensaje).join(' ')}
            </Typography>
          ) : f.vinculoId ? (
            <Link component={RouterLink} to={`/e/${empresa.id}/vinculos/${f.vinculoId}`} onClick={(e) => e.stopPropagation()}>
              {f.accion ? t(`lotes.detalle.acciones.${f.accion as 'Invitado' | 'Reinvitado' | 'SinCambios'}`) : t('lotes.detalle.verVinculo')}
            </Link>
          ) : f.duplicadaDe ? (
            t('lotes.nuevoLote.duplicadaDe', { n: String(f.duplicadaDe) })
          ) : (
            '—'
          ),
      },
    ],
    [t, empresa.id],
  );

  if (!permisos.puede(SCOPES.vinculosLeer)) return <SinPermiso seccion={t('lotes.titulo')} />;
  const volver = <EnlaceVolver a={`/e/${empresa.id}/invitaciones/lotes`}>{t('lotes.titulo')}</EnlaceVolver>;
  if (consulta.isPending) return <EsqueletoTarjeta alto={240} />;
  if (consulta.isError) {
    return esErrorApi(consulta.error) && consulta.error.esNoEncontrado ? (
      <PaginaEstado icono={SearchX} titulo={t('lotes.detalle.noEncontrado')} texto={t('lotes.detalle.noEncontradoAyuda')} acciones={volver} />
    ) : (
      <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
    );
  }

  const lote = consulta.data;
  const acciones = permisos.puede(SCOPES.vinculosEscribir) ? accionesDeLote(lote) : [];
  const ejecutar = (nombre: AccionLote, exito: string) =>
    accion.mutate(
      { accion: nombre, id: lote.id },
      { onSuccess: () => mostrar(exito, 'success'), onError: (error) => mostrar(mensajeDeError(error, t), 'error'), onSettled: () => setCancelando(false) },
    );
  // Decisión AI: un lote con un tipo que exige aprobación espera a que otra persona lo apruebe o lo rechace como lote completo.
  const puedeDecidir = lote.estado === 'PorAprobar' && lote.puedesDecidir === true && permisos.puede(SCOPES.vinculosEscribir);
  const aprobar = () =>
    decidir.mutate(
      { loteId: lote.id, decision: 'aprobar' },
      { onSuccess: () => mostrar(t('invitaciones.porAprobar.loteAprobado'), 'success'), onError: (error) => mostrar(mensajeDeErrorDeDecision(error, t), 'error') },
    );
  const rechazar = (motivo: string) =>
    decidir.mutate(
      { loteId: lote.id, decision: 'rechazar', motivo },
      {
        onSuccess: () => {
          setRechazando(false);
          mostrar(t('invitaciones.porAprobar.loteRechazado'), 'success');
        },
      },
    );
  const informe = () =>
    descargar.mutate(
      { tipo: 'informe', id: lote.id },
      {
        onSuccess: (a) => guardarArchivo(a, t('lotes.detalle.informeNombre')),
        onError: (error) => mostrar(`${t('lotes.detalle.informeError')} ${mensajeDeError(error, t)}`, 'error'),
      },
    );

  return (
    <>
      <EncabezadoPagina
        antes={volver}
        titulo={lote.nombre}
        subtitulo={<ChipLote estado={lote.estado} />}
        acciones={
          <>
            {puedeDecidir && (
              <>
                <Button variant="outlined" color="error" startIcon={<CircleSlash size={16} aria-hidden />} disabled={decidir.isPending} onClick={() => setRechazando(true)}>
                  {t('invitaciones.lote.rechazar')}
                </Button>
                <Button variant="contained" startIcon={<CircleCheck size={16} aria-hidden />} disabled={decidir.isPending} onClick={aprobar}>
                  {t('invitaciones.lote.aprobar')}
                </Button>
              </>
            )}
            {acciones.includes('pausar') && (
              <Button variant="outlined" startIcon={<Pause size={16} aria-hidden />} disabled={accion.isPending} onClick={() => ejecutar('pausar', t('lotes.detalle.pausado'))}>
                {t('lotes.detalle.pausar')}
              </Button>
            )}
            {acciones.includes('reanudar') && (
              <Button variant="contained" startIcon={<Play size={16} aria-hidden />} disabled={accion.isPending} onClick={() => ejecutar('reanudar', t('lotes.detalle.reanudado'))}>
                {t('lotes.detalle.reanudar')}
              </Button>
            )}
            {acciones.includes('reintentar') && (
              <Button variant="contained" startIcon={<RotateCcw size={16} aria-hidden />} disabled={accion.isPending} onClick={() => ejecutar('reintentar', t('lotes.detalle.reintentado'))}>
                {t('lotes.detalle.reintentar', { count: lote.totales.fallidas })}
              </Button>
            )}
            {acciones.includes('cancelar') && (
              <Button variant="outlined" color="error" startIcon={<Ban size={16} aria-hidden />} disabled={accion.isPending} onClick={() => setCancelando(true)}>
                {t('lotes.detalle.cancelar')}
              </Button>
            )}
            <Button variant="outlined" startIcon={<Download size={16} aria-hidden />} disabled={descargar.isPending} onClick={informe}>
              {t('lotes.detalle.informe')}
            </Button>
          </>
        }
      />
      <Stack spacing={2}>
        {lote.estado === 'PorAprobar' && (
          <Alert severity="warning">
            {t('invitaciones.lote.avisoPendiente')}
            {lote.tiposPorAprobar && lote.tiposPorAprobar.length > 0 && <> {t('invitaciones.lote.avisoTipos', { tipos: lote.tiposPorAprobar.join(', ') })}</>}{' '}
            {lote.puedesDecidir === true ? t('invitaciones.lote.decidesTu') : t('invitaciones.lote.decideOtra')}
          </Alert>
        )}
        {lote.estado !== 'PorAprobar' && lote.aprobadoPor && lote.aprobadoEn && (
          <Alert severity="info">{t('invitaciones.lote.aprobadoPor', { quien: nombres.get(lote.aprobadoPor.replace('Usuario:', '')) ?? t('invitaciones.aviso.alguien'), fecha: formatearFecha(lote.aprobadoEn) })}</Alert>
        )}
        {lote.motivoDeRechazo && <Alert severity="error">{t('invitaciones.lote.rechazadoMotivo', { motivo: lote.motivoDeRechazo })}</Alert>}
        {decidir.isError && !rechazando && <Alert severity="error">{mensajeDeErrorDeDecision(decidir.error, t)}</Alert>}
        <Tarjeta>
          <Box sx={{ mb: 2 }}>
            <AvanceLote lote={lote} ancho="min(480px, 60vw)" />
          </Box>
          <RejillaDatos>
            <Dato etiqueta={t('lotes.detalle.totales.exitosas')}>{lote.totales.exitosas}</Dato>
            <Dato etiqueta={t('lotes.detalle.totales.fallidas')}>{lote.totales.fallidas}</Dato>
            <Dato etiqueta={t('lotes.detalle.totales.pendientes')}>{lote.totales.pendientes}</Dato>
            <Dato etiqueta={t('lotes.detalle.totales.invalidas')}>{lote.totales.invalidas}</Dato>
            <Dato etiqueta={t('lotes.detalle.totales.duplicadas')}>{lote.totales.duplicadas}</Dato>
          </RejillaDatos>
        </Tarjeta>
        <BarraFiltros>
          <FiltroMenu
            etiqueta={t('lotes.detalle.filtroEstado')}
            textoTodos={t('comun.todos')}
            valor={estado}
            onCambiar={setEstado}
            opciones={ESTADOS_FILA.map((e) => ({ valor: e, etiqueta: t(`lotes.detalle.estadosFila.${e}`) }))}
          />
        </BarraFiltros>
        <TablaConCursor
          etiqueta={t('lotes.detalle.filas')}
          columnas={columnas}
          filas={filas.data?.datos}
          claveFila={(f) => String(f.n)}
          cargando={filas.isPending}
          error={filas.isError ? `${t('lotes.detalle.errorFilas')} ${mensajeDeError(filas.error, t)}` : null}
          onReintentar={() => void filas.refetch()}
          paginacion={paginacion}
          siguienteCursor={filas.data?.siguienteCursor}
          vacio={<EstadoVacio compacto titulo={t('lotes.detalle.sinFilas')} />}
        />
      </Stack>
      {rechazando && (
        <DialogoRechazo
          tipo="lote"
          subtitulo={lote.nombre}
          ocupado={decidir.isPending}
          error={decidir.isError ? mensajeDeErrorDeDecision(decidir.error, t) : null}
          onConfirmar={rechazar}
          onCerrar={() => {
            decidir.reset();
            setRechazando(false);
          }}
        />
      )}
      {cancelando && (
        <DialogoConfirmacion
          titulo={t('lotes.detalle.cancelarTitulo')}
          mensaje={t('lotes.detalle.cancelarTexto')}
          confirmar={t('lotes.detalle.cancelarConfirmar')}
          ocupado={accion.isPending}
          onConfirmar={() => ejecutar('cancelar', t('lotes.detalle.cancelado'))}
          onCerrar={() => setCancelando(false)}
        />
      )}
    </>
  );
}
