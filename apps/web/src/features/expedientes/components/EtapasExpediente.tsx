import { Box, Button, Chip, IconButton, ListItemIcon, ListItemText, Menu, MenuItem, Stack, Tooltip, Typography } from '@mui/material';
import { SCOPES, type AreaExpedienteDto, type ExpedienteDto, type VeredictoEmitidoDto } from '@validasoft/api-client';
import { AvatarActor, formatearFecha, formatearFechaHora } from '@validasoft/ui';
import { BadgeCheck, Ellipsis, Gavel, ShieldX, Sparkles, Timer, Undo2 } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { Aprobaciones, ChipEstadoArea } from '../componentes';
import {
  bloqueoVeredicto,
  puedeAnularVeredicto,
  puedeAprobarComoAdministrador,
  puedeRechazar,
  type ContextoRevisor,
  type DialogoExpediente,
} from '../reglas';

export interface EtapasExpedienteProps {
  expediente: ExpedienteDto;
  contexto: (area: AreaExpedienteDto | null) => ContextoRevisor;
  /** id de usuario → nombre (vacío si la persona no puede listar usuarios). */
  nombres: ReadonlyMap<string, string>;
  /** código de motivo → nombre. */
  motivos: ReadonlyMap<string, string>;
  onAccion: (dialogo: DialogoExpediente) => void;
}

/** Etapas en secuencia; dentro de cada una, sus áreas revisan en paralelo (FL-2). */
export function EtapasExpediente({ expediente, contexto, nombres, motivos, onAccion }: EtapasExpedienteProps) {
  const { t } = useTranslation();
  return (
    <Tarjeta titulo={t('expedientes.etapas.titulo')} acciones={<Typography color="text.secondary" sx={{ fontSize: 12 }}>{t('expedientes.etapas.paralelo')}</Typography>}>
      <Stack spacing={2}>
        {expediente.etapas.map((etapa) => (
          <Box key={etapa.orden} component="section" aria-label={t('expedientes.etapas.etapa', { orden: String(etapa.orden) })}>
            <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 1 }}>
              <Typography variant="overline" component="h3" color="text.secondary">
                {t('expedientes.etapas.etapa', { orden: String(etapa.orden) })}
              </Typography>
              <Typography component="span" sx={{ fontSize: 12, color: etapa.estado === 'Activa' ? 'acento.main' : 'text.secondary' }}>
                · {t(`expedientes.etapas.estadoEtapa.${etapa.estado}`)}
              </Typography>
            </Stack>
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0,1fr)', md: 'repeat(auto-fit, minmax(280px, 1fr))' }, gap: 1.5 }}>
              {etapa.areas.map((area) => (
                <TarjetaArea key={area.areaId} expediente={expediente} area={area} contexto={contexto(area)} nombres={nombres} motivos={motivos} onAccion={onAccion} />
              ))}
            </Box>
          </Box>
        ))}
      </Stack>
    </Tarjeta>
  );
}

