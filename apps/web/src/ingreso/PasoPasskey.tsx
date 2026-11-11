import { Alert, Box, Button, Stack, TextField, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { registrarPasskeyIngreso, verificarPasskeyIngreso, type PasoDeIngresoDto } from '@validasoft/api-client';
import { Fingerprint, KeyRound } from 'lucide-react';
import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../shared/componentes';
import type { OpcionesJson } from './estado';
import { mensajeDeIngreso } from './mensajes';
import { crearPasskey, firmarConPasskey, nombreSugeridoDePasskey } from './passkeys';

const SIN_CREDENCIAL = { token: null, empresa: null } as const;

function Cabecera({ icono, titulo, texto }: { icono: 'verificar' | 'registrar'; titulo: string; texto: string }) {
  const Icono = icono === 'verificar' ? Fingerprint : KeyRound;
  return (
    <Stack spacing={1.5} sx={{ alignItems: 'flex-start' }}>
      <Box aria-hidden sx={{ display: 'grid', placeItems: 'center', width: 48, height: 48, borderRadius: '50%', bgcolor: 'superficie.nivel2', color: 'text.secondary' }}>
        <Icono size={24} strokeWidth={1.75} />
      </Box>
      <Box>
        <Typography variant="h2" component="h1" sx={{ mb: 0.5 }}>
          {titulo}
        </Typography>
        <Typography sx={{ color: 'text.secondary' }}>{texto}</Typography>
      </Box>
    </Stack>
  );
}

export interface PasoPasskeyProps {
  desafio: string;
  opciones: OpcionesJson;
  alResultado: (paso: PasoDeIngresoDto) => void;
  alVolver: () => void;
}

/** Tras el código, una cuenta con passkey la confirma con ella: se intenta sola al llegar y se puede repetir con el botón. */
export function PasoVerificarPasskey({ desafio, opciones, alResultado, alVolver }: PasoPasskeyProps) {
  const { t } = useTranslation();
  const verificar = useMutation({
    mutationFn: async () => verificarPasskeyIngreso({ desafio, respuesta: await firmarConPasskey(opciones) }, SIN_CREDENCIAL),
    onSuccess: alResultado,
  });
  const intentada = useRef(false);

  useEffect(() => {
    // Una vez al llegar (StrictMode monta dos veces): el navegador ofrece la passkey sin pedir otro clic.
    if (intentada.current) return;
    intentada.current = true;
    verificar.mutate();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Stack spacing={2.5}>
      <Cabecera icono="verificar" titulo={t('ingreso.passkeyTitulo')} texto={t('ingreso.passkeyTexto')} />
      {verificar.error !== null && (
        <Alert severity="error" role="alert">
          {mensajeDeIngreso(verificar.error, t)}
        </Alert>
      )}
      <Button variant="contained" size="large" fullWidth disabled={verificar.isPending} onClick={() => verificar.mutate()} startIcon={verificar.isPending ? <IconoCarga /> : undefined}>
        {verificar.isPending ? t('ingreso.esperandoPasskey') : t('ingreso.usarPasskey')}
      </Button>
      <Button variant="text" size="small" onClick={alVolver} disabled={verificar.isPending} sx={{ alignSelf: 'center' }}>
        {t('ingreso.otroCorreo')}
      </Button>
    </Stack>
  );
}

/** Cuenta que exige passkey y aún no tiene: se registra ahí mismo (alta o recuperación) y con eso se entra. */
export function PasoRegistrarPasskey({ desafio, opciones, alResultado, alVolver }: PasoPasskeyProps) {
  const { t } = useTranslation();
  const [nombre, setNombre] = useState(() => nombreSugeridoDePasskey(navigator.userAgent));
  const registrar = useMutation({
    mutationFn: async () => registrarPasskeyIngreso({ desafio, respuesta: await crearPasskey(opciones), nombre: nombre.trim() || null }, SIN_CREDENCIAL),
    onSuccess: alResultado,
  });

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    if (!registrar.isPending) registrar.mutate();
  };

  return (
    <Box component="form" onSubmit={enviar} noValidate>
      <Stack spacing={2.5}>
        <Cabecera icono="registrar" titulo={t('ingreso.registrarTitulo')} texto={t('ingreso.registrarTexto')} />
        {registrar.error !== null && (
          <Alert severity="error" role="alert">
            {mensajeDeIngreso(registrar.error, t)}
          </Alert>
        )}
        <TextField
          label={t('ingreso.registrarNombre')}
          helperText={t('ingreso.registrarNombreAyuda')}
          value={nombre}
          onChange={(e) => setNombre(e.target.value)}
          fullWidth
          slotProps={{ htmlInput: { maxLength: 60 } }}
        />
        <Button type="submit" variant="contained" size="large" fullWidth disabled={registrar.isPending} startIcon={registrar.isPending ? <IconoCarga /> : undefined}>
          {registrar.isPending ? t('ingreso.registrando') : t('ingreso.registrar')}
        </Button>
        <Button variant="text" size="small" onClick={alVolver} disabled={registrar.isPending} sx={{ alignSelf: 'center' }}>
          {t('ingreso.cancelar')}
        </Button>
      </Stack>
    </Box>
  );
}
