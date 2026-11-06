import { Alert, Box, Button, Card, CardContent, Chip, Grid2 as Grid, Skeleton, Stack, Typography } from '@mui/material';
import { accionesDisponibles, esErrorApi, SCOPES, type DefinicionAccion, type VinculoDto } from '@validasoft/api-client';
import { ChipEstado, formatearFecha, formatearFechaHora, LineaDeTiempo } from '@validasoft/ui';
import { ArrowLeft } from 'lucide-react';
import { useMemo, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useParams } from 'react-router';
import { useSesion } from '../../../sesion/sesionStore';
import { ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useNombresUsuarios } from '../../configuracion/usuarios/api';
import { useHistorialVinculo, useVinculo } from '../api';
import { DialogoAccion } from '../components/DialogoAccion';
import { NombreTercero } from '../components/NombreTercero';
import { eventoATimeline } from '../historial';

function Dato({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" component="dt">
        {etiqueta}
      </Typography>
      <Typography variant="body2" component="dd" sx={{ m: 0, overflowWrap: 'anywhere' }}>
        {children}
      </Typography>
    </Box>
  );
}

export function DetalleVinculo() {
  const { t } = useTranslation();
  const { vinculoId = '' } = useParams();
  const { empresa } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const consulta = useVinculo(empresa.id, vinculoId);
  const [accion, setAccion] = useState<DefinicionAccion | null>(null);

  if (!permisos.puede(SCOPES.vinculosLeer)) return <SinPermiso seccion={t('nav.vinculos')} />;

  const volver = (
    <Button component={RouterLink} to=".." relative="path" startIcon={<ArrowLeft size={16} />} sx={{ mb: 2 }}>
      {t('nav.vinculos')}
    </Button>
  );

  if (consulta.isPending) {
    return (
      <>
        {volver}
        <Skeleton variant="rounded" height={48} sx={{ mb: 2 }} />
        <Skeleton variant="rounded" height={240} />
      </>
    );
  }
  if (consulta.isError) {
    return (
      <>
        {volver}
        {esErrorApi(consulta.error) && consulta.error.esNoEncontrado ? (
          <Alert severity="warning">{t('vinculos.detalle.noEncontrado')}</Alert>
        ) : (
          <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
        )}
      </>
    );
  }

  const vinculo = consulta.data;
  const acciones = accionesDisponibles(vinculo, permisos.scopes);

  return (
    <>
      {volver}
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems={{ sm: 'center' }} justifyContent="space-between" sx={{ mb: 3 }}>
        <Stack direction="row" spacing={2} alignItems="center" sx={{ minWidth: 0 }}>
          <Typography variant="h2" component="h1" sx={{ minWidth: 0 }}>
            <NombreTercero terceroId={vinculo.terceroId} conIdentificacion={false} variante="h2" />
          </Typography>
          <ChipEstado estado={vinculo.estado} size="medium" />
        </Stack>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap aria-label={t('vinculos.detalle.acciones')}>
          {acciones.map((d) => (
            <Button key={d.accion} variant={d.destructiva ? 'outlined' : 'contained'} color={d.destructiva ? 'error' : 'primary'} onClick={() => setAccion(d)}>
              {t(`acciones.${d.accion}.boton`)}
            </Button>
          ))}
          {acciones.length === 0 && (
            <Typography variant="body2" color="text.secondary">
              {t('vinculos.detalle.sinAcciones')}
            </Typography>
          )}
        </Stack>
      </Stack>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 7 }}>
          <Stack spacing={2}>
            <DatosVinculo vinculo={vinculo} />
            <ReferenciasVinculo vinculo={vinculo} />
          </Stack>
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <HistorialVinculo empresaId={empresa.id} vinculoId={vinculo.id} />
        </Grid>
      </Grid>

      {accion && <DialogoAccion definicion={accion} vinculo={vinculo} empresaId={empresa.id} onCerrar={() => setAccion(null)} />}
    </>
  );
}

