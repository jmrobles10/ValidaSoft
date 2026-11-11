import { Alert, Button, Chip, Skeleton, Stack, Typography } from '@mui/material';
import { Dato, formatearFechaHora, RejillaDatos } from '@validasoft/ui';
import { KeyRound, Power, PowerOff } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, ErrorRecuperable, Tarjeta } from '../../shared/componentes';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useCambiarClaveDeArranque, useClaveDeArranque } from './apiIdentidad';

/**
 * La clave de arranque (PI-17): la llave de configuración que da de alta al primer operador de Plataforma. Con operadores que ya entran con su passkey ya no
 * hace falta: se desactiva aquí, sin tocar la configuración del servidor, y se puede reactivar si hay que rescatar la consola.
 */
export function TarjetaClaveDeArranque() {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const consulta = useClaveDeArranque();
  const cambiar = useCambiarClaveDeArranque();
  const [confirmando, setConfirmando] = useState<'desactivar' | 'reactivar' | null>(null);
  const clave = consulta.data;

  const confirmar = () => {
    if (!confirmando) return;
    const desactivar = confirmando === 'desactivar';
    cambiar.mutate(desactivar, {
      onSuccess: () => mostrar(desactivar ? t('consola.claveArranque.desactivada') : t('consola.claveArranque.reactivada')),
      onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
      onSettled: () => setConfirmando(null),
    });
  };

  return (
    <Tarjeta titulo={t('consola.claveArranque.titulo')}>
      <Typography variant="body2" sx={{ color: 'text.secondary', mb: 1.5 }}>
        {t('consola.claveArranque.ayuda')}
      </Typography>
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      {consulta.isPending && <Skeleton variant="rounded" height={72} role="status" aria-busy aria-label={t('comun.cargando')} />}
      {clave && (
        <Stack spacing={1.5}>
          <RejillaDatos columnas={3}>
            <Dato etiqueta={t('consola.claveArranque.estado')}>
              <Chip
                size="small"
                variant="outlined"
                color={clave.configurada && !clave.desactivada ? 'warning' : 'default'}
                label={!clave.configurada ? t('consola.claveArranque.noConfigurada') : clave.desactivada ? t('consola.claveArranque.estadoDesactivada') : t('consola.claveArranque.estadoActiva')}
              />
            </Dato>
            <Dato etiqueta={t('consola.claveArranque.operadores')}>
              <Typography component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.75 }}>
                <KeyRound size={14} strokeWidth={1.75} aria-hidden />
                {t('consola.claveArranque.operadoresConPasskey', { count: clave.operadoresConPasskey })}
              </Typography>
            </Dato>
            {clave.desactivada && clave.desactivadaEn && (
              <Dato etiqueta={t('consola.claveArranque.desactivadaEl')}>
                {formatearFechaHora(clave.desactivadaEn)}
                {clave.desactivadaPor ? ` · ${clave.desactivadaPor}` : ''}
              </Dato>
            )}
          </RejillaDatos>
          {clave.configurada && !clave.desactivada && clave.operadoresConPasskey === 0 && <Alert severity="info">{t('consola.claveArranque.sinOperadores')}</Alert>}
          {clave.configurada && !clave.desactivada && clave.operadoresConPasskey > 0 && <Alert severity="warning">{t('consola.claveArranque.sigueActiva')}</Alert>}
          {clave.configurada && (
            <Stack direction="row" spacing={1}>
              {clave.desactivada ? (
                <Button variant="outlined" startIcon={<Power size={16} aria-hidden />} onClick={() => setConfirmando('reactivar')}>
                  {t('consola.claveArranque.reactivar')}
                </Button>
              ) : (
                <Button variant="outlined" color="error" startIcon={<PowerOff size={16} aria-hidden />} onClick={() => setConfirmando('desactivar')} disabled={clave.operadoresConPasskey === 0}>
                  {t('consola.claveArranque.desactivar')}
                </Button>
              )}
            </Stack>
          )}
        </Stack>
      )}
      {confirmando && (
        <DialogoConfirmacion
          titulo={confirmando === 'desactivar' ? t('consola.claveArranque.desactivarTitulo') : t('consola.claveArranque.reactivarTitulo')}
          mensaje={confirmando === 'desactivar' ? t('consola.claveArranque.desactivarAyuda') : t('consola.claveArranque.reactivarAyuda')}
          confirmar={confirmando === 'desactivar' ? t('consola.claveArranque.desactivar') : t('consola.claveArranque.reactivar')}
          tono={confirmando === 'desactivar' ? 'error' : 'primary'}
          ocupado={cambiar.isPending}
          onConfirmar={confirmar}
          onCerrar={() => setConfirmando(null)}
        />
      )}
    </Tarjeta>
  );
}
