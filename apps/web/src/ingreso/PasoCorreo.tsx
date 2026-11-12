import { Alert, Box, Button, Divider, Stack, TextField, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { obtenerOpcionesPasskeyIngreso, solicitarCodigoIngreso, verificarPasskeyIngreso, type CodigoSolicitadoDto, type PasoDeIngresoDto } from '@validasoft/api-client';
import { enfocarNodo } from '@validasoft/ui';
import { KeyRound } from 'lucide-react';
import { useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../shared/componentes';
import { BotonSso } from './BotonSso';
import { esCorreoPlausible } from './estado';
import { mensajeDeIngreso } from './mensajes';
import { firmarConPasskey, soportaPasskeys } from './passkeys';
import { useIniciarSso, useSsoDelCorreo } from './useSso';
import { useUiCondicionalDePasskeys } from './useUiCondicional';

const SIN_CREDENCIAL = { token: null, empresa: null } as const;

export interface PasoCorreoProps {
  /** Correo escrito antes (al volver desde el código). */
  correoInicial?: string;
  alCodigo: (correo: string, codigo: CodigoSolicitadoDto) => void;
  alResultado: (paso: PasoDeIngresoDto) => void;
}

/**
 * Primer paso: el correo (recibe un código), entrar con el proveedor de identidad de la empresa si el dominio lo tiene (PI-1) o, si el navegador puede,
 * solo con una passkey: sin escribir nada o escogiéndola en el autocompletado del propio campo (PI-14).
 */
export function PasoCorreo({ correoInicial = '', alCodigo, alResultado }: PasoCorreoProps) {
  const { t } = useTranslation();
  const [correo, setCorreo] = useState(correoInicial);
  const [intentado, setIntentado] = useState(false);
  const [falloCondicional, setFalloCondicional] = useState<unknown>(null);
  const sso = useSsoDelCorreo(correo);
  // Con el SSO exclusivo la persona no entra con código ni con passkey: solo con el proveedor de su empresa.
  const soloSso = sso?.reemplazaElCodigo === true;

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

  const iniciarSso = useIniciarSso(correo);
  useUiCondicionalDePasskeys(alResultado, setFalloCondicional, !soloSso);

  const invalido = intentado && !esCorreoPlausible(correo);
  // Tras iniciar el SSO el navegador se va: el botón queda ocupado hasta que cambie de página.
  const ocupado = pedirCodigo.isPending || conPasskey.isPending || iniciarSso.isPending || iniciarSso.isSuccess;
  const error = pedirCodigo.error ?? conPasskey.error ?? iniciarSso.error ?? falloCondicional;

  const reiniciarErrores = () => {
    conPasskey.reset();
    pedirCodigo.reset();
    iniciarSso.reset();
    setFalloCondicional(null);
  };

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    setIntentado(true);
    if (!esCorreoPlausible(correo) || ocupado) return;
    reiniciarErrores();
    if (soloSso) iniciarSso.mutate();
    else pedirCodigo.mutate();
  };

  return (
    <Box component="form" onSubmit={enviar} noValidate>
      <Stack spacing={2.5}>
        <Box>
          <Typography variant="h2" component="h1" sx={{ mb: 0.5 }}>
            {t('ingreso.titulo')}
          </Typography>
          <Typography sx={{ color: 'text.secondary' }}>{soloSso ? t('ingreso.sso.subtituloExclusivo', { nombre: sso.nombre ?? '' }) : t('ingreso.subtitulo')}</Typography>
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
        {soloSso ? (
          <BotonSso nombre={sso.nombre ?? ''} principal ocupado={ocupado} cargando={iniciarSso.isPending || iniciarSso.isSuccess} alPulsar={() => undefined} />
        ) : (
          <>
            <Button type="submit" variant="contained" size="large" fullWidth disabled={ocupado} startIcon={pedirCodigo.isPending ? <IconoCarga /> : undefined}>
              {t('ingreso.continuar')}
            </Button>
            {sso && (
              <BotonSso
                nombre={sso.nombre ?? ''}
                principal={false}
                ocupado={ocupado}
                cargando={iniciarSso.isPending || iniciarSso.isSuccess}
                alPulsar={() => {
                  reiniciarErrores();
                  iniciarSso.mutate();
                }}
              />
            )}
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
                      reiniciarErrores();
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
          </>
        )}
      </Stack>
    </Box>
  );
}
