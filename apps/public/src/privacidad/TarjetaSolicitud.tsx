import { Alert, Box, Button, Card, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Typography, useMediaQuery, useTheme } from '@mui/material';
import type { ConstanciaHabeasDto, SolicitudHabeasDto } from '@validasoft/api-client';
import { ChipEstado, EsqueletoTarjetas, formatearDiaCivil, type Estado } from '@validasoft/ui';
import { FileCheck2, ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable } from '../shared/componentes';
import { useConstancia } from './api';
import { esAlcanceDeSupresion, esResultadoDeSolicitud, esTipoDeSolicitud, estadoDePlazo } from './modelo';

type ClaveDeEstadoDeSolicitud = 'rechazada' | 'parcial' | 'atendida' | 'vencida' | 'porVencer' | 'enPlazo';

function estadoDeLaSolicitud(s: SolicitudHabeasDto): { estado: Estado; etiqueta: ClaveDeEstadoDeSolicitud } {
  if (s.estado === 'Resuelta') {
    return s.resultado === 'Rechazada'
      ? { estado: 'Rechazado', etiqueta: 'rechazada' }
      : { estado: 'Aprobado', etiqueta: s.resultado === 'Parcial' ? 'parcial' : 'atendida' };
  }
  const plazo = estadoDePlazo(s);
  return plazo === 'Vencida' ? { estado: 'Vencido', etiqueta: 'vencida' } : plazo === 'PorVencer' ? { estado: 'PorVencer', etiqueta: 'porVencer' } : { estado: 'EnRevision', etiqueta: 'enPlazo' };
}

