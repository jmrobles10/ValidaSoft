import { Badge, IconButton, Popover, Tooltip } from '@mui/material';
import type { NotificacionDto } from '@validasoft/api-client';
import { Bell } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { mensajeDeError } from '../../shared/errores';
import { useAvisos } from '../../shared/useAvisos';
import { useBandejaNotificaciones, useMarcarLeida, useMarcarTodas } from './api';
import { destinoDeNotificacion, textoContador } from './bandeja';
import { DialogoPreferencias } from './DialogoPreferencias';
import { PanelNotificaciones } from './PanelNotificaciones';
import { useStreamNotificaciones } from './useStreamNotificaciones';

/**
 * Campana de la barra superior: contador de no leídas, panel con la lista y preferencias de correo. Mantiene abierto el
 * stream de notificaciones de la empresa activa; si no está disponible, refresca la lista cada tanto.
 */
export function Campana({ empresaId }: { empresaId: string }) {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const mostrar = useAvisos((s) => s.mostrar);
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const [preferencias, setPreferencias] = useState(false);
  const estado = useStreamNotificaciones(empresaId);
  const respaldo = estado === 'respaldo';
  const consulta = useBandejaNotificaciones(empresaId, respaldo);
  const marcarLeida = useMarcarLeida(empresaId);
  const marcarTodas = useMarcarTodas(empresaId);
  const noLeidas = consulta.data?.noLeidas ?? 0;
  const etiqueta = noLeidas > 0 ? t('notificaciones.abrirConContador', { count: noLeidas }) : t('notificaciones.abrir');

  const abrir = (notificacion: NotificacionDto) => {
    if (!notificacion.leidaEn) marcarLeida.mutate(notificacion.id, { onError: () => mostrar(t('notificaciones.errorMarcar'), 'error') });
    const destino = destinoDeNotificacion(notificacion, empresaId);
    if (destino) {
      setAncla(null);
      void navegar(destino);
    }
  };

  return (
    <>
      <Tooltip title={t('notificaciones.titulo')}>
        <IconButton
          aria-label={etiqueta}
          aria-haspopup="dialog"
          aria-expanded={ancla !== null}
          onClick={(e) => setAncla(e.currentTarget)}
          sx={{ flex: 'none' }}
        >
          <Badge
            badgeContent={textoContador(noLeidas)}
            invisible={noLeidas === 0}
            color="error"
            sx={{ '& .MuiBadge-badge': { fontSize: 11, height: 16, minWidth: 16, px: 0.5 } }}
          >
            <Bell size={18} strokeWidth={1.75} />
          </Badge>
        </IconButton>
      </Tooltip>
      <Popover
        open={ancla !== null}
        anchorEl={ancla}
        onClose={() => setAncla(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{
          paper: {
            role: 'dialog',
            'aria-labelledby': 'titulo-notificaciones',
            sx: { width: 380, maxWidth: 'calc(100vw - 16px)', maxHeight: 'min(520px, calc(100vh - 64px))', display: 'flex', flexDirection: 'column' },
          },
        }}
      >
        <PanelNotificaciones
          bandeja={consulta.data}
          cargando={consulta.isPending}
          error={consulta.isError ? `${t('notificaciones.errorCarga')} ${mensajeDeError(consulta.error, t)}` : null}
          respaldo={respaldo}
          marcandoTodas={marcarTodas.isPending}
          onReintentar={() => void consulta.refetch()}
          onAbrir={abrir}
          onMarcarTodas={() =>
            marcarTodas.mutate(undefined, {
              onSuccess: (r) => mostrar(t('notificaciones.marcadas', { count: r.marcadas })),
              onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
            })
          }
          onPreferencias={() => {
            setAncla(null);
            setPreferencias(true);
          }}
        />
      </Popover>
      {preferencias && <DialogoPreferencias onCerrar={() => setPreferencias(false)} />}
    </>
  );
}
