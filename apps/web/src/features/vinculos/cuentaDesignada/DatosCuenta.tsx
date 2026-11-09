import { Stack, Typography } from '@mui/material';
import type { CuentaDesignadaDto } from '@validasoft/api-client';
import { ChipEstado, Dato, RejillaDatos } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { esEstadoCuenta, numeroVisible, textoTipoCuenta } from './modelo';

/** Sobretítulo de cada parte de la tarjeta («Designada», «Por aprobar»…) con el chip del estado de la cuenta. */
export function SeccionCuenta({ titulo, cuenta, fecha, children }: { titulo: string; cuenta: CuentaDesignadaDto; fecha?: string | null; children: ReactNode }) {
  return (
    <Stack component="section" aria-label={titulo} spacing={1}>
      <Stack direction="row" spacing={1} alignItems="center">
        <Typography variant="overline" component="h3" color="text.secondary">
          {titulo}
        </Typography>
        {esEstadoCuenta(cuenta.estado) && <ChipEstado estado={cuenta.estado} fecha={fecha ?? null} />}
      </Stack>
      {children}
    </Stack>
  );
}

/** Datos de la cuenta; el número va en mono para cotejarlo dígito a dígito con la certificación bancaria. */
export function DatosCuenta({ cuenta }: { cuenta: CuentaDesignadaDto }) {
  const { t } = useTranslation();
  return (
    <>
      <RejillaDatos>
        <Dato etiqueta={t('cuentas.campos.banco')}>{cuenta.banco}</Dato>
        <Dato etiqueta={t('cuentas.campos.tipo')}>{textoTipoCuenta(cuenta.tipoCuenta, t)}</Dato>
        <Dato etiqueta={t('cuentas.campos.numero')}>
          <Typography component="span" variant="mono">
            {numeroVisible(cuenta)}
          </Typography>
        </Dato>
        <Dato etiqueta={t('cuentas.campos.titular')}>{cuenta.titular}</Dato>
        <Dato etiqueta={t('cuentas.campos.identificacionTitular')}>
          <Typography component="span" variant="mono">
            {cuenta.identificacionTitular}
          </Typography>
        </Dato>
        <Dato etiqueta={t('cuentas.campos.moneda')}>{cuenta.moneda}</Dato>
      </RejillaDatos>
      {cuenta.numero === null && (
        <Typography variant="caption" color="text.secondary">
          {t('cuentas.numeroOculto')}
        </Typography>
      )}
    </>
  );
}

/** La última propuesta rechazada, en tono secundario: qué cuenta era y por qué no se aceptó. */
export function CuentaRechazada({ cuenta }: { cuenta: CuentaDesignadaDto }) {
  const { t } = useTranslation();
  return (
    <SeccionCuenta titulo={t('cuentas.secciones.rechazada')} cuenta={cuenta}>
      <Typography variant="body2" color="text.secondary">
        {t('cuentas.rechazadaResumen', { banco: cuenta.banco, tipo: textoTipoCuenta(cuenta.tipoCuenta, t), numero: numeroVisible(cuenta) })}
      </Typography>
      {cuenta.motivoRechazo && (
        <Typography variant="body2" color="text.secondary">
          {t('cuentas.motivo', { motivo: cuenta.motivoRechazo })}
        </Typography>
      )}
    </SeccionCuenta>
  );
}
