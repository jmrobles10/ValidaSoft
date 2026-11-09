import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  activarWebhook,
  actualizarWebhook,
  crearWebhook,
  desactivarWebhook,
  getListarEntregasWebhookQueryOptions,
  getListarTiposEventoQueryOptions,
  getListarWebhooksQueryOptions,
  reenviarEntregaWebhook,
  rotarSecretoWebhook,
  type WebhookDto,
} from '@validasoft/api-client';
import { invalidarRuta } from '../../../shared/consultas';
import { cuerpoCrearWebhook, cuerpoEditarWebhook, type BorradorWebhook } from './modelo';

/** Webhooks y su catálogo de eventos son del grupo (tenant): sin empresa activa. */
const SIN_EMPRESA = { empresa: null } as const;

export function useWebhooks(habilitado: boolean) {
  return useQuery({ ...getListarWebhooksQueryOptions({ request: SIN_EMPRESA }), enabled: habilitado });
}

export function useTiposEvento(habilitado: boolean) {
  return useQuery({ ...getListarTiposEventoQueryOptions({ request: SIN_EMPRESA }), enabled: habilitado, staleTime: 30 * 60_000 });
}

export function useEntregas(webhookId: string, estado: string | null) {
  return useQuery(getListarEntregasWebhookQueryOptions(webhookId, { limite: 50, ...(estado ? { estado } : {}) }, { request: SIN_EMPRESA }));
}

/** Crear (la respuesta trae el secreto, que se muestra una sola vez) o editar una suscripción. */
export function useGuardarWebhook(webhook: WebhookDto | null) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (b: BorradorWebhook) => (webhook ? actualizarWebhook(webhook.id, cuerpoEditarWebhook(b), SIN_EMPRESA) : crearWebhook(cuerpoCrearWebhook(b), SIN_EMPRESA)),
    onSuccess: () => invalidarRuta(cliente, '/api/v1/webhooks'),
  });
}

export function useAccionesWebhook(webhookId: string) {
  const cliente = useQueryClient();
  const refrescar = () => invalidarRuta(cliente, '/api/v1/webhooks');
  return {
    activar: useMutation({ mutationFn: () => activarWebhook(webhookId, SIN_EMPRESA), onSuccess: refrescar }),
    desactivar: useMutation({ mutationFn: () => desactivarWebhook(webhookId, SIN_EMPRESA), onSuccess: refrescar }),
    rotar: useMutation({ mutationFn: () => rotarSecretoWebhook(webhookId, SIN_EMPRESA), onSuccess: refrescar }),
    reenviar: useMutation({ mutationFn: (entregaId: string) => reenviarEntregaWebhook(webhookId, entregaId, SIN_EMPRESA), onSuccess: refrescar }),
  };
}
