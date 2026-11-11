import { Alert, Box, Button, Stack, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { solicitarCodigoIngreso, verificarCodigoIngreso, type CodigoSolicitadoDto, type PasoDeIngresoDto } from '@validasoft/api-client';
import { CampoCodigo, formatearHora } from '@validasoft/ui';
import { useEffect, useRef, useState, type SubmitEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../shared/componentes';
import { useAvisos } from '../shared/useAvisos';
import { exigeEmpezarDeNuevo, intentosRestantesDe, segundosParaReenviar } from './estado';
import { mensajeDeIngreso } from './mensajes';

const SIN_CREDENCIAL = { token: null, empresa: null } as const;

export interface PasoCodigoProps {
  correo: string;
  desafio: string;
  expira: Date;
  alResultado: (paso: PasoDeIngresoDto) => void;
  /** Un código nuevo (el desafío anterior ya no sirve). */
  alReenviar: (codigo: CodigoSolicitadoDto) => void;
  alVolver: () => void;
}

/**
 * Segundo paso: el código de 6 dígitos del correo. Se verifica solo al completar los seis dígitos; si es incorrecto dice cuántos intentos quedan, y si
 * venció o se agotó ofrece pedir otro (con una espera corta para no agotar el límite de envíos).
 */
export function PasoCodigo({ correo, desafio, expira, alResultado, alReenviar, alVolver }: PasoCodigoProps) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const [codigo, setCodigo] = useState('');
  const [pedidoEn, setPedidoEn] = useState(() => Date.now());
  const [ahora, setAhora] = useState(() => Date.now());
  const enviado = useRef('');

  useEffect(() => {
    const reloj = window.setInterval(() => setAhora(Date.now()), 1000);
    return () => window.clearInterval(reloj);
  }, []);

  const verificar = useMutation({
    mutationFn: (digitos: string) => verificarCodigoIngreso({ desafio, codigo: digitos }, SIN_CREDENCIAL),
    onSuccess: alResultado,
    onError: () => {
      enviado.current = '';
      setCodigo('');
    },
  });

  const reenviar = useMutation({
    mutationFn: () => solicitarCodigoIngreso({ correo }, SIN_CREDENCIAL),
    onSuccess: (respuesta) => {
      mostrar(t('ingreso.reenviado'));
      alReenviar(respuesta);
    },
  });

  const enviarCodigo = (digitos: string) => {
    if (digitos.length !== 6 || verificar.isPending || enviado.current === digitos) return;
    enviado.current = digitos;
    verificar.mutate(digitos);
  };

  const alCambiar = (nuevo: string) => {
    setCodigo(nuevo);
    if (nuevo.length === 6) enviarCodigo(nuevo);
  };

  const enviar = (evento: SubmitEvent) => {
    evento.preventDefault();
    enviarCodigo(codigo);
  };

  const espera = segundosParaReenviar(pedidoEn, ahora);
  const restantes = intentosRestantesDe(verificar.error);
  const terminado = exigeEmpezarDeNuevo(verificar.error);

  return (
    <Box component="form" onSubmit={enviar} noValidate>
      <Stack spacing={2.5}>
        <Box>
          <Typography variant="h2" component="h1" sx={{ mb: 0.5 }}>
            {t('ingreso.codigoTitulo')}
          </Typography>
          <Typography sx={{ color: 'text.secondary' }}>{t('ingreso.codigoTexto', { correo, hora: formatearHora(expira) })}</Typography>
        </Box>
        {verificar.error !== null && (
          <Alert severity="error" role="alert">
            {mensajeDeIngreso(verificar.error, t)}
          </Alert>
        )}
        {reenviar.error !== null && (
          <Alert severity="error" role="alert">
            {mensajeDeIngreso(reenviar.error, t)}
          </Alert>
        )}
        <CampoCodigo
          valor={codigo}
          onCambiar={alCambiar}
          deshabilitado={verificar.isPending || terminado}
          error={restantes !== null}
          etiqueta={t('ingreso.codigoCampo')}
          etiquetaDigito={(n) => t('ingreso.codigoDigito', { n: String(n) })}
          enfocarAlMontar
        />
        <Button
          type="submit"
          variant="contained"
          size="large"
          fullWidth
          disabled={codigo.length !== 6 || verificar.isPending || terminado}
          startIcon={verificar.isPending ? <IconoCarga /> : undefined}
        >
          {t('ingreso.verificar')}
        </Button>
        <Typography variant="body2" sx={{ color: 'text.secondary' }}>
          {t('ingreso.codigoAyuda')}
        </Typography>
        <Stack direction="row" spacing={1} sx={{ justifyContent: 'space-between' }}>
          <Button variant="text" size="small" onClick={alVolver} disabled={verificar.isPending}>
            {t('ingreso.otroCorreo')}
          </Button>
          <Button
            variant="text"
            size="small"
            disabled={reenviar.isPending || (espera > 0 && !terminado)}
            onClick={() => {
              setPedidoEn(Date.now());
              reenviar.mutate();
            }}
            startIcon={reenviar.isPending ? <IconoCarga /> : undefined}
          >
            {espera > 0 && !terminado ? t('ingreso.reenviarEn', { segundos: espera }) : t('ingreso.reenviar')}
          </Button>
        </Stack>
      </Stack>
    </Box>
  );
}
