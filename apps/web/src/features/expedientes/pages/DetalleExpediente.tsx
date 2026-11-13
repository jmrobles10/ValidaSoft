import { Alert, AlertTitle, Box, Button, Menu, MenuItem, ListItemIcon, ListItemText, Skeleton, Stack, Typography } from '@mui/material';
import { esErrorApi, SCOPES, type AreaExpedienteDto } from '@validasoft/api-client';
import { ChipEstado, EsqueletoTarjeta, formatearFecha, PaginaEstado } from '@validasoft/ui';
import { Ban, ChevronDown, Link2, Lock, RotateCcw, SearchX, Send, Undo2 } from 'lucide-react';
import { useCallback, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams } from 'react-router';
import { useSesion } from '../../../sesion/sesionStore';
import { useScrollPropio } from '../../../sesion/alturaFija';
import { EncabezadoPagina, EnlaceVolver, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAreas } from '../../configuracion/areas/api';
import { useNombresUsuarios } from '../../configuracion/usuarios/api';
import { PanelVerificaciones } from '../../cumplimiento/components/PanelVerificaciones';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { BotonExportarExpediente } from '../../informes/BotonExportarExpediente';
import { AvisoObservado } from '../../recordatorios/AvisoObservado';
import { useVinculo } from '../../vinculos/api';
import { identificacionResumida } from '../../vinculos/tercero';
import { useExpediente, useMotivos } from '../api';
import { AvisoEsperandoCuenta } from '../components/AvisoEsperandoCuenta';
import { DialogoDecision } from '../components/DialogoDecision';
import { EtapasExpediente } from '../components/EtapasExpediente';
import { DialogoDecisionArea } from '../components/DialogoDecisionArea';
import { PanelDecisionArea } from '../components/PanelDecisionArea';
import { PanelRequisitos } from '../components/PanelRequisitos';
import { AvisoPlazoCargue } from '../components/PlazoCargue';
import { DatosExpediente, HistorialExpediente, UltimaRespuesta } from '../components/PanelesExpediente';
import { usuariosDelExpediente } from '../requisitos';
import { esDialogoDeArea, accionesDeExpediente, areasFrenadas, ESTADOS_ABIERTOS, areasQueVuelvenARevisar, dialogoAplica, type ContextoRevisor, type DialogoExpediente } from '../reglas';

/** Hasta dónde devolvió a revisión el administrador (DS-1, DS-2): cada uno tiene su texto. */
const ALCANCES_DE_DEVOLUCION = ['Etapa', 'FaseInicial', 'ExpedienteAprobado'] as const;

