import { Box, Chip, Stack, Typography } from '@mui/material';
import type { PasskeyDto, SesionDeUsuarioDto } from '@validasoft/api-client';
import { formatearFechaHora } from '@validasoft/ui';
import { KeyRound, Laptop } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { nombreSugeridoDePasskey } from '../../ingreso/passkeys';

function Fila({ icono, deshabilitada = false, children, acciones }: { icono: ReactNode; deshabilitada?: boolean; children: ReactNode; acciones?: ReactNode }) {
  return (
    <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', py: 1.25, opacity: deshabilitada ? 0.7 : 1 }}>
      <Box aria-hidden sx={{ display: 'grid', placeItems: 'center', flex: 'none', width: 32, height: 32, borderRadius: '50%', bgcolor: 'superficie.nivel2', color: 'text.secondary' }}>
        {icono}
      </Box>
      <Box sx={{ flex: 1, minWidth: 0 }}>{children}</Box>
      {acciones && <Stack direction="row" spacing={0.5} sx={{ flex: 'none', alignItems: 'center' }}>{acciones}</Stack>}
    </Stack>
  );
}

/** Lista con una línea entre filas (passkeys y sesiones, de la persona o vistas por un administrador). */
export function ListaDeFilas({ children, etiqueta }: { children: ReactNode; etiqueta: string }) {
  return (
    <Stack component="ul" aria-label={etiqueta} divider={<Box component="li" role="presentation" sx={{ borderTop: 1, borderColor: 'divider', listStyle: 'none' }} />} sx={{ m: 0, p: 0, listStyle: 'none' }}>
      {children}
    </Stack>
  );
}

/** Una passkey: su nombre, si se sincroniza o fue revocada, y cuándo se creó y se usó por última vez. */
export function FilaDePasskey({ passkey, acciones }: { passkey: PasskeyDto; acciones?: ReactNode }) {
  const { t } = useTranslation();
  const revocada = !passkey.activa;
  return (
    <Box component="li" sx={{ listStyle: 'none' }}>
      <Fila icono={<KeyRound size={16} strokeWidth={1.75} />} deshabilitada={revocada} acciones={acciones}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', minWidth: 0 }}>
          <Typography noWrap sx={{ fontWeight: 600 }}>
            {passkey.nombre}
          </Typography>
          {passkey.sincronizada && <Chip size="small" variant="outlined" label={t('cuenta.passkeys.sincronizada')} title={t('cuenta.passkeys.sincronizadaAyuda')} />}
          {revocada && <Chip size="small" color="warning" variant="outlined" label={t('accesoUsuario.revocada')} />}
        </Stack>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {revocada && passkey.revocadaEn
            ? t('accesoUsuario.revocadaPor', { fecha: formatearFechaHora(passkey.revocadaEn) }) + (passkey.motivoRevocacion ? ` · ${passkey.motivoRevocacion}` : '')
            : `${t('cuenta.passkeys.creada')} ${formatearFechaHora(passkey.creadaEn)} · ${t('cuenta.passkeys.ultimoUso')} ${passkey.ultimoUso ? formatearFechaHora(passkey.ultimoUso) : t('cuenta.passkeys.nunca')}`}
        </Typography>
      </Fila>
    </Box>
  );
}

/** Una sesión abierta: el dispositivo (navegador y sistema), su IP y cuándo se usó por última vez y termina. */
export function FilaDeSesion({ sesion, acciones }: { sesion: SesionDeUsuarioDto; acciones?: ReactNode }) {
  const { t } = useTranslation();
  const dispositivo = sesion.dispositivo ? nombreSugeridoDePasskey(sesion.dispositivo) : '—';
  return (
    <Box component="li" sx={{ listStyle: 'none' }}>
      <Fila icono={<Laptop size={16} strokeWidth={1.75} />} acciones={acciones}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center', minWidth: 0 }}>
          <Typography noWrap sx={{ fontWeight: 600 }}>
            {dispositivo}
          </Typography>
          {sesion.actual && <Chip size="small" color="primary" variant="outlined" label={t('cuenta.sesiones.actual')} />}
        </Stack>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {[sesion.ip ? `${t('cuenta.sesiones.ip')} ${sesion.ip}` : null, `${t('cuenta.sesiones.ultimaActividad')} ${formatearFechaHora(sesion.ultimaActividad)}`, `${t('cuenta.sesiones.vence')} ${formatearFechaHora(sesion.expiraEn)}`]
            .filter(Boolean)
            .join(' · ')}
        </Typography>
      </Fila>
    </Box>
  );
}