function TarjetaArea({
  expediente,
  area,
  contexto,
  nombres,
  motivos,
  onAccion,
}: Omit<EtapasExpedienteProps, 'contexto'> & { area: AreaExpedienteDto; contexto: ContextoRevisor }) {
  const { t } = useTranslation();
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const bloqueo = bloqueoVeredicto(expediente, area, contexto);
  const rechaza = puedeRechazar(expediente, area, contexto);
  const aprobarAdmin = puedeAprobarComoAdministrador(expediente, area, contexto);
  const anular = puedeAnularVeredicto(expediente, area, contexto.scopes);
  const nombre = (id: string) => (id === contexto.usuarioId ? t('expedientes.veredictos.tu') : (nombres.get(id) ?? null));
  const pendientes = area.miembrosPendientes.map((id) => nombre(id) ?? t('expedientes.etapas.personaDelArea'));
  // Solo se explica el bloqueo a quien revisa y cuando dice algo útil (no "tu rol no emite veredictos").
  const explicar = bloqueo && bloqueo !== 'sin-permiso' && bloqueo !== 'expediente-cerrado' && bloqueo !== 'area-cerrada' && contexto.scopes.has(SCOPES.expedientesRevisar);

  return (
    <Box sx={{ border: 1, borderColor: area.estado === 'Observada' ? 'estado.advertencia.borde' : 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5, minWidth: 0 }}>
      <Stack direction="row" spacing={1} alignItems="flex-start" justifyContent="space-between">
        <Box sx={{ minWidth: 0 }}>
          <Typography component="h4" sx={{ fontWeight: 600, fontSize: 14, lineHeight: '20px' }}>
            {area.nombre}
          </Typography>
          <Stack direction="row" spacing={0.5} useFlexGap flexWrap="wrap" sx={{ mt: 0.5 }}>
            {area.automatica && <Chip size="small" variant="outlined" icon={<Sparkles size={12} aria-hidden />} label={t('expedientes.etapas.automatica')} />}
            {area.puedeRechazarExpediente && <Chip size="small" variant="outlined" icon={<ShieldX size={12} aria-hidden />} label={t('expedientes.etapas.puedeRechazar')} />}
            {area.slaVence && <Chip size="small" variant="outlined" icon={<Timer size={12} aria-hidden />} label={formatearFechaHora(area.slaVence)} />}
          </Stack>
        </Box>
        <Stack direction="row" spacing={0.5} alignItems="center" sx={{ flex: 'none' }}>
          <ChipEstadoArea estado={area.estado} desde={area.pendienteDesde} />
          {(aprobarAdmin || anular) && (
            <>
              <Tooltip title={t('expedientes.acciones.administrar', { area: area.nombre })}>
                <IconButton size="small" aria-label={t('expedientes.acciones.administrar', { area: area.nombre })} aria-haspopup="menu" onClick={(e) => setAncla(e.currentTarget)}>
                  <Ellipsis size={16} strokeWidth={1.75} />
                </IconButton>
              </Tooltip>
              <Menu anchorEl={ancla} open={Boolean(ancla)} onClose={() => setAncla(null)} anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }} transformOrigin={{ vertical: 'top', horizontal: 'right' }}>
                {aprobarAdmin && (
                  <MenuItem onClick={() => { setAncla(null); onAccion({ tipo: 'aprobarComoAdministrador', area }); }}>
                    <ListItemIcon><BadgeCheck size={16} strokeWidth={1.75} aria-hidden /></ListItemIcon>
                    <ListItemText primary={t('expedientes.acciones.aprobarComoAdministrador')} />
                  </MenuItem>
                )}
                {anular && (
                  <MenuItem onClick={() => { setAncla(null); onAccion({ tipo: 'anularVeredicto', area }); }}>
                    <ListItemIcon><Undo2 size={16} strokeWidth={1.75} aria-hidden /></ListItemIcon>
                    <ListItemText primary={t('expedientes.acciones.anularVeredicto')} />
                  </MenuItem>
                )}
              </Menu>
            </>
          )}
        </Stack>
      </Stack>

      <Stack direction="row" spacing={1.5} alignItems="center" useFlexGap flexWrap="wrap" sx={{ mt: 1.25 }}>
        <Aprobaciones aprobaciones={area.aprobaciones} requeridas={area.aprobadoresRequeridos} />
        {area.aprobacionesFaltantes > 0 && area.estado !== 'Esperando' && (
          <Typography component="span" color="text.secondary" sx={{ fontSize: 12 }}>
            {t('expedientes.etapas.faltan', { count: area.aprobacionesFaltantes })}
          </Typography>
        )}
      </Stack>
      {pendientes.length > 0 && area.estado === 'Pendiente' && (
        <Typography color="text.secondary" sx={{ fontSize: 12, mt: 0.5 }}>
          {t('expedientes.etapas.porPronunciarse', { nombres: [...new Set(pendientes)].join(', ') })}
        </Typography>
      )}

      <Veredictos veredictos={area.veredictos} nombre={nombre} motivos={motivos} />

      {(bloqueo === null || rechaza || explicar) && (
        <Stack direction="row" spacing={1} alignItems="center" useFlexGap flexWrap="wrap" sx={{ mt: 1.5 }}>
          {bloqueo === null && (
            <Button variant="contained" size="small" startIcon={<Gavel size={14} aria-hidden />} onClick={() => onAccion({ tipo: 'veredicto', area })}>
              {t('expedientes.acciones.emitir')}
            </Button>
          )}
          {rechaza && (
            <Button variant="outlined" color="error" size="small" onClick={() => onAccion({ tipo: 'rechazar', area })}>
              {t('expedientes.acciones.rechazar')}
            </Button>
          )}
          {explicar && (
            <Typography color="text.secondary" sx={{ fontSize: 12 }}>
              {t(`expedientes.bloqueos.${bloqueo}`)}
            </Typography>
          )}
        </Stack>
      )}
    </Box>
  );
}

