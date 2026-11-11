import { Alert, Box, ButtonBase, Chip, Stack, Typography } from '@mui/material';
import { useMutation } from '@tanstack/react-query';
import { elegirCuentaIngreso, type CuentaDeIngresoDto, type PasoDeIngresoDto } from '@validasoft/api-client';
import { AvatarActor } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { mensajeDeIngreso } from './mensajes';

export interface PasoCuentaProps {
  desafio: string;
  cuentas: readonly CuentaDeIngresoDto[];
  alResultado: (paso: PasoDeIngresoDto) => void;
}

/** Un correo con cuentas en varios grupos (o también operador de Plataforma): se elige con cuál se entra. */
export function PasoCuenta({ desafio, cuentas, alResultado }: PasoCuentaProps) {
  const { t } = useTranslation();
  const elegir = useMutation({
    mutationFn: (cuenta: string) => elegirCuentaIngreso({ desafio, cuenta }, { token: null, empresa: null }),
    onSuccess: alResultado,
  });

  return (
    <Stack spacing={2.5}>
      <Box>
        <Typography variant="h2" component="h1" sx={{ mb: 0.5 }}>
          {t('ingreso.cuentaTitulo')}
        </Typography>
        <Typography sx={{ color: 'text.secondary' }}>{t('ingreso.cuentaTexto')}</Typography>
      </Box>
      {elegir.error !== null && (
        <Alert severity="error" role="alert">
          {mensajeDeIngreso(elegir.error, t)}
        </Alert>
      )}
      <Stack spacing={1} component="ul" sx={{ listStyle: 'none', m: 0, p: 0 }}>
        {cuentas.map((cuenta) => (
          <li key={cuenta.id}>
            <ButtonBase
              onClick={() => elegir.mutate(cuenta.id)}
              disabled={elegir.isPending}
              sx={(th) => ({
                width: '100%',
                justifyContent: 'flex-start',
                gap: 1.5,
                p: 1.5,
                textAlign: 'left',
                border: 1,
                borderColor: 'divider',
                borderRadius: `${String(th.radio.md)}px`,
                bgcolor: 'background.paper',
                '&:hover': { bgcolor: 'action.hover' },
                '&:focus-visible': { outlineOffset: 2 },
                '&.Mui-disabled': { opacity: 0.6 },
              })}
            >
              <AvatarActor tipo={cuenta.tipo === 'Operador' ? 'persona' : 'tercero'} nombre={cuenta.grupo ?? cuenta.nombre} tamano={36} />
              <Box sx={{ minWidth: 0, flex: 1 }}>
                <Typography noWrap sx={{ fontWeight: 600 }}>
                  {cuenta.grupo ?? t('ingreso.cuentaPlataforma')}
                </Typography>
                <Typography noWrap variant="body2" sx={{ color: 'text.secondary' }}>
                  {cuenta.nombre}
                </Typography>
              </Box>
              {(cuenta.tienePasskey || cuenta.exigePasskey) && (
                <Chip size="small" variant="outlined" label={cuenta.tienePasskey ? t('ingreso.cuentaPasskey') : t('ingreso.cuentaExigePasskey')} />
              )}
            </ButtonBase>
          </li>
        ))}
      </Stack>
    </Stack>
  );
}
