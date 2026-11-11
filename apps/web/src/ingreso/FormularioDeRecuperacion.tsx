import { Alert, Box, Button, Stack, TextField, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { usarCodigoRecuperacion, type PasoDeIngresoDto } from '@validasoft/api-client';
import { enfocarNodo } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../shared/componentes';
import { codigoDeRecuperacionCompleto, normalizarCodigoDeRecuperacion } from './estado';
import { mensajeDeIngreso } from './mensajes';

const SIN_CREDENCIAL = { token: null, empresa: null } as const;

interface Props {
  desafio: string;
  /** Con un código bueno el servidor ofrece registrar una passkey nueva: el paso que sigue llega aquí. */
  alResultado: (paso: PasoDeIngresoDto) => void;
  alCancelar: () => void;
}

/**
 * Sin la passkey a la mano (PI-15): un código de recuperación de los diez que la persona guardó. Sirve una sola vez y, al usarlo, el servidor la lleva a
 * registrar una passkey nueva; no abre la sesión por sí solo.
 */
export function FormularioDeRecuperacion({ desafio, alResultado, alCancelar }: Props) {
  const { t } = useTranslation();
  const [codigo, setCodigo] = useState('');
  const usar = useMutation({
    mutationFn: () => usarCodigoRecuperacion({ desafio, codigo }, SIN_CREDENCIAL),
    onSuccess: alResultado,
  });
  const completo = codigoDeRecuperacionCompleto(codigo);

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (completo && !usar.isPending) usar.mutate();
  };

  return (
    <Box component="form" onSubmit={enviar} noValidate>
      <Stack spacing={2.5}>
        <Box>
          <Typography variant="h2" component="h1" sx={{ mb: 0.5 }}>
            {t('ingreso.recuperacion.titulo')}
          </Typography>
          <Typography sx={{ color: 'text.secondary' }}>{t('ingreso.recuperacion.texto')}</Typography>
        </Box>
        {usar.error !== null && (
          <Alert severity="error" role="alert">
            {mensajeDeIngreso(usar.error, t)}
          </Alert>
        )}
        <TextField
          label={t('ingreso.recuperacion.campo')}
          helperText={t('ingreso.recuperacion.ayuda')}
          value={codigo}
          onChange={(e) => setCodigo(normalizarCodigoDeRecuperacion(e.target.value))}
          placeholder="XXXXX-XXXXX"
          inputRef={enfocarNodo}
          fullWidth
          slotProps={{ htmlInput: { autoComplete: 'off', autoCapitalize: 'characters', spellCheck: false, maxLength: 11, style: { fontFamily: 'monospace', letterSpacing: 1 } } }}
        />
        <Button type="submit" variant="contained" size="large" fullWidth disabled={!completo || usar.isPending} startIcon={usar.isPending ? <IconoCarga /> : undefined}>
          {t('ingreso.recuperacion.usar')}
        </Button>
        <Button variant="text" size="small" onClick={alCancelar} disabled={usar.isPending} sx={{ alignSelf: 'center' }}>
          {t('ingreso.recuperacion.volver')}
        </Button>
      </Stack>
    </Box>
  );
}
