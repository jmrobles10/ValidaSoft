import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Radio, RadioGroup, Stack, Typography } from '@mui/material';
import type { ApiKeyDto } from '@validasoft/api-client';
import { RefreshCw } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useRotarApiKey } from './api';
import { anteriorValidaHasta, claveVisible, SOLAPAMIENTOS, type Solapamiento } from './modelo';

export interface DialogoRotarApiKeyProps {
  llave: ApiKeyDto;
  onCerrar: () => void;
  /** Recibe la llave nueva con `valor` (la clave completa, una sola vez) y hasta cuándo vale la anterior. */
  onRotada: (nueva: ApiKeyDto, anteriorHasta: string) => void;
}

/**
 * Rotar una API key (CN-3): se emite otra con los mismos permisos y alcance, y la actual sigue valiendo el tiempo elegido
 * (24 horas por defecto) para que el sistema cambie la clave sin cortar el servicio.
 */
export function DialogoRotarApiKey({ llave, onCerrar, onRotada }: DialogoRotarApiKeyProps) {
  const { t } = useTranslation();
  const [solapamiento, setSolapamiento] = useState<Solapamiento>('24h');
  const rotar = useRotarApiKey();

  const confirmar = () => {
    const hasta = anteriorValidaHasta(solapamiento, new Date());
    rotar.mutate({ id: llave.id, anteriorValidaHasta: hasta }, { onSuccess: (nueva) => onRotada(nueva, hasta) });
  };

  return (
    <Dialog open onClose={rotar.isPending ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 480, maxWidth: 'calc(100% - 32px)' } } }} aria-labelledby="titulo-rotar">
      <DialogTitle id="titulo-rotar">
        {t('apiKeys.rotarA', { nombre: llave.nombre })}
        <Typography variant="body2" component="span" sx={{ color: 'text.secondary', display: 'block', mt: 0.25, fontWeight: 400 }}>
          {claveVisible(llave)}
        </Typography>
      </DialogTitle>
      <DialogContent>
        <Stack spacing={2}>
          {rotar.isError && <Alert severity="error">{mensajeDeError(rotar.error, t)}</Alert>}
          <Typography variant="body2">{t('apiKeys.rotarAyuda')}</Typography>
          <Box component="fieldset" sx={{ border: 0, m: 0, p: 0, minWidth: 0 }}>
            <Typography component="legend" sx={{ fontSize: 14, fontWeight: 600 }}>
              {t('apiKeys.rotarSolapamiento')}
            </Typography>
            <RadioGroup value={solapamiento} onChange={(e) => setSolapamiento(e.target.value as Solapamiento)}>
              {(Object.keys(SOLAPAMIENTOS) as Solapamiento[]).map((opcion) => (
                <FormControlLabel key={opcion} value={opcion} control={<Radio size="small" />} label={t(`apiKeys.solapamientos.${opcion}`)} />
              ))}
            </RadioGroup>
          </Box>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={rotar.isPending} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button variant="contained" onClick={confirmar} disabled={rotar.isPending} startIcon={rotar.isPending ? <IconoCarga /> : <RefreshCw size={16} aria-hidden />}>
          {t('apiKeys.rotarConfirmar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