export function DetalleExpediente() {
  const { t } = useTranslation();
  // Encabezado con las acciones siempre a la vista: embebida, la página usa scroll propio.
  useScrollPropio();
  const { expedienteId = '' } = useParams();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const usuarioId = useSesion((s) => s.sesion?.usuarioId ?? null);
  const consulta = useExpediente(empresa.id, expedienteId);
  const vinculo = useVinculo(empresa.id, consulta.data?.vinculoId ?? '');
  const areas = useAreas(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const motivos = useMotivos(empresa.id, permisos.puede(SCOPES.configuracionLeer));
  const nombres = useNombresUsuarios(empresa.id, useMemo(() => (consulta.data ? usuariosDelExpediente(consulta.data) : []), [consulta.data]));
  const [dialogo, setDialogo] = useState<DialogoExpediente | null>(null);
  const [ancla, setAncla] = useState<HTMLElement | null>(null);

  const vinculoBloqueado = vinculo.data?.estado === 'Bloqueado';
  const contexto = useCallback(
    (area: AreaExpedienteDto | null): ContextoRevisor => {
      const delArea = area ? areas.data?.find((a) => a.id === area.areaId) : undefined;
      return {
        scopes: permisos.scopes,
        usuarioId,
        esMiembro: delArea && usuarioId ? delArea.activa && delArea.miembros.includes(usuarioId) : null,
        vinculoBloqueado,
      };
    },
    [areas.data, permisos.scopes, usuarioId, vinculoBloqueado],
  );
  const nombresMotivos = useMemo(() => new Map((motivos.data ?? []).map((m) => [m.codigo, m.nombre])), [motivos.data]);

  if (!permisos.puede(SCOPES.expedientesLeer)) return <SinPermiso seccion={t('nav.bandeja')} />;

  const volver = <EnlaceVolver a={`/e/${empresa.id}/bandeja`}>{t('nav.bandeja')}</EnlaceVolver>;

  if (consulta.isPending) {
    return (
      <Box aria-busy>
        {volver}
        <Skeleton variant="rounded" width={320} height={28} sx={{ mb: 1 }} />
        <Skeleton variant="rounded" width={240} height={14} sx={{ mb: 3 }} />
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', lg: 'minmax(0,1.7fr) minmax(0,1fr)' }, gap: 2 }}>
          <EsqueletoTarjeta alto={280} />
          <EsqueletoTarjeta alto={320} />
        </Box>
      </Box>
    );
  }
  if (consulta.isError) {
    if (esErrorApi(consulta.error) && consulta.error.esNoEncontrado) {
      return <PaginaEstado icono={SearchX} titulo={t('expedientes.noEncontrado')} texto={t('expedientes.noEncontradoAyuda')} acciones={volver} />;
    }
    return (
      <>
        {volver}
        <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
      </>
    );
  }

  const expediente = consulta.data;
  const acciones = accionesDeExpediente(expediente, permisos.scopes, vinculoBloqueado);
  const masAcciones = acciones.includes('anular') || acciones.includes('faseInicial') || acciones.includes('desaprobar');
  const nombreDeArea = (id: string) => expediente.etapas.flatMap((e) => e.areas).find((a) => a.areaId === id)?.nombre ?? '—';
  const observadas = areasQueVuelvenARevisar(expediente);
  const frenadas = areasFrenadas(expediente);

  return (
    <>
      <EncabezadoPagina
        antes={volver}
        titulo={expediente.tercero.nombre}
        tituloCompleto={expediente.tercero.nombre}
        etiquetaAcciones={t('vinculos.detalle.acciones')}
        subtitulo={
          <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', mt: 0.25, fontSize: 13 }}>
            <Typography component="span" variant="mono">
              {expediente.folio}
            </Typography>
            <span aria-hidden>·</span>
            <span>{identificacionResumida(expediente.tercero.identificacion)}</span>
            <span aria-hidden>·</span>
            <span>{expediente.tipoVinculo.nombre}</span>
            <span aria-hidden>·</span>
            <ChipEstado estado={expediente.estado} fecha={expediente.cerradoEn ?? expediente.actualizadoEn} />
          </Stack>
        }
        acciones={
          <>
            <Button component={RouterLink} to={`/e/${empresa.id}/vinculos/${expediente.vinculoId}`} variant="outlined" startIcon={<Link2 size={16} aria-hidden />}>
              {t('expedientes.verVinculo')}
            </Button>
            <BotonExportarExpediente empresaId={empresa.id} expedienteId={expediente.id} />
            {acciones.includes('reenviar') && (
              <Button variant="contained" startIcon={<Send size={16} aria-hidden />} onClick={() => setDialogo({ tipo: 'reenviar' })}>
                {t('expedientes.acciones.reenviar')}
              </Button>
            )}
            {masAcciones && (
              <>
                <Button variant="outlined" endIcon={<ChevronDown size={16} aria-hidden />} aria-haspopup="menu" onClick={(e) => setAncla(e.currentTarget)}>
                  {t('expedientes.acciones.mas')}
                </Button>
                <Menu anchorEl={ancla} open={Boolean(ancla)} onClose={() => setAncla(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}>
                  {acciones.includes('faseInicial') && (
                    <MenuItem
                      onClick={() => {
                        setAncla(null);
                        setDialogo({ tipo: 'faseInicial' });
                      }}
                    >
                      <ListItemIcon>
                        <RotateCcw size={16} strokeWidth={1.75} aria-hidden />
                      </ListItemIcon>
                      <ListItemText primary={t('expedientes.acciones.faseInicial')} />
                    </MenuItem>
                  )}
                  {acciones.includes('desaprobar') && (
                    <MenuItem
                      onClick={() => {
                        setAncla(null);
                        setDialogo({ tipo: 'desaprobar' });
                      }}
                      sx={{ color: 'estado.error.texto', '& .MuiListItemIcon-root': { color: 'estado.error.texto' } }}
                    >
                      <ListItemIcon>
                        <Undo2 size={16} strokeWidth={1.75} aria-hidden />
                      </ListItemIcon>
                      <ListItemText primary={t('expedientes.acciones.desaprobar')} />
                    </MenuItem>
                  )}
                  {acciones.includes('anular') && (
                    <MenuItem
                      onClick={() => {
                        setAncla(null);
                        setDialogo({ tipo: 'anular' });
                      }}
                      sx={{ color: 'estado.error.texto', '& .MuiListItemIcon-root': { color: 'estado.error.texto' } }}
                    >
                      <ListItemIcon>
                        <Ban size={16} strokeWidth={1.75} aria-hidden />
                      </ListItemIcon>
                      <ListItemText primary={t('expedientes.acciones.anular')} />
                    </MenuItem>
                  )}
                </Menu>
              </>
            )}
          </>
        }
      />

      {vinculoBloqueado && expediente.estado !== 'Aprobado' && (
        <Alert severity="error" icon={<Lock size={18} strokeWidth={1.75} />} sx={{ mb: 2 }}>
          <AlertTitle>{t('expedientes.avisos.bloqueadoTitulo')}</AlertTitle>
          {t('expedientes.avisos.bloqueado')}
        </Alert>
      )}
      {expediente.estado === 'Observado' && (
        <AvisoObservado
          expediente={expediente}
          empresaId={empresa.id}
          scopes={permisos.scopes}
          texto={
            t('expedientes.avisos.observado', { count: observadas.length, areas: observadas.map((a) => a.nombre).join(', ') }) +
            (frenadas.length > 0 ? ` ${t('expedientes.avisos.frenadas', { areas: frenadas.map((a) => a.nombre).join(', ') })}` : '')
          }
        />
      )}
      {expediente.esperandoVerificaciones && (
        <Alert severity="info" sx={{ mb: 2 }}>
          <AlertTitle>{t('expedientes.avisos.esperandoVerificacionesTitulo')}</AlertTitle>
          {t('expedientes.avisos.esperandoVerificaciones', {
            verificaciones: (expediente.verificaciones ?? [])
              .filter((v) => v.bloqueante && v.estado !== 'Satisfecha')
              .map((v) => `${t(`cumplimiento.tipos.${v.tipo}`)} (${t(`cumplimiento.estados.${v.estado}`).toLowerCase()})`)
              .join(', '),
          })}
        </Alert>
      )}
      {expediente.esperandoCuentaDesignada && <AvisoEsperandoCuenta empresaId={empresa.id} vinculoId={expediente.vinculoId} />}
      {expediente.ultimaDesaprobacion && ESTADOS_ABIERTOS.includes(expediente.estado) && (
        <Alert severity="info" icon={<Undo2 size={18} strokeWidth={1.75} />} sx={{ mb: 2 }}>
          <AlertTitle>{t('expedientes.avisos.devueltoTitulo')}</AlertTitle>
          {t(`expedientes.avisos.devuelto.${ALCANCES_DE_DEVOLUCION.find((a) => a === expediente.ultimaDesaprobacion?.alcance) ?? 'Etapa'}`, {
            fecha: formatearFecha(expediente.ultimaDesaprobacion.fecha),
            etapa: String(expediente.ultimaDesaprobacion.etapa),
            persona: nombres.get(expediente.ultimaDesaprobacion.usuario) ?? t('expedientes.veredictos.administrador'),
          })}{' '}
          “{expediente.ultimaDesaprobacion.justificacion}”
        </Alert>
      )}
      {expediente.estado === 'Aprobado' && (
        <Alert severity="success" sx={{ mb: 2 }}>
          <AlertTitle>{t('expedientes.avisos.aprobadoTitulo')}</AlertTitle>
          {t('expedientes.avisos.aprobado', { fecha: formatearFecha(expediente.cerradoEn ?? expediente.actualizadoEn) })}
        </Alert>
      )}
      {expediente.estado === 'Rechazado' && expediente.rechazo && (
        <Alert severity="error" sx={{ mb: 2 }}>
          <AlertTitle>{expediente.rechazo.origen === 'Cumplimiento' ? t('cumplimiento.rechazo.avisoTitulo') : t('expedientes.avisos.rechazadoTitulo')}</AlertTitle>
          {expediente.tipo === 'Actualizacion'
            ? t('expedientes.avisos.rechazadoActualizacion', { area: nombreDeArea(expediente.rechazo.areaId), fecha: formatearFecha(expediente.rechazo.fecha) })
            : expediente.rechazo.origen === 'Cumplimiento'
              ? t('cumplimiento.rechazo.aviso', { fecha: formatearFecha(expediente.rechazo.fecha) })
              : t('expedientes.avisos.rechazado', { area: nombreDeArea(expediente.rechazo.areaId), fecha: formatearFecha(expediente.rechazo.fecha) })}{' '}
          “{expediente.rechazo.justificacion}”
        </Alert>
      )}
      {expediente.estado === 'Anulado' && (
        <Alert severity="info" sx={{ mb: 2 }}>
          <AlertTitle>{t('expedientes.avisos.anuladoTitulo')}</AlertTitle>
          {t(expediente.tipo === 'Actualizacion' ? 'expedientes.avisos.anuladoActualizacion' : 'expedientes.avisos.anulado', { fecha: formatearFecha(expediente.cerradoEn ?? expediente.actualizadoEn) })}
          {expediente.motivoAnulacion ? ` “${expediente.motivoAnulacion}”` : ''}
        </Alert>
      )}

      <AvisoPlazoCargue expediente={expediente} empresaId={empresa.id} scopes={permisos.scopes} />

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', lg: 'minmax(0,1.7fr) minmax(0,1fr)' }, gap: 2, alignItems: 'start' }}>
        <Stack spacing={2} sx={{ minWidth: 0 }}>
          <PanelDecisionArea expediente={expediente} contexto={contexto} onAccion={setDialogo} />
          <PanelRequisitos expediente={expediente} contexto={contexto} nombres={nombres} onAccion={setDialogo} />
          <PanelVerificaciones expediente={expediente} empresaId={empresa.id} scopes={permisos.scopes} nombres={nombres} />
          <EtapasExpediente expediente={expediente} contexto={contexto} nombres={nombres} motivos={nombresMotivos} onAccion={setDialogo} />
          <UltimaRespuesta expediente={expediente} />
          <DatosExpediente expediente={expediente} />
        </Stack>
        <HistorialExpediente empresaId={empresa.id} expediente={expediente} />
      </Box>

      {dialogo && esDialogoDeArea(dialogo) && (
        <DialogoDecisionArea
          dialogo={dialogo}
          expediente={expediente}
          empresaId={empresa.id}
          motivos={motivos.data ?? []}
          aplica={dialogoAplica(dialogo, expediente, contexto)}
          onCerrar={() => setDialogo(null)}
        />
      )}
      {dialogo && !esDialogoDeArea(dialogo) && (
        <DialogoDecision
          dialogo={dialogo}
          expediente={expediente}
          empresaId={empresa.id}
          motivos={motivos.data ?? []}
          aplica={dialogoAplica(dialogo, expediente, contexto)}
          onCerrar={() => setDialogo(null)}
        />
      )}
    </>
  );
}
