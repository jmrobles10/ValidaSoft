import { Alert, AlertTitle, Button, Stack, Typography } from '@mui/material';
import type { VinculoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import { CircleCheck, CircleSlash, Hourglass, SendHorizontal } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { IconoCarga } from '../../shared/componentes';
import { useAvisos } from '../../shared/useAvisos';
import { mensajeDeErrorDeDecision, useDecidirInvitacion } from './api';
import { DialogoRechazo } from './DialogoRechazo';

const POR_ESTADO = {
  Pendiente: { severidad: 'warning', icono: Hourglass },
  Aprobada: { severidad: 'success', icono: CircleCheck },
  Rechazada: { severidad: 'error', icono: CircleSlash },
} as const;

/**
 * Aprobación antes de invitar (decisión AI): si el tipo del vínculo exige aprobación, dice en qué va. Pendiente: la invitación está retenida (no hay correo ni enlace y el
 * tercero no ve el vínculo) y quien puede decidirla —de las que el tipo designó, y no quien la pidió— la aprueba (sale la invitación) o la rechaza con un motivo (el vínculo
 * queda anulado). Un vínculo que no pasó por aprobación no lleva aviso.
 */
export function AvisoInvitacionPorAprobar({ vinculo, empresaId }: { vinculo: Pick<VinculoDto, 'id' | 'version' | 'invitacionPorAprobar'>; empresaId: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const decidir = useDecidirInvitacion(empresaId);
  const [rechazando, setRechazando] = useState(false);
  const retenida = vinculo.invitacionPorAprobar;
  if (!retenida) return null;
  // Quién pidió o decidió: el nombre de la persona o, si fue una integración o no se conoce, un texto genérico.
  const quienEs = (nombre: string | null | undefined, actor: string | null | undefined) =>
    nombre ?? (actor?.startsWith('Integracion:') ? t('invitaciones.porAprobar.pidioIntegracion') : t('invitaciones.aviso.alguien'));

  const { severidad, icono: Icono } = POR_ESTADO[retenida.estado];
  const fecha = formatearFecha(retenida.estado === 'Pendiente' ? retenida.solicitadaEn : (retenida.decididaEn ?? retenida.solicitadaEn));
  const quien = retenida.estado === 'Pendiente' ? quienEs(retenida.solicitadaPorNombre, retenida.solicitadaPor) : quienEs(retenida.decididaPorNombre, retenida.decididaPor);
  const error = decidir.isError ? mensajeDeErrorDeDecision(decidir.error, t) : null;

  const aprobar = () =>
    decidir.mutate(
      { vinculoId: vinculo.id, version: vinculo.version, decision: 'aprobar' },
      { onSuccess: () => mostrar(t('invitaciones.porAprobar.aprobada'), 'success'), onError: (e) => mostrar(mensajeDeErrorDeDecision(e, t), 'error') },
    );
  const rechazar = (motivo: string) =>
    decidir.mutate(
      { vinculoId: vinculo.id, version: vinculo.version, decision: 'rechazar', motivo },
      {
        onSuccess: () => {
          setRechazando(false);
          mostrar(t('invitaciones.porAprobar.rechazada'), 'success');
        },
      },
    );

  return (
    <>
      <Alert
        severity={severidad}
        icon={<Icono size={18} strokeWidth={1.75} />}
        sx={{ mb: 2 }}
        action={
          retenida.estado === 'Pendiente' && retenida.puedesDecidir ? (
            <Stack direction="row" spacing={1}>
              <Button size="small" color="inherit" variant="outlined" disabled={decidir.isPending} onClick={() => setRechazando(true)}>
                {t('invitaciones.aviso.rechazar')}
              </Button>
              <Button
                size="small"
                color="inherit"
                variant="outlined"
                disabled={decidir.isPending}
                startIcon={decidir.isPending && !rechazando ? <IconoCarga /> : <SendHorizontal size={14} aria-hidden />}
                onClick={aprobar}
              >
                {t('invitaciones.aviso.aprobar')}
              </Button>
            </Stack>
          ) : undefined
        }
      >
        <AlertTitle>{t(`invitaciones.aviso.${retenida.estado}.titulo`, { correo: retenida.correoDestino })}</AlertTitle>
        <Typography variant="body2" component="span" sx={{ display: 'block' }}>
          {t(`invitaciones.aviso.${retenida.estado}.texto`, { quien, fecha, correo: retenida.correoDestino, motivo: retenida.motivoDeRechazo ?? '' })}
        </Typography>
        {retenida.estado === 'Pendiente' && (
          <Typography variant="body2" component="span" sx={{ display: 'block', mt: 0.5 }}>
            {retenida.puedesDecidir ? t('invitaciones.aviso.Pendiente.decidesTu') : t('invitaciones.aviso.Pendiente.decideOtra')}
          </Typography>
        )}
        {error && !rechazando && (
          <Typography variant="body2" component="span" role="alert" sx={{ display: 'block', mt: 0.5, color: 'estado.error.texto' }}>
            {error}
          </Typography>
        )}
      </Alert>
      {rechazando && (
        <DialogoRechazo
          tipo="invitacion"
          subtitulo={retenida.correoDestino}
          ocupado={decidir.isPending}
          error={error}
          onConfirmar={rechazar}
          onCerrar={() => {
            decidir.reset();
            setRechazando(false);
          }}
        />
      )}
    </>
  );
}
