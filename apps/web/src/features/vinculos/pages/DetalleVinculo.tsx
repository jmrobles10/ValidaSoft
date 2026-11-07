import { Alert, AlertTitle, Box, Button, Chip, ListItemIcon, ListItemText, Menu, MenuItem, Skeleton, Stack, Typography } from '@mui/material';
import { accionesDisponibles, esErrorApi, SCOPES, type DefinicionAccion, type VinculoDto } from '@validasoft/api-client';
import { AvatarActor, ChipEstado, Dato, EstadoVacio, formatearFecha, LineaDeTiempo, MarcoTabla, PaginaEstado, RejillaDatos } from '@validasoft/ui';
import { ChevronDown, Lock, SearchX } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams } from 'react-router';
import { useSesion } from '../../../sesion/sesionStore';
import { EnlaceVolver, ErrorRecuperable, EsqueletoHistorial, SinPermiso, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { DocumentosVinculo } from '../../documentos/DocumentosVinculo';
import { TarjetaExpedienteVinculo } from '../../expedientes/components/TarjetaExpedienteVinculo';
import { actoresDeHistorial, useNombresUsuarios } from '../../configuracion/usuarios/api';
import { ICONO_ACCION } from '../acciones';
import { useHistorialVinculo, useTercero, useVinculo } from '../api';
import { DialogoAccion } from '../components/DialogoAccion';
import { identificacionDe } from '../tercero';
import { eventoATimeline } from '../historial';

const Mono = ({ children }: { children: string }) => (
  <Typography component="span" variant="mono">
    {children}
  </Typography>
);

export function DetalleVinculo() {
  const { t } = useTranslation();
  const { vinculoId = '' } = useParams();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const consulta = useVinculo(empresa.id, vinculoId);
  const [accion, setAccion] = useState<DefinicionAccion | null>(null);

  if (!permisos.puede(SCOPES.vinculosLeer)) return <SinPermiso seccion={t('nav.vinculos')} />;

  const volver = <EnlaceVolver a="..">{t('nav.vinculos')}</EnlaceVolver>;

  if (consulta.isPending) {
    return (
      <Box aria-busy>
        {volver}
        <Skeleton variant="rounded" width={320} height={28} sx={{ mb: 1 }} />
        <Skeleton variant="rounded" width={220} height={14} sx={{ mb: 3 }} />
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', lg: 'minmax(0,1.7fr) minmax(0,1fr)' }, gap: 2 }}>
          <Skeleton variant="rounded" height={220} />
          <Skeleton variant="rounded" height={320} />
        </Box>
      </Box>
    );
  }
  if (consulta.isError) {
    if (esErrorApi(consulta.error) && consulta.error.esNoEncontrado) {
      return <PaginaEstado icono={SearchX} titulo={t('vinculos.detalle.noEncontrado')} texto={t('vinculos.detalle.noEncontradoAyuda')} acciones={volver} />;
    }
    return (
      <>
        {volver}
        <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
      </>
    );
  }

  const vinculo = consulta.data;
  const disponibles = accionesDisponibles(vinculo, permisos.scopes);

  return (
    <>
      {volver}
      <Encabezado vinculo={vinculo} acciones={disponibles} onAccion={setAccion} />
      {vinculo.bloqueo && (
        <Alert severity="error" icon={<Lock size={18} strokeWidth={1.75} />} sx={{ mb: 2 }}>
          <AlertTitle>{t('vinculos.detalle.bloqueo')}</AlertTitle>
          {vinculo.bloqueo.motivo}{' '}
          {t('vinculos.detalle.bloqueoDetalle', {
            desde: formatearFecha(vinculo.bloqueo.desde),
            hasta: vinculo.bloqueo.hasta ? t('vinculos.detalle.bloqueoHasta', { hasta: formatearFecha(vinculo.bloqueo.hasta) }) : '',
          })}
        </Alert>
      )}
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', lg: 'minmax(0,1.7fr) minmax(0,1fr)' }, gap: 2, alignItems: 'start' }}>
        <Stack spacing={2} sx={{ minWidth: 0 }}>
          <DatosVinculo vinculo={vinculo} empresaNombre={empresa.nombre} />
          <TarjetaExpedienteVinculo vinculo={vinculo} />
          <DocumentosVinculo vinculo={vinculo} />
          <ReferenciasVinculo vinculo={vinculo} />
        </Stack>
        <HistorialVinculo empresaId={empresa.id} vinculoId={vinculo.id} />
      </Box>
      {accion && <DialogoAccion definicion={accion} vinculo={vinculo} empresaId={empresa.id} onCerrar={() => setAccion(null)} />}
    </>
  );
}

/** Título con NIT, tipo y estado; las acciones seguras van como botones y las destructivas en "Más acciones". */
function Encabezado({ vinculo, acciones, onAccion }: { vinculo: VinculoDto; acciones: DefinicionAccion[]; onAccion: (d: DefinicionAccion) => void }) {
  const { t } = useTranslation();
  const tercero = useTercero(vinculo.terceroId);
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const seguras = acciones.filter((d) => !d.destructiva);
  const destructivas = acciones.filter((d) => d.destructiva);

  return (
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ md: 'flex-end' }} justifyContent="space-between" sx={{ mb: 2 }}>
      <Box sx={{ minWidth: 0 }}>
        <Typography variant="h2" component="h1" noWrap>
          {tercero.data?.nombre ?? (tercero.isPending ? <Skeleton width={280} /> : t('vinculos.terceroDesconocido'))}
        </Typography>
        <Stack direction="row" spacing={1} alignItems="center" sx={{ mt: 0.5, color: 'text.secondary', fontSize: 13, flexWrap: 'wrap' }} useFlexGap>
          {tercero.data && <Mono>{identificacionDe(tercero.data)}</Mono>}
          {tercero.data && <span aria-hidden>·</span>}
          <span>{vinculo.tipoVinculo.nombre}</span>
          <span aria-hidden>·</span>
          <ChipEstado estado={vinculo.estado} fecha={vinculo.bloqueo?.desde ?? vinculo.actualizadoEn} />
        </Stack>
      </Box>
      {acciones.length > 0 && (
        <Stack direction="row" spacing={1} aria-label={t('vinculos.detalle.acciones')} role="group" sx={{ flex: 'none' }}>
          {seguras.map((d) => {
            const Icono = ICONO_ACCION[d.accion];
            return (
              <Button key={d.accion} variant="outlined" startIcon={<Icono size={16} aria-hidden />} onClick={() => onAccion(d)}>
                {t(`acciones.${d.accion}.boton`)}
              </Button>
            );
          })}
          {destructivas.length > 0 && (
            <>
              <Button
                variant="outlined"
                endIcon={<ChevronDown size={16} aria-hidden />}
                aria-haspopup="menu"
                aria-expanded={ancla ? 'true' : undefined}
                onClick={(e) => setAncla(e.currentTarget)}
              >
                {t('vinculos.detalle.masAcciones')}
              </Button>
              <Menu
                anchorEl={ancla}
                open={Boolean(ancla)}
                onClose={() => setAncla(null)}
                anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
                transformOrigin={{ vertical: 'top', horizontal: 'right' }}
                slotProps={{ paper: { sx: { width: 280, mt: 0.5 } } }}
              >
                {destructivas.map((d) => {
                  const Icono = ICONO_ACCION[d.accion];
                  return (
                    <MenuItem
                      key={d.accion}
                      onClick={() => {
                        setAncla(null);
                        onAccion(d);
                      }}
                      sx={{ alignItems: 'flex-start', py: 0.75, color: 'estado.error.texto', '& .MuiListItemIcon-root': { color: 'estado.error.texto', mt: '2px' } }}
                    >
                      <ListItemIcon>
                        <Icono size={16} strokeWidth={1.75} aria-hidden />
                      </ListItemIcon>
                      <ListItemText
                        primary={t(`acciones.${d.accion}.boton`)}
                        secondary={t(`acciones.${d.accion}.resumen`)}
                        slotProps={{ primary: { sx: { fontSize: 14 } }, secondary: { sx: { fontSize: 12, lineHeight: '16px', whiteSpace: 'normal' } } }}
                        sx={{ my: 0 }}
                      />
                    </MenuItem>
                  );
                })}
              </Menu>
            </>
          )}
        </Stack>
      )}
    </Stack>
  );
}

