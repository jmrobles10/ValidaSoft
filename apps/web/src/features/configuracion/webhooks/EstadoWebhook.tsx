import type { WebhookDto } from '@validasoft/api-client';
import { ChipEstado, type Estado } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { estadoWebhook } from './modelo';

const CHIP: Record<ReturnType<typeof estadoWebhook>, Estado> = { activa: 'Activo', conFallos: 'PorVencer', deshabilitadaPorFallos: 'Bloqueado', desactivada: 'Anulado' };

/** Activa, activa con fallos seguidos, deshabilitada por fallos o desactivada a mano. */
export function EstadoWebhook({ webhook }: { webhook: Pick<WebhookDto, 'activa' | 'fallosSeguidos' | 'causaDeshabilitacion'> }) {
  const { t } = useTranslation();
  const estado = estadoWebhook(webhook);
  return <ChipEstado estado={CHIP[estado]} etiqueta={t(`webhooks.estados.${estado}`, { count: webhook.fallosSeguidos })} />;
}
