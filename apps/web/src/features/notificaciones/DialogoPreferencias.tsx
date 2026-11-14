import { Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Skeleton, Stack, Switch, Typography } from '@mui/material';
import { cambiosDePreferencias } from '@validasoft/api-client';
import { ErrorEnLinea, FaltaParaEnviar, FormularioDeDialogo } from '@validasoft/ui';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useGuardarPreferencias, usePreferenciasNotificacion } from './api';
import { claveDeTipo } from './tipos';

/**
 * Preferencias de correo de la persona (`GET/PUT /api/v1/usuarios/me/preferencias-notificacion`): un interruptor por
 * cada tipo que puede ir por correo. La campana no se apaga. «Guardar» se habilita solo si algo cambió.
 */
export function DialogoPreferencias({ onCerrar }: { onCerrar: () => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const consulta = usePreferenciasNotificacion(true);
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
    <Dialog
      open
      onClose={guardar.isPending ? undefined : onCerrar}
      maxWidth={false}
      slotProps={{ paper: { sx: { width: 520, maxWidth: 'calc(100% - 32px)' } } }}
      aria-labelledby="titulo-preferencias-notificacion"
    >
      <FormularioDeDialogo onSubmit={enviar}>
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
                {consulta.data.tiposConCorreo.map((tipo) => {
                  const clave = claveDeTipo(tipo);
                  const activo = editados[tipo] ?? consulta.data.correo[tipo] ?? true;
                  return (
                    <FormControlLabel
                      key={tipo}
                      labelPlacement="start"
                      control={
                        <Switch
                          checked={activo}
                          onChange={(e) => setEditados((previos) => ({ ...previos, [tipo]: e.target.checked }))}
                          disabled={guardar.isPending}
                        />
                      }
                      label={
                        <Box sx={{ py: 1.25, pr: 2 }}>
                          <Typography component="span" sx={{ display: 'block', fontSize: 14, lineHeight: '20px', fontWeight: 500 }}>
                            {clave ? t(`notificaciones.tipos.${clave}.titulo`) : tipo}
                          </Typography>
                          {clave && (
                            <Typography component="span" variant="body2" sx={{ color: 'text.secondary', display: 'block' }}>
                              {t(`notificaciones.tipos.${clave}.explicacion`)}
                            </Typography>
                          )}
                        </Box>
                      }
                      sx={{ mx: 0, justifyContent: 'space-between', alignItems: 'center', '& .MuiFormControlLabel-label': { flex: 1 } }}
                    />
                  );
                })}
              </Stack>
            )}
          </Stack>
        </DialogContent>
        <DialogActions>
          {preferencias && !hayCambios && <FaltaParaEnviar>{t('notificaciones.preferencias.sinCambios')}</FaltaParaEnviar>}
          <Button variant="text" onClick={onCerrar} disabled={guardar.isPending} sx={{ color: 'text.primary' }}>
            {t('comun.cancelar')}
          </Button>
          <Button type="submit" variant="contained" disabled={!hayCambios || guardar.isPending} startIcon={guardar.isPending ? <IconoCarga /> : undefined}>
            {t('comun.guardar')}
          </Button>
        </DialogActions>
      </FormularioDeDialogo>
    </Dialog>
  );
}