function DatosVinculo({ vinculo, empresaNombre }: { vinculo: VinculoDto; empresaNombre: string }) {
  const { t } = useTranslation();
  const tercero = useTercero(vinculo.terceroId);
  const atributos = Object.entries(vinculo.atributos);
  const invitacion = vinculo.invitacionAceptadaEn
    ? t('vinculos.detalle.invitacionAceptada', { fecha: formatearFecha(vinculo.invitacionAceptadaEn) })
    : vinculo.invitacion
      ? t('vinculos.detalle.invitacionDetalle', { envios: String(vinculo.invitacion.envios), expira: formatearFecha(vinculo.invitacion.expira) })
      : t('vinculos.detalle.sinInvitacion');

  return (
    <Tarjeta titulo={t('vinculos.detalle.datos')}>
      <RejillaDatos>
        <Dato etiqueta={t('vinculos.detalle.tercero')}>{tercero.data?.nombre ?? '—'}</Dato>
        <Dato etiqueta={t('vinculos.detalle.identificacion')}>{tercero.data ? <Mono>{identificacionDe(tercero.data)}</Mono> : '—'}</Dato>
        <Dato etiqueta={t('vinculos.detalle.tipo')}>{vinculo.tipoVinculo.nombre}</Dato>
        <Dato etiqueta={t('vinculos.detalle.empresa')}>{empresaNombre}</Dato>
        <Dato etiqueta={t('vinculos.detalle.correo')}>
          <Mono>{vinculo.invitacion?.correoDestino ?? vinculo.correoTercero}</Mono>
        </Dato>
        <Dato etiqueta={t('vinculos.detalle.invitacion')}>{invitacion}</Dato>
        <Dato etiqueta={t('vinculos.detalle.creado')}>
          <Mono>{formatearFecha(vinculo.creadoEn)}</Mono> · {t(`vinculos.origen.${vinculo.origen}`)}
        </Dato>
        <Dato etiqueta={t('vinculos.detalle.actualizado')}>
          <Mono>{formatearFecha(vinculo.actualizadoEn)}</Mono>
        </Dato>
        {atributos.length > 0 && (
          <Dato etiqueta={t('vinculos.detalle.atributos')} ancho>
            <Box component="span" sx={{ display: 'flex', gap: 0.5, flexWrap: 'wrap' }}>
              {atributos.map(([clave, valor]) => (
                <Chip key={clave} size="small" variant="outlined" label={`${clave}: ${typeof valor === 'string' ? valor : JSON.stringify(valor)}`} />
              ))}
            </Box>
          </Dato>
        )}
      </RejillaDatos>
    </Tarjeta>
  );
}