function DatosVinculo({ vinculo }: { vinculo: VinculoDto }) {
  const { t } = useTranslation();
  const atributos = Object.entries(vinculo.atributos);
  return (
    <Card>
      <CardContent>
        <Typography variant="h3" component="h2" sx={{ mb: 2 }}>
          {t('vinculos.detalle.datos')}
        </Typography>
        <Box component="dl" sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, m: 0 }}>
          <Dato etiqueta={t('vinculos.detalle.tercero')}>
            <NombreTercero terceroId={vinculo.terceroId} />
          </Dato>
          <Dato etiqueta={t('vinculos.detalle.tipo')}>{vinculo.tipoVinculo.nombre}</Dato>
          <Dato etiqueta={t('vinculos.detalle.correo')}>{vinculo.correoTercero}</Dato>
          <Dato etiqueta={t('vinculos.detalle.origen')}>{t(`vinculos.origen.${vinculo.origen}`)}</Dato>
          <Dato etiqueta={t('vinculos.detalle.creado')}>{formatearFechaHora(vinculo.creadoEn)}</Dato>
          <Dato etiqueta={t('vinculos.detalle.actualizado')}>{formatearFechaHora(vinculo.actualizadoEn)}</Dato>
          <Dato etiqueta={t('vinculos.detalle.invitacion')}>
            {vinculo.invitacionAceptadaEn
              ? t('vinculos.detalle.invitacionAceptada', { fecha: formatearFecha(vinculo.invitacionAceptadaEn) })
              : vinculo.invitacion
                ? t('vinculos.detalle.invitacionDetalle', {
                    correo: vinculo.invitacion.correoDestino,
                    envios: String(vinculo.invitacion.envios),
                    expira: formatearFecha(vinculo.invitacion.expira),
                  })
                : t('vinculos.detalle.sinInvitacion')}
          </Dato>
          <Dato etiqueta={t('vinculos.detalle.version')}>
            <Typography variant="mono">{vinculo.version}</Typography>
          </Dato>
          {vinculo.bloqueo && (
            <Box sx={{ gridColumn: '1 / -1' }}>
              <Alert severity="error" variant="outlined">
                <strong>{t('vinculos.detalle.bloqueo')}: </strong>
                {t('vinculos.detalle.bloqueoDetalle', {
                  desde: formatearFecha(vinculo.bloqueo.desde),
                  hasta: vinculo.bloqueo.hasta ? t('vinculos.detalle.bloqueoHasta', { hasta: formatearFecha(vinculo.bloqueo.hasta) }) : '',
                  motivo: vinculo.bloqueo.motivo,
                })}
              </Alert>
            </Box>
          )}
          <Box sx={{ gridColumn: '1 / -1' }}>
            <Dato etiqueta={t('vinculos.detalle.atributos')}>
              {atributos.length === 0
                ? t('vinculos.detalle.sinAtributos')
                : atributos.map(([clave, valor]) => (
                    <Chip key={clave} size="small" variant="outlined" label={`${clave}: ${JSON.stringify(valor)}`} sx={{ mr: 0.5, mb: 0.5 }} />
                  ))}
            </Dato>
          </Box>
        </Box>
      </CardContent>
    </Card>
  );
}

function ReferenciasVinculo({ vinculo }: { vinculo: VinculoDto }) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardContent>
        <Typography variant="h3" component="h2">
          {t('vinculos.detalle.referencias')}
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          {t('vinculos.detalle.referenciasAyuda')}
        </Typography>
        {vinculo.referencias.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {t('vinculos.detalle.sinReferencias')}
          </Typography>
        ) : (
          <Stack component="ul" spacing={1} sx={{ listStyle: 'none', m: 0, p: 0 }}>
            {vinculo.referencias.map((r) => (
              <Stack component="li" key={`${r.sistema}|${r.id}`} direction="row" spacing={1} alignItems="center">
                <Chip size="small" label={r.sistema} color="primary" variant="outlined" />
                <Typography variant="mono">{r.id}</Typography>
              </Stack>
            ))}
          </Stack>
        )}
      </CardContent>
    </Card>
  );
}

/** Las acciones invalidan `/api/v1/vinculos*`, así que el historial se recarga solo después de cada cambio. */
function HistorialVinculo({ empresaId, vinculoId }: { empresaId: string; vinculoId: string }) {
  const { t } = useTranslation();
  const usuarioActual = useSesion((s) => s.sesion?.usuarioId ?? null);
  const nombres = useNombresUsuarios(empresaId);
  const consulta = useHistorialVinculo(empresaId, vinculoId);
  const { refetch } = consulta;
  const eventos = useMemo(
    // Del más reciente al más antiguo por versión: varios eventos de una misma transacción comparten la hora.
    () => [...(consulta.data ?? [])].reverse().map((e) => eventoATimeline(e, { usuarioActual, nombres }, t)),
    [consulta.data, usuarioActual, nombres, t],
  );

  return (
    <Card>
      <CardContent>
        <Typography variant="h3" component="h2" sx={{ mb: 2 }}>
          {t('vinculos.detalle.historial')}
        </Typography>
        {consulta.isPending && <Skeleton variant="rounded" height={160} />}
        {consulta.isError && <ErrorRecuperable mensaje={t('vinculos.detalle.errorHistorial')} onReintentar={() => void refetch()} />}
        {consulta.data && <LineaDeTiempo eventos={eventos} />}
      </CardContent>
    </Card>
  );
}
