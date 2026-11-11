import { Box, IconButton, MenuItem, Stack, TextField, Typography } from '@mui/material';
import type { PersonaConsultadaDto } from '@validasoft/api-client';
import { Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import type { BorradorCoincidenciaManual } from '../consultaManual';
import { CampoNumero } from '@validasoft/ui';

export interface FilaCoincidenciaManualProps {
  numero: number;
  coincidencia: BorradorCoincidenciaManual;
  personas: readonly PersonaConsultadaDto[];
  claves: readonly string[];
  /** Marca en rojo lo que falta (después de intentar enviar). */
  conErrores: boolean;
  onCambiar: (cambio: Partial<BorradorCoincidenciaManual>) => void;
  onQuitar: () => void;
}

/** Una coincidencia hallada: la persona de la consulta, la lista, el nombre que aparece y, opcional, el detalle y la similitud. */
export function FilaCoincidenciaManual({ numero, coincidencia: c, personas, claves, conErrores, onCambiar, onQuitar }: FilaCoincidenciaManualProps) {
  const { t } = useTranslation();
  return (
    <Box component="li" sx={{ border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.sm)}px`, p: 1.5, listStyle: 'none' }} aria-label={t('cumplimiento.manual.coincidencia', { n: String(numero) })}>
      <Stack spacing={1.5}>
        <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between' }}>
          <Typography sx={{ fontSize: 13, fontWeight: 500 }}>{t('cumplimiento.manual.coincidencia', { n: String(numero) })}</Typography>
          <IconButton size="small" aria-label={t('cumplimiento.manual.quitar', { n: String(numero) })} onClick={onQuitar}>
            <Trash2 size={16} aria-hidden />
          </IconButton>
        </Stack>
        <TextField
          select
          label={t('cumplimiento.manual.persona')}
          value={c.persona}
          onChange={(e) => onCambiar({ persona: e.target.value })}
          error={conErrores && c.persona === ''}
          slotProps={{ select: { displayEmpty: true } }}
        >
          <MenuItem value="" disabled>
            {t('cumplimiento.matriz.elige')}
          </MenuItem>
          {personas.map((p, i) => (
            <MenuItem key={claves[i] ?? p.identificacion} value={claves[i] ?? ''}>
              {`${p.nombre} · ${t(`cumplimiento.roles.${p.rol as 'Tercero'}`, { defaultValue: p.rol })} · ${p.identificacion}`}
            </MenuItem>
          ))}
        </TextField>
        <Box sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))' }, gap: 1.5 }}>
          <TextField label={t('cumplimiento.manual.lista')} value={c.lista} onChange={(e) => onCambiar({ lista: e.target.value })} error={conErrores && c.lista.trim() === ''} slotProps={{ htmlInput: { maxLength: 100 } }} />
          <TextField label={t('cumplimiento.manual.nombre')} value={c.nombre} onChange={(e) => onCambiar({ nombre: e.target.value })} error={conErrores && c.nombre.trim() === ''} slotProps={{ htmlInput: { maxLength: 200 } }} />
          <TextField label={t('cumplimiento.manual.detalle')} value={c.detalle} onChange={(e) => onCambiar({ detalle: e.target.value })} slotProps={{ htmlInput: { maxLength: 500 } }} />
          <CampoNumero etiqueta={t('cumplimiento.manual.puntaje')} valor={c.puntaje} onCambiar={(puntaje) => onCambiar({ puntaje })} maxDigitos={3} fullWidth />
        </Box>
      </Stack>
    </Box>
  );
}
