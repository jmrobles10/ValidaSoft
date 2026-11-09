import { Alert, Box, Button, CircularProgress, Stack, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { verificarOtpEnlace, type SesionTerceroDto } from '@validasoft/api-client';
import { formatearHora, PaginaEstado } from '@validasoft/ui';
import { CircleAlert, Clock, Link2Off, WifiOff } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink, useNavigate, useParams } from 'react-router';
import { useSesionTercero } from '../sesion/sesionTercero';
import { canjearUnaVez, destinoTrasSesion, falloDeEnlace, intentosRestantesDe, type FalloEnlace, type ResultadoCanje } from './canje';
import { CodigoOtp } from './CodigoOtp';

const ICONO_FALLO: Record<FalloEnlace, typeof Clock> = {
  vencido: Clock,
  usado: Link2Off,
  'no-encontrado': Link2Off,
  'otp-vencido': Clock,
  'otp-bloqueado': CircleAlert,
  'demasiadas-peticiones': Clock,
  red: WifiOff,
  otro: CircleAlert,
};

/** Fallos que se pueden reintentar con el mismo enlace (los demás lo dejaron inservible: 404, 410 y el bloqueo del OTP). */
const REINTENTABLES: readonly FalloEnlace[] = ['red', 'demasiadas-peticiones'];

/**
 * `/e/:token`: canjea el enlace del correo (una sola vez), pide el código si el tipo de vínculo exige OTP y abre la
 * sesión. Un enlace vencido o usado (410) o que ya no existe (404) tiene su pantalla con el siguiente paso.
 */
export function PaginaEnlace() {
  const { t } = useTranslation();
  const { token = '' } = useParams();
  const navegar = useNavigate();
  const establecer = useSesionTercero((s) => s.establecer);
  const [canje, setCanje] = useState<ResultadoCanje | null>(null);
  const [fallo, setFallo] = useState<FalloEnlace | null>(null);
  const [intento, setIntento] = useState(0);
  const [codigo, setCodigo] = useState('');

  const entrar = (sesion: SesionTerceroDto) => {
    establecer(sesion);
    void navegar(destinoTrasSesion(sesion), { replace: true });
  };

  useEffect(() => {
    let vigente = true;
    canjearUnaVez(token).then(
      (resultado) => {
        if (!vigente) return;
        if (resultado.tipo === 'sesion') entrar(resultado.sesion);
        else setCanje(resultado);
      },
      (error: unknown) => vigente && setFallo(falloDeEnlace(error)),
    );
    return () => {
      vigente = false;
    };
    // `entrar` cambia en cada render; el canje depende solo del token y del reintento.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token, intento]);

  const verificacion = useMutation({
    mutationFn: () => verificarOtpEnlace(token, { codigo }, { token: null }),
    onSuccess: entrar,
    onError: (error) => {
      setCodigo('');
      if (intentosRestantesDe(error) === null) setFallo(falloDeEnlace(error));
    },
  });

  if (fallo) {
    return (
      <PaginaEstado
        icono={ICONO_FALLO[fallo]}
        titulo={t(`enlace.fallos.${fallo}.titulo`)}
        texto={t(`enlace.fallos.${fallo}.texto`)}
        rol="alert"
        acciones={
          !REINTENTABLES.includes(fallo) && fallo !== 'otro' ? (
            // Enlace usado, vencido o bloqueado: se pide otro con el correo (POST /public/v1/acceso).
            <Button component={RouterLink} to="/entrar" variant="contained">
              {t('acceso.titulo')}
            </Button>
          ) : REINTENTABLES.includes(fallo) ? (
            <Button
              variant="contained"
              onClick={() => {
                setFallo(null);
                setIntento((n) => n + 1);
              }}
            >
              {t('comun.reintentar')}
            </Button>
          ) : undefined
        }
      />
    );
  }

  if (canje?.tipo !== 'otp') {
    return (
      <Stack alignItems="center" spacing={2} sx={{ py: 10 }} role="status">
        <CircularProgress size={28} aria-hidden />
        <Typography color="text.secondary">{t('enlace.abriendo')}</Typography>
      </Stack>
    );
  }

  const restantes = intentosRestantesDe(verificacion.error);
  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    if (codigo.length === 6 && !verificacion.isPending) verificacion.mutate();
  };

  return (
    <Box component="form" onSubmit={enviar} noValidate sx={{ maxWidth: 420, mx: 'auto' }}>
      <Typography variant="h2" component="h1" sx={{ mb: 1 }}>
        {t('enlace.otpTitulo')}
      </Typography>
      <Typography color="text.secondary" sx={{ mb: 3, fontSize: 15, lineHeight: '22px' }}>
        {t('enlace.otpTexto', { hora: formatearHora(canje.expira) })}
      </Typography>
      <Stack spacing={2.5}>
        <CodigoOtp valor={codigo} onCambiar={setCodigo} deshabilitado={verificacion.isPending} error={restantes !== null} />
        {restantes !== null && (
          <Alert severity="error" role="alert">
            {t('enlace.otpIntentos', { count: restantes })}
          </Alert>
        )}
        <Button type="submit" variant="contained" size="large" fullWidth disabled={codigo.length !== 6 || verificacion.isPending}>
          {t('enlace.otpVerificar')}
        </Button>
        <Typography color="text.secondary" sx={{ fontSize: 14 }}>
          {t('enlace.otpSpam')}
        </Typography>
      </Stack>
    </Box>
  );
}
