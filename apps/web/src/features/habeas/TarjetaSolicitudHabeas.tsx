import { Box, Button, Stack, Typography } from '@mui/material';
import type { SolicitudHabeasDto } from '@validasoft/api-client';
import { ChipEstado, formatearDiaCivil, formatearFechaHora } from '@validasoft/ui';
import { MessageSquareReply, ShieldCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { chipDeSolicitud, estaAbierta, estadoDePlazo } from './modelo';
import { NombreDeDerecho } from './NombreDeDerecho';

/**
 * Una solicitud del titular tal como la ve quien responde (OD-1, OD-3): derecho, radicado, quién la presentó, cuándo vence en días
 * hábiles y, si ya se respondió, qué se respondió. Sin respuesta, el botón abre el diálogo para atenderla o rechazarla.
 */
export function TarjetaSolicitudHabeas({ solicitud, onResponder }: { solicitud: SolicitudHabeasDto; onResponder: (s: SolicitudHabeasDto) => void }) {
  const { t } = useTranslation();
  const { estado, clave } = chipDeSolicitud(solicitud);
  const abierta = estaAbierta(solicitud);
  const vencida = estadoDePlazo(solicitud) === 'Vencida';

  return (
    <Box
      component="article"
      aria-labelledby={`habeas-${solicitud.id}`}
      sx={(th) => ({ p: { xs: 1.5, sm: 2 }, border: 1, borderColor: vencida ? 'error.main' : 'divider', borderRadius: `${String(th.radio.md)}px`, bgcolor: 'background.paper', minWidth: 0 })}
    >
      <Stack direction="row" useFlexGap sx={{ alignItems: 'flex-start', flexWrap: 'wrap', columnGap: 1.5, rowGap: 0.75 }}>
        <Box aria-hidden sx={(th) => ({ display: 'grid', placeItems: 'center', flex: 'none', width: 28, height: 28, borderRadius: `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2', color: 'text.secondary' })}>
          <ShieldCheck size={18} />
        </Box>
        <Box sx={{ minWidth: 0, flex: '1 1 220px' }}>
          <Typography id={`habeas-${solicitud.id}`} component="h3" sx={{ fontWeight: 600, fontSize: 15, lineHeight: '20px' }}>
            <NombreDeDerecho solicitud={solicitud} />
          </Typography>
          <Typography variant="mono" component="p" sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
            {solicitud.radicado}
          </Typography>
        </Box>
        <Box sx={{ ml: 'auto', flex: 'none' }}>
          <ChipEstado estado={estado} etiqueta={t(`habeas.estados.${clave}`)} size="medium" />
        </Box>
      </Stack>

      {solicitud.titular && (
        <Box component="dl" sx={{ m: 0, mt: 1.25, display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'minmax(0, 1fr) minmax(0, 1fr) minmax(0, 1.6fr)' }, gap: 1.5 }}>
          {(
            [
              ['nombre', solicitud.titular.nombre],
              ['identificacion', solicitud.titular.identificacion],
              ['correo', solicitud.titular.correo],
            ] as const
          ).map(([campo, valor]) => (
            <Box key={campo} sx={{ minWidth: 0 }}>
              <Typography component="dt" sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
                {t(`habeas.titular.${campo}`)}
              </Typography>
              <Typography component="dd" sx={{ m: 0, fontSize: 14, lineHeight: '20px', overflowWrap: 'anywhere' }}>
                {valor}
              </Typography>
            </Box>
          ))}
        </Box>
      )}

      {abierta && (
        <Typography sx={{ mt: 1.25, fontSize: 14, lineHeight: '20px', color: vencida ? 'error.main' : 'text.primary' }}>
          {vencida
            ? t('habeas.tarjeta.vencida', { fecha: formatearDiaCivil(solicitud.venceEl), count: Math.abs(solicitud.diasHabilesRestantes) })
            : solicitud.diasHabilesRestantes === 0
              ? t('habeas.tarjeta.venceHoy', { fecha: formatearDiaCivil(solicitud.venceEl) })
              : t('habeas.tarjeta.responderAntes', { fecha: formatearDiaCivil(solicitud.venceEl), count: solicitud.diasHabilesRestantes })}
        </Typography>
      )}

      {solicitud.detalle && (
        <Box sx={{ mt: 1 }}>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>{t('habeas.tarjeta.detalle')}</Typography>
          <Typography sx={{ fontSize: 14, lineHeight: '20px', whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{solicitud.detalle}</Typography>
        </Box>
      )}
      {solicitud.contenidoSuprimido && (
        <Typography sx={{ mt: 1, fontSize: 13, color: 'text.secondary' }}>{t('habeas.tarjeta.contenidoSuprimido')}</Typography>
      )}

      {!abierta && (
        <Box sx={{ mt: 1.25 }}>
          <Typography sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
            {t('habeas.tarjeta.resueltaEl', { fecha: solicitud.resueltaEn ? formatearFechaHora(solicitud.resueltaEn) : '—' })}
            {' · '}
            {solicitud.dentroDelPlazo === false ? t('habeas.tarjeta.fueraDePlazo') : t('habeas.tarjeta.dentroDePlazo')}
          </Typography>
          {solicitud.respuesta && (
            <Box sx={(th) => ({ mt: 0.5, borderLeft: 3, borderColor: 'divider', pl: 1.5, borderRadius: `${String(th.radio.sm)}px` })}>
              <Typography sx={{ fontSize: 14, lineHeight: '20px', whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{solicitud.respuesta}</Typography>
            </Box>
          )}
        </Box>
      )}

      {abierta && (
        <Stack direction="row" sx={{ mt: 1.5, justifyContent: 'flex-end' }}>
          <Button variant="contained" size="small" startIcon={<MessageSquareReply size={14} aria-hidden />} onClick={() => onResponder(solicitud)}>
            {t('habeas.tarjeta.responder')}
          </Button>
        </Stack>
      )}
    </Box>
  );
}
