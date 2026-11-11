import { Box, Chip, FormControlLabel, Radio, RadioGroup, Stack, Typography } from '@mui/material';
import { Bell, Bot, Hand, Sparkles, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { NIVELES, type NivelDeRevision } from '../../revisionAutomatica/modelo';

const ICONOS: Readonly<Record<NivelDeRevision, LucideIcon>> = { Manual: Hand, Asistido: Sparkles, Automatico: Bot };

export interface TarjetasDeNivelProps {
  valor: NivelDeRevision;
  onCambiar: (nivel: NivelDeRevision) => void;
  /** Automático necesita el plan de IA activo con su llave: sin él no se puede elegir. */
  automaticoDisponible: boolean;
  deshabilitado: boolean;
}

/**
 * Los tres niveles de la revisión automática como tarjetas-opción (un grupo de radios): qué hace cada uno y a quién avisa.
 * Automático se deshabilita, con su motivo, si la empresa no tiene el plan de IA activo y con su llave.
 */
export function TarjetasDeNivel({ valor, onCambiar, automaticoDisponible, deshabilitado }: TarjetasDeNivelProps) {
  const { t } = useTranslation();
  return (
    <RadioGroup
      aria-label={t('revisionAutomatica.config.nivel.grupo')}
      value={valor}
      onChange={(_, elegido) => onCambiar(elegido as NivelDeRevision)}
      sx={{ display: 'grid', gridTemplateColumns: { xs: 'minmax(0, 1fr)', md: 'repeat(3, minmax(0, 1fr))' }, gap: 1.5 }}
    >
      {NIVELES.map((nivel) => {
        const Icono = ICONOS[nivel];
        const elegido = valor === nivel;
        const bloqueado = nivel === 'Automatico' && !automaticoDisponible;
        return (
          <FormControlLabel
            key={nivel}
            value={nivel}
            disabled={deshabilitado || bloqueado}
            control={<Radio size="small" sx={{ p: 0.25, mt: 0.25 }} />}
            sx={{
              m: 0,
              alignItems: 'flex-start',
              gap: 1,
              py: 1.5,
              px: 1.5,
              border: 1,
              borderColor: elegido ? 'acento.main' : 'divider',
              borderRadius: (th) => `${String(th.radio.md)}px`,
              bgcolor: elegido ? 'acento.tinte' : 'background.paper',
              '&:hover': { borderColor: elegido ? 'acento.main' : 'borde.control' },
              '& .MuiFormControlLabel-label': { minWidth: 0, flex: 1 },
            }}
            label={
              <Stack spacing={0.75}>
                <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                  <Icono size={16} strokeWidth={1.75} aria-hidden />
                  <Typography component="span" sx={{ fontWeight: 600, fontSize: 15, lineHeight: '22px' }}>
                    {t(`revisionAutomatica.niveles.${nivel}`)}
                  </Typography>
                  {bloqueado && <Chip size="small" variant="outlined" label={t('revisionAutomatica.config.nivel.noDisponible')} sx={{ borderRadius: (th) => `${String(th.radio.xs)}px` }} />}
                </Stack>
                <Typography component="span" variant="body2" sx={{ display: 'block' }}>
                  {t(`revisionAutomatica.config.nivel.${nivel}.texto`)}
                </Typography>
                <Box sx={{ display: 'flex', alignItems: 'flex-start', gap: 0.75, color: 'text.secondary' }}>
                  <Bell size={14} strokeWidth={1.75} aria-hidden style={{ flex: 'none', marginTop: 2 }} />
                  <Typography component="span" variant="caption">
                    {t('revisionAutomatica.config.nivel.avisaA')}: {t(`revisionAutomatica.config.nivel.${nivel}.avisa`)}
                  </Typography>
                </Box>
              </Stack>
            }
          />
        );
      })}
    </RadioGroup>
  );
}
