import { Box, Button, Chip, Stack, Typography } from '@mui/material';
import type { CoincidenciaDto, DecisionCoincidencia } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { ShieldCheck, ShieldX } from 'lucide-react';
import { useTranslation } from 'react-i18next';

/** Una coincidencia de la consulta de listas con su decisión, o los botones para descartarla o confirmarla. */
export function CoincidenciaDeListas({
  coincidencia: c,
  puedeDecidir,
  quien,
  onResolver,
}: {
  coincidencia: CoincidenciaDto;
  puedeDecidir: boolean;
  quien: (id: string | null | undefined) => string;
  onResolver: (decision: DecisionCoincidencia) => void;
}) {
  const { t } = useTranslation();
  return (
    <Box component="li" sx={{ border: 1, borderColor: c.decision ? 'divider' : 'warning.main', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.25 }} aria-label={`${c.persona} · ${c.lista}`}>
      <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
        <Typography sx={{ fontWeight: 500, fontSize: 14 }}>{c.persona}</Typography>
        <Typography component="span" sx={{ color: 'text.secondary', fontSize: 12 }}>
          {t(`cumplimiento.roles.${c.rol as 'Tercero'}`, { defaultValue: c.rol })} · {c.identificacion}
        </Typography>
        <Chip size="small" variant="outlined" label={c.lista} />
        {c.puntaje !== null && (
          <Typography component="span" variant="mono" sx={{ fontSize: 12 }}>
            {t('cumplimiento.panel.puntaje', { puntaje: String(c.puntaje) })}
          </Typography>
        )}
      </Stack>
      <Typography sx={{ fontSize: 13, mt: 0.5 }}>
        «{c.nombreEncontrado}»{c.detalle ? ` · ${c.detalle}` : ''}
      </Typography>
      {c.decision ? (
        <Typography sx={{ fontSize: 12, color: 'text.secondary', mt: 0.5 }}>
          {c.heredada
            ? t('cumplimiento.panel.heredada', { fecha: c.vigenteHasta ? formatearFecha(c.vigenteHasta) : '' })
            : t('cumplimiento.panel.decidida', {
                decision: t(`cumplimiento.decisiones.${c.decision as DecisionCoincidencia}`),
                quien: quien(c.resueltaPor),
                fecha: c.resueltaEn ? formatearFecha(c.resueltaEn) : '',
              })}
          {c.justificacion ? ` — “${c.justificacion}”` : ''}
        </Typography>
      ) : (
        puedeDecidir && (
          <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
            <Button size="small" variant="outlined" startIcon={<ShieldCheck size={16} aria-hidden />} onClick={() => onResolver('Descartada')}>
              {t('cumplimiento.panel.descartar')}
            </Button>
            <Button size="small" variant="outlined" color="error" startIcon={<ShieldX size={16} aria-hidden />} onClick={() => onResolver('Confirmada')}>
              {t('cumplimiento.panel.confirmar')}
            </Button>
          </Stack>
        )
      )}
    </Box>
  );
}
