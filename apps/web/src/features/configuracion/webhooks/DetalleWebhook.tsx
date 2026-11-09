import { Alert, Button, Divider, Stack, Typography } from '@mui/material';
import type { EmpresaDto, WebhookDto } from '@validasoft/api-client';
import { Dato, formatearFechaHora, RejillaDatos } from '@validasoft/ui';
import { KeyRound, Pencil, Play, Power } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { DialogoConfirmacion, Tarjeta } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useAccionesWebhook } from './api';
import { EntregasWebhook } from './EntregasWebhook';
import { EstadoWebhook } from './EstadoWebhook';
import { TODOS } from './modelo';

type Confirmacion = 'rotar' | 'desactivar' | null;

/** Una suscripción: a dónde va, qué recibe, cómo le va y sus acciones (editar, rotar secreto, activar o desactivar). */
export function DetalleWebhook({ webhook, empresas, onEditar, onSecreto }: { webhook: WebhookDto; empresas: readonly EmpresaDto[]; onEditar: () => void; onSecreto: (w: WebhookDto) => void }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const { activar, desactivar, rotar } = useAccionesWebhook(webhook.id);
  const [confirmar, setConfirmar] = useState<Confirmacion>(null);
  const nombreEmpresa = (id: string) => empresas.find((e) => e.id === id)?.nombre ?? id;
  const fallo = activar.error ?? desactivar.error ?? rotar.error;

  return (
    <Tarjeta etiqueta={webhook.url} titulo={webhook.descripcion ?? webhook.url}>
      <Stack spacing={2}>
        {fallo && <Alert severity="error">{mensajeDeError(fallo, t)}</Alert>}
        {!webhook.activa && webhook.causaDeshabilitacion === 'fallos-consecutivos' && <Alert severity="warning">{t('webhooks.deshabilitadoPorFallos')}</Alert>}
        <RejillaDatos>
          <Dato etiqueta={t('webhooks.campos.url')}>
            <Typography component="span" variant="mono" sx={{ overflowWrap: 'anywhere' }}>
              {webhook.url}
            </Typography>
          </Dato>
          <Dato etiqueta={t('webhooks.columnas.estado')}>
            <EstadoWebhook webhook={webhook} />
          </Dato>
          <Dato etiqueta={t('webhooks.campos.eventos')}>
            {webhook.tipos.includes(TODOS) ? t('webhooks.todosLosEventos') : webhook.tipos.join(', ')}
          </Dato>
          <Dato etiqueta={t('webhooks.campos.empresas')}>
            {webhook.empresas.includes(TODOS) ? t('webhooks.campos.todasLasEmpresas') : webhook.empresas.map(nombreEmpresa).join(', ')}
          </Dato>
          <Dato etiqueta={t('webhooks.sistema')}>{webhook.sistema}</Dato>
          <Dato etiqueta={t('webhooks.ultimaEntrega')}>{webhook.ultimaEntregaEn ? formatearFechaHora(webhook.ultimaEntregaEn) : '—'}</Dato>
          {webhook.secretoAnteriorVence && <Dato etiqueta={t('webhooks.secretoAnterior')}>{formatearFechaHora(webhook.secretoAnteriorVence)}</Dato>}
        </RejillaDatos>
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
          <Button variant="outlined" startIcon={<Pencil size={16} aria-hidden />} onClick={onEditar}>
            {t('webhooks.editar')}
          </Button>
          <Button variant="outlined" startIcon={<KeyRound size={16} aria-hidden />} onClick={() => setConfirmar('rotar')}>
            {t('webhooks.rotar')}
          </Button>
          {webhook.activa ? (
            <Button variant="outlined" color="error" startIcon={<Power size={16} aria-hidden />} onClick={() => setConfirmar('desactivar')} sx={{ color: 'estado.error.texto', borderColor: 'estado.error.borde' }}>
              {t('webhooks.desactivar')}
            </Button>
          ) : (
            <Button variant="outlined" startIcon={<Play size={16} aria-hidden />} disabled={activar.isPending} onClick={() => activar.mutate(undefined, { onSuccess: () => mostrar(t('webhooks.activado')) })}>
              {t('webhooks.activar')}
            </Button>
          )}
        </Stack>
        <Divider />
        <EntregasWebhook webhookId={webhook.id} />
      </Stack>
      {confirmar === 'rotar' && (
        <DialogoConfirmacion
          titulo={t('webhooks.rotarTitulo')}
          subtitulo={webhook.url}
          mensaje={t('webhooks.rotarAyuda')}
          confirmar={t('webhooks.rotar')}
          icono={<KeyRound size={16} aria-hidden />}
          ocupado={rotar.isPending}
          onConfirmar={() => rotar.mutate(undefined, { onSuccess: (r) => { setConfirmar(null); onSecreto(r); } })}
          onCerrar={() => setConfirmar(null)}
        />
      )}
      {confirmar === 'desactivar' && (
        <DialogoConfirmacion
          titulo={t('webhooks.desactivarTitulo')}
          subtitulo={webhook.url}
          mensaje={t('webhooks.desactivarAyuda')}
          confirmar={t('webhooks.desactivar')}
          icono={<Power size={16} aria-hidden />}
          ocupado={desactivar.isPending}
          onConfirmar={() => desactivar.mutate(undefined, { onSuccess: () => { setConfirmar(null); mostrar(t('webhooks.desactivado')); } })}
          onCerrar={() => setConfirmar(null)}
        />
      )}
    </Tarjeta>
  );
}