function Constancia({ constancia }: { constancia: ConstanciaHabeasDto }) {
  const { t } = useTranslation();
  return (
    <Stack spacing={1.5}>
      <Typography sx={{ color: 'text.secondary', fontSize: 14 }}>{t('privacidad.constancia.ayuda')}</Typography>
      <Box component="dl" sx={{ m: 0, display: 'grid', gridTemplateColumns: 'max-content minmax(0, 1fr)', columnGap: 2, rowGap: 0.5, fontSize: 14 }}>
        {(
          [
            ['radicado', constancia.radicado],
            ['respondio', constancia.destino],
            ['recibida', formatearDiaCivil(constancia.recibidaEl)],
            ['resuelta', formatearDiaCivil(constancia.resueltaEl)],
            ['plazo', constancia.dentroDelPlazo ? t('privacidad.constancia.enPlazo') : t('privacidad.constancia.fueraDePlazo')],
            ['resultado', esResultadoDeSolicitud(constancia.resultado) ? t(`privacidad.solicitud.resultados.${constancia.resultado}`) : constancia.resultado],
          ] as const
        ).map(([clave, valor]) => (
          <Box key={clave} sx={{ display: 'contents' }}>
            <Typography component="dt" sx={{ color: 'text.secondary', fontSize: 13 }}>
              {t(`privacidad.constancia.campos.${clave}`)}
            </Typography>
            <Typography component="dd" sx={{ m: 0, fontSize: 14, overflowWrap: 'anywhere' }}>
              {valor}
            </Typography>
          </Box>
        ))}
      </Box>
      {constancia.suprimidos.length > 0 && (
        <Box>
          <Typography component="h3" sx={{ fontWeight: 600, fontSize: 14 }}>
            {t('privacidad.constancia.suprimidos')}
          </Typography>
          <Box component="ul" sx={{ m: 0, pl: 2.5, fontSize: 14 }}>
            {constancia.suprimidos.map((e) => (
              <li key={`${e.tipo}-${e.referencia}`}>{e.descripcion}</li>
            ))}
          </Box>
        </Box>
      )}
      {constancia.retenidos.length > 0 && (
        <Box>
          <Typography component="h3" sx={{ fontWeight: 600, fontSize: 14 }}>
            {t('privacidad.constancia.retenidos')}
          </Typography>
          <Box component="ul" sx={{ m: 0, pl: 2.5, fontSize: 14 }}>
            {constancia.retenidos.map((e) => (
              <li key={`${e.tipo}-${e.referencia}`}>
                {e.descripcion}
                {e.retenidaHasta ? ` — ${t('privacidad.constancia.hasta', { fecha: formatearDiaCivil(e.retenidaHasta) })}` : ''}
                <Typography component="span" sx={{ display: 'block', color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
                  {e.baseLegal}
                </Typography>
              </li>
            ))}
          </Box>
        </Box>
      )}
      {constancia.anonimizados.length > 0 && (
        <Typography sx={{ fontSize: 14 }}>
          {t('privacidad.constancia.anonimizados')}: {constancia.anonimizados.join(', ')}.
        </Typography>
      )}
      <Box>
        <Typography sx={{ color: 'text.secondary', fontSize: 13 }}>{t('privacidad.constancia.huella')}</Typography>
        <Typography variant="mono" component="p" sx={{ fontSize: 12, overflowWrap: 'anywhere' }}>
          {constancia.hash}
        </Typography>
      </Box>
    </Stack>
  );
}

function DialogoConstancia({ solicitud, onCerrar }: { solicitud: SolicitudHabeasDto; onCerrar: () => void }) {
  const { t } = useTranslation();
  const tema = useTheme();
  const pantallaCompleta = useMediaQuery(tema.breakpoints.down('sm'));
  const constancia = useConstancia(solicitud.id, true);
  return (
    <Dialog open onClose={onCerrar} fullWidth maxWidth="sm" fullScreen={pantallaCompleta} aria-labelledby="titulo-constancia">
      <DialogTitle id="titulo-constancia">{t('privacidad.constancia.titulo', { radicado: solicitud.radicado })}</DialogTitle>
      <DialogContent>
        {constancia.isPending ? <EsqueletoTarjetas cantidad={1} lineas={5} /> : constancia.isError ? <ErrorRecuperable error={constancia.error} onReintentar={() => void constancia.refetch()} /> : <Constancia constancia={constancia.data} />}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2 }}>
        <Button variant="contained" onClick={onCerrar}>
          {t('comun.cerrar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/** Una solicitud del tercero: radicado, derecho, a quién, cuándo se responde y, si ya se respondió, la respuesta y su constancia. */
export function TarjetaSolicitud({ solicitud }: { solicitud: SolicitudHabeasDto }) {
  const { t } = useTranslation();
  const [viendoConstancia, setViendoConstancia] = useState(false);
  const { estado, etiqueta } = estadoDeLaSolicitud(solicitud);
  const resuelta = solicitud.estado === 'Resuelta';
  const plazo = estadoDePlazo(solicitud);

  return (
    <Card component="article" aria-labelledby={`solicitud-${solicitud.id}`} sx={{ p: { xs: 1.5, sm: 2 } }}>
      <Stack direction="row" useFlexGap sx={{ alignItems: 'flex-start', flexWrap: 'wrap', columnGap: 1.25, rowGap: 0.5 }}>
        <Box aria-hidden sx={(th) => ({ display: 'grid', placeItems: 'center', flex: 'none', width: 28, height: 28, borderRadius: `${String(th.radio.sm)}px`, bgcolor: 'superficie.nivel2', color: 'text.secondary' })}>
          <ShieldCheck size={18} />
        </Box>
        <Box sx={{ minWidth: 0, flex: '1 1 200px' }}>
          <Typography id={`solicitud-${solicitud.id}`} component="h3" sx={{ fontWeight: 600, fontSize: 15, lineHeight: '20px' }}>
            {esTipoDeSolicitud(solicitud.tipo) ? t(`privacidad.solicitud.tipos.${solicitud.tipo}.titulo`) : solicitud.tipo}
          </Typography>
          <Typography variant="mono" component="p" sx={{ color: 'text.secondary', fontSize: 12, lineHeight: '16px' }}>
            {solicitud.radicado}
          </Typography>
          <Typography sx={{ color: 'text.secondary', fontSize: 13, lineHeight: '18px' }}>
            {t('privacidad.solicitud.dirigidaA', { destino: solicitud.destino.nombre })}
            {solicitud.alcance && esAlcanceDeSupresion(solicitud.alcance) ? ` · ${t(`privacidad.solicitud.alcances.${solicitud.alcance}`)}` : ''}
          </Typography>
        </Box>
        <Box sx={{ ml: 'auto', flex: 'none' }}>
          <ChipEstado estado={estado} etiqueta={t(`privacidad.solicitud.estados.${etiqueta}`)} size="medium" />
        </Box>
      </Stack>

      {!resuelta && (
        <Typography sx={{ mt: 1, fontSize: 14, lineHeight: '20px', color: plazo === 'Vencida' ? 'error.main' : 'text.primary' }}>
          {plazo === 'Vencida'
            ? t('privacidad.solicitud.vencida', { fecha: formatearDiaCivil(solicitud.venceEl) })
            : solicitud.diasHabilesRestantes === 0
              ? t('privacidad.solicitud.responderemosHoy', { fecha: formatearDiaCivil(solicitud.venceEl) })
              : t('privacidad.solicitud.responderemos', { fecha: formatearDiaCivil(solicitud.venceEl), count: solicitud.diasHabilesRestantes })}
        </Typography>
      )}

      {solicitud.detalle && !resuelta && (
        <Typography sx={{ mt: 0.75, fontSize: 13, color: 'text.secondary', whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{solicitud.detalle}</Typography>
      )}

      {resuelta && (
        <Box sx={{ mt: 1 }}>
          {solicitud.contenidoSuprimido ? (
            <Alert severity="info" icon={<ShieldCheck size={18} />}>
              {t('privacidad.solicitud.contenidoSuprimido')}
            </Alert>
          ) : (
            solicitud.respuesta && (
              <Box sx={(th) => ({ borderLeft: 3, borderColor: 'divider', pl: 1.5, borderRadius: `${String(th.radio.sm)}px` })}>
                <Typography sx={{ color: 'text.secondary', fontSize: 12 }}>{t('privacidad.solicitud.respuesta', { destino: solicitud.destino.nombre })}</Typography>
                <Typography sx={{ fontSize: 14, lineHeight: '20px', whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{solicitud.respuesta}</Typography>
              </Box>
            )
          )}
          <Button variant="text" onClick={() => setViendoConstancia(true)} startIcon={<FileCheck2 size={16} aria-hidden />} sx={{ mt: 0.5 }}>
            {t('privacidad.solicitud.verConstancia')}
          </Button>
        </Box>
      )}
      {viendoConstancia && <DialogoConstancia solicitud={solicitud} onCerrar={() => setViendoConstancia(false)} />}
    </Card>
  );
}
