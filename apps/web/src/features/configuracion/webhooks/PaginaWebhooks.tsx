import { Box, Button, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from '@mui/material';
import { SCOPES, type WebhookDto } from '@validasoft/api-client';
import { EstadoVacio, FilasEsqueleto, formatearFechaHora, MarcoTabla } from '@validasoft/ui';
import { Plus, Webhook } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { EncabezadoPagina, ErrorRecuperable, SinPermiso } from '../../../shared/componentes';
import { mensajeDeError } from '../../../shared/errores';
import { usePermisos } from '../../../shared/usePermisos';
import { useAvisos } from '../../../shared/useAvisos';
import { useSesion } from '../../../sesion/sesionStore';
import { useEmpresaActiva } from '../../empresas/useEmpresas';
import { useSistemas } from '../apiKeys/api';
import { useTiposEvento, useWebhooks } from './api';
import { DetalleWebhook } from './DetalleWebhook';
import { DialogoSecreto } from './DialogoSecreto';
import { DrawerWebhook } from './DrawerWebhook';
import { EstadoWebhook } from './EstadoWebhook';
import { TODOS } from './modelo';

type Edicion = { tipo: 'crear' } | { tipo: 'editar'; webhook: WebhookDto };

/** Webhooks del grupo: a qué sistemas se les avisa cada evento (vínculos, expedientes, documentos, certificados). */
export function PaginaWebhooks() {
  const { t } = useTranslation();
  const { empresa, empresas } = useEmpresaActiva();
  const permisos = usePermisos(empresa.id);
  const puede = permisos.puede(SCOPES.webhooksAdministrar);
  const consulta = useWebhooks(puede);
  const catalogo = useTiposEvento(puede);
  // Al crear sin venir de un sistema integrado (ingreso directo) se elige el sistema dueño del webhook.
  const sistemaDeLaSesion = useSesion((s) => s.sesion?.sistema ?? null);
  const sistemas = useSistemas(puede && sistemaDeLaSesion === null && permisos.puede(SCOPES.sistemasAdministrar));
  const mostrar = useAvisos((s) => s.mostrar);
  const [elegidoId, setElegidoId] = useState<string | null>(null);
  const [edicion, setEdicion] = useState<Edicion | null>(null);
  const [secreto, setSecreto] = useState<WebhookDto | null>(null);

  if (!puede) return <SinPermiso seccion={t('nav.webhooks')} />;

  const webhooks = consulta.data ?? [];
  const elegido = webhooks.find((w) => w.id === elegidoId) ?? webhooks[0] ?? null;

  return (
    <>
      <EncabezadoPagina
        titulo={t('webhooks.titulo')}
        subtitulo={t('webhooks.ayuda')}
        acciones={
          <Button variant="contained" startIcon={<Plus size={16} aria-hidden />} onClick={() => setEdicion({ tipo: 'crear' })}>
            {t('webhooks.nuevo')}
          </Button>
        }
      />
      {consulta.isError && (
        <Box sx={{ mb: 1.5 }}>
          <ErrorRecuperable mensaje={mensajeDeError(consulta.error, t)} onReintentar={() => void consulta.refetch()} />
        </Box>
      )}
      <MarcoTabla>
        <TableContainer>
          <Table size="small" aria-label={t('webhooks.titulo')} aria-busy={consulta.isPending}>
            <TableHead>
              <TableRow>
                <TableCell>{t('webhooks.columnas.destino')}</TableCell>
                <TableCell sx={{ width: 150 }}>{t('webhooks.columnas.eventos')}</TableCell>
                <TableCell sx={{ width: 170 }}>{t('webhooks.columnas.estado')}</TableCell>
                <TableCell sx={{ width: 170 }}>{t('webhooks.ultimaEntrega')}</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {consulta.isPending && <FilasEsqueleto columnas={4} filas={2} />}
              {consulta.data && webhooks.length === 0 && (
                <TableRow>
                  <TableCell colSpan={4} sx={{ borderBottom: 0 }}>
                    <EstadoVacio compacto icono={Webhook} titulo={t('webhooks.vacio')} texto={t('webhooks.vacioAyuda')} />
                  </TableCell>
                </TableRow>
              )}
              {webhooks.map((w) => (
                <TableRow
                  key={w.id}
                  selected={w.id === elegido?.id}
                  tabIndex={0}
                  aria-label={t('webhooks.ver', { url: w.url })}
                  onClick={() => setElegidoId(w.id)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      e.preventDefault();
                      setElegidoId(w.id);
                    }
                  }}
                  sx={{ cursor: 'pointer' }}
                >
                  <TableCell>
                    <Typography variant="monoTabla" sx={{ display: 'block', overflowWrap: 'anywhere' }}>
                      {w.url}
                    </Typography>
                    {w.descripcion && (
                      <Typography variant="body2" sx={{ color: 'text.secondary' }}>
                        {w.descripcion}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>{w.tipos.includes(TODOS) ? t('webhooks.todosLosEventos') : t('webhooks.nEventos', { count: w.tipos.length })}</TableCell>
                  <TableCell>
                    <EstadoWebhook webhook={w} />
                  </TableCell>
                  <TableCell>
                    <Typography variant="monoTabla">{w.ultimaEntregaEn ? formatearFechaHora(w.ultimaEntregaEn) : '—'}</Typography>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      </MarcoTabla>

      {elegido && (
        <Box sx={{ mt: 2 }}>
          <DetalleWebhook key={elegido.id} webhook={elegido} empresas={empresas} onEditar={() => setEdicion({ tipo: 'editar', webhook: elegido })} onSecreto={setSecreto} />
        </Box>
      )}

      {edicion && (
        <DrawerWebhook
          webhook={edicion.tipo === 'editar' ? edicion.webhook : null}
          catalogo={catalogo.data ?? []}
          empresas={empresas}
          sistemas={sistemas.data}
          sistemaDeLaSesion={sistemaDeLaSesion}
          onCerrar={() => setEdicion(null)}
          onCreado={(creado) => {
            setEdicion(null);
            setElegidoId(creado.id);
            mostrar(t('webhooks.creado'));
            setSecreto(creado);
          }}
        />
      )}
      {secreto?.secreto && <DialogoSecreto secreto={secreto.secreto} anteriorVence={secreto.secretoAnteriorVence} onCerrar={() => setSecreto(null)} />}
    </>
  );
}
