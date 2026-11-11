import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, Skeleton, Stack, TextField, Typography } from '@mui/material';
import { useMutation, type UseQueryResult } from '@tanstack/react-query';
import type { PasskeyDto, SesionDeUsuarioDto } from '@validasoft/api-client';
import { enfocarNodo } from '@validasoft/ui';
import { Ban } from 'lucide-react';
import { useState, type ReactNode, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeIngreso } from '../../../ingreso/mensajes';
import { IconoCarga, Tarjeta } from '../../../shared/componentes';
import { useAvisos } from '../../../shared/useAvisos';
import { FilaDePasskey, FilaDeSesion, ListaDeFilas } from '../../cuenta/FilasDeAcceso';

export interface PanelDeAccesoProps {
  /** Nombre de la persona (rotula los diálogos). */
  persona: string;
  exigePasskey: boolean;
  passkeys: UseQueryResult<PasskeyDto[]>;
  sesiones: UseQueryResult<SesionDeUsuarioDto[]>;
  onRevocar: (passkeyId: string, motivo: string | null) => Promise<unknown>;
  onCerrarSesion: (sesionId: string) => Promise<unknown>;
  /** Se llama tras cambiar algo para recargar las listas (y la lista de personas). */
  alCambiar: () => void | Promise<void>;
  /** Acción extra de la cabecera de sesiones/passkeys (p. ej. reenviar la invitación). */
  extra?: ReactNode;
}

function Cargando({ etiqueta }: { etiqueta: string }) {
  return (
    <Stack spacing={1.5} role="status" aria-busy aria-label={etiqueta}>
      {[0, 1].map((i) => (
        <Skeleton key={i} variant="rounded" height={44} />
      ))}
    </Stack>
  );
}

function DialogoRevocar({ passkey, persona, ocupado, error, onConfirmar, onCerrar }: { passkey: PasskeyDto; persona: string; ocupado: boolean; error: unknown; onConfirmar: (motivo: string | null) => void; onCerrar: () => void }) {
  const { t } = useTranslation();
  const [motivo, setMotivo] = useState('');
  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (!ocupado) onConfirmar(motivo.trim() || null);
  };
  return (
    <Dialog open onClose={ocupado ? undefined : onCerrar} maxWidth={false} slotProps={{ paper: { sx: { width: 460, maxWidth: 'calc(100% - 32px)' }, component: 'form', onSubmit: enviar } }} aria-labelledby="titulo-revocar-passkey">
      <DialogTitle id="titulo-revocar-passkey">{t('accesoUsuario.revocarTitulo', { nombre: passkey.nombre })}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{ pt: 0.5 }}>
          <Typography>{t('accesoUsuario.revocarTexto', { persona })}</Typography>
          {error !== null && <Alert severity="error">{mensajeDeIngreso(error, t)}</Alert>}
          <TextField label={t('accesoUsuario.motivo')} helperText={t('accesoUsuario.motivoAyuda')} value={motivo} onChange={(e) => setMotivo(e.target.value)} inputRef={enfocarNodo} fullWidth slotProps={{ htmlInput: { maxLength: 200 } }} />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button variant="text" onClick={onCerrar} disabled={ocupado} sx={{ color: 'text.primary' }}>
          {t('comun.cancelar')}
        </Button>
        <Button type="submit" variant="contained" color="error" disabled={ocupado} startIcon={ocupado ? <IconoCarga /> : <Ban size={16} aria-hidden />}>
          {t('accesoUsuario.revocar')}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/**
 * Lo que un administrador ve de una persona (o un operador de Plataforma ve de otro): sus passkeys, con la revocación (la recuperación cuando pierde un
 * dispositivo), y sus sesiones abiertas, que puede cerrar. Cada acción queda en la auditoría de identidad.
 */
export function PanelDeAcceso({ persona, exigePasskey, passkeys, sesiones, onRevocar, onCerrarSesion, alCambiar, extra }: PanelDeAccesoProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [porRevocar, setPorRevocar] = useState<PasskeyDto | null>(null);

  const revocar = useMutation({
    mutationFn: ({ id, motivo }: { id: string; motivo: string | null }) => onRevocar(id, motivo),
    onSuccess: () => {
      mostrar(t('accesoUsuario.revocadaOk'));
      setPorRevocar(null);
    },
    onSettled: () => alCambiar(),
  });
  const cerrar = useMutation({
    mutationFn: (id: string) => onCerrarSesion(id),
    onSuccess: () => mostrar(t('accesoUsuario.sesionCerrada')),
    onError: (error) => mostrar(mensajeDeIngreso(error, t), 'error'),
    onSettled: () => alCambiar(),
  });

  return (
    <Stack spacing={2}>
      {extra}
      <Tarjeta titulo={t('accesoUsuario.passkeys')} nivel="h3">
        <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1 }}>
          {t('accesoUsuario.passkeysAyuda')}
        </Typography>
        {passkeys.isPending && <Cargando etiqueta={t('comun.cargando')} />}
        {passkeys.isError && <Alert severity="error">{t('accesoUsuario.errorCarga')}</Alert>}
        {passkeys.data?.length === 0 && (
          <Typography variant="body2" sx={{ color: 'text.secondary', py: 1 }}>
            {exigePasskey ? t('accesoUsuario.passkeysVacioExige') : t('accesoUsuario.passkeysVacio')}
          </Typography>
        )}
        {passkeys.data && passkeys.data.length > 0 && (
          <ListaDeFilas etiqueta={t('accesoUsuario.passkeys')}>
            {passkeys.data.map((p) => (
              <FilaDePasskey
                key={p.id}
                passkey={p}
                acciones={
                  p.activa ? (
                    <Button
                      size="small"
                      variant="text"
                      color="error"
                      onClick={() => {
                        revocar.reset();
                        setPorRevocar(p);
                      }}
                    >
                      {t('accesoUsuario.revocar')}
                    </Button>
                  ) : undefined
                }
              />
            ))}
          </ListaDeFilas>
        )}
      </Tarjeta>

      <Tarjeta titulo={t('accesoUsuario.sesiones')} nivel="h3">
        {sesiones.isPending && <Cargando etiqueta={t('comun.cargando')} />}
        {sesiones.isError && <Alert severity="error">{t('accesoUsuario.errorCarga')}</Alert>}
        {sesiones.data?.length === 0 && (
          <Typography variant="body2" sx={{ color: 'text.secondary', py: 1 }}>
            {t('accesoUsuario.sesionesVacio')}
          </Typography>
        )}
        {sesiones.data && sesiones.data.length > 0 && (
          <ListaDeFilas etiqueta={t('accesoUsuario.sesiones')}>
            {sesiones.data.map((s) => (
              <FilaDeSesion
                key={s.id}
                sesion={s}
                acciones={
                  <Button size="small" variant="text" color="error" disabled={cerrar.isPending} onClick={() => cerrar.mutate(s.id)}>
                    {t('accesoUsuario.cerrarSesion')}
                  </Button>
                }
              />
            ))}
          </ListaDeFilas>
        )}
      </Tarjeta>

      {porRevocar && (
        <DialogoRevocar
          passkey={porRevocar}
          persona={persona}
          ocupado={revocar.isPending}
          error={revocar.error}
          onConfirmar={(motivo) => revocar.mutate({ id: porRevocar.id, motivo })}
          onCerrar={() => setPorRevocar(null)}
        />
      )}
    </Stack>
  );
}
