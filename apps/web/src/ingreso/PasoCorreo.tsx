import { Alert, Box, Button, Divider, Stack, TextField, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { obtenerOpcionesPasskeyIngreso, solicitarCodigoIngreso, verificarPasskeyIngreso, type CodigoSolicitadoDto, type PasoDeIngresoDto } from '@validasoft/api-client';
import { enfocarNodo } from '@validasoft/ui';
import { KeyRound } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../shared/componentes';
import { esCorreoPlausible } from './estado';
import { mensajeDeIngreso } from './mensajes';
import { firmarConPasskey, soportaPasskeys } from './passkeys';

const SIN_CREDENCIAL = { token: null, empresa: null } as const;

export interface PasoCorreoProps {
  /** Correo escrito antes (al volver desde el código). */
  correoInicial?: string;
  alCodigo: (correo: string, codigo: CodigoSolicitadoDto) => void;
  alResultado: (paso: PasoDeIngresoDto) => void;
}

/** Primer paso: el correo (recibe un código) o, si el navegador puede, entrar solo con una passkey sin escribir nada. */
export function PasoCorreo({ correoInicial = '', alCodigo, alResultado }: PasoCorreoProps) {
  const { t } = useTranslation();
  const [correo, setCorreo] = useState(correoInicial);
  const [intentado, setIntentado] = useState(false);

  const pedirCodigo = useMutation({
    mutationFn: () => solicitarCodigoIngreso({ correo: correo.trim() }, SIN_CREDENCIAL),
    onSuccess: (respuesta) => alCodigo(correo.trim().toLowerCase(), respuesta),
  });

  const conPasskey = useMutation({
    mutationFn: async () => {
      const { desafio, opciones } = await obtenerOpcionesPasskeyIngreso(SIN_CREDENCIAL);
      const respuesta = await firmarConPasskey(opciones);
      return verificarPasskeyIngreso({ desafio, respuesta }, SIN_CREDENCIAL);
    },
    onSuccess: alResultado,
  });

  const invalido = intentado && !esCorreoPlausible(correo);
  const ocupado = pedirCodigo.isPending || conPasskey.isPending;
  const error = pedirCodigo.error ?? conPasskey.error;

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (!esCorreoPlausible(correo) || ocupado) return;
    conPasskey.reset();
    pedirCodigo.mutate();
  };

  return (
    <Box component="form" onSubmit={enviar} noValidate>
      <Stack spacing={2.5}>
        <Box>
          <Typography variant="h2" component="h1" sx={{ mb: 0.5 }}>
            {t('ingreso.titulo')}
          </Typography>
          <Typography sx={{ color: 'text.secondary' }}>{t('ingreso.subtitulo')}</Typography>
        </Box>
        {error !== null && (
          <Alert severity="error" role="alert">
            {mensajeDeIngreso(error, t)}
          </Alert>
        )}
        <TextField
          label={t('ingreso.correo')}
          type="email"
          value={correo}
          onChange={(e) => setCorreo(e.target.value)}
          error={invalido}
          helperText={invalido ? t('errores.correo-invalido') : undefined}
          placeholder={t('ingreso.correoPlaceholder')}
          inputRef={enfocarNodo}
          fullWidth
          slotProps={{ htmlInput: { autoComplete: 'username webauthn', inputMode: 'email', maxLength: 254 } }}
        />
        <Button type="submit" variant="contained" size="large" fullWidth disabled={ocupado} startIcon={pedirCodigo.isPending ? <IconoCarga /> : undefined}>
          {t('ingreso.continuar')}
        </Button>
        {soportaPasskeys() && (
          <>
            <Divider>
              <Typography component="span" variant="body2" sx={{ color: 'text.secondary' }}>
                {t('ingreso.o')}
              </Typography>
            </Divider>
            <Stack spacing={0.75}>
              <Button
                variant="outlined"
                fullWidth
                disabled={ocupado}
                onClick={() => {
                  pedirCodigo.reset();
                  conPasskey.mutate();
                }}
                startIcon={conPasskey.isPending ? <IconoCarga /> : <KeyRound size={16} strokeWidth={1.75} aria-hidden />}
              >
                {t('ingreso.conPasskey')}
              </Button>
              <Typography variant="body2" sx={{ color: 'text.secondary', textAlign: 'center' }}>
                {t('ingreso.passkeyAyuda')}
              </Typography>
            </Stack>
          </>
        )}
      </Stack>
    </Box>
  );
}
