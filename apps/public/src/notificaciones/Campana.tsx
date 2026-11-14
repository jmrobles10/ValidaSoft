import type { NotificacionTerceroDto } from '@validasoft/api-client';
import { CampanaDeNotificaciones } from '@validasoft/ui';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useNavigate } from 'react-router';
import { mensajeDeError } from '../shared/errores';
import { useAvisos } from '../shared/useAvisos';
import { useBandejaDelTercero, useMarcarLeida, useMarcarTodas } from './api';
import { destinoDeNotificacion } from './bandeja';
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
      <CampanaDeNotificaciones
        noLeidas={noLeidas}
        etiqueta={etiqueta}
        titulo={t('notificaciones.titulo')}
        ancla={ancla}
        onAnclar={setAncla}
        tamanoIcono={20}
        ancho={400}
        alturaMaxima="min(560px, calc(100dvh - 72px))"
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
          onPreferencias={() => {
            setAncla(null);
            setPreferencias(true);
          }}
        />
      </CampanaDeNotificaciones>
      {preferencias && <DialogoPreferencias onCerrar={() => setPreferencias(false)} />}
    </>
  );
}
