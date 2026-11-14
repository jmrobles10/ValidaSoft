import { Box, Button, Chip, Stack, Typography } from '@mui/material';
import { SCOPES, type ExencionDeVinculoDto, type VinculoDto } from '@validasoft/api-client';
import { EstadoVacio, formatearFecha, MarcoTabla } from '@validasoft/ui';
import { ShieldOff, Undo2 } from 'lucide-react';
import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Tarjeta } from '../../../shared/componentes';
import { usePermisos } from '../../../shared/usePermisos';
import { useNombresUsuarios } from '../../configuracion/usuarios/api';
import { useTercero } from '../api';
import { DialogoEximir } from './DialogoEximir';
import { DialogoRevocarExencion } from './DialogoRevocarExencion';
import { admiteExencion, alcanceDe, cierreDe, vigenciaDe } from './modelo';

const COLOR_ESTADO = { Vigente: 'success', Revocada: 'default', Vencida: 'warning' } as const;

/**
 * Exenciones del vínculo (EX-6): qué se eximió, por qué, hasta cuándo y quién (con la revocación o el vencimiento de las que ya no rigen). Eximir y revocar
 * piden `vinculos:eximir`; quien solo lee ve la lista. Es una decisión privada de la empresa: nada de esto llega al tercero ni a otra empresa. Sin exenciones
 * y sin permiso, la tarjeta no aparece.
 */
export function TarjetaExenciones({ vinculo, empresaId }: { vinculo: VinculoDto; empresaId: string }) {
  const { t } = useTranslation();
  const tercero = useTercero(vinculo.terceroId);
  const permisos = usePermisos(empresaId);
  const puedeEximir = permisos.puede(SCOPES.vinculosEximir) && admiteExencion(vinculo.estado);
  const puedeRevocar = permisos.puede(SCOPES.vinculosEximir) && vinculo.estado !== 'Anulado' && vinculo.estado !== 'Desvinculado';
  const exenciones = useMemo(() => vinculo.exenciones ?? [], [vinculo.exenciones]);
  const personas = useMemo(() => exenciones.flatMap((e) => [e.por, e.revocadaPor ?? '']).filter(Boolean), [exenciones]);
  const nombres = useNombresUsuarios(empresaId, personas);
  const [eximiendo, setEximiendo] = useState(false);
  const [revocando, setRevocando] = useState<ExencionDeVinculoDto | null>(null);
  const nombreDe = (id: string) => nombres.get(id) ?? t('exenciones.personaDesconocida');

  if (exenciones.length === 0 && !puedeEximir) return null;

  return (
    <Tarjeta
      titulo={t('exenciones.titulo')}
      acciones={
        puedeEximir ? (
          <Button size="small" variant="outlined" startIcon={<ShieldOff size={14} aria-hidden />} onClick={() => setEximiendo(true)}>
            {t('exenciones.eximir')}
          </Button>
        ) : undefined
      }
    >
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1 }}>
        {t('exenciones.ayuda')}
      </Typography>
      {exenciones.length === 0 ? (
        <MarcoTabla>
          <EstadoVacio compacto titulo={t('exenciones.sinExenciones')} texto={t('exenciones.sinExencionesAyuda')} />
        </MarcoTabla>
      ) : (
        <Stack component="ul" spacing={1} sx={{ listStyle: 'none', m: 0, p: 0 }}>
          {exenciones.map((e) => {
            const cierre = cierreDe(e, nombreDe, t);
            return (
              <Box component="li" key={e.id} sx={{ border: 1, borderColor: 'divider', borderRadius: 1, p: 1.25 }}>
                <Stack direction="row" spacing={1} sx={{ alignItems: 'flex-start', justifyContent: 'space-between' }}>
                  <Box sx={{ minWidth: 0 }}>
                    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }} useFlexGap>
                      <Chip size="small" color={COLOR_ESTADO[e.estado]} variant={e.estado === 'Vigente' ? 'filled' : 'outlined'} label={t(`exenciones.estados.${e.estado}`)} />
                      <Typography component="span" variant="body2" sx={{ fontWeight: 600 }}>
                        {alcanceDe(e, t)}
                      </Typography>
                    </Stack>
                    <Typography variant="body2" sx={{ mt: 0.5, overflowWrap: 'anywhere' }}>
                      {e.justificacion}
                    </Typography>
                    <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', mt: 0.5 }}>
                      {vigenciaDe(e, t)} · {t('exenciones.hechaPor', { persona: nombreDe(e.por), fecha: formatearFecha(e.creadaEn) })}
                    </Typography>
                    {cierre && (
                      <Typography variant="caption" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, overflowWrap: 'anywhere' }}>
                        {cierre}
                      </Typography>
                    )}
                  </Box>
                  {e.estado === 'Vigente' && puedeRevocar && (
                    <Button
                      size="small"
                      variant="text"
                      startIcon={<Undo2 size={14} aria-hidden />}
                      aria-label={t('exenciones.revocarFila', { alcance: alcanceDe(e, t) })}
                      onClick={() => setRevocando(e)}
                      sx={{ flex: 'none' }}
                    >
                      {t('exenciones.revocar')}
                    </Button>
                  )}
                </Stack>
              </Box>
            );
          })}
        </Stack>
      )}
      {eximiendo && <DialogoEximir empresaId={empresaId} vinculoId={vinculo.id} tercero={tercero.data?.nombre} onCerrar={() => setEximiendo(false)} />}
      {revocando && <DialogoRevocarExencion empresaId={empresaId} vinculoId={vinculo.id} exencion={revocando} onCerrar={() => setRevocando(null)} />}
    </Tarjeta>
  );
}
