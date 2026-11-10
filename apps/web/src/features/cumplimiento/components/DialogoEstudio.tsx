import {
  Alert,
  Box,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  FormGroup,
  FormHelperText,
  FormLabel,
  MenuItem,
  Stack,
  Switch,
  TextField,
  ToggleButton,
  ToggleButtonGroup,
  Typography,
} from '@mui/material';
import type { ConceptoCumplimiento, DebidaDiligencia, ExpedienteDto, RegistrarEstudioCumplimientoDto } from '@validasoft/api-client';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { claveRiesgo, ESTUDIO_VACIO, estudioADto, evidenciasPosibles, validarEstudio, type BorradorEstudio } from '../modelo';

export interface DialogoEstudioProps {
  expediente: ExpedienteDto;
  /** Vigencia del flujo; `null` = la de la configuración (30 días). */
  vigenciaDias: number | null;
  ocupado: boolean;
  error: unknown;
  onGuardar: (dto: RegistrarEstudioCumplimientoDto) => void;
  onCerrar: () => void;
}

/**
 * Estudio del oficial (campos del legado): nivel de riesgo 1–3, conflicto de interés, debida diligencia simple o ampliada
 * (obligatoria con riesgo alto), concepto, observaciones (obligatorias con conflicto o concepto desfavorable) y evidencias
 * tomadas de los documentos del expediente.
 */
export function DialogoEstudio({ expediente, vigenciaDias, ocupado, error, onGuardar, onCerrar }: DialogoEstudioProps) {
  const { t } = useTranslation();
  const [borrador, setBorrador] = useState<BorradorEstudio>(ESTUDIO_VACIO);
  const [intentado, setIntentado] = useState(false);
  const errores = validarEstudio(borrador);
  const visible = (codigo: (typeof errores)[number]) => intentado && errores.includes(codigo);
  const posibles = evidenciasPosibles(expediente);
  const cambiar = (cambio: Partial<BorradorEstudio>) => setBorrador((b) => ({ ...b, ...cambio }));

  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 600, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-estudio">
      <DialogTitle id="titulo-estudio">
        {t('cumplimiento.estudio.titulo')}
        <Typography variant="body2" component="span" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
          {vigenciaDias ? t('cumplimiento.estudio.ayuda', { dias: String(vigenciaDias) }) : t('cumplimiento.estudio.ayudaSinDias')}
        </Typography>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2.5} sx={{ pt: 1 }}>
          <Box>
            <FormLabel id="riesgo" sx={{ fontSize: 13 }}>
              {t('cumplimiento.panel.estudio.riesgo')}
            </FormLabel>
            <ToggleButtonGroup
              exclusive
              size="small"
              aria-labelledby="riesgo"
              value={borrador.nivelRiesgo}
              onChange={(_, valor: 1 | 2 | 3 | null) => cambiar({ nivelRiesgo: valor })}
              sx={{ display: 'flex', mt: 0.5 }}
            >
              {([1, 2, 3] as const).map((n) => (
                <ToggleButton key={n} value={n} sx={{ flex: 1 }}>
                  {t(`cumplimiento.riesgos.${claveRiesgo(n)}`)}
                </ToggleButton>
              ))}
            </ToggleButtonGroup>
            {visible('nivel-riesgo-requerido') && <FormHelperText error>{t('cumplimiento.errores.nivel-riesgo-requerido')}</FormHelperText>}
          </Box>
          <TextField
            select
            label={t('cumplimiento.panel.estudio.debida')}
            value={borrador.debidaDiligencia}
            onChange={(e) => cambiar({ debidaDiligencia: e.target.value as DebidaDiligencia })}
            error={visible('debida-diligencia-ampliada-requerida')}
            helperText={visible('debida-diligencia-ampliada-requerida') ? t('cumplimiento.errores.debida-diligencia-ampliada-requerida') : undefined}
          >
            {(['Simple', 'Ampliada'] as const).map((d) => (
              <MenuItem key={d} value={d}>
                {t(`cumplimiento.debida.${d}`)}
              </MenuItem>
            ))}
          </TextField>
          <FormControlLabel
            control={<Switch checked={borrador.conflictoDeInteres} onChange={(e) => cambiar({ conflictoDeInteres: e.target.checked })} />}
            label={t('cumplimiento.estudio.conflictoSi')}
            sx={{ mx: 0 }}
          />
          <TextField
            select
            label={t('cumplimiento.panel.estudio.concepto')}
            value={borrador.concepto ?? ''}
            onChange={(e) => cambiar({ concepto: e.target.value as ConceptoCumplimiento })}
            error={visible('concepto-requerido')}
            helperText={visible('concepto-requerido') ? t('cumplimiento.errores.concepto-requerido') : undefined}
          >
            {(['Favorable', 'Desfavorable'] as const).map((c) => (
              <MenuItem key={c} value={c}>
                {t(`cumplimiento.conceptos.${c}`)}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            label={t('cumplimiento.panel.estudio.observaciones')}
            value={borrador.observaciones}
            onChange={(e) => cambiar({ observaciones: e.target.value })}
            multiline
            minRows={3}
            error={visible('observaciones-requeridas')}
            helperText={visible('observaciones-requeridas') ? t('cumplimiento.errores.observaciones-requeridas') : undefined}
            slotProps={{ htmlInput: { maxLength: 4000 } }}
          />
          <Box>
            <FormLabel sx={{ fontSize: 13 }}>{t('cumplimiento.panel.estudio.evidencias')}</FormLabel>
            <FormHelperText sx={{ mt: 0 }}>{posibles.length === 0 ? t('cumplimiento.estudio.sinEvidencias') : t('cumplimiento.estudio.evidenciasAyuda')}</FormHelperText>
            <FormGroup>
              {posibles.map((p) => (
                <FormControlLabel
                  key={p.clave}
                  control={
                    <Checkbox
                      checked={borrador.evidencias.includes(p.clave)}
                      onChange={(e) =>
                        cambiar({ evidencias: e.target.checked ? [...borrador.evidencias, p.clave] : borrador.evidencias.filter((x) => x !== p.clave) })
                      }
                    />
                  }
                  label={p.nombre}
                />
              ))}
            </FormGroup>
          </Box>
          {error !== null && error !== undefined && <Alert severity="error">{mensajeDeError(error, t)}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button
          variant="contained"
          disabled={ocupado}
          startIcon={ocupado ? <IconoCarga /> : undefined}
          onClick={() => {
            setIntentado(true);
            if (errores.length === 0) onGuardar(estudioADto(borrador));
          }}
        >
          {t('cumplimiento.estudio.guardar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
