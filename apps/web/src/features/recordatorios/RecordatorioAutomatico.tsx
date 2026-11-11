import { Alert, Box, Button, FormControlLabel, Paper, Skeleton, Stack, Switch, TextField, Typography } from '@mui/material';
import type { RecordatorioAutomaticoDto } from '@validasoft/api-client';
import { ErrorEnLinea } from '@validasoft/ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useGuardarRecordatorioAutomatico, useRecordatorioAutomatico } from './api';
import { diasValidos } from './automatico';

/**
 * Recordatorio automático de observaciones pendientes de la empresa activa (NT-3): apagado por omisión; encendido, Validasoft le
 * recuerda al tercero sus observaciones cada N días mientras siga devuelto. Va en Plantillas, junto al correo que se envía.
 */
export function RecordatorioAutomatico({ empresaId, puedeEscribir }: { empresaId: string; puedeEscribir: boolean }) {
  const { t } = useTranslation();
  const consulta = useRecordatorioAutomatico(empresaId);

  return (
    <Paper variant="outlined" component="section" aria-labelledby="titulo-recordatorio-automatico" sx={{ p: 2, mb: 2 }}>
      <Typography id="titulo-recordatorio-automatico" component="h2" sx={{ fontSize: 15, lineHeight: '22px', fontWeight: 600 }}>
        {t('recordatorios.automatico.titulo')}
      </Typography>
      <Typography variant="body2" sx={{ color: 'text.secondary', mt: 0.25, mb: 1.5 }}>
        {t('recordatorios.automatico.ayuda')}
      </Typography>
      {consulta.isPending ? (
        <Stack spacing={1} aria-busy>
          <Skeleton variant="rounded" height={32} width={260} />
          <Skeleton variant="rounded" height={40} width={160} />
        </Stack>
      ) : consulta.isError ? (
        <ErrorEnLinea
          mensaje={`${t('recordatorios.automatico.errorCarga')} ${mensajeDeError(consulta.error, t)}`}
          onReintentar={() => void consulta.refetch()}
          textoReintentar={t('comun.reintentar')}
        />
      ) : (
        // Se vuelve a armar el formulario si el servidor cambia (guardar o refrescar): sin estado derivado en efectos.
        <Formulario key={`${consulta.data.activo}-${consulta.data.cadaDias}-${consulta.data.actualizadoEn ?? ''}`} configuracion={consulta.data} empresaId={empresaId} puedeEscribir={puedeEscribir} />
      )}
    </Paper>
  );
}

function Formulario({ configuracion, empresaId, puedeEscribir }: { configuracion: RecordatorioAutomaticoDto; empresaId: string; puedeEscribir: boolean }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const guardar = useGuardarRecordatorioAutomatico(empresaId);
  const [activo, setActivo] = useState(configuracion.activo);
  const [dias, setDias] = useState(String(configuracion.cadaDias));
  const valido = diasValidos(dias);
  const cambio = activo !== configuracion.activo || dias !== String(configuracion.cadaDias);

  return (
    <Box
      component="form"
      noValidate
      onSubmit={(evento) => {
        evento.preventDefault();
        if (!valido || !cambio) return;
        guardar.mutate({ activo, cadaDias: Number(dias) }, { onSuccess: () => mostrar(t('recordatorios.automatico.guardado')) });
      }}
    >
      <Stack spacing={1.5}>
        {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
        <FormControlLabel
          control={<Switch checked={activo} onChange={(e) => setActivo(e.target.checked)} disabled={!puedeEscribir || guardar.isPending} />}
          label={t('recordatorios.automatico.activar')}
        />
        <TextField
          label={t('recordatorios.automatico.cada')}
          value={dias}
          onChange={(e) => setDias(e.target.value)}
          disabled={!puedeEscribir || !activo || guardar.isPending}
          error={!valido}
          helperText={valido ? t('recordatorios.automatico.cadaAyuda') : t('recordatorios.automatico.cadaInvalido')}
          slotProps={{ htmlInput: { inputMode: 'numeric', min: 1, max: 60 } }}
          sx={{ width: 200 }}
        />
        {puedeEscribir ? (
          <Box>
            <Button type="submit" variant="contained" disabled={!cambio || !valido || guardar.isPending}>
              {t('comun.guardar')}
            </Button>
          </Box>
        ) : (
          <Typography variant="body2" sx={{ color: 'text.secondary' }}>
            {t('recordatorios.automatico.soloLectura')}
          </Typography>
        )}
      </Stack>
    </Box>
  );
}
