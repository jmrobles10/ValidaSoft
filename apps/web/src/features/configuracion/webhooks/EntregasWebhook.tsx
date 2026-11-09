import { Box, Button, Collapse, IconButton, MenuItem, Stack, Table, TableBody, TableCell, TableHead, TableRow, TextField, Typography } from '@mui/material';
import type { EntregaWebhookDto } from '@validasoft/api-client';
import { ChipEstado, formatearFechaHora, MarcoTabla, type Estado } from '@validasoft/ui';
import { ChevronDown, ChevronRight, RotateCw } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { ErrorRecuperable, IconoCarga } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { useAvisos } from '../../../shared/useAvisos';
import { useAccionesWebhook, useEntregas } from './api';

const ESTADOS_ENTREGA = ['Pendiente', 'Entregada', 'Reintentando', 'Fallida', 'Cancelada'] as const;
type EstadoEntrega = (typeof ESTADOS_ENTREGA)[number];
const esEstadoEntrega = (v: string): v is EstadoEntrega => ESTADOS_ENTREGA.some((e) => e === v);

/** Ícono y tono del chip para cada estado de entrega (el texto va en `etiqueta`). */
const CHIP: Record<EstadoEntrega, Estado> = { Pendiente: 'Pendiente', Entregada: 'Aprobado', Reintentando: 'PorVencer', Fallida: 'Rechazado', Cancelada: 'Anulado' };

function FilaEntrega({ entrega, webhookId }: { entrega: EntregaWebhookDto; webhookId: string }) {
  const { t } = useTranslation();
  const mostrar = useAvisos((s) => s.mostrar);
  const { reenviar } = useAccionesWebhook(webhookId);
  const [abierta, setAbierta] = useState(false);
  const estado = esEstadoEntrega(entrega.estado) ? entrega.estado : null;

  return (
    <>
      <TableRow hover>
        <TableCell sx={{ width: 40, pr: 0 }}>
          <IconButton size="small" aria-expanded={abierta} aria-label={t('webhooks.entregas.verIntentos', { tipo: entrega.tipoEvento })} onClick={() => setAbierta(!abierta)}>
            {abierta ? <ChevronDown size={16} aria-hidden /> : <ChevronRight size={16} aria-hidden />}
          </IconButton>
        </TableCell>
        <TableCell>
          <Typography variant="monoTabla">{entrega.tipoEvento}</Typography>
        </TableCell>
        <TableCell>{estado ? <ChipEstado estado={CHIP[estado]} etiqueta={t(`webhooks.entregas.estados.${estado}`)} /> : entrega.estado}</TableCell>
        <TableCell>
          <Typography variant="monoTabla">{entrega.intentos}</Typography>
        </TableCell>
        <TableCell>
          <Typography variant="body2">{formatearFechaHora(entrega.creadaEn)}</Typography>
          {entrega.proximoIntentoEn && (
            <Typography variant="caption" color="text.secondary">
              {t('webhooks.entregas.proximo', { fecha: formatearFechaHora(entrega.proximoIntentoEn) })}
            </Typography>
          )}
        </TableCell>
        <TableCell align="right">
          <Button
            size="small"
            variant="text"
            startIcon={reenviar.isPending && reenviar.variables === entrega.id ? <IconoCarga /> : <RotateCw size={14} aria-hidden />}
            disabled={reenviar.isPending}
            onClick={() => reenviar.mutate(entrega.id, { onSuccess: () => mostrar(t('webhooks.entregas.reenviada')), onError: (e) => mostrar(mensajeDeError(e, t), 'error') })}
          >
            {t('webhooks.entregas.reenviar')}
          </Button>
        </TableCell>
      </TableRow>
      <TableRow>
        <TableCell colSpan={6} sx={{ py: 0, borderBottom: abierta ? undefined : 0 }}>
          <Collapse in={abierta} unmountOnExit>
            <Stack component="ol" spacing={0.5} sx={{ m: 0, py: 1, pl: 3 }}>
              {entrega.historial.length === 0 && <Typography variant="body2" color="text.secondary">{t('webhooks.entregas.sinIntentos')}</Typography>}
              {entrega.historial.map((i) => (
                <Typography component="li" variant="body2" key={i.intentoId}>
                  {t('webhooks.entregas.intento', {
                    n: String(i.numero),
                    fecha: formatearFechaHora(i.fecha),
                    resultado: i.resultado === 'Exitoso' ? t('webhooks.entregas.exitoso') : t('webhooks.entregas.fallido'),
                    detalle: i.codigoHttp ? `HTTP ${String(i.codigoHttp)}` : (i.error ?? '—'),
                    ms: String(i.duracionMs),
                  })}
                  {i.manual ? ` · ${t('webhooks.entregas.manual')}` : ''}
                </Typography>
              ))}
            </Stack>
          </Collapse>
        </TableCell>
      </TableRow>
    </>
  );
}

/** Últimas entregas de la suscripción (más recientes primero), con su historial de intentos y el reenvío manual. */
export function EntregasWebhook({ webhookId }: { webhookId: string }) {
  const { t } = useTranslation();
  const [estado, setEstado] = useState<EstadoEntrega | ''>('');
  const consulta = useEntregas(webhookId, estado || null);
  const entregas = consulta.data?.datos ?? [];

  return (
    <Box component="section" aria-labelledby={`entregas-${webhookId}`}>
      <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ mb: 1 }}>
        <Typography id={`entregas-${webhookId}`} component="h3" sx={{ fontSize: 14, fontWeight: 600 }}>
          {t('webhooks.entregas.titulo')}
        </Typography>
        <TextField select size="small" label={t('webhooks.entregas.filtro')} value={estado} onChange={(e) => setEstado(esEstadoEntrega(e.target.value) ? e.target.value : '')} sx={{ minWidth: 170 }}>
          <MenuItem value="">{t('webhooks.entregas.todas')}</MenuItem>
          {ESTADOS_ENTREGA.map((e) => (
            <MenuItem key={e} value={e}>
              {t(`webhooks.entregas.estados.${e}`)}
            </MenuItem>
          ))}
        </TextField>
      </Stack>
      {consulta.isError && <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />}
      <MarcoTabla>
        <Table size="small" aria-label={t('webhooks.entregas.titulo')} aria-busy={consulta.isPending}>
          <TableHead>
            <TableRow>
              <TableCell />
              <TableCell>{t('webhooks.entregas.evento')}</TableCell>
              <TableCell sx={{ width: 140 }}>{t('webhooks.entregas.estado')}</TableCell>
              <TableCell sx={{ width: 80 }}>{t('webhooks.entregas.intentos')}</TableCell>
              <TableCell sx={{ width: 190 }}>{t('webhooks.entregas.fecha')}</TableCell>
              <TableCell sx={{ width: 120 }} />
            </TableRow>
          </TableHead>
          <TableBody>
            {consulta.data && entregas.length === 0 && (
              <TableRow>
                <TableCell colSpan={6}>
                  <Typography variant="body2" color="text.secondary">
                    {t('webhooks.entregas.vacio')}
                  </Typography>
                </TableCell>
              </TableRow>
            )}
            {entregas.map((e) => (
              <FilaEntrega key={e.id} entrega={e} webhookId={webhookId} />
            ))}
          </TableBody>
        </Table>
      </MarcoTabla>
    </Box>
  );
}
