import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Skeleton, Stack, Switch, Typography } from '@mui/material';
import { ErrorEnLinea } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { useGuardarPreferencias, usePreferenciasDelTercero } from './api';
import { cambiosDePreferencias, tiposConCorreo } from './bandeja';

/**
 * Preferencias de correo del tercero (`GET/PUT /public/v1/preferencias-notificacion`): un interruptor por cada tipo que puede ir
 * por correo; valen para todas sus empresas. La campana no se apaga. «Guardar» se habilita solo si algo cambió.
 */
export function DialogoPreferencias({ onCerrar }: { onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const consulta = usePreferenciasDelTercero();
  const guardar = useGuardarPreferencias();
  // Solo lo que la persona tocó; lo demás se lee del servidor.
  const [editados, setEditados] = useState<Record<string, boolean>>({});
  const preferencias = consulta.data;
  const cambios = preferencias ? cambiosDePreferencias(preferencias.correo, editados) : {};
  const hayCambios = Object.keys(cambios).length > 0;

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (!hayCambios) return;
    guardar.mutate(cambios, {
      onSuccess: () => {
        mostrar(t('notificaciones.preferencias.guardadas'));
        onCerrar();
      },
    });
  };

  return (
    <Dialog open onClose={guardar.isPending ? undefined : onCerrar} fullWidth maxWidth="sm" aria-labelledby="titulo-preferencias-notificacion">
      <form onSubmit={enviar} noValidate>
        <DialogTitle id="titulo-preferencias-notificacion">{t('notificaciones.preferencias.titulo')}</DialogTitle>
        <DialogContent>
          <Stack spacing={2}>
            <Typography variant="body2" sx={{ color: 'text.secondary' }}>
              {t('notificaciones.preferencias.ayuda')}
            </Typography>
            {guardar.isError && <Alert severity="error">{mensajeDeError(guardar.error, t)}</Alert>}
            {consulta.isPending ? (
              <Stack spacing={1.5} role="status" aria-label={t('comun.cargando')}>
                {[0, 1, 2].map((i) => (
                  <Skeleton key={i} variant="rounded" height={56} />
                ))}
              </Stack>
            ) : consulta.isError ? (
              <ErrorEnLinea mensaje={t('notificaciones.preferencias.errorCarga')} onReintentar={() => void consulta.refetch()} textoReintentar={t('comun.reintentar')} />
            ) : (
              <Stack divider={<Box sx={{ borderTop: 1, borderColor: 'divider' }} />}>
                {tiposConCorreo(consulta.data.tiposConCorreo).map(({ tipo, clave }) => (
                  <FormControlLabel
                    key={tipo}
                    labelPlacement="start"
                    control={
                      <Switch
                        checked={editados[tipo] ?? consulta.data.correo[tipo] ?? true}
                        onChange={(e) => setEditados((previos) => ({ ...previos, [tipo]: e.target.checked }))}
                        disabled={guardar.isPending}
                      />
                    }
                    label={
                      <Box sx={{ py: 1.25, pr: 2 }}>
                        <Typography component="span" sx={{ display: 'block', fontSize: 14, lineHeight: '20px', fontWeight: 500 }}>
                          {t(`notificaciones.tipos.${clave}.titulo`)}
                        </Typography>
                        <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block' }}>
                          {t(`notificaciones.tipos.${clave}.explicacion`)}
                        </Typography>
                      </Box>
                    }
                    sx={{ mx: 0, justifyContent: 'space-between', alignItems: 'center', '& .MuiFormControlLabel-label': { flex: 1 } }}
                  />
                ))}
              </Stack>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          {preferencias && !hayCambios && (
            <Typography variant="body2" sx={{ color: 'text.secondary', mr: 'auto', pl: 1 }}>
              {t('notificaciones.preferencias.sinCambios')}
            </Typography>
          )}
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={!hayCambios || guardar.isPending}>
            {t('notificaciones.preferencias.guardar')}
          </Button>
        </DialogActions>
      </form>
    </Dialog>
  );
}
