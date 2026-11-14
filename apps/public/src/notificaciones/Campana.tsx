import { Badge, IconButton, Popover, Tooltip } from '@mui/material';
import type { NotificacionTerceroDto } from '@validasoft/api-client';
import { Bell } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { useSesionTercero } from '../sesion/sesionTercero';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { useBandejaDelTercero, useMarcarLeida, useMarcarTodas } from './api';
import { destinoDeNotificacion, textoContador } from './bandeja';
import { DialogoPreferencias } from './DialogoPreferencias';
import { PanelNotificaciones } from './PanelNotificaciones';
import { useStreamDelTercero } from './tiempoReal';

/**
 * Campana del tercero en la cabecera: contador de no leídas (de todas sus empresas), panel con la lista —cada aviso con la marca de
 * su empresa— y preferencias de correo. Un clic marca el aviso
 * como leído y lleva a lo que hay que hacer (el expediente observado, la bóveda, «Mi información»…). Mantiene el stream en tiempo real
 * del portal (notificaciones y avisos de cambio, NT-12); si no logra conectar, refresca cada 30 s.
 */
export function Campana() {
  const { t } = useTranslation();
  const navegar = useNavigate();
  const mostrar = useAvisos((s) => s.mostrar);
  const [ancla, setAncla] = useState<HTMLElement | null>(null);
  const [preferencias, setPreferencias] = useState(false);
  const estado = useStreamDelTercero();
  const aislada = useSesionTercero((s) => s.sesion?.aislada === true);
  const consulta = useBandejaDelTercero(estado === 'respaldo');
  const marcarLeida = useMarcarLeida();
  const marcarTodas = useMarcarTodas();
  const noLeidas = consulta.data?.noLeidas ?? 0;
  const etiqueta = noLeidas > 0 ? t('notificaciones.abrirConContador', { count: noLeidas }) : t('notificaciones.abrir');

  const abrir = (notificacion: NotificacionTerceroDto) => {
    if (!notificacion.leidaEn) marcarLeida.mutate(notificacion.id, { onError: () => mostrar(t('notificaciones.errorMarcar'), 'error') });
    const destino = destinoDeNotificacion(notificacion);
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
            <Bell size={20} strokeWidth={1.75} />
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
            sx: { width: 400, maxWidth: 'calc(100vw - 16px)', maxHeight: 'min(560px, calc(100dvh - 72px))', display: 'flex', flexDirection: 'column' },
          },
        }}
      >
        <PanelNotificaciones
          bandeja={consulta.data}
          cargando={consulta.isPending}
          error={consulta.isError ? `${t('notificaciones.errorCarga')} ${mensajeDeError(consulta.error, t)}` : null}
          onReintentar={() => void consulta.refetch()}
          onAbrir={abrir}
          onMarcarTodas={() =>
            marcarTodas.mutate(undefined, {
              onSuccess: (r) => mostrar(t('notificaciones.marcadas', { count: r.marcadas })),
              onError: (error) => mostrar(mensajeDeError(error, t), 'error'),
            })
          }
          onPreferencias={
            aislada
              ? undefined
              : () => {
                  setAncla(null);
                  setPreferencias(true);
                }
          }
        />
      </Popover>
      {preferencias && <DialogoPreferencias onCerrar={() => setPreferencias(false)} />}
    </>
  );
}
