import { Box, Checkbox, FormControlLabel, FormGroup, FormHelperText, FormLabel } from '@mui/material';
import { useTranslation } from 'react-i18next';

export interface EvidenciasDelEstudioProps {
  /** Documentos del expediente con versión que se pueden citar (`documentoId:version` y su nombre). */
  posibles: readonly { clave: string; nombre: string }[];
  elegidas: readonly string[];
  onCambiar: (claves: string[]) => void;
}

/** Documentos del expediente que soportan el estudio: el oficial marca los que cita. */
export function EvidenciasDelEstudio({ posibles, elegidas, onCambiar }: EvidenciasDelEstudioProps) {
  const { t } = useTranslation();
  return (
    <Box>
      <FormLabel sx={{ fontSize: 13 }}>{t('cumplimiento.panel.estudio.evidencias')}</FormLabel>
      <FormHelperText sx={{ mt: 0 }}>{posibles.length === 0 ? t('cumplimiento.estudio.sinEvidencias') : t('cumplimiento.estudio.evidenciasAyuda')}</FormHelperText>
      <FormGroup>
        {posibles.map((p) => (
          <FormControlLabel
            key={p.clave}
            control={<Checkbox checked={elegidas.includes(p.clave)} onChange={(e) => onCambiar(e.target.checked ? [...elegidas, p.clave] : elegidas.filter((x) => x !== p.clave))} />}
            label={p.nombre}
          />
        ))}
      </FormGroup>
    </Box>
  );
}
