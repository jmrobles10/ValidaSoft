import { Alert, AlertTitle, Box, Button, Stack, TextField, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { esErrorApi } from '@validasoft/api-client';
import { MailCheck } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { mensajeDeError } from '../shared/errores';
import { correoValido } from './correo';
import { solicitarAcceso } from './sesionApi';
import type { EstadoSesionTercero } from './sesionTercero';

/**
 * «Entrar con mi correo» (`POST /public/v1/acceso`): el tercero escribe el correo de su invitación y recibe un enlace
 * para entrar. La respuesta es siempre la misma, exista o no el correo, y la pantalla también: no revela quién está
 * registrado. Aparece donde antes se pedía «abre el enlace de tu correo» y en los enlaces usados o vencidos.
 */
export function PaginaAcceso({ motivo = 'sin-sesion' }: { motivo?: Exclude<EstadoSesionTercero, 'activa' | 'restaurando'> }) {
  const { t } = useTranslation();
  const [correo, setCorreo] = useState('');
  const [intentado, setIntentado] = useState(false);
  const solicitud = useMutation({ mutationFn: (valor: string) => solicitarAcceso(valor) });
  const invalido = !correoValido(correo);

  const enviar = (evento: FormEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (!invalido && !solicitud.isPending) solicitud.mutate(correo);
  };

  if (solicitud.isSuccess) {
    return (
      <Stack alignItems="center" spacing={1.5} sx={{ maxWidth: 440, mx: 'auto', py: 6, textAlign: 'center' }} role="status">
        <Box sx={{ color: 'text.secondary' }} aria-hidden>
          <MailCheck size={40} strokeWidth={1.5} />
        </Box>
        <Typography variant="h2" component="h1">
          {t('acceso.enviadoTitulo')}
        </Typography>
        <Typography color="text.secondary" sx={{ fontSize: 15, lineHeight: '22px' }}>
          {t('acceso.enviado')}
        </Typography>
        <Button variant="text" onClick={() => solicitud.reset()}>
          {t('acceso.otroCorreo')}
        </Button>
      </Stack>
    );
  }

  const limitado = esErrorApi(solicitud.error) && solicitud.error.estado === 429;
  // 422 `correo-invalido`: la API solo valida la forma; se marca el campo como lo haría la validación local.
  const correoRechazado = esErrorApi(solicitud.error) && solicitud.error.codigo === 'correo-invalido';
  return (
    <Box component="form" onSubmit={enviar} noValidate sx={{ maxWidth: 440, mx: 'auto', pt: { xs: 1, sm: 4 } }}>
      {motivo !== 'sin-sesion' && (
        <Alert severity="info" sx={{ mb: 2.5 }}>
          <AlertTitle>{t(`acceso.motivos.${motivo}.titulo`)}</AlertTitle>
          {t(`acceso.motivos.${motivo}.texto`)}
        </Alert>
      )}
      <Typography variant="h2" component="h1" sx={{ mb: 1 }}>
        {t('acceso.titulo')}
      </Typography>
      <Typography color="text.secondary" sx={{ mb: 3, fontSize: 15, lineHeight: '22px' }}>
        {t('acceso.texto')}
      </Typography>
      <Stack spacing={2}>
        {solicitud.isError && !correoRechazado && <Alert severity="error">{limitado ? t('acceso.limitado') : mensajeDeError(solicitud.error, t)}</Alert>}
        <TextField
          type="email"
          label={t('acceso.correo')}
          value={correo}
          onChange={(e) => {
            setCorreo(e.target.value);
            if (solicitud.isError) solicitud.reset();
          }}
          autoComplete="email"
          inputMode="email"
          error={(intentado && invalido) || correoRechazado}
          helperText={(intentado && invalido) || correoRechazado ? t('acceso.correoInvalido') : t('acceso.correoAyuda')}
          disabled={solicitud.isPending}
          fullWidth
        />
        <Button type="submit" variant="contained" size="large" fullWidth disabled={solicitud.isPending}>
          {t('acceso.enviar')}
        </Button>
      </Stack>
    </Box>
  );
}
