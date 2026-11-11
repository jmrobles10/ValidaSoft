import { Box, Skeleton, Stack, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { completarSsoIngreso, type PasoDeIngresoDto } from '@validasoft/api-client';
import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';

const SIN_CREDENCIAL = { token: null, empresa: null } as const;

export interface PasoSsoProps {
  desafio: string;
  alResultado: (paso: PasoDeIngresoDto) => void;
  /** El retorno no se pudo canjear (venció, ya se usó o la cuenta no tiene acceso): se vuelve al correo con el motivo. */
  alFallar: (error: unknown) => void;
}

/**
 * De vuelta del proveedor de identidad de la empresa (PI-1): el servidor ya validó la identidad y dejó el ingreso listo; aquí se canjea por la sesión
 * —o por el paso que siga, si la cuenta exige passkey—. El retorno sirve una sola vez, por eso se intenta una sola vez.
 */
export function PasoCompletarSso({ desafio, alResultado, alFallar }: PasoSsoProps) {
  const { t } = useTranslation();
  const completar = useMutation({
    mutationFn: () => completarSsoIngreso({ desafio }, SIN_CREDENCIAL),
    onSuccess: alResultado,
    onError: alFallar,
  });
  const intentado = useRef(false);

  useEffect(() => {
    // Una vez (StrictMode monta dos veces): el retorno es de un solo uso.
    if (intentado.current) return;
    intentado.current = true;
    completar.mutate();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Stack spacing={2} role="status" aria-busy aria-label={t('ingreso.sso.terminando')}>
      <Box>
        <Typography variant="h2" component="h1" sx={{ mb: 0.5 }}>
          {t('ingreso.sso.terminando')}
        </Typography>
        <Typography sx={{ color: 'text.secondary' }}>{t('ingreso.sso.terminandoAyuda')}</Typography>
      </Box>
      <Skeleton variant="rounded" height={40} />
    </Stack>
  );
}