function ReferenciasVinculo({ vinculo }: { vinculo: VinculoDto }) {
  const { t } = useTranslation();
  return (
    <Tarjeta titulo={t('vinculos.detalle.referencias')}>
      {vinculo.referencias.length === 0 ? (
        <MarcoTabla>
          <EstadoVacio compacto titulo={t('vinculos.detalle.sinReferencias')} texto={t('vinculos.detalle.referenciasAyuda')} />
        </MarcoTabla>
      ) : (
        <MarcoTabla>
          <Box component="table" sx={{ width: '100%', borderCollapse: 'collapse', '& th, & td': { height: 36, px: 1.5, textAlign: 'left', borderBottom: 1, borderColor: 'divider' }, '& tr:last-child td': { borderBottom: 0 } }}>
            <thead>
              <tr>
                <Box component="th" scope="col" sx={{ color: 'text.secondary', fontSize: 12, fontWeight: 500 }}>
                  {t('vinculos.detalle.sistema')}
                </Box>
                <Box component="th" scope="col" sx={{ color: 'text.secondary', fontSize: 12, fontWeight: 500 }}>
                  {t('vinculos.detalle.referencia')}
                </Box>
              </tr>
            </thead>
            <tbody>
              {vinculo.referencias.map((r) => (
                <tr key={`${r.sistema}|${r.id}`}>
                  <td>
                    <Stack direction="row" spacing={1} alignItems="center">
                      <AvatarActor tipo="integracion" tamano={24} />
                      <span>{r.sistema}</span>
                    </Stack>
                  </td>
                  <td>
                    <Mono>{r.id}</Mono>
                  </td>
                </tr>
              ))}
            </tbody>
          </Box>
        </MarcoTabla>
      )}
    </Tarjeta>
  );
}

/** Las acciones invalidan `/api/v1/vinculos*`, así que el historial se recarga solo después de cada cambio. */
function HistorialVinculo({ empresaId, vinculoId }: { empresaId: string; vinculoId: string }) {
  const { t } = useTranslation();
  const usuarioActual = useSesion((s) => s.sesion?.usuarioId ?? null);
  const consulta = useHistorialVinculo(empresaId, vinculoId);
  const nombres = useNombresUsuarios(empresaId, useMemo(() => actoresDeHistorial(consulta.data), [consulta.data]));
  const { refetch } = consulta;
  const eventos = useMemo(
    // Del más reciente al más antiguo por versión: varios eventos de una misma transacción comparten la hora.
    () => [...(consulta.data ?? [])].reverse().map((e) => eventoATimeline(e, { usuarioActual, nombres }, t)),
    [consulta.data, usuarioActual, nombres, t],
  );

  return (
    <Tarjeta titulo={t('vinculos.detalle.historial')}>
      {consulta.isPending && (
        <EsqueletoHistorial />
      )}
      {consulta.isError && <ErrorRecuperable mensaje={t('vinculos.detalle.errorHistorial')} onReintentar={() => void refetch()} />}
      {consulta.data && <LineaDeTiempo eventos={eventos} vacio={t('vinculos.detalle.sinHistorial')} />}
    </Tarjeta>
  );
}