function Veredictos({ veredictos, nombre, motivos }: { veredictos: readonly VeredictoEmitidoDto[]; nombre: (id: string) => string | null; motivos: ReadonlyMap<string, string> }) {
  const { t } = useTranslation();
  if (veredictos.length === 0) return null;
  return (
    <Box component="ul" sx={{ m: 0, mt: 1.25, p: 0, listStyle: 'none', display: 'flex', flexDirection: 'column', gap: 1, borderTop: 1, borderColor: 'divider', pt: 1.25 }}>
      {[...veredictos].reverse().map((v) => {
        const quien =
          v.origen === 'Sistema'
            ? t('expedientes.veredictos.sistema')
            : v.usuario
              ? (nombre(v.usuario) ?? (v.origen === 'Administrador' ? t('expedientes.veredictos.administrador') : t('expedientes.veredictos.persona')))
              : t('expedientes.veredictos.persona');
        const accion = v.origen === 'Administrador' ? t('expedientes.veredictos.porAdministrador', { nombre: quien }) : `${quien} · ${t(`expedientes.veredictos.${v.veredicto}`).toLowerCase()}`;
        const inactivo = !v.vigente;
        return (
          <Box component="li" key={v.id} sx={{ display: 'flex', gap: 1, opacity: inactivo ? 0.65 : 1 }}>
            <AvatarActor tipo={v.origen === 'Sistema' ? 'sistema' : 'persona'} nombre={quien} tamano={24} />
            <Box sx={{ minWidth: 0 }}>
              <Typography sx={{ fontSize: 13, lineHeight: '18px', textDecoration: v.anulado ? 'line-through' : 'none' }}>
                {accion}
                <Typography component="span" color="text.secondary" sx={{ fontSize: 12 }}>
                  {' · '}
                  {formatearFecha(v.fecha)}
                  {v.superado ? ` · ${t('expedientes.veredictos.superado')}` : ''}
                  {v.anulado ? ` · ${t('expedientes.veredictos.anulado')}` : ''}
                </Typography>
              </Typography>
              {v.motivos.length > 0 && (
                <Stack direction="row" spacing={0.5} useFlexGap flexWrap="wrap" sx={{ mt: 0.5 }}>
                  {v.motivos.map((m) => (
                    <Chip key={m} size="small" variant="outlined" label={motivos.get(m) ?? m} />
                  ))}
                </Stack>
              )}
              {(v.observacion ?? v.justificacion) && (
                <Typography color="text.secondary" sx={{ fontSize: 12, mt: 0.5, fontStyle: 'italic', whiteSpace: 'pre-wrap' }}>
                  “{v.observacion ?? v.justificacion}”
                </Typography>
              )}
            </Box>
          </Box>
        );
      })}
    </Box>
  );
}
